using LibraryManager.Application.Abstractions;
using LibraryManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LibraryManager.Infrastructure.Idempotency;

public sealed class IdempotencyStore(LibraryDbContext db, IClock clock) : IIdempotencyStore
{
    public async Task<IdempotencyLookup> TryReserveAsync(
        string endpoint,
        string key,
        string requestHash,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var id = Guid.NewGuid();
        var createdAtUtc = clock.UtcNow;
        var inserted = await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
             INSERT INTO idempotency_entries (id, endpoint, key, request_hash, created_at_utc)
             VALUES ({id}, {endpoint}, {key}, {requestHash}, {createdAtUtc})
             ON CONFLICT (endpoint, key) DO NOTHING
             """,
            cancellationToken);

        var entry = await db.IdempotencyEntries
            .AsNoTracking()
            .SingleAsync(item => item.Endpoint == endpoint && item.Key == key, cancellationToken);

        return new IdempotencyLookup(
            inserted == 1,
            entry.RequestHash,
            entry.ResponseStatus,
            entry.ResponseBody);
    }

    public async Task CompleteAsync(
        string endpoint,
        string key,
        int responseStatus,
        string responseBody,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var entry = await db.IdempotencyEntries
            .FirstOrDefaultAsync(item => item.Endpoint == endpoint && item.Key == key, cancellationToken);
        if (entry is null)
        {
            return;
        }

        entry.ResponseStatus = responseStatus;
        entry.ResponseBody = responseBody;
        entry.CompletedAtUtc = clock.UtcNow;
    }
}
