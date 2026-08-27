namespace LibraryManager.Domain;

public sealed class Result<T> : IResult
{
    private readonly T? _value;
    private readonly Error? _error;

    private Result(bool isSuccess, T? value, Error? error)
    {
        IsSuccess = isSuccess;
        _value = value;
        _error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Cannot access Value on a failed result.");

    public Error Error => IsFailure
        ? _error!
        : throw new InvalidOperationException("Cannot access Error on a successful result.");

    public static Result<T> Success(T value) => new(true, value, null);

    public static Result<T> Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new(false, default, error);
    }

    public Result<TOther> AsFailure<TOther>()
    {
        if (IsSuccess)
        {
            throw new InvalidOperationException("Cannot convert a successful result to a typed failure.");
        }

        return Result<TOther>.Failure(Error);
    }

    public Result AsFailure()
    {
        if (IsSuccess)
        {
            throw new InvalidOperationException("Cannot convert a successful result to a failure.");
        }

        return Result.Failure(Error);
    }
}
