using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LibraryManager.Api.Middleware;
using LibraryManager.Api.Security;
using LibraryManager.Application.Books;
using LibraryManager.Application.Common;
using LibraryManager.Application.Loans;
using LibraryManager.Application.Users;
using LibraryManager.Domain;
using LibraryManager.Infrastructure.Persistence;
using LibraryManager.IntegrationTests;
using LibraryManager.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LibraryManager.IntegrationTests.Loans;

[Collection(DatabaseCollection.Name)]
public sealed class ReturnAndCancelTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly DatabaseFixture _database;
    private CustomWebApplicationFactory _hostA = null!;
    private CustomWebApplicationFactory _hostB = null!;
    private HttpClient _librarianA = null!;
    private HttpClient _librarianB = null!;

    public ReturnAndCancelTests(DatabaseFixture database)
    {
        _database = database;
    }

    public Task InitializeAsync()
    {
        _hostA = new CustomWebApplicationFactory(_database);
        _hostB = new CustomWebApplicationFactory(
            _database.PostgresConnectionString,
            _database.RedisConnectionString);
        _librarianA = _hostA.CreateClient().WithTestAuth("return-host-a", LibrarianPolicy.Role);
        _librarianB = _hostB.CreateClient().WithTestAuth("return-host-b", LibrarianPolicy.Role);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _librarianA.Dispose();
        _librarianB.Dispose();
        await _hostA.DisposeAsync();
        await _hostB.DisposeAsync();
    }

    [Fact]
    public async Task Return_restores_one_copy_and_persists_audit_and_outbox()
    {
        const string subject = "return-host-a";
        const string correlationId = "loan-return-correlation";
        _librarianA.DefaultRequestHeaders.Remove(CorrelationIdMiddleware.HeaderName);
        _librarianA.DefaultRequestHeaders.Add(CorrelationIdMiddleware.HeaderName, correlationId);

        var (book, user, loan) = await CreateActiveLoanAsync(totalCopies: 1);
        Assert.Equal(0, (await GetBookAsync(book.Id)).AvailableCopies);

        var response = await _librarianA.PostAsync($"/loans/{loan.Id}/return", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var returned = await response.Content.ReadFromJsonAsync<LoanDto>(JsonOptions);
        Assert.NotNull(returned);
        Assert.Equal("Returned", returned.Status);
        Assert.NotNull(returned.ReturnedAtUtc);
        Assert.Null(returned.CancelledAtUtc);
        Assert.Equal(1, (await GetBookAsync(book.Id)).AvailableCopies);

        var history = await GetUserLoansAsync(user.Id);
        Assert.Contains(history.Items, item => item.Id == loan.Id && item.Status == "Returned");

        await using var scope = _hostA.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
        var audit = await db.AuditEvents.SingleAsync(item =>
            item.Action == AuditMetadata.LoanReturned && item.EntityId == loan.Id);
        Assert.Equal(subject, audit.ActorId);
        Assert.Equal(correlationId, audit.CorrelationId);
        var outbox = await db.OutboxMessages
            .Where(message => message.Type == AvailabilityOutbox.MessageType)
            .ToListAsync();
        Assert.Contains(outbox, message => message.PayloadJson.Contains(book.Id.ToString(), StringComparison.Ordinal));

        _librarianA.DefaultRequestHeaders.Remove(CorrelationIdMiddleware.HeaderName);
    }

    [Fact]
    public async Task Cancel_restores_one_copy_and_keeps_the_loan_in_history()
    {
        var (book, user, loan) = await CreateActiveLoanAsync(totalCopies: 1);

        var response = await _librarianA.PostAsync($"/loans/{loan.Id}/cancel", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var cancelled = await response.Content.ReadFromJsonAsync<LoanDto>(JsonOptions);
        Assert.NotNull(cancelled);
        Assert.Equal("Cancelled", cancelled.Status);
        Assert.NotNull(cancelled.CancelledAtUtc);
        Assert.Null(cancelled.ReturnedAtUtc);
        Assert.Equal(1, (await GetBookAsync(book.Id)).AvailableCopies);

        var history = await GetUserLoansAsync(user.Id);
        Assert.Contains(history.Items, item => item.Id == loan.Id && item.Status == "Cancelled");
    }

    [Fact]
    public async Task History_preserves_returned_and_cancelled_loans_including_after_book_deactivation()
    {
        var returnedBook = await CreateBookAsync(1);
        var cancelledBook = await CreateBookAsync(1);
        var user = await CreateUserAsync("History Borrower");
        var returnedLoan = await CreateLoanAsync(returnedBook.Id, user.Id);
        var cancelledLoan = await CreateLoanAsync(cancelledBook.Id, user.Id);

        var returned = await _librarianA.PostAsync($"/loans/{returnedLoan.Id}/return", null);
        var cancelled = await _librarianA.PostAsync($"/loans/{cancelledLoan.Id}/cancel", null);
        Assert.Equal(HttpStatusCode.OK, returned.StatusCode);
        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);

        var deactivateReturned = await _librarianA.DeleteAsync($"/books/{returnedBook.Id}");
        var deactivateCancelled = await _librarianA.DeleteAsync($"/books/{cancelledBook.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deactivateReturned.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, deactivateCancelled.StatusCode);

        var history = await GetUserLoansAsync(user.Id);
        Assert.Equal(2, history.TotalCount);
        Assert.Contains(history.Items, item => item.Id == returnedLoan.Id && item.Status == "Returned");
        Assert.Contains(history.Items, item => item.Id == cancelledLoan.Id && item.Status == "Cancelled");
        Assert.Equal(1, (await GetBookAsync(returnedBook.Id)).AvailableCopies);
        Assert.Equal(1, (await GetBookAsync(cancelledBook.Id)).AvailableCopies);
    }

    [Fact]
    public async Task Active_loan_on_deactivated_book_can_still_be_returned()
    {
        var (book, _, loan) = await CreateActiveLoanAsync(totalCopies: 1);
        var deactivate = await _librarianA.DeleteAsync($"/books/{book.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deactivate.StatusCode);

        var response = await _librarianA.PostAsync($"/loans/{loan.Id}/return", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, (await GetBookAsync(book.Id)).AvailableCopies);
        Assert.False((await GetBookAsync(book.Id)).IsActive);
    }

    [Fact]
    public async Task Return_of_non_active_loan_returns_422_and_does_not_change_copies()
    {
        var (book, _, loan) = await CreateActiveLoanAsync(totalCopies: 1);
        var first = await _librarianA.PostAsync($"/loans/{loan.Id}/return", null);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await _librarianA.PostAsync($"/loans/{loan.Id}/return", null);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, second.StatusCode);
        var problem = await second.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        Assert.NotNull(problem?.Detail);
        Assert.Contains("Active", problem.Detail, StringComparison.Ordinal);
        Assert.Equal(1, (await GetBookAsync(book.Id)).AvailableCopies);
    }

    [Fact]
    public async Task Cancel_of_returned_loan_returns_422()
    {
        var (_, _, loan) = await CreateActiveLoanAsync(totalCopies: 1);
        var returned = await _librarianA.PostAsync($"/loans/{loan.Id}/return", null);
        Assert.Equal(HttpStatusCode.OK, returned.StatusCode);

        var cancel = await _librarianA.PostAsync($"/loans/{loan.Id}/cancel", null);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, cancel.StatusCode);
    }

    [Fact]
    public async Task Unknown_loan_return_and_cancel_return_404()
    {
        var id = Guid.NewGuid();

        var returnResponse = await _librarianA.PostAsync($"/loans/{id}/return", null);
        var cancelResponse = await _librarianA.PostAsync($"/loans/{id}/cancel", null);

        Assert.Equal(HttpStatusCode.NotFound, returnResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, cancelResponse.StatusCode);
    }

    [Fact]
    public async Task Concurrent_duplicate_return_through_two_hosts_restores_inventory_once()
    {
        var (book, _, loan) = await CreateActiveLoanAsync(totalCopies: 1);

        var firstTask = _librarianA.PostAsync($"/loans/{loan.Id}/return", null);
        var secondTask = _librarianB.PostAsync($"/loans/{loan.Id}/return", null);
        await Task.WhenAll(firstTask, secondTask);

        var first = await firstTask;
        var second = await secondTask;
        var statuses = new[] { first.StatusCode, second.StatusCode };

        Assert.Equal(1, statuses.Count(status => status == HttpStatusCode.OK));
        Assert.Equal(1, statuses.Count(status => status == HttpStatusCode.UnprocessableEntity));
        Assert.Equal(1, (await GetBookAsync(book.Id)).AvailableCopies);

        await using var scope = _hostA.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
        var stored = await db.Loans.SingleAsync(item => item.Id == loan.Id);
        Assert.Equal(LoanStatus.Returned, stored.Status);
        Assert.Equal(
            1,
            await db.AuditEvents.CountAsync(item =>
                item.EntityId == loan.Id && item.Action == AuditMetadata.LoanReturned));
    }

    [Fact]
    public async Task Concurrent_return_and_cancel_leave_one_terminal_status_and_restore_once()
    {
        var (book, _, loan) = await CreateActiveLoanAsync(totalCopies: 1);

        var returnTask = _librarianA.PostAsync($"/loans/{loan.Id}/return", null);
        var cancelTask = _librarianB.PostAsync($"/loans/{loan.Id}/cancel", null);
        await Task.WhenAll(returnTask, cancelTask);

        var returned = await returnTask;
        var cancelled = await cancelTask;
        var statuses = new[] { returned.StatusCode, cancelled.StatusCode };

        Assert.Equal(1, statuses.Count(status => status == HttpStatusCode.OK));
        Assert.Equal(1, statuses.Count(status => status == HttpStatusCode.UnprocessableEntity));
        Assert.Equal(1, (await GetBookAsync(book.Id)).AvailableCopies);

        await using var scope = _hostA.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
        var stored = await db.Loans.SingleAsync(item => item.Id == loan.Id);
        Assert.True(stored.Status is LoanStatus.Returned or LoanStatus.Cancelled);
        Assert.Equal(
            1,
            await db.AuditEvents.CountAsync(item =>
                item.EntityId == loan.Id &&
                (item.Action == AuditMetadata.LoanReturned || item.Action == AuditMetadata.LoanCancelled)));
    }

    private async Task<(BookDto Book, UserDto User, LoanDto Loan)> CreateActiveLoanAsync(int totalCopies)
    {
        var book = await CreateBookAsync(totalCopies);
        var user = await CreateUserAsync("Circulation Borrower");
        var loan = await CreateLoanAsync(book.Id, user.Id);
        return (book, user, loan);
    }

    private async Task<BookDto> CreateBookAsync(int totalCopies)
    {
        var response = await _librarianA.PostAsJsonAsync(
            "/books",
            new
            {
                title = "Dune",
                isbn = Guid.NewGuid().ToString("N")[..12],
                author = "Frank Herbert",
                totalCopies
            });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var book = await response.Content.ReadFromJsonAsync<BookDto>(JsonOptions);
        Assert.NotNull(book);
        return book;
    }

    private async Task<UserDto> CreateUserAsync(string name)
    {
        var response = await _librarianA.PostAsJsonAsync(
            "/users",
            new { name, email = $"{Guid.NewGuid():N}@example.com" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var user = await response.Content.ReadFromJsonAsync<UserDto>(JsonOptions);
        Assert.NotNull(user);
        return user;
    }

    private async Task<LoanDto> CreateLoanAsync(Guid bookId, Guid userId)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/loans")
        {
            Content = JsonContent.Create(new { bookId, userId })
        };
        request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.NewGuid().ToString("N"));
        var response = await _librarianA.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var loan = await response.Content.ReadFromJsonAsync<LoanDto>(JsonOptions);
        Assert.NotNull(loan);
        return loan;
    }

    private async Task<BookDto> GetBookAsync(Guid bookId)
    {
        var book = await _librarianA.GetFromJsonAsync<BookDto>($"/books/{bookId}", JsonOptions);
        Assert.NotNull(book);
        return book;
    }

    private async Task<PagedResult<LoanDto>> GetUserLoansAsync(Guid userId)
    {
        var page = await _librarianA.GetFromJsonAsync<PagedResult<LoanDto>>(
            $"/users/{userId}/loans",
            JsonOptions);
        Assert.NotNull(page);
        return page;
    }
}
