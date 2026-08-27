using LibraryManager.Domain;
using LibraryManager.Domain.Validation;

namespace LibraryManager.UnitTests.Domain;

public sealed class DomainGuardTests
{
    [Fact]
    public void Required_trims_and_succeeds()
    {
        var result = new DomainGuard()
            .Required("  Dune  ", ErrorCodes.BookTitleRequired, out var value)
            .ToResult();

        Assert.True(result.IsSuccess);
        Assert.Equal("Dune", value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Required_rejects_missing_string_and_stops(string? value)
    {
        var result = new DomainGuard()
            .Required(value, ErrorCodes.BookTitleRequired, out var normalized)
            .Positive(0, ErrorCodes.BookTotalCopiesInvalid)
            .ToResult();

        Assert.Equal(string.Empty, normalized);
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.BookTitleRequired, result.Error.Code);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }

    [Fact]
    public void Required_with_max_length_and_transform_normalizes_then_enforces_length()
    {
        var result = new DomainGuard()
            .Required(
                "  Ada@Example.COM  ",
                ErrorCodes.UserEmailRequired,
                User.EmailMaxLength,
                ErrorCodes.UserEmailTooLong,
                out var email,
                static value => value.ToLowerInvariant())
            .ToResult();

        Assert.True(result.IsSuccess);
        Assert.Equal("ada@example.com", email);
    }

    [Fact]
    public void Required_with_max_length_rejects_too_long_after_required_succeeds()
    {
        var result = new DomainGuard()
            .Required(new string('a', 6), ErrorCodes.BookTitleRequired, 5, ErrorCodes.BookTitleTooLong, out _)
            .ToResult();

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.BookTitleTooLong, result.Error.Code);
        Assert.NotNull(result.Error.Arguments);
        Assert.Equal(5, Assert.Single(result.Error.Arguments));
    }

    [Fact]
    public void RequiredGuid_rejects_empty()
    {
        var result = new DomainGuard()
            .RequiredGuid(Guid.Empty, ErrorCodes.LoanBookIdRequired)
            .ToResult();

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.LoanBookIdRequired, result.Error.Code);
    }

    [Fact]
    public void Positive_rejects_values_below_one()
    {
        var result = new DomainGuard()
            .Positive(0, ErrorCodes.BookTotalCopiesInvalid)
            .ToResult();

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.BookTotalCopiesInvalid, result.Error.Code);
    }

    [Fact]
    public void MaxLength_includes_limit_in_arguments()
    {
        var result = new DomainGuard()
            .Required(new string('a', 6), ErrorCodes.BookTitleRequired, out var value)
            .MaxLength(value, 5, ErrorCodes.BookTitleTooLong)
            .ToResult();

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.BookTitleTooLong, result.Error.Code);
        Assert.NotNull(result.Error.Arguments);
        Assert.Equal(5, Assert.Single(result.Error.Arguments));
    }

    [Fact]
    public void UtcTimestamp_rejects_local_kind()
    {
        var result = new DomainGuard()
            .UtcTimestamp(DateTime.Now, ErrorCodes.LoanDueDateInvalid)
            .ToResult();

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.LoanDueDateInvalid, result.Error.Code);
    }

    [Fact]
    public void UtcTimestamp_accepts_utc_and_unspecified_kind()
    {
        Assert.True(
            new DomainGuard().UtcTimestamp(DateTime.UtcNow, ErrorCodes.LoanDueDateInvalid).ToResult().IsSuccess);
        Assert.True(
            new DomainGuard()
                .UtcTimestamp(new DateTime(2026, 8, 27, 12, 0, 0, DateTimeKind.Unspecified), ErrorCodes.LoanDueDateInvalid)
                .ToResult()
                .IsSuccess);
    }

    [Fact]
    public void Ensure_keeps_the_first_failure()
    {
        var result = new DomainGuard()
            .Ensure(false, Error.BusinessRule(ErrorCodes.BookInactive))
            .Ensure(false, Error.Validation(ErrorCodes.BookTitleRequired))
            .ToResult();

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.BookInactive, result.Error.Code);
        Assert.Equal(ErrorType.BusinessRule, result.Error.Type);
    }

    [Fact]
    public void Apply_runs_only_when_validation_succeeds()
    {
        var ran = false;
        var success = new DomainGuard().Apply(() => ran = true);
        Assert.True(success.IsSuccess);
        Assert.True(ran);

        ran = false;
        var failure = new DomainGuard()
            .Positive(0, ErrorCodes.BookTotalCopiesInvalid)
            .Apply(() => ran = true);
        Assert.True(failure.IsFailure);
        Assert.False(ran);
        Assert.Equal(ErrorCodes.BookTotalCopiesInvalid, failure.Error.Code);
    }
}
