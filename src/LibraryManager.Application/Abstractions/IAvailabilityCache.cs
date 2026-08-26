namespace LibraryManager.Application.Abstractions;

public sealed record BookAvailabilityCacheItem(
    Guid BookId,
    int AvailableCopies,
    int TotalCopies,
    bool IsActive);

public interface IAvailabilityCache
{
    Task<BookAvailabilityCacheItem?> GetAsync(Guid bookId, CancellationToken cancellationToken);

    Task SetAsync(BookAvailabilityCacheItem item, CancellationToken cancellationToken);

    Task RemoveAsync(Guid bookId, CancellationToken cancellationToken);
}
