using LibraryManager.Domain;
using LibraryManager.Domain.Validation;

namespace LibraryManager.UnitTests.Domain;

public sealed class DomainGuardTests
{
    [Fact]
    public void Required_trims_and_succeeds()
    {
        var guard = new DomainGuard();
        guard.Required("  Dune  ", ErrorCodes.BookTitleRequired, out var value);

        Assert.False(guard.HasError);
        Assert.Equal("Dune", value);
        Assert.True(guard.ToResult().IsSuccess);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Required_rejects_missing_string_and_stops(string? value)
    {
        var guard = new DomainGuard();
        guard.Required(value, ErrorCodes.BookTitleRequired, out var normalized);
        guard.Positive(0, ErrorCodes.BookTotalCopiesInvalid);

        Assert.Equal(string.Empty, normalized);
        var result = guard.ToResult();
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.BookTitleRequired, result.Error.Code);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }

    [Fact]
    public void RequiredGuid_rejects_empty()
    {
        var guard = new DomainGuard();
        guard.RequiredGuid(Guid.Empty, ErrorCodes.LoanBookIdRequired);

        var result = guard.ToResult();
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.LoanBookIdRequired, result.Error.Code);
    }

    [Fact]
    public void Positive_rejects_values_below_one()
    {
        var guard = new DomainGuard();
        guard.Positive(0, ErrorCodes.BookTotalCopiesInvalid);

        Assert.True(guard.ToResult().IsFailure);
        Assert.Equal(ErrorCodes.BookTotalCopiesInvalid, guard.ToResult().Error.Code);
    }

    [Fact]
    public void MaxLength_includes_limit_in_arguments()
    {
        var guard = new DomainGuard();
        guard.Required(new string('a', 6), ErrorCodes.BookTitleRequired, out var value);
        guard.MaxLength(value, 5, ErrorCodes.BookTitleTooLong);

        var result = guard.ToResult();
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.BookTitleTooLong, result.Error.Code);
        Assert.NotNull(result.Error.Arguments);
        Assert.Equal(5, Assert.Single(result.Error.Arguments));
    }

    [Fact]
    public void UtcTimestamp_rejects_local_kind()
    {
        var guard = new DomainGuard();
        guard.UtcTimestamp(DateTime.Now, ErrorCodes.LoanDueDateInvalid);

        Assert.True(guard.ToResult().IsFailure);
        Assert.Equal(ErrorCodes.LoanDueDateInvalid, guard.ToResult().Error.Code);
    }

    [Fact]
    public void UtcTimestamp_accepts_utc_kind()
    {
        var guard = new DomainGuard();
        guard.UtcTimestamp(DateTime.UtcNow, ErrorCodes.LoanDueDateInvalid);

        Assert.True(guard.ToResult().IsSuccess);
    }
}
