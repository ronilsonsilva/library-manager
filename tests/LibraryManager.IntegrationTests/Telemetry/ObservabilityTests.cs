using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Http.Json;
using LibraryManager.Api.Security;
using LibraryManager.Application.Books;
using LibraryManager.Application.Users;
using LibraryManager.IntegrationTests;
using LibraryManager.IntegrationTests.Infrastructure;

namespace LibraryManager.IntegrationTests.Telemetry;

[Collection(DatabaseCollection.Name)]
public sealed class ObservabilityTests : IAsyncLifetime
{
    private readonly DatabaseFixture _database;
    private readonly CustomWebApplicationFactory _factory;
    private HttpClient _librarian = null!;

    public ObservabilityTests(DatabaseFixture database)
    {
        _database = database;
        _factory = new CustomWebApplicationFactory(database);
    }

    public Task InitializeAsync()
    {
        _librarian = _factory.CreateClient().WithTestAuth("telemetry-librarian", LibrarianPolicy.Role);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _librarian.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Create_loan_records_created_and_duration_metrics()
    {
        var created = 0L;
        var durations = 0;
        using var listener = Listen(
            ("library_manager_loans_created", value => created += value),
            ("library_manager_loan_duration", _ => durations++));

        var book = await CreateBookAsync(1);
        var user = await CreateUserAsync("Telemetry Borrower");
        var response = await PostLoanAsync(book.Id, user.Id, Guid.NewGuid().ToString("N"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.True(created >= 1);
        Assert.True(durations >= 1);
    }

    [Fact]
    public async Task Unavailable_loan_records_unavailable_metric()
    {
        var unavailable = 0L;
        using var listener = Listen(("library_manager_loans_unavailable", value => unavailable += value));

        var book = await CreateBookAsync(1);
        var firstUser = await CreateUserAsync("First Telemetry Borrower");
        var secondUser = await CreateUserAsync("Second Telemetry Borrower");
        var first = await PostLoanAsync(book.Id, firstUser.Id, Guid.NewGuid().ToString("N"));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await PostLoanAsync(book.Id, secondUser.Id, Guid.NewGuid().ToString("N"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, second.StatusCode);
        Assert.True(unavailable >= 1);
    }

    [Fact]
    public async Task Cache_invalidation_failure_records_metric_without_failing_the_loan()
    {
        var failures = 0L;
        using var listener = Listen(("library_manager_cache_invalidation_failures", value => failures += value));

        await using var host = new CustomWebApplicationFactory(
            _database,
            services => CallbackAvailabilityCache.Register(services, cache => cache.FailAllRemoves = true));
        var client = host.CreateClient().WithTestAuth("telemetry-cache-librarian", LibrarianPolicy.Role);

        var bookResponse = await client.PostAsJsonAsync(
            "/books",
            new { title = "Dune", isbn = Guid.NewGuid().ToString("N")[..12], author = "Frank Herbert", totalCopies = 1 });
        Assert.Equal(HttpStatusCode.Created, bookResponse.StatusCode);
        var book = await bookResponse.Content.ReadFromJsonAsync<BookDto>();
        Assert.NotNull(book);

        var userResponse = await client.PostAsJsonAsync(
            "/users",
            new { name = "Cache Metric Borrower", email = $"{Guid.NewGuid():N}@example.com" });
        Assert.Equal(HttpStatusCode.Created, userResponse.StatusCode);
        var user = await userResponse.Content.ReadFromJsonAsync<UserDto>();
        Assert.NotNull(user);

        var request = new HttpRequestMessage(HttpMethod.Post, "/loans")
        {
            Content = JsonContent.Create(new { bookId = book.Id, userId = user.Id })
        };
        request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.NewGuid().ToString("N"));
        var loan = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Created, loan.StatusCode);
        Assert.True(failures >= 1);
    }

    private async Task<BookDto> CreateBookAsync(int totalCopies)
    {
        var response = await _librarian.PostAsJsonAsync(
            "/books",
            new { title = "Dune", isbn = Guid.NewGuid().ToString("N")[..12], author = "Frank Herbert", totalCopies });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var book = await response.Content.ReadFromJsonAsync<BookDto>();
        Assert.NotNull(book);
        return book;
    }

    private async Task<UserDto> CreateUserAsync(string name)
    {
        var response = await _librarian.PostAsJsonAsync(
            "/users",
            new { name, email = $"{Guid.NewGuid():N}@example.com" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var user = await response.Content.ReadFromJsonAsync<UserDto>();
        Assert.NotNull(user);
        return user;
    }

    private async Task<HttpResponseMessage> PostLoanAsync(Guid bookId, Guid userId, string key)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/loans")
        {
            Content = JsonContent.Create(new { bookId, userId })
        };
        request.Headers.TryAddWithoutValidation("Idempotency-Key", key);
        return await _librarian.SendAsync(request);
    }

    private static MeterListener Listen(params (string Name, Action<long> OnValue)[] instruments)
    {
        var listener = new MeterListener();
        var lookups = instruments.ToDictionary(item => item.Name, item => item.OnValue, StringComparer.Ordinal);
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == "LibraryManager" && lookups.ContainsKey(instrument.Name))
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, measurement, _, _) =>
        {
            if (lookups.TryGetValue(instrument.Name, out var onValue))
            {
                onValue(measurement);
            }
        });
        listener.SetMeasurementEventCallback<double>((instrument, measurement, _, _) =>
        {
            if (lookups.TryGetValue(instrument.Name, out var onValue))
            {
                onValue((long)measurement);
            }
        });
        listener.Start();
        return listener;
    }
}
