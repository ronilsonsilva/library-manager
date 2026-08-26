using LibraryManager.Application.Abstractions;
using LibraryManager.Application.Books;
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
    public async Task<BookDto> ExecuteAsync(
        Guid id,
        string title,
        string author,
        int totalCopies,
        CancellationToken cancellationToken)
    {
        var book = await books.GetByIdAsync(id, cancellationToken)
            ?? throw new EntityNotFoundException(AuditMetadata.BookEntity);

        if (totalCopies < 1)
        {
            throw new DomainException("TotalCopies must be at least 1.");
        }

        var availabilityChanged = totalCopies != book.TotalCopies;
        if (availabilityChanged)
        {
            var updated = await books.TryUpdateTotalCopiesAsync(id, totalCopies, cancellationToken);
            if (!updated)
            {
                throw new BusinessRuleException(
                    "TotalCopies cannot be below the number of copies currently on loan.");
            }
        }

        var utcNow = clock.UtcNow;
        book.UpdateCatalog(title, author, utcNow);

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
        await audits.AddAsync(audit, cancellationToken);

        if (availabilityChanged)
        {
            await outbox.WriteAsync(
                AvailabilityOutbox.MessageType,
                AvailabilityOutbox.Payload(book.Id, correlation.CorrelationId),
                utcNow,
                cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        if (availabilityChanged)
        {
            await AvailabilityCacheInvalidation.TryRemoveAsync(cache, logger, metrics, book.Id, cancellationToken);
        }

        return BookDto.From(book);
    }
}
