using LibraryManager.Domain;

namespace LibraryManager.Application.Loans;

public sealed record LoanDto(
    Guid Id,
    Guid BookId,
    Guid UserId,
    string Status,
    DateTime BorrowedAtUtc,
    DateTime DueAtUtc,
    DateTime? ReturnedAtUtc,
    DateTime? CancelledAtUtc)
{
    public static LoanDto From(Loan loan) =>
        new(
            loan.Id,
            loan.BookId,
            loan.UserId,
            loan.Status.ToString(),
            loan.BorrowedAtUtc,
            loan.DueAtUtc,
            loan.ReturnedAtUtc,
            loan.CancelledAtUtc);
}
