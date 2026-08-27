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

        var loan = Loan.Create(bookId, userId, borrowedAtUtc).Value;

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
        var result = Loan.Create(Guid.Empty, Guid.NewGuid(), DateTime.UtcNow);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.LoanBookIdRequired, result.Error.Code);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }

    [Fact]
    public void Create_rejects_empty_user_id()
    {
        var result = Loan.Create(Guid.NewGuid(), Guid.Empty, DateTime.UtcNow);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.LoanUserIdRequired, result.Error.Code);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }

    [Fact]
    public void MarkReturned_sets_returned_status_and_timestamp()
    {
        var now = DateTime.UtcNow;
        var loan = Loan.Create(Guid.NewGuid(), Guid.NewGuid(), now).Value;

        Assert.True(loan.MarkReturned(now.AddHours(1)).IsSuccess);

        Assert.Equal(LoanStatus.Returned, loan.Status);
        Assert.Equal(now.AddHours(1), loan.ReturnedAtUtc);
        Assert.Null(loan.CancelledAtUtc);
    }

    [Fact]
    public void MarkCancelled_sets_cancelled_status_and_timestamp()
    {
        var now = DateTime.UtcNow;
        var loan = Loan.Create(Guid.NewGuid(), Guid.NewGuid(), now).Value;

        Assert.True(loan.MarkCancelled(now.AddHours(1)).IsSuccess);

        Assert.Equal(LoanStatus.Cancelled, loan.Status);
        Assert.Equal(now.AddHours(1), loan.CancelledAtUtc);
        Assert.Null(loan.ReturnedAtUtc);
    }

    [Fact]
    public void MarkReturned_rejects_non_active_loan()
    {
        var now = DateTime.UtcNow;
        var loan = Loan.Create(Guid.NewGuid(), Guid.NewGuid(), now).Value;
        Assert.True(loan.MarkReturned(now.AddHours(1)).IsSuccess);

        var result = loan.MarkReturned(now.AddHours(2));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.LoanInvalidState, result.Error.Code);
        Assert.Equal(ErrorType.BusinessRule, result.Error.Type);
        Assert.Equal(LoanStatus.Returned, loan.Status);
    }

    [Fact]
    public void MarkCancelled_rejects_non_active_loan()
    {
        var now = DateTime.UtcNow;
        var loan = Loan.Create(Guid.NewGuid(), Guid.NewGuid(), now).Value;
        Assert.True(loan.MarkCancelled(now.AddHours(1)).IsSuccess);

        var result = loan.MarkCancelled(now.AddHours(2));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.LoanInvalidState, result.Error.Code);
        Assert.Equal(ErrorType.BusinessRule, result.Error.Type);
        Assert.Equal(LoanStatus.Cancelled, loan.Status);
    }
}
