namespace LibraryManager.Domain.Validation;

public sealed class DomainGuard
{
    private Error? _error;

    public bool HasError => _error is not null;

    public DomainGuard Required(string? value, string code, out string normalized)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            normalized = string.Empty;
            return Fail(Error.Validation(code));
        }

        normalized = value.Trim();
        return this;
    }

    public DomainGuard Required(
        string? value,
        string requiredCode,
        int maxLength,
        string tooLongCode,
        out string normalized,
        Func<string, string>? transform = null)
    {
        Required(value, requiredCode, out normalized);
        if (_error is not null)
        {
            return this;
        }

        if (transform is not null)
        {
            normalized = transform(normalized);
        }

        return MaxLength(normalized, maxLength, tooLongCode);
    }

    public DomainGuard MaxLength(string value, int maxLength, string code)
    {
        if (value.Length > maxLength)
        {
            return Fail(Error.Validation(code, maxLength));
        }

        return this;
    }

    public DomainGuard RequiredGuid(Guid value, string code)
    {
        if (value == Guid.Empty)
        {
            return Fail(Error.Validation(code));
        }

        return this;
    }

    public DomainGuard Positive(int value, string code)
    {
        if (value < 1)
        {
            return Fail(Error.Validation(code));
        }

        return this;
    }

    public DomainGuard UtcTimestamp(DateTime value, string code)
    {
        if (value.Kind == DateTimeKind.Local)
        {
            return Fail(Error.Validation(code));
        }

        return this;
    }

    public DomainGuard Ensure(bool condition, Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return condition ? this : Fail(error);
    }

    public Result ToResult() => _error is null ? Result.Success() : Result.Failure(_error);

    public Result<T> ToResult<T>(Func<T> create) =>
        _error is null ? Result<T>.Success(create()) : Result<T>.Failure(_error);

    public Result Apply(Action onSuccess)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        if (_error is not null)
        {
            return Result.Failure(_error);
        }

        onSuccess();
        return Result.Success();
    }

    private DomainGuard Fail(Error error)
    {
        _error ??= error;
        return this;
    }
}
