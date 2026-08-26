using System.Text.Json;
using LibraryManager.Application.Audit;

namespace LibraryManager.Api.Contracts.Audit.Responses;

public sealed record AuditEventResponse(
    Guid Id,
    string EntityType,
    Guid EntityId,
    string Action,
    string ActorId,
    DateTime OccurredAtUtc,
    string CorrelationId,
    JsonElement DataJson)
{
    public static AuditEventResponse From(AuditEventDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return new(
            dto.Id,
            dto.EntityType,
            dto.EntityId,
            dto.Action,
            dto.ActorId,
            dto.OccurredAtUtc,
            dto.CorrelationId,
            dto.DataJson);
    }
}
