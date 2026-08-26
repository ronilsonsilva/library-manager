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
        cancellationToken.ThrowIfCancellationRequested();

        var outcome = await unitOfWork.ExecuteInTransactionAsync(
            async ct =>
            {
                var book = await books.GetByIdAsync(id, ct)
                    ?? throw new EntityNotFoundException(AuditMetadata.BookEntity);

                if (totalCopies < 1)
                {
                    throw new DomainException("TotalCopies must be at least 1.");
                }

                var availabilityChanged = totalCopies != book.TotalCopies;
                if (availabilityChanged)
                {
                    var updated = await books.TryUpdateTotalCopiesAsync(id, totalCopies, ct);
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
                await audits.AddAsync(audit, ct);

                if (availabilityChanged)
                {
                    await outbox.WriteAsync(
                        AvailabilityOutbox.MessageType,
                        AvailabilityOutbox.Payload(book.Id, correlation.CorrelationId),
                        utcNow,
                        ct);
                }

                await unitOfWork.SaveChangesAsync(ct);
                return new UpdateBookOutcome(BookDto.From(book), availabilityChanged);
            },
            cancellationToken);

        if (outcome.AvailabilityChanged)
        {
            await AvailabilityCacheInvalidation.TryRemoveAsync(
                cache,
                logger,
                metrics,
                outcome.Book.Id,
                cancellationToken);
        }

        return outcome.Book;
    }

    private sealed record UpdateBookOutcome(BookDto Book, bool AvailabilityChanged);
}
