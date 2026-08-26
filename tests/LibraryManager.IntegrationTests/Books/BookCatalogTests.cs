using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LibraryManager.Api.Middleware;
using LibraryManager.Api.Security;
using LibraryManager.Application.Books;
using LibraryManager.Application.Common;
using LibraryManager.Infrastructure.Persistence;
using LibraryManager.IntegrationTests;
using LibraryManager.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LibraryManager.IntegrationTests.Books;

[Collection(DatabaseCollection.Name)]
public sealed class BookCatalogTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly CustomWebApplicationFactory _factory;
    private HttpClient _librarian = null!;

    public BookCatalogTests(DatabaseFixture database)
    {
        _factory = new CustomWebApplicationFactory(database);
    }

    public Task InitializeAsync()
    {
        _librarian = CreateLibrarian("catalog-librarian");
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _librarian.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Create_get_list_update_and_deactivate_book()
    {
        const string subject = "catalog-librarian";
        const string correlationId = "book-create-correlation";
        var isbn = UniqueIsbn();
        _librarian.DefaultRequestHeaders.Remove(CorrelationIdMiddleware.HeaderName);
        _librarian.DefaultRequestHeaders.Add(CorrelationIdMiddleware.HeaderName, correlationId);

        var createdResponse = await _librarian.PostAsJsonAsync(
            "/books",
            new { title = "Dune", isbn, author = "Frank Herbert", totalCopies = 2 });
        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        Assert.Equal(correlationId, createdResponse.Headers.GetValues(CorrelationIdMiddleware.HeaderName).Single());
        var created = await createdResponse.Content.ReadFromJsonAsync<BookDto>(JsonOptions);
        Assert.NotNull(created);
        Assert.Equal("Dune", created.Title);
        Assert.Equal(isbn, created.Isbn);
        Assert.Equal(2, created.TotalCopies);
        Assert.Equal(2, created.AvailableCopies);
        Assert.True(created.IsActive);

        var get = await _librarian.GetAsync($"/books/{created.Id}");
        get.EnsureSuccessStatusCode();
        var fetched = await get.Content.ReadFromJsonAsync<BookDto>(JsonOptions);
        Assert.NotNull(fetched);
        Assert.Equal(created.Id, fetched.Id);
        Assert.Equal(isbn, fetched.Isbn);

        var listed = await _librarian.GetFromJsonAsync<PagedResult<BookDto>>("/books?page=1&pageSize=100", JsonOptions);
        Assert.NotNull(listed);
        Assert.Contains(listed.Items, book => book.Id == created.Id);

        var updatedResponse = await _librarian.PutAsJsonAsync(
            $"/books/{created.Id}",
            new { title = "Dune Messiah", author = "Frank Herbert", totalCopies = 5 });
        updatedResponse.EnsureSuccessStatusCode();
        var updated = await updatedResponse.Content.ReadFromJsonAsync<BookDto>(JsonOptions);
        Assert.NotNull(updated);
        Assert.Equal("Dune Messiah", updated.Title);
        Assert.Equal(isbn, updated.Isbn);
        Assert.Equal(5, updated.TotalCopies);
        Assert.Equal(5, updated.AvailableCopies);

        var deactivate = await _librarian.DeleteAsync($"/books/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deactivate.StatusCode);

        var afterDelete = await _librarian.GetFromJsonAsync<BookDto>($"/books/{created.Id}", JsonOptions);
        Assert.NotNull(afterDelete);
        Assert.False(afterDelete.IsActive);
        Assert.Equal(isbn, afterDelete.Isbn);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
        var audits = await db.AuditEvents.Where(audit => audit.EntityId == created.Id).ToListAsync();
        Assert.Contains(audits, audit => audit.Action == AuditMetadata.BookCreated && audit.ActorId == subject);
        Assert.Contains(audits, audit => audit.Action == AuditMetadata.BookUpdated);
        Assert.Contains(audits, audit => audit.Action == AuditMetadata.BookDeactivated);
        Assert.Contains(audits, audit => audit.CorrelationId == correlationId);

        var outbox = await db.OutboxMessages
            .Where(message => message.Type == AvailabilityOutbox.MessageType)
            .ToListAsync();
        Assert.Contains(outbox, message => message.PayloadJson.Contains(created.Id.ToString(), StringComparison.Ordinal));

        _librarian.DefaultRequestHeaders.Remove(CorrelationIdMiddleware.HeaderName);
    }

    [Fact]
    public async Task Duplicate_isbn_returns_422_and_does_not_write_success_audit()
    {
        var isbn = UniqueIsbn();
        var first = await CreateBookAsync("Dune", isbn, "Frank Herbert", 1);

        var duplicate = await _librarian.PostAsJsonAsync(
            "/books",
            new { title = "Other", isbn, author = "Other Author", totalCopies = 1 });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, duplicate.StatusCode);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
        Assert.Equal(1, await db.Books.CountAsync(book => book.Isbn == isbn));
        Assert.Equal(
            1,
            await db.AuditEvents.CountAsync(audit =>
                audit.Action == AuditMetadata.BookCreated && audit.EntityId == first.Id));
    }

    [Fact]
    public async Task Put_does_not_change_isbn()
    {
        var isbn = UniqueIsbn();
        var created = await CreateBookAsync("Dune", isbn, "Frank Herbert", 1);

        var response = await _librarian.PutAsJsonAsync(
            $"/books/{created.Id}",
            new { title = "Dune Messiah", author = "Frank Herbert", totalCopies = 1, isbn = "changed-isbn" });
        response.EnsureSuccessStatusCode();

        var updated = await response.Content.ReadFromJsonAsync<BookDto>(JsonOptions);
        Assert.NotNull(updated);
        Assert.Equal(isbn, updated.Isbn);
    }

    [Fact]
    public async Task Total_copies_below_borrowed_returns_422()
    {
        var created = await CreateBookAsync("Dune", UniqueIsbn(), "Frank Herbert", 2);
        await SeedBorrowedCopiesAsync(created.Id, borrowedCopies: 2);

        var response = await _librarian.PutAsJsonAsync(
            $"/books/{created.Id}",
            new { title = "Dune", author = "Frank Herbert", totalCopies = 1 });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);

        var unchanged = await _librarian.GetFromJsonAsync<BookDto>($"/books/{created.Id}", JsonOptions);
        Assert.NotNull(unchanged);
        Assert.Equal(2, unchanged.TotalCopies);
        Assert.Equal(0, unchanged.AvailableCopies);
    }

    [Fact]
    public async Task Get_unknown_book_returns_404()
    {
        var response = await _librarian.GetAsync($"/books/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task List_uses_default_page_size_and_caps_page_size_at_100()
    {
        var prefix = UniqueIsbn();
        for (var index = 0; index < 21; index++)
        {
            await CreateBookAsync($"Title {index:00}", $"{prefix}{index:00}", "Author", 1);
        }

        var defaults = await _librarian.GetFromJsonAsync<PagedResult<BookDto>>("/books", JsonOptions);
        Assert.NotNull(defaults);
        Assert.Equal(1, defaults.Page);
        Assert.Equal(20, defaults.PageSize);
        Assert.Equal(20, defaults.Items.Count);
        Assert.True(defaults.TotalCount >= 21);

        var oversized = await _librarian.GetFromJsonAsync<PagedResult<BookDto>>("/books?page=1&pageSize=1000", JsonOptions);
        Assert.NotNull(oversized);
        Assert.Equal(100, oversized.PageSize);
        Assert.True(oversized.Items.Count <= 100);
    }

    private HttpClient CreateLibrarian(string subject) =>
        _factory.CreateClient().WithTestAuth(subject, LibrarianPolicy.Role);

    private async Task<BookDto> CreateBookAsync(string title, string isbn, string author, int totalCopies)
    {
        var response = await _librarian.PostAsJsonAsync(
            "/books",
            new { title, isbn, author, totalCopies });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var book = await response.Content.ReadFromJsonAsync<BookDto>(JsonOptions);
        Assert.NotNull(book);
        return book;
    }

    private async Task SeedBorrowedCopiesAsync(Guid bookId, int borrowedCopies)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE books SET available_copies = total_copies - {borrowedCopies} WHERE id = {bookId}");
    }

    private static string UniqueIsbn() => Guid.NewGuid().ToString("N")[..12];
}
