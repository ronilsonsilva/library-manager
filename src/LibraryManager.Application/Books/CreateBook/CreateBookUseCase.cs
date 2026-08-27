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
    public async Task<Result<BookDto>> ExecuteAsync(
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
            return Result.Failure<BookDto>(Error.BusinessRule(ErrorCodes.BookDuplicateIsbn));
        }

        var utcNow = clock.UtcNow;
        var created = Book.Create(title, isbn, author, totalCopies, utcNow);
        if (created.IsFailure)
        {
            return created.AsFailure<BookDto>();
        }

        var book = created.Value;
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
        if (audit.IsFailure)
        {
            return audit.AsFailure<BookDto>();
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
            return saved.AsFailure<BookDto>();
        }

        return Result.Success(BookDto.From(book));
    }
}
