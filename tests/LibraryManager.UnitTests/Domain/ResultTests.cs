using LibraryManager.Domain;

namespace LibraryManager.UnitTests.Domain;

public sealed class ResultTests
{
    [Fact]
    public void Success_exposes_value_and_rejects_error_access()
    {
        var result = Result.Success(42);

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal(42, result.Value);
        Assert.Throws<InvalidOperationException>(() => _ = result.Error);
    }

    [Fact]
    public void Failure_exposes_error_and_rejects_value_access()
    {
        var error = Error.NotFound(ErrorCodes.BookNotFound);
        var result = Result.Failure<int>(error);

        Assert.True(result.IsFailure);
        Assert.False(result.IsSuccess);
        Assert.Same(error, result.Error);
        Assert.Equal(ErrorCodes.BookNotFound, result.Error.Code);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
        Assert.Throws<InvalidOperationException>(() => _ = result.Value);
    }

    [Fact]
    public void Non_generic_success_rejects_error_access()
    {
        var result = Result.Success();

        Assert.True(result.IsSuccess);
        Assert.Throws<InvalidOperationException>(() => _ = result.Error);
    }

    [Fact]
    public void Error_stores_code_type_and_arguments_without_a_message()
    {
        var error = Error.Validation(ErrorCodes.BookTitleTooLong, Book.TitleMaxLength);

        Assert.Equal(ErrorCodes.BookTitleTooLong, error.Code);
        Assert.Equal(ErrorType.Validation, error.Type);
        Assert.NotNull(error.Arguments);
        Assert.Equal(Book.TitleMaxLength, Assert.Single(error.Arguments));
    }

    [Fact]
    public void Error_factories_cover_expected_categories()
    {
        Assert.Equal(ErrorType.NotFound, Error.NotFound(ErrorCodes.UserNotFound).Type);
        Assert.Equal(ErrorType.BusinessRule, Error.BusinessRule(ErrorCodes.BookInactive).Type);
        Assert.Equal(ErrorType.Conflict, Error.Conflict(ErrorCodes.IdempotencyPayloadMismatch).Type);
    }
}
