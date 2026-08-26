using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LibraryManager.Api.Middleware;
using LibraryManager.Api.Security;
using LibraryManager.Application.Audit;
using LibraryManager.Application.Books;
using LibraryManager.Application.Common;
using LibraryManager.Application.Loans;
using LibraryManager.Application.Users;
using LibraryManager.IntegrationTests;
using LibraryManager.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace LibraryManager.IntegrationTests.Audit;

[Collection(DatabaseCollection.Name)]
public sealed class AuditEventTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly CustomWebApplicationFactory _factory;
    private HttpClient _librarian = null!;

    public AuditEventTests(DatabaseFixture database)
    {
        _factory = new CustomWebApplicationFactory(database);
    }

    public Task InitializeAsync()
    {
        _librarian = _factory.CreateClient().WithTestAuth("audit-librarian", LibrarianPolicy.Role);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _librarian.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Librarian_can_query_loan_created_audit_with_actor_correlation_and_context()
    {
        const string subject = "audit-librarian";
        const string correlationId = "audit-loan-created-correlation";
        _librarian.DefaultRequestHeaders.Remove(CorrelationIdMiddleware.HeaderName);
        _librarian.DefaultRequestHeaders.Add(CorrelationIdMiddleware.HeaderName, correlationId);

        var book = await CreateBookAsync(1);
        var user = await CreateUserAsync("Audit Borrower");
        var loan = await CreateLoanAsync(book.Id, user.Id);

        var page = await _librarian.GetFromJsonAsync<PagedResult<AuditEventDto>>(
            $"/audit-events?entityType=Loan&entityId={loan.Id}",
            JsonOptions);
        Assert.NotNull(page);
        Assert.Equal(1, page.Page);
        Assert.Equal(20, page.PageSize);
        var created = Assert.Single(page.Items, item => item.Action == AuditMetadata.LoanCreated);
        Assert.Equal(AuditMetadata.LoanEntity, created.EntityType);
        Assert.Equal(loan.Id, created.EntityId);
        Assert.Equal(subject, created.ActorId);
        Assert.Equal(correlationId, created.CorrelationId);
        Assert.Equal(book.Id, created.DataJson.GetProperty("bookId").GetGuid());
        Assert.Equal(user.Id, created.DataJson.GetProperty("userId").GetGuid());
        Assert.True(created.DataJson.TryGetProperty("dueAtUtc", out _));
        Assert.NotEqual(default, created.OccurredAtUtc);

        _librarian.DefaultRequestHeaders.Remove(CorrelationIdMiddleware.HeaderName);
    }

    [Fact]
    public async Task Audit_events_require_librarian_and_authentication()
    {
        var anonymous = _factory.CreateClient();
        var unauthorized = await anonymous.GetAsync("/audit-events");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);

        var reader = _factory.CreateClient().WithTestAuth("audit-reader");
        var forbidden = await reader.GetAsync("/audit-events");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task Rejected_mutation_does_not_write_a_success_audit_for_that_correlation()
    {
        const string correlationId = "audit-rejected-duplicate-isbn";
        var isbn = Guid.NewGuid().ToString("N")[..12];
        var first = await _librarian.PostAsJsonAsync(
            "/books",
            new { title = "Dune", isbn, author = "Frank Herbert", totalCopies = 1 });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        _librarian.DefaultRequestHeaders.Remove(CorrelationIdMiddleware.HeaderName);
        _librarian.DefaultRequestHeaders.Add(CorrelationIdMiddleware.HeaderName, correlationId);
        var duplicate = await _librarian.PostAsJsonAsync(
            "/books",
            new { title = "Other", isbn, author = "Other", totalCopies = 1 });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, duplicate.StatusCode);

        var page = await _librarian.GetFromJsonAsync<PagedResult<AuditEventDto>>(
            "/audit-events?page=1&pageSize=100",
            JsonOptions);
        Assert.NotNull(page);
        Assert.DoesNotContain(page.Items, item => item.CorrelationId == correlationId);

        _librarian.DefaultRequestHeaders.Remove(CorrelationIdMiddleware.HeaderName);
    }

    [Fact]
    public async Task Rejected_loan_does_not_write_loan_created_audit()
    {
        const string correlationId = "audit-rejected-inactive-loan";
        var book = await CreateBookAsync(1);
        var user = await CreateUserAsync("Rejected Loan Borrower");
        var deactivate = await _librarian.DeleteAsync($"/books/{book.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deactivate.StatusCode);

        _librarian.DefaultRequestHeaders.Remove(CorrelationIdMiddleware.HeaderName);
        _librarian.DefaultRequestHeaders.Add(CorrelationIdMiddleware.HeaderName, correlationId);
        var response = await PostLoanAsync(book.Id, user.Id);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        Assert.NotNull(problem?.Detail);

        var page = await _librarian.GetFromJsonAsync<PagedResult<AuditEventDto>>(
            "/audit-events?page=1&pageSize=100",
            JsonOptions);
        Assert.NotNull(page);
        Assert.DoesNotContain(
            page.Items,
            item => item.CorrelationId == correlationId && item.Action == AuditMetadata.LoanCreated);

        _librarian.DefaultRequestHeaders.Remove(CorrelationIdMiddleware.HeaderName);
    }

    [Fact]
    public async Task Audit_list_caps_page_size_at_100()
    {
        var page = await _librarian.GetFromJsonAsync<PagedResult<AuditEventDto>>(
            "/audit-events?page=1&pageSize=1000",
            JsonOptions);
        Assert.NotNull(page);
        Assert.Equal(100, page.PageSize);
        Assert.True(page.Items.Count <= 100);
    }

    [Fact]
    public async Task Authenticated_non_librarian_can_read_user_and_book_loan_history()
    {
        var book = await CreateBookAsync(1);
        var user = await CreateUserAsync("History Reader Borrower");
        var loan = await CreateLoanAsync(book.Id, user.Id);

        var reader = _factory.CreateClient().WithTestAuth("history-reader");
        var userHistory = await reader.GetFromJsonAsync<PagedResult<LoanDto>>(
            $"/users/{user.Id}/loans",
            JsonOptions);
        Assert.NotNull(userHistory);
        Assert.Contains(userHistory.Items, item => item.Id == loan.Id);

        var bookHistory = await reader.GetFromJsonAsync<PagedResult<LoanDto>>(
            $"/books/{book.Id}/loans",
            JsonOptions);
        Assert.NotNull(bookHistory);
        Assert.Contains(bookHistory.Items, item => item.Id == loan.Id);
    }

    private async Task<BookDto> CreateBookAsync(int totalCopies)
    {
        var response = await _librarian.PostAsJsonAsync(
            "/books",
            new { title = "Dune", isbn = Guid.NewGuid().ToString("N")[..12], author = "Frank Herbert", totalCopies });
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

    private async Task<LoanDto> CreateLoanAsync(Guid bookId, Guid userId)
    {
        var response = await PostLoanAsync(bookId, userId);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var loan = await response.Content.ReadFromJsonAsync<LoanDto>(JsonOptions);
        Assert.NotNull(loan);
        return loan;
    }

    private async Task<HttpResponseMessage> PostLoanAsync(Guid bookId, Guid userId)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/loans")
        {
            Content = JsonContent.Create(new { bookId, userId })
        };
        request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.NewGuid().ToString("N"));
        return await _librarian.SendAsync(request);
    }
}
