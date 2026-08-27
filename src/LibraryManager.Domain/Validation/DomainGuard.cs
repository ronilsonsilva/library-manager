namespace LibraryManager.Domain.Validation;

public sealed class DomainGuard
{
    private Error? _error;

    public bool HasError => _error is not null;

    public DomainGuard Required(string? value, string code, out string normalized)
    {
        normalized = string.Empty;
        if (_error is not null)
        {
            return this;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            _error = Error.Validation(code);
            return this;
        }

        normalized = value.Trim();
        return this;
    }

    public DomainGuard MaxLength(string value, int maxLength, string code)
    {
        if (_error is not null)
        {
            return this;
        }

        if (value.Length > maxLength)
        {
            _error = Error.Validation(code, maxLength);
        }

        return this;
    }

    public DomainGuard RequiredGuid(Guid value, string code)
    {
        if (_error is not null)
        {
            return this;
        }

        if (value == Guid.Empty)
        {
            _error = Error.Validation(code);
        }

        return this;
    }

    public DomainGuard Positive(int value, string code)
    {
        if (_error is not null)
        {
            return this;
        }

        if (value < 1)
        {
            _error = Error.Validation(code);
        }

        return this;
    }

    public DomainGuard UtcTimestamp(DateTime value, string code)
    {
        if (_error is not null)
        {
            return this;
        }

        if (value.Kind == DateTimeKind.Local)
        {
            _error = Error.Validation(code);
        }

        return this;
    }

    public Result ToResult() => _error is null ? Result.Success() : Result.Failure(_error);

    public Result<T> ToResult<T>(Func<T> create) =>
        _error is null ? Result<T>.Success(create()) : Result<T>.Failure(_error);
}
