using LibraryManager.Application.Abstractions;
using LibraryManager.Application.Common;
using LibraryManager.Domain;
using Microsoft.Extensions.Logging;

namespace LibraryManager.Application.Books.DeactivateBook;

public sealed class DeactivateBookUseCase(
    IBookRepository books,
    IAuditRepository audits,
    IOutboxWriter outbox,
    IUnitOfWork unitOfWork,
    IAvailabilityCache cache,
    IClock clock,
    ICurrentUserContext currentUser,
    ICorrelationContext correlation,
    ILogger<DeactivateBookUseCase> logger)
{
    public async Task ExecuteAsync(Guid id, CancellationToken cancellationToken)
    {
        var book = await books.GetByIdAsync(id, cancellationToken)
            ?? throw new EntityNotFoundException(AuditMetadata.BookEntity);

        if (!book.IsActive)
        {
            return;
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
        await audits.AddAsync(audit, cancellationToken);

        await outbox.WriteAsync(
            AvailabilityOutbox.MessageType,
            AvailabilityOutbox.Payload(book.Id, correlation.CorrelationId),
            utcNow,
            cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await AvailabilityCacheInvalidation.TryRemoveAsync(cache, logger, book.Id, cancellationToken);
    }
}
