using LibraryManager.Application.Abstractions;
using LibraryManager.Application.Common;
using LibraryManager.Domain;
using Microsoft.Extensions.Logging;

namespace LibraryManager.Application.Books.UpdateBook;

public sealed class UpdateBookUseCase(
    IBookRepository books,
    IAuditRepository audits,
    IOutboxWriter outbox,
    IUnitOfWork unitOfWork,
    IAvailabilityCache cache,
    IClock clock,
    ICurrentUserContext currentUser,
    ICorrelationContext correlation,
    ILogger<UpdateBookUseCase> logger,
    ILibraryManagerMetrics metrics)
{
    public async Task<Result<BookDto>> ExecuteAsync(
        Guid id,
        string title,
        string author,
        int totalCopies,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var outcome = await unitOfWork.ExecuteInTransactionAsync(
            async ct =>
            {
                var book = await books.GetByIdAsync(id, ct);
                if (book is null)
                {
                    return Result.Failure<UpdateBookOutcome>(Error.NotFound(ErrorCodes.BookNotFound));
                }

                if (totalCopies < 1)
                {
                    return Result.Failure<UpdateBookOutcome>(Error.Validation(ErrorCodes.BookTotalCopiesInvalid));
                }

                var availabilityChanged = totalCopies != book.TotalCopies;
                if (availabilityChanged)
                {
                    var updated = await books.TryUpdateTotalCopiesAsync(id, totalCopies, ct);
                    if (!updated)
                    {
                        return Result.Failure<UpdateBookOutcome>(
                            Error.BusinessRule(ErrorCodes.BookTotalCopiesBelowBorrowed));
                    }
                }

                var utcNow = clock.UtcNow;
                var catalog = book.UpdateCatalog(title, author, utcNow);
                if (catalog.IsFailure)
                {
                    return catalog.AsFailure<UpdateBookOutcome>();
                }

                var audit = AuditEvent.Create(
                    AuditMetadata.BookEntity,
                    book.Id,
                    AuditMetadata.BookUpdated,
                    currentUser.ActorId,
                    utcNow,
                    correlation.CorrelationId,
                    JsonPayload.Serialize(new
                    {
                        book.Title,
                        book.Isbn,
                        book.Author,
                        book.TotalCopies
                    }));
                if (audit.IsFailure)
                {
                    return audit.AsFailure<UpdateBookOutcome>();
                }

                await audits.AddAsync(audit.Value, ct);

                if (availabilityChanged)
                {
                    await outbox.WriteAsync(
                        AvailabilityOutbox.MessageType,
                        AvailabilityOutbox.Payload(book.Id, correlation.CorrelationId),
                        utcNow,
                        ct);
                }

                var saved = await unitOfWork.SaveChangesAsync(ct);
                if (saved.IsFailure)
                {
                    return saved.AsFailure<UpdateBookOutcome>();
                }

                return Result.Success(new UpdateBookOutcome(BookDto.From(book), availabilityChanged));
            },
            cancellationToken);

        if (outcome.IsFailure)
        {
            return outcome.AsFailure<BookDto>();
        }

        if (outcome.Value.AvailabilityChanged)
        {
            await AvailabilityCacheInvalidation.TryRemoveAsync(
                cache,
                logger,
                metrics,
                outcome.Value.Book.Id,
                cancellationToken);
        }

        return Result.Success(outcome.Value.Book);
    }

    private sealed record UpdateBookOutcome(BookDto Book, bool AvailabilityChanged);
}
