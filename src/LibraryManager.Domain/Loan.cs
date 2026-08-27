using LibraryManager.Domain.Validation;

namespace LibraryManager.Domain;

public sealed class Loan
{
    private Loan()
    {
    }

    public Guid Id { get; private set; }

    public Guid BookId { get; private set; }

    public Guid UserId { get; private set; }

    public LoanStatus Status { get; private set; }

    public DateTime BorrowedAtUtc { get; private set; }

    public DateTime DueAtUtc { get; private set; }

    public DateTime? ReturnedAtUtc { get; private set; }

    public DateTime? CancelledAtUtc { get; private set; }

    public static Result<Loan> Create(Guid bookId, Guid userId, DateTime borrowedAtUtc)
    {
        var dueAtUtc = borrowedAtUtc.AddDays(14);
        return new DomainGuard()
            .RequiredGuid(bookId, ErrorCodes.LoanBookIdRequired)
            .RequiredGuid(userId, ErrorCodes.LoanUserIdRequired)
            .Ensure(dueAtUtc > borrowedAtUtc, Error.Validation(ErrorCodes.LoanDueDateInvalid))
            .ToResult(() => new Loan
            {
                Id = Guid.NewGuid(),
                BookId = bookId,
                UserId = userId,
                Status = LoanStatus.Active,
                BorrowedAtUtc = borrowedAtUtc,
                DueAtUtc = dueAtUtc
            });
    }

    public Result MarkReturned(DateTime utcNow) =>
        ActiveGuard().Apply(() =>
        {
            Status = LoanStatus.Returned;
            ReturnedAtUtc = utcNow;
        });

    public Result MarkCancelled(DateTime utcNow) =>
        ActiveGuard().Apply(() =>
        {
            Status = LoanStatus.Cancelled;
            CancelledAtUtc = utcNow;
        });

    private DomainGuard ActiveGuard() =>
        new DomainGuard().Ensure(
            Status == LoanStatus.Active,
            Error.BusinessRule(ErrorCodes.LoanInvalidState));
}
