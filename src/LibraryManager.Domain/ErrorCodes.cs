namespace LibraryManager.Domain;

public static class ErrorCodes
{
    public const string AuditEntityTypeRequired = "Audit.EntityTypeRequired";
    public const string AuditEntityIdRequired = "Audit.EntityIdRequired";
    public const string AuditActionRequired = "Audit.ActionRequired";
    public const string AuditActorIdRequired = "Audit.ActorIdRequired";
    public const string AuditCorrelationIdRequired = "Audit.CorrelationIdRequired";
    public const string AuditDataJsonRequired = "Audit.DataJsonRequired";

    public const string BookNotFound = "Book.NotFound";
    public const string BookUnavailable = "Book.Unavailable";
    public const string BookInactive = "Book.Inactive";
    public const string BookDuplicateIsbn = "Book.DuplicateIsbn";
    public const string BookTotalCopiesBelowBorrowed = "Book.TotalCopiesBelowBorrowed";
    public const string BookTitleRequired = "Book.TitleRequired";
    public const string BookIsbnRequired = "Book.IsbnRequired";
    public const string BookAuthorRequired = "Book.AuthorRequired";
    public const string BookTitleTooLong = "Book.TitleTooLong";
    public const string BookIsbnTooLong = "Book.IsbnTooLong";
    public const string BookAuthorTooLong = "Book.AuthorTooLong";
    public const string BookTotalCopiesInvalid = "Book.TotalCopiesInvalid";

    public const string UserNotFound = "User.NotFound";
    public const string UserDuplicateEmail = "User.DuplicateEmail";
    public const string UserNameRequired = "User.NameRequired";
    public const string UserEmailRequired = "User.EmailRequired";
    public const string UserNameTooLong = "User.NameTooLong";
    public const string UserEmailTooLong = "User.EmailTooLong";

    public const string LoanNotFound = "Loan.NotFound";
    public const string LoanInvalidState = "Loan.InvalidState";
    public const string LoanDuplicateActive = "Loan.DuplicateActive";
    public const string LoanBookIdRequired = "Loan.BookIdRequired";
    public const string LoanUserIdRequired = "Loan.UserIdRequired";
    public const string LoanDueDateInvalid = "Loan.DueDateInvalid";

    public const string IdempotencyPayloadMismatch = "Idempotency.PayloadMismatch";
}
