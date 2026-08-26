using LibraryManager.Domain;

namespace LibraryManager.UnitTests.Domain;

public sealed class LoanTests
{
    [Fact]
    public void Create_sets_due_date_fourteen_days_after_borrowed_at_in_utc()
    {
        var bookId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var borrowedAtUtc = new DateTime(2026, 8, 25, 15, 30, 0, DateTimeKind.Utc);

        var loan = Loan.Create(bookId, userId, borrowedAtUtc);

        Assert.Equal(bookId, loan.BookId);
        Assert.Equal(userId, loan.UserId);
        Assert.Equal(LoanStatus.Active, loan.Status);
        Assert.Equal(borrowedAtUtc, loan.BorrowedAtUtc);
        Assert.Equal(borrowedAtUtc.AddDays(14), loan.DueAtUtc);
        Assert.Equal(DateTimeKind.Utc, loan.DueAtUtc.Kind);
        Assert.True(loan.DueAtUtc > loan.BorrowedAtUtc);
        Assert.Null(loan.ReturnedAtUtc);
        Assert.Null(loan.CancelledAtUtc);
    }

    [Fact]
    public void Create_rejects_empty_book_id()
    {
        var exception = Assert.Throws<DomainException>(() =>
            Loan.Create(Guid.Empty, Guid.NewGuid(), DateTime.UtcNow));

        Assert.Contains("BookId", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_rejects_empty_user_id()
    {
        var exception = Assert.Throws<DomainException>(() =>
            Loan.Create(Guid.NewGuid(), Guid.Empty, DateTime.UtcNow));

        Assert.Contains("UserId", exception.Message, StringComparison.Ordinal);
    }
}
