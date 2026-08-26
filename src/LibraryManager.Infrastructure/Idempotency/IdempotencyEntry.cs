namespace LibraryManager.Infrastructure.Idempotency;

public sealed class IdempotencyEntry
{
    public Guid Id { get; set; }

    public string Endpoint { get; set; } = string.Empty;

    public string Key { get; set; } = string.Empty;

    public string RequestHash { get; set; } = string.Empty;

    public int? ResponseStatus { get; set; }

    public string? ResponseBody { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? CompletedAtUtc { get; set; }
}
