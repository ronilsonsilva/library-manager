using LibraryManager.Application.Abstractions;
using LibraryManager.Application.Common;
using LibraryManager.Application.Loans;

namespace LibraryManager.Application.Users.GetUserLoans;

public sealed class GetUserLoansUseCase(IUserRepository users, ILoanRepository loans)
{
    public async Task<PagedResult<LoanDto>> ExecuteAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            throw new EntityNotFoundException(AuditMetadata.UserEntity);
        }

        var (normalizedPage, normalizedPageSize) = Pagination.Normalize(page, pageSize);
        var (items, totalCount) = await loans.ListByUserAsync(
            userId,
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
