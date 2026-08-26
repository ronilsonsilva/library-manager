using LibraryManager.Application.Abstractions;
using LibraryManager.Application.Common;
using LibraryManager.Domain;
using Microsoft.Extensions.Logging;

namespace LibraryManager.Application.Loans;

internal static class CompleteActiveLoan
{
    public static async Task<LoanDto> ExecuteAsync(
        ILoanRepository loans,
        IBookRepository books,
        IAuditRepository audits,
        IOutboxWriter outbox,
        IUnitOfWork unitOfWork,
        IAvailabilityCache cache,
        IClock clock,
        ICurrentUserContext currentUser,
        ICorrelationContext correlation,
        ILogger logger,
        Guid loanId,
        LoanStatus terminalStatus,
        string auditAction,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var completed = await unitOfWork.ExecuteInTransactionAsync(
            async ct =>
            {
                var loan = await loans.GetByIdAsync(loanId, ct)
                    ?? throw new EntityNotFoundException(AuditMetadata.LoanEntity);

                var utcNow = clock.UtcNow;
                var transitioned = await loans.TryCompleteActiveAsync(loanId, terminalStatus, utcNow, ct);
                if (transitioned != 1)
                {
                    throw new BusinessRuleException("Only an Active loan can be returned or cancelled.");
                }

                var restored = await books.TryRestoreAvailabilityAsync(loan.BookId, ct);
                if (restored != 1)
                {
                    throw new InvalidOperationException("Availability could not be restored without exceeding TotalCopies.");
                }

                var audit = AuditEvent.Create(
                    AuditMetadata.LoanEntity,
                    loan.Id,
                    auditAction,
                    currentUser.ActorId,
                    utcNow,
                    correlation.CorrelationId,
                    JsonPayload.Serialize(new
                    {
                        loan.BookId,
                        loan.UserId,
                        Status = terminalStatus.ToString()
                    }));
                await audits.AddAsync(audit, ct);

                await outbox.WriteAsync(
                    AvailabilityOutbox.MessageType,
                    AvailabilityOutbox.Payload(loan.BookId, correlation.CorrelationId),
                    utcNow,
                    ct);

                await unitOfWork.SaveChangesAsync(ct);

                var reloaded = await loans.GetByIdAsync(loanId, ct)
                    ?? throw new EntityNotFoundException(AuditMetadata.LoanEntity);
                return LoanDto.From(reloaded);
            },
            cancellationToken);

        await AvailabilityCacheInvalidation.TryRemoveAsync(cache, logger, completed.BookId, cancellationToken);
        return completed;
    }
}
