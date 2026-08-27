using LibraryManager.Application.Abstractions;
using LibraryManager.Infrastructure.Caching;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;

namespace LibraryManager.UnitTests.Infrastructure;

public sealed class ResilientAvailabilityCacheDecoratorTests
{
    [Fact]
    public async Task GetAsync_returns_null_on_redis_failure()
    {
        var inner = new FakeAvailabilityCache
        {
            GetError = new RedisException("Simulated Redis failure.")
        };
        var (decorator, metrics) = Create(inner);

        var result = await decorator.GetAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(0, metrics.CacheInvalidationFailures);
        Assert.Equal(1, inner.GetCount);
    }

    [Fact]
    public async Task GetAsync_returns_inner_value_when_redis_succeeds()
    {
        var item = new BookAvailabilityCacheItem(Guid.NewGuid(), 2, 3, true);
        var inner = new FakeAvailabilityCache { Item = item };
        var (decorator, _) = Create(inner);

        var result = await decorator.GetAsync(item.BookId, CancellationToken.None);

        Assert.Equal(item, result);
    }

    [Fact]
    public async Task SetAsync_does_not_throw_on_redis_failure()
    {
        var inner = new FakeAvailabilityCache
        {
            SetError = new RedisTimeoutException("Simulated Redis failure.", CommandStatus.Unknown)
        };
        var (decorator, metrics) = Create(inner);
        var item = new BookAvailabilityCacheItem(Guid.NewGuid(), 1, 1, true);

        await decorator.SetAsync(item, CancellationToken.None);

        Assert.Equal(1, inner.SetCount);
        Assert.Equal(0, metrics.CacheInvalidationFailures);
    }

    [Fact]
    public async Task RemoveAsync_does_not_throw_and_records_invalidation_failure()
    {
        var inner = new FakeAvailabilityCache
        {
            RemoveError = new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Simulated Redis failure.")
        };
        var (decorator, metrics) = Create(inner);

        await decorator.RemoveAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Equal(1, inner.RemoveCount);
        Assert.Equal(1, metrics.CacheInvalidationFailures);
    }

    [Fact]
    public async Task RemoveAsync_does_not_record_metric_when_redis_succeeds()
    {
        var (decorator, metrics) = Create(new FakeAvailabilityCache());

        await decorator.RemoveAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Equal(0, metrics.CacheInvalidationFailures);
    }

    [Fact]
    public async Task GetAsync_does_not_call_inner_when_cancellation_is_requested()
    {
        var inner = new FakeAvailabilityCache();
        var (decorator, _) = Create(inner);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            decorator.GetAsync(Guid.NewGuid(), cancelled.Token));

        Assert.Equal(0, inner.GetCount);
    }

    [Fact]
    public async Task SetAsync_does_not_call_inner_when_cancellation_is_requested()
    {
        var inner = new FakeAvailabilityCache();
        var (decorator, _) = Create(inner);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            decorator.SetAsync(new BookAvailabilityCacheItem(Guid.NewGuid(), 1, 1, true), cancelled.Token));

        Assert.Equal(0, inner.SetCount);
    }

    [Fact]
    public async Task RemoveAsync_does_not_call_inner_when_cancellation_is_requested()
    {
        var inner = new FakeAvailabilityCache();
        var (decorator, metrics) = Create(inner);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            decorator.RemoveAsync(Guid.NewGuid(), cancelled.Token));

        Assert.Equal(0, inner.RemoveCount);
        Assert.Equal(0, metrics.CacheInvalidationFailures);
    }

    [Fact]
    public async Task GetAsync_propagates_operation_canceled_from_inner()
    {
        var inner = new FakeAvailabilityCache { GetError = new OperationCanceledException() };
        var (decorator, metrics) = Create(inner);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            decorator.GetAsync(Guid.NewGuid(), CancellationToken.None));

        Assert.Equal(0, metrics.CacheInvalidationFailures);
    }

    [Fact]
    public async Task SetAsync_propagates_operation_canceled_from_inner()
    {
        var inner = new FakeAvailabilityCache { SetError = new TaskCanceledException() };
        var (decorator, _) = Create(inner);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            decorator.SetAsync(new BookAvailabilityCacheItem(Guid.NewGuid(), 1, 1, true), CancellationToken.None));
    }

    [Fact]
    public async Task RemoveAsync_propagates_operation_canceled_from_inner()
    {
        var inner = new FakeAvailabilityCache { RemoveError = new OperationCanceledException() };
        var (decorator, metrics) = Create(inner);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            decorator.RemoveAsync(Guid.NewGuid(), CancellationToken.None));

        Assert.Equal(0, metrics.CacheInvalidationFailures);
    }

    [Fact]
    public async Task GetAsync_does_not_swallow_non_redis_exceptions()
    {
        var inner = new FakeAvailabilityCache { GetError = new InvalidOperationException("not redis") };
        var (decorator, _) = Create(inner);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            decorator.GetAsync(Guid.NewGuid(), CancellationToken.None));
    }

    private static (ResilientAvailabilityCacheDecorator Decorator, FakeMetrics Metrics) Create(
        IAvailabilityCache inner)
    {
        var metrics = new FakeMetrics();
        var decorator = new ResilientAvailabilityCacheDecorator(
            inner,
            NullLogger<ResilientAvailabilityCacheDecorator>.Instance,
            metrics);
        return (decorator, metrics);
    }

    private sealed class FakeAvailabilityCache : IAvailabilityCache
    {
        public BookAvailabilityCacheItem? Item { get; init; }

        public Exception? GetError { get; init; }

        public Exception? SetError { get; init; }

        public Exception? RemoveError { get; init; }

        public int GetCount { get; private set; }

        public int SetCount { get; private set; }

        public int RemoveCount { get; private set; }

        public Task<BookAvailabilityCacheItem?> GetAsync(Guid bookId, CancellationToken cancellationToken)
        {
            GetCount++;
            return GetError is null
                ? Task.FromResult(Item)
                : Task.FromException<BookAvailabilityCacheItem?>(GetError);
        }

        public Task SetAsync(BookAvailabilityCacheItem item, CancellationToken cancellationToken)
        {
            SetCount++;
            return SetError is null ? Task.CompletedTask : Task.FromException(SetError);
        }

        public Task RemoveAsync(Guid bookId, CancellationToken cancellationToken)
        {
            RemoveCount++;
            return RemoveError is null ? Task.CompletedTask : Task.FromException(RemoveError);
        }
    }

    private sealed class FakeMetrics : ILibraryManagerMetrics
    {
        public int CacheInvalidationFailures { get; private set; }

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

        public void RecordCacheInvalidationFailure() => CacheInvalidationFailures++;

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
}
