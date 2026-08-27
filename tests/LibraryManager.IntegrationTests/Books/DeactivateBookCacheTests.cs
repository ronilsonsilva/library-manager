using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LibraryManager.Api.Security;
using LibraryManager.Application.Abstractions;
using LibraryManager.Application.Books;
using LibraryManager.Application.Common;
using LibraryManager.Infrastructure.Outbox;
using LibraryManager.Infrastructure.Persistence;
using LibraryManager.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LibraryManager.IntegrationTests.Books;

[Collection(DatabaseCollection.Name)]
public sealed class DeactivateBookCacheTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly DatabaseFixture _database;
    private readonly CustomWebApplicationFactory _factory;
    private HttpClient _librarian = null!;

    public DeactivateBookCacheTests(DatabaseFixture database)
    {
        _database = database;
        _factory = new CustomWebApplicationFactory(database);
    }

    public Task InitializeAsync()
    {
        _librarian = _factory.CreateClient().WithTestAuth("deactivate-cache-librarian", LibrarianPolicy.Role);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _librarian.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Deactivation_clears_cached_active_availability_and_persists_outbox()
    {
        var book = await CreateBookAsync(_librarian, totalCopies: 3);
        var cache = _factory.Services.GetRequiredService<IAvailabilityCache>();

        var cached = await GetAvailabilityAsync(_librarian, book.Id);
        Assert.True(cached.IsActive);
        Assert.Equal(3, cached.AvailableCopies);

        var redis = await cache.GetAsync(book.Id, CancellationToken.None);
        Assert.NotNull(redis);
        Assert.True(redis.IsActive);

        var deactivate = await _librarian.DeleteAsync($"/books/{book.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deactivate.StatusCode);
        Assert.Null(await cache.GetAsync(book.Id, CancellationToken.None));

        var after = await GetAvailabilityAsync(_librarian, book.Id);
        Assert.False(after.IsActive);
        Assert.Equal(book.Id, after.BookId);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
        var persisted = await db.Books.SingleAsync(item => item.Id == book.Id);
        Assert.False(persisted.IsActive);
        Assert.Contains(
            await db.AuditEvents.Where(audit => audit.EntityId == book.Id).ToListAsync(),
            audit => audit.Action == AuditMetadata.BookDeactivated);
        Assert.Contains(
            await LoadAvailabilityMessagesAsync(db, book.Id),
            message => message.ProcessedAtUtc is null);
    }

    [Fact]
    public async Task Redis_invalidation_failure_does_not_rollback_deactivation()
    {
        await using var host = new CustomWebApplicationFactory(
            _database,
            services => CallbackAvailabilityCache.Register(services, cache => cache.FailAllRemoves = true));
        var client = host.CreateClient().WithTestAuth("deactivate-redis-fail-librarian", LibrarianPolicy.Role);

        var book = await CreateBookAsync(client, totalCopies: 2);
        var availability = await GetAvailabilityAsync(client, book.Id);
        Assert.True(availability.IsActive);

        var callback = host.Services.GetRequiredService<CallbackAvailabilityCache>();
        var cachedBefore = await callback.GetAsync(book.Id, CancellationToken.None);
        Assert.NotNull(cachedBefore);
        Assert.True(cachedBefore.IsActive);

        var deactivate = await client.DeleteAsync($"/books/{book.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deactivate.StatusCode);

        var staleCache = await callback.GetAsync(book.Id, CancellationToken.None);
        Assert.NotNull(staleCache);
        Assert.True(staleCache.IsActive);

        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
        var persisted = await db.Books.SingleAsync(item => item.Id == book.Id);
        Assert.False(persisted.IsActive);
        Assert.Contains(
            await db.AuditEvents.Where(audit => audit.EntityId == book.Id).ToListAsync(),
            audit => audit.Action == AuditMetadata.BookDeactivated);
        Assert.Contains(
            await LoadAvailabilityMessagesAsync(db, book.Id),
            message => message.Type == AvailabilityOutbox.MessageType && message.ProcessedAtUtc is null);

        var catalog = await client.GetFromJsonAsync<BookDto>($"/books/{book.Id}", JsonOptions);
        Assert.NotNull(catalog);
        Assert.False(catalog.IsActive);
    }

    private static async Task<BookDto> CreateBookAsync(HttpClient client, int totalCopies)
    {
        var response = await client.PostAsJsonAsync(
            "/books",
            new { title = "Dune", isbn = Guid.NewGuid().ToString("N")[..12], author = "Frank Herbert", totalCopies });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var book = await response.Content.ReadFromJsonAsync<BookDto>(JsonOptions);
        Assert.NotNull(book);
        Assert.True(book.IsActive);
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

    private static async Task<List<OutboxMessage>> LoadAvailabilityMessagesAsync(
        LibraryDbContext db,
        Guid bookId)
    {
        var bookIdText = bookId.ToString();
        var messages = await db.OutboxMessages
            .Where(message => message.Type == AvailabilityOutbox.MessageType)
            .ToListAsync();
        return messages
            .Where(message => message.PayloadJson.Contains(bookIdText, StringComparison.Ordinal))
            .ToList();
    }
}
