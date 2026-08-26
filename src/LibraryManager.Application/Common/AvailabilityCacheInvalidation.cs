using LibraryManager.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace LibraryManager.Application.Common;

internal static class AvailabilityCacheInvalidation
{
    public static async Task TryRemoveAsync(
        IAvailabilityCache cache,
        ILogger logger,
        ILibraryManagerMetrics metrics,
        Guid bookId,
        CancellationToken cancellationToken)
    {
        try
        {
            await cache.RemoveAsync(bookId, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            metrics.RecordCacheInvalidationFailure();
            logger.LogWarning(
                exception,
                "Failed to invalidate availability cache for book {BookId}",
                bookId);
        }
    }
}
