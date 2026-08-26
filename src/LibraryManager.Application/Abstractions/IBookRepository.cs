using LibraryManager.Domain;

namespace LibraryManager.Application.Abstractions;

public interface IBookRepository
{
    Task<Book?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<Book?> GetByIsbnAsync(string isbn, CancellationToken cancellationToken);

    Task<(IReadOnlyList<Book> Items, int TotalCount)> ListAsync(
        int page,
        int pageSize,
        bool? isActive,
        CancellationToken cancellationToken);

    Task AddAsync(Book book, CancellationToken cancellationToken);

    Task<int> TryReserveAvailabilityAsync(Guid bookId, CancellationToken cancellationToken);

    Task<bool> TryUpdateTotalCopiesAsync(Guid bookId, int newTotalCopies, CancellationToken cancellationToken);
}
