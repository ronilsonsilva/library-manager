using LibraryManager.Domain;

namespace LibraryManager.Application.Abstractions;

public interface ILoanRepository
{
    Task<Loan?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task AddAsync(Loan loan, CancellationToken cancellationToken);

    Task<(IReadOnlyList<Loan> Items, int TotalCount)> ListByUserAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<(IReadOnlyList<Loan> Items, int TotalCount)> ListByBookAsync(
        Guid bookId,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<int> TryCompleteActiveAsync(
        Guid loanId,
        LoanStatus terminalStatus,
        DateTime completedAtUtc,
        CancellationToken cancellationToken);
}
