using LibraryManager.Application.Abstractions;
using LibraryManager.Application.Common;
using LibraryManager.Domain;
using Microsoft.Extensions.Logging;

namespace LibraryManager.Application.Books.GetBookAvailability;

public sealed class GetBookAvailabilityUseCase(
    IBookRepository books,
    IAvailabilityCache cache,
    ILogger<GetBookAvailabilityUseCase> logger)
{
    public async Task<Result<BookAvailabilityDto>> ExecuteAsync(Guid bookId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var cached = await cache.GetAsync(bookId, cancellationToken);
            if (cached is not null)
            {
                return Result.Success(BookAvailabilityDto.From(cached));
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Failed to read availability cache for book {BookId}", bookId);
        }

        var book = await books.GetByIdAsync(bookId, cancellationToken);
        if (book is null)
        {
            return Result.Failure<BookAvailabilityDto>(Error.NotFound(ErrorCodes.BookNotFound));
        }

        try
        {
            await cache.SetAsync(
                new BookAvailabilityCacheItem(book.Id, book.AvailableCopies, book.TotalCopies, book.IsActive),
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Failed to write availability cache for book {BookId}", bookId);
        }

        return Result.Success(BookAvailabilityDto.From(book));
    }
}
