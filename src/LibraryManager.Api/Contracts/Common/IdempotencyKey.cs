namespace LibraryManager.Api.Contracts.Common;

public sealed class IdempotencyKey
{
    public const string HeaderName = "Idempotency-Key";

    public const int MaxLength = 128;

    public IdempotencyKey(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var normalized = value.Trim();
        if (normalized.Length > MaxLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                $"Idempotency-Key must be at most {MaxLength} characters.");
        }

        Value = normalized;
    }

    public string Value { get; }
}
