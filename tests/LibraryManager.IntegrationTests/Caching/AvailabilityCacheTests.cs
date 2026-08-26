using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LibraryManager.Api.Security;
using LibraryManager.Application.Abstractions;
using LibraryManager.Application.Books;
using LibraryManager.Application.Loans;
using LibraryManager.Application.Users;
using LibraryManager.Infrastructure.Persistence;
using LibraryManager.IntegrationTests;
using LibraryManager.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LibraryManager.IntegrationTests.Caching;

[Collection(DatabaseCollection.Name)]
public sealed class AvailabilityCacheTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly DatabaseFixture _database;
    private readonly CustomWebApplicationFactory _factory;
    private HttpClient _librarian = null!;

    public AvailabilityCacheTests(DatabaseFixture database)
    {
        _database = database;
        _factory = new CustomWebApplicationFactory(database);
    }

    public Task InitializeAsync()
    {
        _librarian = _factory.CreateClient().WithTestAuth("cache-librarian", LibrarianPolicy.Role);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _librarian.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Availability_miss_loads_postgres_and_hit_serves_stale_redis_value()
    {
        var book = await CreateBookAsync(totalCopies: 3);
        var cache = _factory.Services.GetRequiredService<IAvailabilityCache>();

        Assert.Null(await cache.GetAsync(book.Id, CancellationToken.None));

        var first = await GetAvailabilityAsync(_librarian, book.Id);
        Assert.Equal(3, first.AvailableCopies);
        Assert.Equal(3, first.TotalCopies);
        Assert.True(first.IsActive);

        var cached = await cache.GetAsync(book.Id, CancellationToken.None);
        Assert.NotNull(cached);
        Assert.Equal(3, cached.AvailableCopies);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE books SET available_copies = 1 WHERE id = {book.Id}");
        }

        var catalog = await _librarian.GetFromJsonAsync<BookDto>($"/books/{book.Id}", JsonOptions);
        Assert.NotNull(catalog);
        Assert.Equal(1, catalog.AvailableCopies);

        var second = await GetAvailabilityAsync(_librarian, book.Id);
        Assert.Equal(3, second.AvailableCopies);
    }

    [Fact]
    public async Task Stale_redis_availability_cannot_approve_or_block_a_loan()
    {
        var book = await CreateBookAsync(totalCopies: 1);
        var user = await CreateUserAsync("Stale Cache Borrower");
        var cache = _factory.Services.GetRequiredService<IAvailabilityCache>();

        await cache.SetAsync(
            new BookAvailabilityCacheItem(book.Id, 0, 1, true),
            CancellationToken.None);

        var view = await GetAvailabilityAsync(_librarian, book.Id);
        Assert.Equal(0, view.AvailableCopies);

        var response = await PostLoanAsync(_librarian, book.Id, user.Id, Guid.NewGuid().ToString("N"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var catalog = await _librarian.GetFromJsonAsync<BookDto>($"/books/{book.Id}", JsonOptions);
        Assert.NotNull(catalog);
        Assert.Equal(0, catalog.AvailableCopies);
    }

    [Fact]
    public async Task Loan_invalidates_availability_cache_after_commit()
    {
        var book = await CreateBookAsync(totalCopies: 1);
        var user = await CreateUserAsync("Invalidate Cache Borrower");
        var cache = _factory.Services.GetRequiredService<IAvailabilityCache>();

        var before = await GetAvailabilityAsync(_librarian, book.Id);
        Assert.Equal(1, before.AvailableCopies);
        Assert.NotNull(await cache.GetAsync(book.Id, CancellationToken.None));

        var response = await PostLoanAsync(_librarian, book.Id, user.Id, Guid.NewGuid().ToString("N"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Null(await cache.GetAsync(book.Id, CancellationToken.None));

        var after = await GetAvailabilityAsync(_librarian, book.Id);
        Assert.Equal(0, after.AvailableCopies);
    }

    [Fact]
    public async Task Loan_succeeds_when_immediate_cache_invalidation_fails()
    {
        await using var host = new CustomWebApplicationFactory(
            _database,
            services => CallbackAvailabilityCache.Register(services, cache => cache.FailAllRemoves = true));
        var client = host.CreateClient().WithTestAuth("cache-fail-librarian", LibrarianPolicy.Role);

        var bookResponse = await client.PostAsJsonAsync(
            "/books",
            new { title = "Dune", isbn = UniqueIsbn(), author = "Frank Herbert", totalCopies = 1 });
        Assert.Equal(HttpStatusCode.Created, bookResponse.StatusCode);
        var book = await bookResponse.Content.ReadFromJsonAsync<BookDto>(JsonOptions);
        Assert.NotNull(book);

        var userResponse = await client.PostAsJsonAsync(
            "/users",
            new { name = "Cache Fail Borrower", email = $"{Guid.NewGuid():N}@example.com" });
        Assert.Equal(HttpStatusCode.Created, userResponse.StatusCode);
        var user = await userResponse.Content.ReadFromJsonAsync<UserDto>(JsonOptions);
        Assert.NotNull(user);

        var response = await PostLoanAsync(client, book.Id, user.Id, Guid.NewGuid().ToString("N"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
        Assert.Equal(1, await db.Loans.CountAsync(item => item.BookId == book.Id));
        var persisted = await db.Books.SingleAsync(item => item.Id == book.Id);
        Assert.Equal(0, persisted.AvailableCopies);
    }

    [Fact]
    public async Task Availability_requires_authentication_and_allows_non_librarian()
    {
        var book = await CreateBookAsync(totalCopies: 2);

        var anonymous = _factory.CreateClient();
        var unauthorized = await anonymous.GetAsync($"/books/{book.Id}/availability");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);

        var reader = _factory.CreateClient().WithTestAuth("cache-reader");
        var allowed = await GetAvailabilityAsync(reader, book.Id);
        Assert.Equal(book.Id, allowed.BookId);
        Assert.Equal(2, allowed.AvailableCopies);
    }

    [Fact]
    public async Task Unknown_book_availability_returns_404()
    {
        var response = await _librarian.GetAsync($"/books/{Guid.NewGuid()}/availability");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<BookDto> CreateBookAsync(int totalCopies)
    {
        var response = await _librarian.PostAsJsonAsync(
            "/books",
            new { title = "Dune", isbn = UniqueIsbn(), author = "Frank Herbert", totalCopies });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var book = await response.Content.ReadFromJsonAsync<BookDto>(JsonOptions);
        Assert.NotNull(book);
        return book;
    }

    private async Task<UserDto> CreateUserAsync(string name)
    {
        var response = await _librarian.PostAsJsonAsync(
            "/users",
            new { name, email = $"{Guid.NewGuid():N}@example.com" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var user = await response.Content.ReadFromJsonAsync<UserDto>(JsonOptions);
        Assert.NotNull(user);
        return user;
    }

    private static async Task<BookAvailabilityDto> GetAvailabilityAsync(HttpClient client, Guid bookId)
    {
        var response = await client.GetAsync($"/books/{bookId}/availability");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var availability = await response.Content.ReadFromJsonAsync<BookAvailabilityDto>(JsonOptions);
        Assert.NotNull(availability);
        return availability;
    }

    private static async Task<HttpResponseMessage> PostLoanAsync(
        HttpClient client,
        Guid bookId,
        Guid userId,
        string idempotencyKey)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/loans")
        {
            Content = JsonContent.Create(new { bookId, userId })
        };
        request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
        return await client.SendAsync(request);
    }

    private static string UniqueIsbn() => Guid.NewGuid().ToString("N")[..12];
}
