using LibraryManager.Application.Abstractions;
using LibraryManager.Application.Common;
using LibraryManager.Domain;

namespace LibraryManager.Application.Books.CreateBook;

public sealed class CreateBookUseCase(
    IBookRepository books,
    IAuditRepository audits,
    IOutboxWriter outbox,
    IUnitOfWork unitOfWork,
    IClock clock,
    ICurrentUserContext currentUser,
    ICorrelationContext correlation)
{
    public async Task<BookDto> ExecuteAsync(
        string title,
        string isbn,
        string author,
        int totalCopies,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var existing = await books.GetByIsbnAsync(isbn, cancellationToken);
        if (existing is not null)
        {
            throw new BusinessRuleException("A book with this ISBN already exists.");
        }

        var utcNow = clock.UtcNow;
        var book = Book.Create(title, isbn, author, totalCopies, utcNow);
        await books.AddAsync(book, cancellationToken);

        var audit = AuditEvent.Create(
            AuditMetadata.BookEntity,
            book.Id,
            AuditMetadata.BookCreated,
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

        await outbox.WriteAsync(
            AvailabilityOutbox.MessageType,
            AvailabilityOutbox.Payload(book.Id, correlation.CorrelationId),
            utcNow,
            cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return BookDto.From(book);
    }
}
