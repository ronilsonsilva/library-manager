using LibraryManager.Application.Abstractions;
using LibraryManager.Application.Common;
using LibraryManager.Domain;

namespace LibraryManager.Application.Books.GetBookAvailability;

public sealed class GetBookAvailabilityUseCase(
    IBookRepository books,
    IAvailabilityCache cache)
{
    public async Task<Result<BookAvailabilityDto>> ExecuteAsync(Guid bookId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var cached = await cache.GetAsync(bookId, cancellationToken);
        if (cached is not null)
        {
            return Result.Success(BookAvailabilityDto.From(cached));
        }

        var book = await books.GetByIdAsync(bookId, cancellationToken);
        if (book is null)
        {
            return Result.Failure<BookAvailabilityDto>(Error.NotFound(ErrorCodes.BookNotFound));
        }

        await cache.SetAsync(
            new BookAvailabilityCacheItem(book.Id, book.AvailableCopies, book.TotalCopies, book.IsActive),
            cancellationToken);

        return Result.Success(BookAvailabilityDto.From(book));
    }
}
