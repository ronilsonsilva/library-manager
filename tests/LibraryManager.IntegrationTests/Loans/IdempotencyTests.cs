using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LibraryManager.Api.Security;
using LibraryManager.Application.Abstractions;
using LibraryManager.Application.Books;
using LibraryManager.Application.Loans;
using LibraryManager.Application.Users;
using LibraryManager.Infrastructure.Outbox;
using LibraryManager.Infrastructure.Persistence;
using LibraryManager.IntegrationTests;
using LibraryManager.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LibraryManager.IntegrationTests.Loans;

[Collection(DatabaseCollection.Name)]
public sealed class IdempotencyTests : IAsyncLifetime
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

    public IdempotencyTests(DatabaseFixture database)
    {
        _database = database;
    }

    public Task InitializeAsync()
    {
        _hostA = new CustomWebApplicationFactory(_database);
        _hostB = new CustomWebApplicationFactory(
            _database.PostgresConnectionString,
            _database.RedisConnectionString);
        _librarianA = _hostA.CreateClient().WithTestAuth("idempotency-host-a", LibrarianPolicy.Role);
        _librarianB = _hostB.CreateClient().WithTestAuth("idempotency-host-b", LibrarianPolicy.Role);
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
    public async Task Missing_idempotency_key_returns_400()
    {
        var book = await CreateBookAsync(totalCopies: 1);
        var user = await CreateUserAsync("Missing Key");

        var response = await _librarianA.PostAsJsonAsync("/loans", new { bookId = book.Id, userId = user.Id });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await CountLoansForBookAsync(book.Id));
    }

    [Fact]
    public async Task Sequential_replay_returns_201_with_stored_loan_and_does_not_lend_twice()
    {
        var book = await CreateBookAsync(totalCopies: 2);
        var user = await CreateUserAsync("Replay Borrower");
        var key = Guid.NewGuid().ToString("N");

        var firstResponse = await PostLoanAsync(_librarianA, book.Id, user.Id, key);
        var secondResponse = await PostLoanAsync(_librarianA, book.Id, user.Id, key);

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, secondResponse.StatusCode);

        var first = await firstResponse.Content.ReadFromJsonAsync<LoanDto>(JsonOptions);
        var second = await secondResponse.Content.ReadFromJsonAsync<LoanDto>(JsonOptions);
        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(first.Id, second.Id);
        Assert.Equal(first.BookId, second.BookId);
        Assert.Equal(first.UserId, second.UserId);
        Assert.Equal(first.BorrowedAtUtc, second.BorrowedAtUtc);
        Assert.Equal(first.DueAtUtc, second.DueAtUtc);

        var after = await _librarianA.GetFromJsonAsync<BookDto>($"/books/{book.Id}", JsonOptions);
        Assert.NotNull(after);
        Assert.Equal(1, after.AvailableCopies);
        Assert.Equal(1, await CountLoansForBookAsync(book.Id));
    }

    [Fact]
    public async Task Different_payload_with_same_key_returns_409_and_does_not_apply_second_lend()
    {
        var book = await CreateBookAsync(totalCopies: 2);
        var otherBook = await CreateBookAsync(totalCopies: 2);
        var user = await CreateUserAsync("Conflict Borrower");
        var otherUser = await CreateUserAsync("Other Borrower");
        var key = Guid.NewGuid().ToString("N");

        var firstResponse = await PostLoanAsync(_librarianA, book.Id, user.Id, key);
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        var first = await firstResponse.Content.ReadFromJsonAsync<LoanDto>(JsonOptions);
        Assert.NotNull(first);

        var conflict = await PostLoanAsync(_librarianA, otherBook.Id, otherUser.Id, key);

        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        var problem = await conflict.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        Assert.NotNull(problem?.Detail);
        Assert.Contains("Idempotency-Key", problem.Detail, StringComparison.Ordinal);

        Assert.Equal(1, await CountLoansForBookAsync(book.Id));
        Assert.Equal(0, await CountLoansForBookAsync(otherBook.Id));

        var original = await _librarianA.GetFromJsonAsync<BookDto>($"/books/{book.Id}", JsonOptions);
        var untouched = await _librarianA.GetFromJsonAsync<BookDto>($"/books/{otherBook.Id}", JsonOptions);
        Assert.NotNull(original);
        Assert.NotNull(untouched);
        Assert.Equal(1, original.AvailableCopies);
        Assert.Equal(2, untouched.AvailableCopies);
    }

    [Fact]
    public async Task Concurrent_same_key_through_two_hosts_creates_one_loan_and_replays()
    {
        var book = await CreateBookAsync(totalCopies: 2);
        var user = await CreateUserAsync("Concurrent Borrower");
        var key = Guid.NewGuid().ToString("N");

        var firstTask = PostLoanAsync(_librarianA, book.Id, user.Id, key);
        var secondTask = PostLoanAsync(_librarianB, book.Id, user.Id, key);
        await Task.WhenAll(firstTask, secondTask);

        var firstResponse = await firstTask;
        var secondResponse = await secondTask;

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, secondResponse.StatusCode);

        var first = await firstResponse.Content.ReadFromJsonAsync<LoanDto>(JsonOptions);
        var second = await secondResponse.Content.ReadFromJsonAsync<LoanDto>(JsonOptions);
        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(first.Id, second.Id);

        var after = await _librarianA.GetFromJsonAsync<BookDto>($"/books/{book.Id}", JsonOptions);
        Assert.NotNull(after);
        Assert.Equal(1, after.AvailableCopies);
        Assert.Equal(1, await CountLoansForBookAsync(book.Id));
    }

    [Fact]
    public async Task Unexpected_failure_after_key_reserve_rolls_back_ownership_so_retry_creates_the_loan()
    {
        var gate = new OutboxFailureGate();
        await using var factory = new CustomWebApplicationFactory(
            _database.PostgresConnectionString,
            _database.RedisConnectionString,
            services =>
            {
                services.AddSingleton(gate);
                services.RemoveAll<IOutboxWriter>();
                services.AddScoped<IOutboxWriter>(provider => new GatedOutboxWriter(
                    new OutboxWriter(provider.GetRequiredService<LibraryDbContext>()),
                    provider.GetRequiredService<OutboxFailureGate>()));
            });
        using var client = factory.CreateClient().WithTestAuth("idempotency-rollback", LibrarianPolicy.Role);

        var book = await CreateBookAsync(client, totalCopies: 1);
        var user = await CreateUserAsync(client, "Rollback Borrower");
        var key = Guid.NewGuid().ToString("N");

        gate.ThrowOnNextWrite = true;
        var failed = await PostLoanAsync(client, book.Id, user.Id, key);
        Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
            Assert.Equal(0, await db.Loans.CountAsync(item => item.BookId == book.Id));
            Assert.False(await db.IdempotencyEntries.AnyAsync(item => item.Key == key));
        }

        var retry = await PostLoanAsync(client, book.Id, user.Id, key);
        Assert.Equal(HttpStatusCode.Created, retry.StatusCode);
        var loan = await retry.Content.ReadFromJsonAsync<LoanDto>(JsonOptions);
        Assert.NotNull(loan);
        Assert.Equal(book.Id, loan.BookId);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
            Assert.Equal(1, await db.Loans.CountAsync(item => item.BookId == book.Id));
            Assert.True(await db.IdempotencyEntries.AnyAsync(item =>
                item.Key == key && item.ResponseStatus == 201));
        }

        var replay = await PostLoanAsync(client, book.Id, user.Id, key);
        Assert.Equal(HttpStatusCode.Created, replay.StatusCode);
        var replayed = await replay.Content.ReadFromJsonAsync<LoanDto>(JsonOptions);
        Assert.NotNull(replayed);
        Assert.Equal(loan.Id, replayed.Id);
    }

    private Task<BookDto> CreateBookAsync(int totalCopies) => CreateBookAsync(_librarianA, totalCopies);

    private Task<UserDto> CreateUserAsync(string name) => CreateUserAsync(_librarianA, name);

    private static async Task<BookDto> CreateBookAsync(HttpClient client, int totalCopies)
    {
        var response = await client.PostAsJsonAsync(
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

    private static async Task<UserDto> CreateUserAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync(
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

    private sealed class OutboxFailureGate
    {
        public bool ThrowOnNextWrite { get; set; }
    }

    private sealed class GatedOutboxWriter(IOutboxWriter inner, OutboxFailureGate gate) : IOutboxWriter
    {
        public Task WriteAsync(
            string type,
            string payloadJson,
            DateTime occurredAtUtc,
            CancellationToken cancellationToken)
        {
            if (gate.ThrowOnNextWrite)
            {
                gate.ThrowOnNextWrite = false;
                throw new InvalidOperationException("Simulated unexpected failure after idempotency reserve.");
            }

            return inner.WriteAsync(type, payloadJson, occurredAtUtc, cancellationToken);
        }
    }
}
