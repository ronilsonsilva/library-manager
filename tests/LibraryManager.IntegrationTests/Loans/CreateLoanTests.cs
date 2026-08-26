using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LibraryManager.Api.Middleware;
using LibraryManager.Api.Security;
using LibraryManager.Application.Books;
using LibraryManager.Application.Common;
using LibraryManager.Application.Loans;
using LibraryManager.Application.Users;
using LibraryManager.Infrastructure.Persistence;
using LibraryManager.IntegrationTests;
using LibraryManager.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LibraryManager.IntegrationTests.Loans;

[Collection(DatabaseCollection.Name)]
public sealed class CreateLoanTests : IAsyncLifetime
{
    private const int LastCopyRepeatCount = 10;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly DatabaseFixture _database;
    private CustomWebApplicationFactory _hostA = null!;
    private CustomWebApplicationFactory _hostB = null!;
    private HttpClient _librarianA = null!;
    private HttpClient _librarianB = null!;

    public CreateLoanTests(DatabaseFixture database)
    {
        _database = database;
    }

    public Task InitializeAsync()
    {
        _hostA = new CustomWebApplicationFactory(_database);
        _hostB = new CustomWebApplicationFactory(
            _database.PostgresConnectionString,
            _database.RedisConnectionString);
        _librarianA = _hostA.CreateClient().WithTestAuth("loan-host-a", LibrarianPolicy.Role);
        _librarianB = _hostB.CreateClient().WithTestAuth("loan-host-b", LibrarianPolicy.Role);
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
    public async Task Create_loan_returns_201_decrements_availability_and_persists_audit_and_outbox()
    {
        const string subject = "loan-host-a";
        const string correlationId = "loan-create-correlation";
        _librarianA.DefaultRequestHeaders.Remove(CorrelationIdMiddleware.HeaderName);
        _librarianA.DefaultRequestHeaders.Add(CorrelationIdMiddleware.HeaderName, correlationId);

        var book = await CreateBookAsync(totalCopies: 2);
        var user = await CreateUserAsync("Ada Lovelace");

        var response = await PostLoanAsync(_librarianA, book.Id, user.Id, Guid.NewGuid().ToString("N"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var loan = await response.Content.ReadFromJsonAsync<LoanDto>(JsonOptions);
        Assert.NotNull(loan);
        Assert.Equal(book.Id, loan.BookId);
        Assert.Equal(user.Id, loan.UserId);
        Assert.Equal("Active", loan.Status);
        Assert.Equal(loan.BorrowedAtUtc.AddDays(14), loan.DueAtUtc);
        Assert.Null(loan.ReturnedAtUtc);
        Assert.Null(loan.CancelledAtUtc);

        var after = await _librarianA.GetFromJsonAsync<BookDto>($"/books/{book.Id}", JsonOptions);
        Assert.NotNull(after);
        Assert.Equal(1, after.AvailableCopies);

        await using var scope = _hostA.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
        Assert.Equal(1, await db.Loans.CountAsync(item => item.BookId == book.Id));
        var audit = await db.AuditEvents.SingleAsync(item =>
            item.Action == AuditMetadata.LoanCreated && item.EntityId == loan.Id);
        Assert.Equal(AuditMetadata.LoanEntity, audit.EntityType);
        Assert.Equal(subject, audit.ActorId);
        Assert.Equal(correlationId, audit.CorrelationId);
        Assert.Equal(2, await CountAvailabilityOutboxAsync(db, book.Id));

        _librarianA.DefaultRequestHeaders.Remove(CorrelationIdMiddleware.HeaderName);
    }

    [Fact]
    public async Task Unknown_user_returns_404()
    {
        var book = await CreateBookAsync(totalCopies: 1);

        var response = await PostLoanAsync(_librarianA, book.Id, Guid.NewGuid(), Guid.NewGuid().ToString("N"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        Assert.NotNull(problem?.Detail);
        Assert.Contains("User", problem.Detail, StringComparison.Ordinal);
        Assert.Equal(0, await CountLoansForBookAsync(book.Id));
    }

    [Fact]
    public async Task Unknown_book_returns_404()
    {
        var user = await CreateUserAsync("Unknown Book Borrower");

        var response = await PostLoanAsync(_librarianA, Guid.NewGuid(), user.Id, Guid.NewGuid().ToString("N"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        Assert.NotNull(problem?.Detail);
        Assert.Contains("Book", problem.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Missing_idempotency_key_returns_400()
    {
        var book = await CreateBookAsync(totalCopies: 1);
        var user = await CreateUserAsync("Missing Key Borrower");

        var response = await _librarianA.PostAsJsonAsync("/loans", new { bookId = book.Id, userId = user.Id });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(JsonOptions);
        Assert.NotNull(problem);
        Assert.Contains("Validation_IdempotencyKey_Required", problem.Errors.Keys);
        Assert.Equal(0, await CountLoansForBookAsync(book.Id));
    }

    [Fact]
    public async Task Inactive_book_returns_422()
    {
        var book = await CreateBookAsync(totalCopies: 1);
        var user = await CreateUserAsync("Inactive Book Borrower");
        var deactivate = await _librarianA.DeleteAsync($"/books/{book.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deactivate.StatusCode);

        var response = await PostLoanAsync(_librarianA, book.Id, user.Id, Guid.NewGuid().ToString("N"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        Assert.NotNull(problem?.Detail);
        Assert.Contains("not active", problem.Detail, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, await CountLoansForBookAsync(book.Id));
    }

    [Fact]
    public async Task Zero_copies_returns_422()
    {
        var book = await CreateBookAsync(totalCopies: 1);
        await SeedAvailableCopiesAsync(book.Id, 0);
        var user = await CreateUserAsync("Zero Copy Borrower");

        var response = await PostLoanAsync(_librarianA, book.Id, user.Id, Guid.NewGuid().ToString("N"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        Assert.NotNull(problem?.Detail);
        Assert.Contains("available", problem.Detail, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, await CountLoansForBookAsync(book.Id));
        var after = await _librarianA.GetFromJsonAsync<BookDto>($"/books/{book.Id}", JsonOptions);
        Assert.NotNull(after);
        Assert.Equal(0, after.AvailableCopies);
    }

    [Fact]
    public async Task Duplicate_active_loan_returns_422()
    {
        var book = await CreateBookAsync(totalCopies: 2);
        var user = await CreateUserAsync("Duplicate Borrower");
        var first = await PostLoanAsync(_librarianA, book.Id, user.Id, Guid.NewGuid().ToString("N"));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await PostLoanAsync(_librarianA, book.Id, user.Id, Guid.NewGuid().ToString("N"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, second.StatusCode);
        var problem = await second.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        Assert.NotNull(problem?.Detail);
        Assert.Contains("active loan", problem.Detail, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, await CountLoansForBookAsync(book.Id));
        var after = await _librarianA.GetFromJsonAsync<BookDto>($"/books/{book.Id}", JsonOptions);
        Assert.NotNull(after);
        Assert.Equal(1, after.AvailableCopies);
    }

    [Fact]
    public async Task Concurrent_last_copy_through_two_hosts_has_one_winner()
    {
        await AssertLastCopyRaceAsync();
    }

    [Fact]
    public async Task Concurrent_last_copy_remains_correct_across_repeated_races()
    {
        for (var attempt = 0; attempt < LastCopyRepeatCount; attempt++)
        {
            await AssertLastCopyRaceAsync();
        }
    }

    private async Task AssertLastCopyRaceAsync()
    {
        var book = await CreateBookAsync(totalCopies: 1);
        var userA = await CreateUserAsync("Last Copy A");
        var userB = await CreateUserAsync("Last Copy B");

        var firstTask = PostLoanAsync(_librarianA, book.Id, userA.Id, Guid.NewGuid().ToString("N"));
        var secondTask = PostLoanAsync(_librarianB, book.Id, userB.Id, Guid.NewGuid().ToString("N"));
        await Task.WhenAll(firstTask, secondTask);

        using var first = firstTask.Result;
        using var second = secondTask.Result;
        var statuses = new[] { first.StatusCode, second.StatusCode };

        Assert.Equal(1, statuses.Count(status => status == HttpStatusCode.Created));
        Assert.Equal(1, statuses.Count(status => status == HttpStatusCode.UnprocessableEntity));

        var winner = first.StatusCode == HttpStatusCode.Created ? first : second;
        var loser = first.StatusCode == HttpStatusCode.UnprocessableEntity ? first : second;
        var loan = await winner.Content.ReadFromJsonAsync<LoanDto>(JsonOptions);
        Assert.NotNull(loan);
        Assert.Equal(book.Id, loan.BookId);
        var problem = await loser.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        Assert.NotNull(problem?.Detail);
        Assert.Contains("available", problem.Detail, StringComparison.OrdinalIgnoreCase);

        var after = await _librarianA.GetFromJsonAsync<BookDto>($"/books/{book.Id}", JsonOptions);
        Assert.NotNull(after);
        Assert.Equal(0, after.AvailableCopies);
        Assert.True(after.AvailableCopies >= 0);

        await using var scope = _hostA.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
        var loans = await db.Loans.Where(item => item.BookId == book.Id).ToListAsync();
        Assert.Single(loans);
        Assert.Equal(loan.Id, loans[0].Id);
        Assert.Equal(
            1,
            await db.AuditEvents.CountAsync(item =>
                item.Action == AuditMetadata.LoanCreated && item.EntityId == loan.Id));
        Assert.Equal(2, await CountAvailabilityOutboxAsync(db, book.Id));
    }

    private async Task<BookDto> CreateBookAsync(int totalCopies)
    {
        var response = await _librarianA.PostAsJsonAsync(
            "/books",
            new
            {
                title = "Dune",
                isbn = UniqueIsbn(),
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

    private async Task<int> CountLoansForBookAsync(Guid bookId)
    {
        await using var scope = _hostA.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
        return await db.Loans.CountAsync(item => item.BookId == bookId);
    }

    private static async Task<int> CountAvailabilityOutboxAsync(LibraryDbContext db, Guid bookId)
    {
        var messages = await db.OutboxMessages
            .Where(message => message.Type == AvailabilityOutbox.MessageType)
            .ToListAsync();
        var bookIdText = bookId.ToString();
        return messages.Count(message =>
            message.PayloadJson.Contains(bookIdText, StringComparison.Ordinal));
    }

    private async Task SeedAvailableCopiesAsync(Guid bookId, int availableCopies)
    {
        await using var scope = _hostA.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE books SET available_copies = {availableCopies} WHERE id = {bookId}");
    }

    private static string UniqueIsbn() => Guid.NewGuid().ToString("N")[..12];
}
