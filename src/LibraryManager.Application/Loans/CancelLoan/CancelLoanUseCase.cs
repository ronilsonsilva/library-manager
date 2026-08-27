using LibraryManager.Application.Abstractions;
using LibraryManager.Application.Common;
using LibraryManager.Domain;

namespace LibraryManager.Application.Loans.CancelLoan;

public sealed class CancelLoanUseCase(
    ILoanRepository loans,
    IBookRepository books,
    IAuditRepository audits,
    IOutboxWriter outbox,
    IUnitOfWork unitOfWork,
    IAvailabilityCache cache,
    IClock clock,
    ICurrentUserContext currentUser,
    ICorrelationContext correlation)
{
    public Task<Result<LoanDto>> ExecuteAsync(Guid loanId, CancellationToken cancellationToken) =>
        CompleteActiveLoan.ExecuteAsync(
            loans,
            books,
            audits,
            outbox,
            unitOfWork,
            cache,
            clock,
            currentUser,
            correlation,
            loanId,
            LoanStatus.Cancelled,
            AuditMetadata.LoanCancelled,
            cancellationToken);
}
