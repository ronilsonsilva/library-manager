namespace LibraryManager.Application.Common;

public sealed class IdempotencyConflictException : Exception
{
    public IdempotencyConflictException()
        : base("Idempotency-Key was reused with a different request.")
    {
    }
}
