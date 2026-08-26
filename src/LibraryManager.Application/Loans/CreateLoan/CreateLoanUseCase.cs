using System.Diagnostics;
using LibraryManager.Application.Abstractions;
using LibraryManager.Application.Common;
using LibraryManager.Application.Loans;
using LibraryManager.Application.Telemetry;
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
    ILibraryManagerMetrics metrics,
    ILogger<CreateLoanUseCase> logger)
{
    public const string IdempotencyEndpoint = "POST /loans";
    public const int CreatedStatus = 201;

    public async Task<LoanDto> ExecuteAsync(
        Guid bookId,
        Guid userId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var activity = LibraryManagerInstrumentation.ActivitySource.StartActivity("CreateLoan");
        activity?.SetTag("book.id", bookId.ToString());
        activity?.SetTag("user.id", userId.ToString());
        activity?.SetTag("correlation.id", correlation.CorrelationId);

        var started = Stopwatch.StartNew();
        try
        {
            var outcome = await unitOfWork.ExecuteInTransactionAsync(
            async ct =>
            {
                var requestHash = LoanRequestCanonicalizer.ComputeHash(bookId, userId);
                var reservation = await idempotency.TryReserveAsync(
                    IdempotencyEndpoint,
                    idempotencyKey,
                    requestHash,
                    ct);

                if (!reservation.IsOwner)
                {
                    return ReplayOrConflict(reservation, requestHash);
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

                var dto = LoanDto.From(created);
                await idempotency.CompleteAsync(
                    IdempotencyEndpoint,
                    idempotencyKey,
                    CreatedStatus,
                    JsonPayload.Serialize(dto),
                    ct);

                await unitOfWork.SaveChangesAsync(ct);
                return new CreateLoanOutcome(dto, Created: true);
            },
            cancellationToken);

            if (outcome.Created)
            {
                metrics.RecordLoanCreated();
                await AvailabilityCacheInvalidation.TryRemoveAsync(
                    cache,
                    logger,
                    metrics,
                    bookId,
                    cancellationToken);
            }
            else
            {
                metrics.RecordIdempotencyReplay();
            }

            metrics.RecordLoanDuration(started.Elapsed);
            return outcome.Loan;
        }
        catch (BusinessRuleException exception) when (exception.Message == "No copies are available.")
        {
            metrics.RecordLoanUnavailable();
            throw;
        }
    }

    private static CreateLoanOutcome ReplayOrConflict(IdempotencyLookup reservation, string requestHash)
    {
        if (!string.Equals(reservation.RequestHash, requestHash, StringComparison.Ordinal))
        {
            throw new IdempotencyConflictException();
        }

        if (reservation.ResponseStatus == CreatedStatus
            && !string.IsNullOrWhiteSpace(reservation.ResponseBody))
        {
            var replayed = JsonPayload.Deserialize<LoanDto>(reservation.ResponseBody)
                ?? throw new InvalidOperationException("Stored idempotency response is invalid.");
            return new CreateLoanOutcome(replayed, Created: false);
        }

        throw new InvalidOperationException("An operation with this Idempotency-Key is already in progress.");
    }

    private sealed record CreateLoanOutcome(LoanDto Loan, bool Created);
}
