using LibraryManager.Application.Abstractions;
using LibraryManager.Application.Common;
using LibraryManager.Application.Loans;

namespace LibraryManager.Application.Loans.GetBookLoanHistory;

public sealed class GetBookLoanHistoryUseCase(IBookRepository books, ILoanRepository loans)
{
    public async Task<PagedResult<LoanDto>> ExecuteAsync(
        Guid bookId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _ = await books.GetByIdAsync(bookId, cancellationToken)
            ?? throw new EntityNotFoundException(AuditMetadata.BookEntity);

        var (normalizedPage, normalizedPageSize) = Pagination.Normalize(page, pageSize);
        var (items, totalCount) = await loans.ListByBookAsync(
            bookId,
            normalizedPage,
            normalizedPageSize,
            cancellationToken);

        return new PagedResult<LoanDto>(
            items.Select(LoanDto.From).ToArray(),
            normalizedPage,
            normalizedPageSize,
            totalCount);
    }
}
