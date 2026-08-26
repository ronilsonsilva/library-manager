namespace LibraryManager.Application.Common;

public static class AuditMetadata
{
    public const string BookEntity = "Book";
    public const string UserEntity = "User";
    public const string LoanEntity = "Loan";

    public const string BookCreated = "BookCreated";
    public const string BookUpdated = "BookUpdated";
    public const string BookDeactivated = "BookDeactivated";
    public const string UserCreated = "UserCreated";
    public const string LoanCreated = "LoanCreated";
    public const string LoanReturned = "LoanReturned";
    public const string LoanCancelled = "LoanCancelled";
}
