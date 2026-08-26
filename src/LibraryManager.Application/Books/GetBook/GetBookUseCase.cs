using LibraryManager.Application.Abstractions;
using LibraryManager.Application.Common;

namespace LibraryManager.Application.Books.GetBook;

public sealed class GetBookUseCase(IBookRepository books)
{
    public async Task<BookDto> ExecuteAsync(Guid id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var book = await books.GetByIdAsync(id, cancellationToken)
            ?? throw new EntityNotFoundException(AuditMetadata.BookEntity);

        return BookDto.From(book);
    }
}
