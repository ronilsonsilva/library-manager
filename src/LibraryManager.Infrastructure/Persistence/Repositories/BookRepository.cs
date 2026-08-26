using LibraryManager.Application.Abstractions;
using LibraryManager.Domain;
using Microsoft.EntityFrameworkCore;

namespace LibraryManager.Infrastructure.Persistence.Repositories;

public sealed class BookRepository(LibraryDbContext db, IClock clock) : IBookRepository
{
    public Task<Book?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Books.FirstOrDefaultAsync(book => book.Id == id, cancellationToken);

    public Task<Book?> GetByIsbnAsync(string isbn, CancellationToken cancellationToken)
    {
        var normalized = isbn.Trim();
        return db.Books.FirstOrDefaultAsync(book => book.Isbn == normalized, cancellationToken);
    }

    public async Task<(IReadOnlyList<Book> Items, int TotalCount)> ListAsync(
        int page,
        int pageSize,
        bool? isActive,
        CancellationToken cancellationToken)
    {
        var query = db.Books.AsNoTracking();
        if (isActive is not null)
        {
            query = query.Where(book => book.IsActive == isActive.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(book => book.Title)
            .ThenBy(book => book.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task AddAsync(Book book, CancellationToken cancellationToken)
    {
        await db.Books.AddAsync(book, cancellationToken);
    }

    public async Task<int> TryReserveAvailabilityAsync(Guid bookId, CancellationToken cancellationToken)
    {
        return await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
             UPDATE books
             SET available_copies = available_copies - 1,
                 updated_at_utc = {clock.UtcNow}
             WHERE id = {bookId}
               AND is_active = TRUE
               AND available_copies > 0
             """,
            cancellationToken);
    }

    public async Task<bool> TryUpdateTotalCopiesAsync(
        Guid bookId,
        int newTotalCopies,
        CancellationToken cancellationToken)
    {
        if (newTotalCopies < 1)
        {
            return false;
        }

        var rows = await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
             UPDATE books
             SET total_copies = {newTotalCopies},
                 available_copies = {newTotalCopies} - (total_copies - available_copies),
                 updated_at_utc = {clock.UtcNow}
             WHERE id = {bookId}
               AND {newTotalCopies} >= (total_copies - available_copies)
               AND {newTotalCopies} >= 1
             """,
            cancellationToken);

        if (rows != 1)
        {
            return false;
        }

        var tracked = await db.Books.FindAsync([bookId], cancellationToken);
        if (tracked is not null)
        {
            await db.Entry(tracked).ReloadAsync(cancellationToken);
        }

        return true;
    }
}
