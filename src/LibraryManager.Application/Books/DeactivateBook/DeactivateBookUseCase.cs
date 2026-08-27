using LibraryManager.Application.Abstractions;
using LibraryManager.Application.Common;
using LibraryManager.Domain;

namespace LibraryManager.Application.Books.DeactivateBook;

public sealed class DeactivateBookUseCase(
    IBookRepository books,
    IAuditRepository audits,
    IOutboxWriter outbox,
    IUnitOfWork unitOfWork,
    IAvailabilityCache cache,
    IClock clock,
    ICurrentUserContext currentUser,
    ICorrelationContext correlation)
{
    public async Task<Result> ExecuteAsync(Guid id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var book = await books.GetByIdAsync(id, cancellationToken);
        if (book is null)
        {
            return Result.Failure(Error.NotFound(ErrorCodes.BookNotFound));
        }

        if (!book.IsActive)
        {
            return Result.Success();
        }

        var utcNow = clock.UtcNow;
        book.Deactivate(utcNow);

        var audit = AuditEvent.Create(
            AuditMetadata.BookEntity,
            book.Id,
            AuditMetadata.BookDeactivated,
            currentUser.ActorId,
            utcNow,
            correlation.CorrelationId,
            JsonPayload.Serialize(new { book.Isbn, book.IsActive }));
        if (audit.IsFailure)
        {
            return audit.AsFailure();
        }

        await audits.AddAsync(audit.Value, cancellationToken);

        await outbox.WriteAsync(
            AvailabilityOutbox.MessageType,
            AvailabilityOutbox.Payload(book.Id, correlation.CorrelationId),
            utcNow,
            cancellationToken);

        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        if (saved.IsFailure)
        {
            return saved;
        }

        await cache.RemoveAsync(book.Id, cancellationToken);
        return Result.Success();
    }
}
