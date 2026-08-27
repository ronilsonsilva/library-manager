using LibraryManager.Application.Abstractions;
using LibraryManager.Application.Common;
using LibraryManager.Application.Loans;
using LibraryManager.Domain;

namespace LibraryManager.Application.Users.GetUserLoans;

public sealed class GetUserLoansUseCase(IUserRepository users, ILoanRepository loans)
{
    public async Task<Result<PagedResult<LoanDto>>> ExecuteAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = await users.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return Result.Failure<PagedResult<LoanDto>>(Error.NotFound(ErrorCodes.UserNotFound));
        }

        var (normalizedPage, normalizedPageSize) = Pagination.Normalize(page, pageSize);
        var (items, totalCount) = await loans.ListByUserAsync(
            userId,
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
