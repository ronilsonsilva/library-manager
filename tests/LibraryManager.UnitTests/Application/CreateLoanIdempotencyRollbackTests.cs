using LibraryManager.Application.Abstractions;
using LibraryManager.Application.Common;
using LibraryManager.Application.Loans;
using LibraryManager.Application.Loans.CreateLoan;
using LibraryManager.Domain;
using Microsoft.Extensions.Logging;

namespace LibraryManager.UnitTests.Application;

public sealed class CreateLoanIdempotencyRollbackTests
{
    [Fact]
    public async Task Unexpected_failure_after_key_reserve_rolls_back_ownership_and_retry_creates_loan()
    {
        var store = new InMemoryIdempotencyStore();
        var books = new FakeBookRepository();
        var loans = new FakeLoanRepository();
        var cache = new FakeAvailabilityCache();
        var useCase = CreateUseCase(store, books, loans, cache);
        var bookId = books.Book.Id;
        var userId = books.UserId;
        var key = "rollback-key";

        books.ThrowOnReserve = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            useCase.ExecuteAsync(bookId, userId, key, CancellationToken.None));

        Assert.Empty(store.Committed);
        Assert.Empty(loans.Items);
        Assert.Equal(0, cache.RemoveCount);

        books.ThrowOnReserve = false;
        var created = await useCase.ExecuteAsync(bookId, userId, key, CancellationToken.None);

        Assert.True(created.IsSuccess);
        Assert.Equal(bookId, created.Value.BookId);
        Assert.Equal(userId, created.Value.UserId);
        Assert.Single(loans.Items);
        Assert.Equal(1, cache.RemoveCount);
        Assert.Single(store.Committed);

        var replayed = await useCase.ExecuteAsync(bookId, userId, key, CancellationToken.None);

