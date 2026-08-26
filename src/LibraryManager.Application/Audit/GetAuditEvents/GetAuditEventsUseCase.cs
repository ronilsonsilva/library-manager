using LibraryManager.Application.Abstractions;
using LibraryManager.Application.Common;

namespace LibraryManager.Application.Audit.GetAuditEvents;

public sealed class GetAuditEventsUseCase(IAuditRepository audits)
{
    public async Task<PagedResult<AuditEventDto>> ExecuteAsync(
        int page,
        int pageSize,
        string? entityType,
        Guid? entityId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var (normalizedPage, normalizedPageSize) = Pagination.Normalize(page, pageSize);
        var (items, totalCount) = await audits.ListAsync(
            normalizedPage,
            normalizedPageSize,
            entityType,
            entityId,
            cancellationToken);

        return new PagedResult<AuditEventDto>(
            items.Select(AuditEventDto.From).ToArray(),
            normalizedPage,
            normalizedPageSize,
            totalCount);
    }
}
