namespace LibraryManager.Application.Abstractions;

public sealed record IdempotencyLookup(
    bool IsOwner,
    string RequestHash,
    int? ResponseStatus,
    string? ResponseBody);

public interface IIdempotencyStore
{
    Task<IdempotencyLookup> TryReserveAsync(
        string endpoint,
        string key,
        string requestHash,
        CancellationToken cancellationToken);

    Task CompleteAsync(
        string endpoint,
        string key,
        int responseStatus,
        string responseBody,
        CancellationToken cancellationToken);
}
