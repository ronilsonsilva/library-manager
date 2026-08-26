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

    public static Loan Create(Guid bookId, Guid userId, DateTime borrowedAtUtc)
    {
        if (bookId == Guid.Empty)
        {
            throw new DomainException("BookId is required.");
        }

        if (userId == Guid.Empty)
        {
            throw new DomainException("UserId is required.");
        }

        var dueAtUtc = borrowedAtUtc.AddDays(14);
        if (dueAtUtc <= borrowedAtUtc)
        {
            throw new DomainException("DueAtUtc must be later than BorrowedAtUtc.");
        }

        return new Loan
        {
            Id = Guid.NewGuid(),
            BookId = bookId,
            UserId = userId,
            Status = LoanStatus.Active,
            BorrowedAtUtc = borrowedAtUtc,
            DueAtUtc = dueAtUtc
        };
    }

    public void MarkReturned(DateTime utcNow)
    {
        EnsureActive();
        Status = LoanStatus.Returned;
        ReturnedAtUtc = utcNow;
    }

    public void MarkCancelled(DateTime utcNow)
    {
        EnsureActive();
        Status = LoanStatus.Cancelled;
        CancelledAtUtc = utcNow;
    }

    private void EnsureActive()
    {
        if (Status != LoanStatus.Active)
        {
            throw new DomainException("Only an Active loan can be returned or cancelled.");
        }
    }
}
