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
        var guard = new DomainGuard();
        guard.RequiredGuid(bookId, ErrorCodes.LoanBookIdRequired);
        guard.RequiredGuid(userId, ErrorCodes.LoanUserIdRequired);

        var validation = guard.ToResult();
        if (validation.IsFailure)
        {
            return Result.Failure<Loan>(validation.Error);
        }

        var dueAtUtc = borrowedAtUtc.AddDays(14);
        if (dueAtUtc <= borrowedAtUtc)
        {
            return Result.Failure<Loan>(Error.Validation(ErrorCodes.LoanDueDateInvalid));
        }

        return Result.Success(new Loan
        {
            Id = Guid.NewGuid(),
            BookId = bookId,
            UserId = userId,
            Status = LoanStatus.Active,
            BorrowedAtUtc = borrowedAtUtc,
            DueAtUtc = dueAtUtc
        });
    }

    public Result MarkReturned(DateTime utcNow)
    {
        var active = EnsureActive();
        if (active.IsFailure)
        {
            return active;
        }

        Status = LoanStatus.Returned;
        ReturnedAtUtc = utcNow;
        return Result.Success();
    }

    public Result MarkCancelled(DateTime utcNow)
    {
        var active = EnsureActive();
        if (active.IsFailure)
        {
            return active;
        }

        Status = LoanStatus.Cancelled;
        CancelledAtUtc = utcNow;
        return Result.Success();
    }

    private Result EnsureActive()
    {
        if (Status != LoanStatus.Active)
        {
            return Result.Failure(Error.BusinessRule(ErrorCodes.LoanInvalidState));
        }

        return Result.Success();
    }
}