        Assert.True(replayed.IsSuccess);
        Assert.Equal(created.Value.Id, replayed.Value.Id);
        Assert.Single(loans.Items);
        Assert.Equal(1, cache.RemoveCount);
    }

    [Fact]
    public async Task Different_payload_with_same_key_conflicts_without_creating_another_loan()
    {
        var store = new InMemoryIdempotencyStore();
        var books = new FakeBookRepository();
        var loans = new FakeLoanRepository();
        var cache = new FakeAvailabilityCache();
        var useCase = CreateUseCase(store, books, loans, cache);
        var key = "conflict-key";

        var created = await useCase.ExecuteAsync(books.Book.Id, books.UserId, key, CancellationToken.None);
        Assert.True(created.IsSuccess);

        var conflict = await useCase.ExecuteAsync(books.Book.Id, Guid.NewGuid(), key, CancellationToken.None);

        Assert.True(conflict.IsFailure);
        Assert.Equal(ErrorCodes.IdempotencyPayloadMismatch, conflict.Error.Code);
        Assert.Equal(ErrorType.Conflict, conflict.Error.Type);
        Assert.Equal(created.Value.Id, loans.Items.Single().Id);
        Assert.Equal(1, cache.RemoveCount);
    }

    private static CreateLoanUseCase CreateUseCase(
        InMemoryIdempotencyStore store,
        FakeBookRepository books,
        FakeLoanRepository loans,
        FakeAvailabilityCache cache)
    {
        var unitOfWork = new FakeUnitOfWork(store);
        return new CreateLoanUseCase(
            store,
            books.Users,
            books,
            loans,
            new FakeAuditRepository(),
            new FakeOutboxWriter(),
            unitOfWork,
            cache,
            new FixedClock(),
            new FakeCurrentUser(),
            new FakeCorrelation(),
            new NoopMetrics(),
            new NoopLogger());
    }

    private sealed class FakeUnitOfWork(InMemoryIdempotencyStore store) : IUnitOfWork
    {
        public Task<Result> SaveChangesAsync(CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success());

        public async Task<T> ExecuteInTransactionAsync<T>(
            Func<CancellationToken, Task<T>> operation,
            CancellationToken cancellationToken)
        {
            store.Begin();
            try
            {
                var result = await operation(cancellationToken);
                if (result is IResult { IsFailure: true })
                {
                    store.Rollback();
                    return result;
                }

                store.Commit();
                return result;
            }
            catch
            {
                store.Rollback();
                throw;
            }
        }
    }

    private sealed class InMemoryIdempotencyStore : IIdempotencyStore
    {
        private readonly Dictionary<(string Endpoint, string Key), StoredEntry> _committed = new();
        private Dictionary<(string Endpoint, string Key), StoredEntry>? _pending;

        public IReadOnlyDictionary<(string Endpoint, string Key), StoredEntry> Committed => _committed;

        public void Begin() =>
            _pending = _committed.ToDictionary(entry => entry.Key, entry => entry.Value.Clone());

        public void Commit()
        {
            _committed.Clear();
            foreach (var (key, value) in _pending ?? [])
            {
                _committed[key] = value.Clone();
            }

            _pending = null;
        }

        public void Rollback() => _pending = null;

        public Task<IdempotencyLookup> TryReserveAsync(
            string endpoint,
            string key,
            string requestHash,
            CancellationToken cancellationToken)
        {
            var pending = RequirePending();
            var mapKey = (endpoint, key);
            if (pending.TryGetValue(mapKey, out var existing))
            {
                return Task.FromResult(new IdempotencyLookup(
                    false,
                    existing.RequestHash,
                    existing.ResponseStatus,
                    existing.ResponseBody));
            }

            pending[mapKey] = new StoredEntry { RequestHash = requestHash };
            return Task.FromResult(new IdempotencyLookup(true, requestHash, null, null));
        }

        public Task CompleteAsync(
            string endpoint,
            string key,
            int responseStatus,
            string responseBody,
            CancellationToken cancellationToken)
        {
            var pending = RequirePending();
            pending[(endpoint, key)] = new StoredEntry
            {
                RequestHash = pending[(endpoint, key)].RequestHash,
                ResponseStatus = responseStatus,
                ResponseBody = responseBody
            };
            return Task.CompletedTask;
        }

        private Dictionary<(string Endpoint, string Key), StoredEntry> RequirePending() =>
            _pending ?? throw new InvalidOperationException("No ambient idempotency transaction.");

        public sealed class StoredEntry
        {
            public required string RequestHash { get; init; }
            public int? ResponseStatus { get; init; }
            public string? ResponseBody { get; init; }

            public StoredEntry Clone() => new()
            {
                RequestHash = RequestHash,
                ResponseStatus = ResponseStatus,
                ResponseBody = ResponseBody
            };
        }
    }

    private sealed class FakeBookRepository : IBookRepository
    {
        public Book Book { get; } = Book.Create("Dune", "9780441172719", "Frank Herbert", 2, DateTime.UtcNow).Value;
        public FakeUserRepository Users { get; } = new();
        public Guid UserId => Users.User.Id;
        public bool ThrowOnReserve { get; set; }

        public Task<Book?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(id == Book.Id ? Book : null);

        public Task<Book?> GetByIsbnAsync(string isbn, CancellationToken cancellationToken) =>
            Task.FromResult<Book?>(null);

        public Task<(IReadOnlyList<Book> Items, int TotalCount)> ListAsync(
            int page,
            int pageSize,
            bool? isActive,
            CancellationToken cancellationToken) =>
            Task.FromResult<(IReadOnlyList<Book>, int)>(([], 0));

        public Task AddAsync(Book book, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<int> TryReserveAvailabilityAsync(Guid bookId, CancellationToken cancellationToken)
        {
            if (ThrowOnReserve)
            {
                throw new InvalidOperationException("Simulated failure after idempotency reserve.");
            }

            return Task.FromResult(bookId == Book.Id ? 1 : 0);
        }

        public Task<int> TryRestoreAvailabilityAsync(Guid bookId, CancellationToken cancellationToken) =>
            Task.FromResult(bookId == Book.Id ? 1 : 0);

        public Task<bool> TryUpdateTotalCopiesAsync(
            Guid bookId,
            int newTotalCopies,
            CancellationToken cancellationToken) =>
            Task.FromResult(false);
    }

    private sealed class FakeUserRepository : IUserRepository
    {
        public User User { get; } = User.Create("Ada Lovelace", "ada@example.com", DateTime.UtcNow).Value;

        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(id == User.Id ? User : null);

        public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken) =>
            Task.FromResult<User?>(null);

        public Task AddAsync(User user, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeLoanRepository : ILoanRepository
    {
        public List<Loan> Items { get; } = [];

        public Task<Loan?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(Items.SingleOrDefault(item => item.Id == id));

        public Task AddAsync(Loan loan, CancellationToken cancellationToken)
        {
            Items.Add(loan);
            return Task.CompletedTask;
        }

        public Task<(IReadOnlyList<Loan> Items, int TotalCount)> ListByUserAsync(
            Guid userId,
            int page,
            int pageSize,
            CancellationToken cancellationToken) =>
            Task.FromResult<(IReadOnlyList<Loan>, int)>((Items, Items.Count));

        public Task<(IReadOnlyList<Loan> Items, int TotalCount)> ListByBookAsync(
            Guid bookId,
            int page,
            int pageSize,
            CancellationToken cancellationToken) =>
            Task.FromResult<(IReadOnlyList<Loan>, int)>((Items, Items.Count));

        public Task<int> TryCompleteActiveAsync(
            Guid loanId,
            LoanStatus terminalStatus,
            DateTime completedAtUtc,
            CancellationToken cancellationToken) =>
            Task.FromResult(0);
    }

    private sealed class FakeAuditRepository : IAuditRepository
    {
        public Task AddAsync(AuditEvent auditEvent, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<(IReadOnlyList<AuditEvent> Items, int TotalCount)> ListAsync(
            int page,
            int pageSize,
            string? entityType,
            Guid? entityId,
            CancellationToken cancellationToken) =>
            Task.FromResult<(IReadOnlyList<AuditEvent>, int)>(([], 0));
    }

    private sealed class FakeOutboxWriter : IOutboxWriter
    {
        public Task WriteAsync(
            string type,
            string payloadJson,
            DateTime occurredAtUtc,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class FakeAvailabilityCache : IAvailabilityCache
    {
        public int RemoveCount { get; private set; }

        public Task<BookAvailabilityCacheItem?> GetAsync(Guid bookId, CancellationToken cancellationToken) =>
            Task.FromResult<BookAvailabilityCacheItem?>(null);

        public Task SetAsync(BookAvailabilityCacheItem item, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task RemoveAsync(Guid bookId, CancellationToken cancellationToken)
        {
            RemoveCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FixedClock : IClock
    {
        public DateTime UtcNow { get; } = new(2026, 8, 26, 3, 0, 0, DateTimeKind.Utc);
    }

    private sealed class FakeCurrentUser : ICurrentUserContext
    {
        public string ActorId => "librarian";
    }

    private sealed class FakeCorrelation : ICorrelationContext
    {
        public string CorrelationId => "correlation";
    }

    private sealed class NoopMetrics : ILibraryManagerMetrics
    {
        public void RecordLoanCreated()
        {
        }

        public void RecordLoanUnavailable()
        {
        }

        public void RecordIdempotencyReplay()
        {
        }

        public void RecordLoanDuration(TimeSpan duration)
        {
        }

        public void RecordCacheInvalidationFailure()
        {
        }

        public void RecordOutboxProcessed(int count = 1)
        {
        }

        public void RecordOutboxFailure()
        {
        }

        public void SetOutboxPending(long pending)
        {
        }
    }

    private sealed class NoopLogger : ILogger<CreateLoanUseCase>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => false;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
        }
    }
}
