namespace LibraryManager.Infrastructure.Outbox;

public sealed class OutboxMessage
{
    public Guid Id { get; set; }

    public string Type { get; set; } = string.Empty;

    public string PayloadJson { get; set; } = string.Empty;

    public DateTime OccurredAtUtc { get; set; }

    public DateTime? ProcessedAtUtc { get; set; }

    public int AttemptCount { get; set; }

    public DateTime NextAttemptAtUtc { get; set; }

    public DateTime? LockedUntilUtc { get; set; }

    public string? LockedBy { get; set; }

    public string? LastError { get; set; }
}
