using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LibraryManager.Api.Security;
using LibraryManager.Application.Abstractions;
using LibraryManager.Application.Books;
using LibraryManager.Application.Common;
using LibraryManager.Application.Loans;
using LibraryManager.Application.Users;
using LibraryManager.Infrastructure.Outbox;
using LibraryManager.Infrastructure.Persistence;
using LibraryManager.IntegrationTests;
using LibraryManager.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LibraryManager.IntegrationTests.Outbox;

[Collection(DatabaseCollection.Name)]
public sealed class OutboxProcessorTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly CustomWebApplicationFactory _factory;
    private HttpClient _librarian = null!;
    private OutboxProcessor _processor = null!;
    private CallbackAvailabilityCache _cache = null!;

    public OutboxProcessorTests(DatabaseFixture database)
    {
        _factory = new CustomWebApplicationFactory(
            database,
            services => CallbackAvailabilityCache.Register(services));
    }

    public Task InitializeAsync()
    {
        _librarian = _factory.CreateClient().WithTestAuth("outbox-librarian", LibrarianPolicy.Role);
        _processor = _factory.Services.GetRequiredService<OutboxProcessor>();
        _cache = _factory.Services.GetRequiredService<CallbackAvailabilityCache>();
        _cache.Reset();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _librarian.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Loan_persists_unprocessed_availability_outbox_in_the_same_database()
    {
        await SuppressPendingOutboxAsync();
        var book = await CreateBookAsync(1);
        var user = await CreateUserAsync("Outbox Persist Borrower");

        var response = await PostLoanAsync(book.Id, user.Id);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var loan = await response.Content.ReadFromJsonAsync<LoanDto>(JsonOptions);
        Assert.NotNull(loan);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
        var messages = await LoadAvailabilityMessagesAsync(db, book.Id);
        Assert.Contains(messages, message => message.ProcessedAtUtc is null && message.LockedBy is null);
        Assert.True(await db.Loans.AnyAsync(item => item.Id == loan.Id));
    }

    [Fact]
    public async Task Processor_invalidates_redis_after_claim_commit_and_marks_processed()
    {
        await SuppressPendingOutboxAsync();
        var book = await CreateBookAsync(2);
        await SuppressPendingOutboxAsync();
        await GetAvailabilityAsync(book.Id);
        Assert.NotNull(await _cache.GetAsync(book.Id, CancellationToken.None));
        var messageId = await InsertOutboxAsync(book.Id);

        _cache.OnRemove = async (bookId, _) =>
        {
            if (bookId != book.Id)
            {
                return;
            }

            await using var scope = _factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
            var claimed = await db.OutboxMessages.SingleAsync(message => message.Id == messageId);
            Assert.Null(claimed.ProcessedAtUtc);
            Assert.False(string.IsNullOrWhiteSpace(claimed.LockedBy));
            Assert.NotNull(claimed.LockedUntilUtc);
            Assert.True(claimed.AttemptCount >= 1);
        };

        Assert.Equal(1, await DrainAsync("worker-invalidate"));
        Assert.Null(await _cache.GetAsync(book.Id, CancellationToken.None));

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
            var message = await db.OutboxMessages.SingleAsync(item => item.Id == messageId);
            Assert.NotNull(message.ProcessedAtUtc);
            Assert.Null(message.LockedBy);
            Assert.Null(message.LockedUntilUtc);
        }

        var view = await GetAvailabilityAsync(book.Id);
        Assert.Equal(2, view.AvailableCopies);
    }

    [Fact]
    public async Task Failed_processing_retries_with_backoff_then_succeeds()
    {
        await SuppressPendingOutboxAsync();
        var bookId = Guid.NewGuid();
        await _cache.SetAsync(new BookAvailabilityCacheItem(bookId, 4, 4, true), CancellationToken.None);
        var messageId = await InsertOutboxAsync(bookId);

        _cache.FailRemoveForBookId = bookId;
        _cache.RemainingRemoveFailures = 1;

        Assert.Equal(1, await _processor.ProcessBatchAsync("worker-retry", CancellationToken.None));

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
            var failed = await db.OutboxMessages.SingleAsync(item => item.Id == messageId);
            Assert.Null(failed.ProcessedAtUtc);
            Assert.Null(failed.LockedBy);
            Assert.NotNull(failed.LastError);
            Assert.Contains("Simulated Redis failure", failed.LastError, StringComparison.Ordinal);
            Assert.True(failed.NextAttemptAtUtc > DateTime.UtcNow.AddSeconds(-1));
            Assert.True(failed.AttemptCount >= 1);
        }

        Assert.NotNull(await _cache.GetAsync(bookId, CancellationToken.None));

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE outbox_messages SET next_attempt_at_utc = NOW() - INTERVAL '1 second' WHERE id = {messageId}");
        }

        Assert.Equal(1, await _processor.ProcessBatchAsync("worker-retry", CancellationToken.None));
        Assert.Null(await _cache.GetAsync(bookId, CancellationToken.None));

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
            var processed = await db.OutboxMessages.SingleAsync(item => item.Id == messageId);
            Assert.NotNull(processed.ProcessedAtUtc);
            Assert.Null(processed.LastError);
        }
    }

    [Fact]
    public async Task Expired_lease_is_claimed_by_another_worker()
    {
        await SuppressPendingOutboxAsync();
        var bookId = Guid.NewGuid();
        await _cache.SetAsync(new BookAvailabilityCacheItem(bookId, 1, 1, true), CancellationToken.None);
        var messageId = await InsertOutboxAsync(bookId);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                UPDATE outbox_messages
                SET locked_by = 'dead-worker',
                    locked_until_utc = NOW() - INTERVAL '2 minutes',
                    attempt_count = 1
                WHERE id = {messageId}
                """);
        }

        Assert.Equal(1, await _processor.ProcessBatchAsync("worker-recovery", CancellationToken.None));
        Assert.Null(await _cache.GetAsync(bookId, CancellationToken.None));

        await using var verify = _factory.Services.CreateAsyncScope();
        var stored = await verify.ServiceProvider.GetRequiredService<LibraryDbContext>()
            .OutboxMessages.SingleAsync(item => item.Id == messageId);
        Assert.NotNull(stored.ProcessedAtUtc);
        Assert.Null(stored.LockedBy);
    }

    [Fact]
    public async Task Two_workers_claim_distinct_messages_with_skip_locked()
    {
        await SuppressPendingOutboxAsync();
        var bookIds = Enumerable.Range(0, 20).Select(_ => Guid.NewGuid()).ToArray();
        foreach (var bookId in bookIds)
        {
            await _cache.SetAsync(new BookAvailabilityCacheItem(bookId, 1, 1, true), CancellationToken.None);
            await InsertOutboxAsync(bookId);
        }

        await Task.WhenAll(
            DrainAsync("worker-a"),
            DrainAsync("worker-b"));

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
        var messages = await db.OutboxMessages
            .Where(message => message.Type == AvailabilityOutbox.MessageType)
            .ToListAsync();
        var ours = messages.Where(message =>
            bookIds.Any(id => message.PayloadJson.Contains(id.ToString(), StringComparison.Ordinal))).ToList();
        Assert.Equal(20, ours.Count);
        Assert.Equal(20, ours.Count(message => message.ProcessedAtUtc is not null));

        foreach (var bookId in bookIds)
        {
            Assert.Null(await _cache.GetAsync(bookId, CancellationToken.None));
        }
    }

    [Fact]
    public async Task Duplicate_invalidation_is_idempotent()
    {
        await SuppressPendingOutboxAsync();
        var bookId = Guid.NewGuid();
        await _cache.SetAsync(new BookAvailabilityCacheItem(bookId, 2, 2, true), CancellationToken.None);
        var first = await InsertOutboxAsync(bookId);
        var second = await InsertOutboxAsync(bookId);

        await DrainAsync("worker-idempotent");

        Assert.Null(await _cache.GetAsync(bookId, CancellationToken.None));
        await _cache.RemoveAsync(bookId, CancellationToken.None);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
        Assert.NotNull((await db.OutboxMessages.SingleAsync(item => item.Id == first)).ProcessedAtUtc);
        Assert.NotNull((await db.OutboxMessages.SingleAsync(item => item.Id == second)).ProcessedAtUtc);
    }

    private async Task<int> DrainAsync(string workerId)
    {
        var total = 0;
        int claimed;
        do
        {
            claimed = await _processor.ProcessBatchAsync(workerId, CancellationToken.None);
            total += claimed;
        } while (claimed > 0);

        return total;
    }

    private async Task SuppressPendingOutboxAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE outbox_messages SET processed_at_utc = NOW(), locked_by = NULL, locked_until_utc = NULL WHERE processed_at_utc IS NULL");
    }

    private async Task<Guid> InsertOutboxAsync(Guid bookId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
        var id = Guid.NewGuid();
        var now = DateTime.UtcNow;
        db.OutboxMessages.Add(new OutboxMessage
        {
            Id = id,
            Type = AvailabilityOutbox.MessageType,
            PayloadJson = AvailabilityOutbox.Payload(bookId, "outbox-test"),
            OccurredAtUtc = now,
            AttemptCount = 0,
            NextAttemptAtUtc = now
        });
        await db.SaveChangesAsync();
        return id;
    }

    private static async Task<List<OutboxMessage>> LoadAvailabilityMessagesAsync(LibraryDbContext db, Guid bookId)
    {
        var bookIdText = bookId.ToString();
        var messages = await db.OutboxMessages
            .Where(message => message.Type == AvailabilityOutbox.MessageType)
            .ToListAsync();
        return messages.Where(message => message.PayloadJson.Contains(bookIdText, StringComparison.Ordinal)).ToList();
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

    private async Task<BookAvailabilityDto> GetAvailabilityAsync(Guid bookId)
    {
        var response = await _librarian.GetAsync($"/books/{bookId}/availability");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var availability = await response.Content.ReadFromJsonAsync<BookAvailabilityDto>(JsonOptions);
        Assert.NotNull(availability);
        return availability;
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

    private static string UniqueIsbn() => Guid.NewGuid().ToString("N")[..12];
}
