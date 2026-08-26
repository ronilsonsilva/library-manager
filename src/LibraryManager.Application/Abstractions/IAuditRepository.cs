using LibraryManager.Domain;

namespace LibraryManager.Application.Abstractions;

public interface IAuditRepository
{
    Task AddAsync(AuditEvent auditEvent, CancellationToken cancellationToken);

    Task<(IReadOnlyList<AuditEvent> Items, int TotalCount)> ListAsync(
        int page,
        int pageSize,
        string? entityType,
        Guid? entityId,
        CancellationToken cancellationToken);
}
