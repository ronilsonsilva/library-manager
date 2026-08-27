using LibraryManager.Application.Abstractions;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace LibraryManager.Infrastructure.Caching;

public sealed class ResilientAvailabilityCacheDecorator(
    IAvailabilityCache inner,
    ILogger<ResilientAvailabilityCacheDecorator> logger,
    ILibraryManagerMetrics metrics) : IAvailabilityCache
{
    public async Task<BookAvailabilityCacheItem?> GetAsync(Guid bookId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            return await inner.GetAsync(bookId, cancellationToken);
        }
        catch (RedisTimeoutException exception)
        {
            logger.LogWarning(exception, "Failed to read availability cache for book {BookId}", bookId);
            return null;
        }
        catch (RedisException exception)
        {
            logger.LogWarning(exception, "Failed to read availability cache for book {BookId}", bookId);
            return null;
        }
    }

    public async Task SetAsync(BookAvailabilityCacheItem item, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(item);
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            await inner.SetAsync(item, cancellationToken);
        }
        catch (RedisTimeoutException exception)
        {
            logger.LogWarning(exception, "Failed to write availability cache for book {BookId}", item.BookId);
        }
        catch (RedisException exception)
        {
            logger.LogWarning(exception, "Failed to write availability cache for book {BookId}", item.BookId);
        }
    }

    public async Task RemoveAsync(Guid bookId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            await inner.RemoveAsync(bookId, cancellationToken);
        }
        catch (RedisTimeoutException exception)
        {
            RecordInvalidationFailure(exception, bookId);
        }
        catch (RedisException exception)
        {
            RecordInvalidationFailure(exception, bookId);
        }
    }

    private void RecordInvalidationFailure(Exception exception, Guid bookId)
    {
        metrics.RecordCacheInvalidationFailure();
        logger.LogWarning(exception, "Failed to invalidate availability cache for book {BookId}", bookId);
    }
}
