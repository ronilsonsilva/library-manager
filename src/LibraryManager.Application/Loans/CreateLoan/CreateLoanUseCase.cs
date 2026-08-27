using LibraryManager.Application.Abstractions;
using LibraryManager.Application.Common;
using LibraryManager.Application.Telemetry;
using LibraryManager.Domain;
using System.Diagnostics;

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
    ILibraryManagerMetrics metrics)
{
    public const string IdempotencyEndpoint = "POST /loans";
    public const int CreatedStatus = 201;

    public async Task<Result<LoanDto>> ExecuteAsync(
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

                var user = await users.GetByIdAsync(userId, ct);
                if (user is null)
                {
                    return Result.Failure<CreateLoanOutcome>(Error.NotFound(ErrorCodes.UserNotFound));
                }

                var book = await books.GetByIdAsync(bookId, ct);
                if (book is null)
                {
                    return Result.Failure<CreateLoanOutcome>(Error.NotFound(ErrorCodes.BookNotFound));
                }

                if (!book.IsActive)
                {
                    return Result.Failure<CreateLoanOutcome>(Error.BusinessRule(ErrorCodes.BookInactive));
                }

                var reserved = await books.TryReserveAvailabilityAsync(bookId, ct);
                if (reserved != 1)
                {
                    return Result.Failure<CreateLoanOutcome>(Error.BusinessRule(ErrorCodes.BookUnavailable));
                }

                var utcNow = clock.UtcNow;
                var created = Loan.Create(bookId, userId, utcNow);
                if (created.IsFailure)
                {
                    return created.AsFailure<CreateLoanOutcome>();
                }

                await loans.AddAsync(created.Value, ct);

                var audit = AuditEvent.Create(
                    AuditMetadata.LoanEntity,
                    created.Value.Id,
                    AuditMetadata.LoanCreated,
                    currentUser.ActorId,
                    utcNow,
                    correlation.CorrelationId,
                    JsonPayload.Serialize(new
                    {
                        created.Value.BookId,
                        created.Value.UserId,
                        created.Value.DueAtUtc
                    }));
                if (audit.IsFailure)
                {
                    return audit.AsFailure<CreateLoanOutcome>();
                }

                await audits.AddAsync(audit.Value, ct);

                await outbox.WriteAsync(
                    AvailabilityOutbox.MessageType,
                    AvailabilityOutbox.Payload(bookId, correlation.CorrelationId),
                    utcNow,
                    ct);

                var dto = LoanDto.From(created.Value);
                await idempotency.CompleteAsync(
                    IdempotencyEndpoint,
                    idempotencyKey,
                    CreatedStatus,
                    JsonPayload.Serialize(dto),
                    ct);

                var saved = await unitOfWork.SaveChangesAsync(ct);
                if (saved.IsFailure)
                {
                    return saved.AsFailure<CreateLoanOutcome>();
                }

                return Result.Success(new CreateLoanOutcome(dto, Created: true));
            },
            cancellationToken);

        if (outcome.IsFailure)
        {
            if (outcome.Error.Code == ErrorCodes.BookUnavailable)
            {
                metrics.RecordLoanUnavailable();
            }

            return outcome.AsFailure<LoanDto>();
        }

        if (outcome.Value.Created)
        {
            metrics.RecordLoanCreated();
            await cache.RemoveAsync(bookId, cancellationToken);
        }
        else
        {
            metrics.RecordIdempotencyReplay();
        }

        metrics.RecordLoanDuration(started.Elapsed);
        return Result.Success(outcome.Value.Loan);
    }

    private static Result<CreateLoanOutcome> ReplayOrConflict(IdempotencyLookup reservation, string requestHash)
    {
        if (!string.Equals(reservation.RequestHash, requestHash, StringComparison.Ordinal))
        {
            return Result.Failure<CreateLoanOutcome>(Error.Conflict(ErrorCodes.IdempotencyPayloadMismatch));
        }

        if (reservation.ResponseStatus == CreatedStatus
            && !string.IsNullOrWhiteSpace(reservation.ResponseBody))
        {
            var replayed = JsonPayload.Deserialize<LoanDto>(reservation.ResponseBody)
                ?? throw new InvalidOperationException("Stored idempotency response is invalid.");
            return Result.Success(new CreateLoanOutcome(replayed, Created: false));
        }

        throw new InvalidOperationException("An operation with this Idempotency-Key is already in progress.");
    }

    private sealed record CreateLoanOutcome(LoanDto Loan, bool Created);
}
