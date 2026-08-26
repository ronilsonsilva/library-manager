using LibraryManager.Application.Abstractions;
using LibraryManager.Application.Common;
using LibraryManager.Application.Loans;
using LibraryManager.Domain;
using Microsoft.Extensions.Logging;

namespace LibraryManager.Application.Loans.CreateLoan;

public sealed class CreateLoanUseCase(
    IIdempotencyStore idempotency,
    IUserRepository users,
    IBookRepository books,
    ILoanRepository loans,
    IAuditRepository audits,
    IOutboxWriter outbox,
    IUnitOfWork unitOfWork,
    IAvailabilityCache cache,
    IClock clock,
    ICurrentUserContext currentUser,
    ICorrelationContext correlation,
    ILogger<CreateLoanUseCase> logger)
{
    public const string IdempotencyEndpoint = "POST /loans";

    public async Task<LoanDto> ExecuteAsync(
        Guid bookId,
        Guid userId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var loan = await unitOfWork.ExecuteInTransactionAsync(
            async ct =>
            {
                var reservation = await idempotency.TryReserveAsync(
                    IdempotencyEndpoint,
                    idempotencyKey,
                    LoanRequestCanonicalizer.ComputeHash(bookId, userId),
                    ct);

                if (!reservation.IsOwner)
                {
                    throw new BusinessRuleException("An operation with this Idempotency-Key is already in progress.");
                }

                _ = await users.GetByIdAsync(userId, ct)
                    ?? throw new EntityNotFoundException(AuditMetadata.UserEntity);

                var book = await books.GetByIdAsync(bookId, ct)
                    ?? throw new EntityNotFoundException(AuditMetadata.BookEntity);

                if (!book.IsActive)
                {
                    throw new BusinessRuleException("Book is not active.");
                }

                var reserved = await books.TryReserveAvailabilityAsync(bookId, ct);
                if (reserved != 1)
                {
                    throw new BusinessRuleException("No copies are available.");
                }

                var utcNow = clock.UtcNow;
                var created = Loan.Create(bookId, userId, utcNow);
                await loans.AddAsync(created, ct);

                var audit = AuditEvent.Create(
                    AuditMetadata.LoanEntity,
                    created.Id,
                    AuditMetadata.LoanCreated,
                    currentUser.ActorId,
                    utcNow,
                    correlation.CorrelationId,
                    JsonPayload.Serialize(new
                    {
                        created.BookId,
                        created.UserId,
                        created.DueAtUtc
                    }));
                await audits.AddAsync(audit, ct);

                await outbox.WriteAsync(
                    AvailabilityOutbox.MessageType,
                    AvailabilityOutbox.Payload(bookId, correlation.CorrelationId),
                    utcNow,
                    ct);

                await unitOfWork.SaveChangesAsync(ct);
                return created;
            },
            cancellationToken);

        try
        {
            await cache.RemoveAsync(bookId, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(
                exception,
                "Failed to invalidate availability cache for book {BookId}",
                bookId);
        }

        return LoanDto.From(loan);
    }
}
