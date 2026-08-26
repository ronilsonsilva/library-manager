using LibraryManager.Application.Abstractions;
using LibraryManager.Application.Common;
using LibraryManager.Application.Loans;
using LibraryManager.Domain;
using Microsoft.Extensions.Logging;

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
    ICorrelationContext correlation,
    ILogger<CancelLoanUseCase> logger,
    ILibraryManagerMetrics metrics)
{
    public Task<LoanDto> ExecuteAsync(Guid loanId, CancellationToken cancellationToken) =>
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
            logger,
            metrics,
            loanId,
            LoanStatus.Cancelled,
            AuditMetadata.LoanCancelled,
            cancellationToken);
}
