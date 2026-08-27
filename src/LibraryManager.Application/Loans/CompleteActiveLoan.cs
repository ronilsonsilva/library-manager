using LibraryManager.Application.Abstractions;
using LibraryManager.Application.Common;
using LibraryManager.Domain;
using Microsoft.Extensions.Logging;

namespace LibraryManager.Application.Loans;

internal static class CompleteActiveLoan
{
    public static async Task<Result<LoanDto>> ExecuteAsync(
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
        ILibraryManagerMetrics metrics,
        Guid loanId,
        LoanStatus terminalStatus,
        string auditAction,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var completed = await unitOfWork.ExecuteInTransactionAsync(
            async ct =>
            {
                var loan = await loans.GetByIdAsync(loanId, ct);
                if (loan is null)
                {
                    return Result.Failure<LoanDto>(Error.NotFound(ErrorCodes.LoanNotFound));
                }

                var utcNow = clock.UtcNow;
                var transitioned = await loans.TryCompleteActiveAsync(loanId, terminalStatus, utcNow, ct);
                if (transitioned != 1)
                {
                    return Result.Failure<LoanDto>(Error.BusinessRule(ErrorCodes.LoanInvalidState));
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
                if (audit.IsFailure)
                {
                    return audit.AsFailure<LoanDto>();
                }

                await audits.AddAsync(audit.Value, ct);

                await outbox.WriteAsync(
                    AvailabilityOutbox.MessageType,
                    AvailabilityOutbox.Payload(loan.BookId, correlation.CorrelationId),
                    utcNow,
                    ct);

                var saved = await unitOfWork.SaveChangesAsync(ct);
                if (saved.IsFailure)
                {
                    return saved.AsFailure<LoanDto>();
                }

                var reloaded = await loans.GetByIdAsync(loanId, ct);
                if (reloaded is null)
                {
                    return Result.Failure<LoanDto>(Error.NotFound(ErrorCodes.LoanNotFound));
                }

                return Result.Success(LoanDto.From(reloaded));
            },
            cancellationToken);

        if (completed.IsFailure)
        {
            return completed;
        }

        await AvailabilityCacheInvalidation.TryRemoveAsync(
            cache,
            logger,
            metrics,
            completed.Value.BookId,
            cancellationToken);
        return completed;
    }
}
