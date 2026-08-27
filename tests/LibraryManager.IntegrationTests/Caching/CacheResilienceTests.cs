using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LibraryManager.Api.Security;
using LibraryManager.Application.Books;
using LibraryManager.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace LibraryManager.IntegrationTests.Caching;

[Collection(DatabaseCollection.Name)]
public sealed class CacheResilienceTests : IAsyncLifetime
{
    private const string UnavailableRedis = "localhost:1,connectTimeout=250,abortConnect=true,syncTimeout=250";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly DatabaseFixture _database;
    private readonly CustomWebApplicationFactory _factory;
    private HttpClient _librarian = null!;

    public CacheResilienceTests(DatabaseFixture database)
    {
        _database = database;
        _factory = new CustomWebApplicationFactory(database);
    }

    public Task InitializeAsync()
    {
        _librarian = _factory.CreateClient().WithTestAuth("cache-resilience-librarian", LibrarianPolicy.Role);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _librarian.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Redis_unavailable_does_not_break_availability_and_matches_postgres()
    {
        await using var host = new CustomWebApplicationFactory(
            _database.PostgresConnectionString,
            UnavailableRedis);
        var client = host.CreateClient().WithTestAuth("redis-down-librarian", LibrarianPolicy.Role);

        var book = await CreateBookAsync(client, totalCopies: 4);

        var availability = await GetAvailabilityAsync(client, book.Id);
        var catalog = await client.GetFromJsonAsync<BookDto>($"/books/{book.Id}", JsonOptions);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/books/{book.Id}/availability")).StatusCode);
        Assert.NotNull(catalog);
        Assert.Equal(book.Id, availability.BookId);
        Assert.Equal(catalog.AvailableCopies, availability.AvailableCopies);
        Assert.Equal(catalog.TotalCopies, availability.TotalCopies);
        Assert.Equal(catalog.IsActive, availability.IsActive);
        Assert.Equal(4, availability.AvailableCopies);
    }

    [Fact]
    public async Task Redis_set_failure_does_not_fail_postgres_backed_availability()
    {
        await using var host = new CustomWebApplicationFactory(
            _database,
            services => CallbackAvailabilityCache.Register(services, cache => cache.FailAllSets = true));
        var client = host.CreateClient().WithTestAuth("redis-set-fail-librarian", LibrarianPolicy.Role);

        var book = await CreateBookAsync(client, totalCopies: 3);
        var availability = await GetAvailabilityAsync(client, book.Id);

        Assert.Equal(3, availability.AvailableCopies);
        Assert.Equal(3, availability.TotalCopies);
        Assert.True(availability.IsActive);

        var callback = host.Services.GetRequiredService<CallbackAvailabilityCache>();
        Assert.Null(await callback.GetAsync(book.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Availability_get_propagates_cancellation()
    {
        var book = await CreateBookAsync(_librarian, totalCopies: 1);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _librarian.GetAsync($"/books/{book.Id}/availability", cancelled.Token));
    }

    private static async Task<BookDto> CreateBookAsync(HttpClient client, int totalCopies)
    {
        var response = await client.PostAsJsonAsync(
            "/books",
            new { title = "Dune", isbn = Guid.NewGuid().ToString("N")[..12], author = "Frank Herbert", totalCopies });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var book = await response.Content.ReadFromJsonAsync<BookDto>(JsonOptions);
        Assert.NotNull(book);
        return book;
    }

    private static async Task<BookAvailabilityDto> GetAvailabilityAsync(HttpClient client, Guid bookId)
    {
        var response = await client.GetAsync($"/books/{bookId}/availability");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var availability = await response.Content.ReadFromJsonAsync<BookAvailabilityDto>(JsonOptions);
        Assert.NotNull(availability);
        return availability;
    }
}
