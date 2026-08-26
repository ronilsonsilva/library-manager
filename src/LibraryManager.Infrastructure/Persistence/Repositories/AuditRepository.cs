using LibraryManager.Application.Abstractions;
using LibraryManager.Domain;
using Microsoft.EntityFrameworkCore;

namespace LibraryManager.Infrastructure.Persistence.Repositories;

public sealed class AuditRepository(LibraryDbContext db) : IAuditRepository
{
    public async Task AddAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
    {
        await db.AuditEvents.AddAsync(auditEvent, cancellationToken);
    }

    public async Task<(IReadOnlyList<AuditEvent> Items, int TotalCount)> ListAsync(
        int page,
        int pageSize,
        string? entityType,
        Guid? entityId,
        CancellationToken cancellationToken)
    {
        var query = db.AuditEvents.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(entityType))
        {
            query = query.Where(audit => audit.EntityType == entityType);
        }

        if (entityId is not null)
        {
            query = query.Where(audit => audit.EntityId == entityId.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(audit => audit.OccurredAtUtc)
            .ThenBy(audit => audit.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }
}
