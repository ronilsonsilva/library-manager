namespace LibraryManager.Infrastructure.Idempotency;

public static class LoanRequestCanonicalizer
{
    public static string ComputeHash(Guid bookId, Guid userId) =>
        global::LibraryManager.Application.Loans.CreateLoan.LoanRequestCanonicalizer.ComputeHash(bookId, userId);
}
