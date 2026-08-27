using LibraryManager.Application.Abstractions;
using LibraryManager.Application.Common;
using LibraryManager.Domain;

namespace LibraryManager.Application.Books.GetBook;

public sealed class GetBookUseCase(IBookRepository books)
{
    public async Task<Result<BookDto>> ExecuteAsync(Guid id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var book = await books.GetByIdAsync(id, cancellationToken);
        if (book is null)
        {
            return Result.Failure<BookDto>(Error.NotFound(ErrorCodes.BookNotFound));
        }

        return Result.Success(BookDto.From(book));
    }
}
