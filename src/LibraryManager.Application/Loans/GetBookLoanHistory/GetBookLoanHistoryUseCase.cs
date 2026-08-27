using LibraryManager.Application.Abstractions;
using LibraryManager.Application.Common;
using LibraryManager.Domain;

namespace LibraryManager.Application.Loans.GetBookLoanHistory;

public sealed class GetBookLoanHistoryUseCase(IBookRepository books, ILoanRepository loans)
{
    public async Task<Result<PagedResult<LoanDto>>> ExecuteAsync(
        Guid bookId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var book = await books.GetByIdAsync(bookId, cancellationToken);
        if (book is null)
        {
            return Result.Failure<PagedResult<LoanDto>>(Error.NotFound(ErrorCodes.BookNotFound));
        }

        var (normalizedPage, normalizedPageSize) = Pagination.Normalize(page, pageSize);
        var (items, totalCount) = await loans.ListByBookAsync(
            bookId,
            normalizedPage,
            normalizedPageSize,
            cancellationToken);

        return Result.Success(new PagedResult<LoanDto>(
            items.Select(LoanDto.From).ToArray(),
            normalizedPage,
            normalizedPageSize,
            totalCount));
    }
}
