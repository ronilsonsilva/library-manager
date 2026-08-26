using LibraryManager.Application.Abstractions;
using LibraryManager.Application.Common;

namespace LibraryManager.Application.Books.ListBooks;

public sealed class ListBooksUseCase(IBookRepository books)
{
    public async Task<PagedResult<BookDto>> ExecuteAsync(
        int page,
        int pageSize,
        bool? isActive,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var (normalizedPage, normalizedPageSize) = Pagination.Normalize(page, pageSize);
        var (items, totalCount) = await books.ListAsync(
            normalizedPage,
            normalizedPageSize,
            isActive,
            cancellationToken);

        return new PagedResult<BookDto>(
            items.Select(BookDto.From).ToArray(),
            normalizedPage,
            normalizedPageSize,
            totalCount);
    }
}
