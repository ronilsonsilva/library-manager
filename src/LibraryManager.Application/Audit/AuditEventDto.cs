using System.Text.Json;
using LibraryManager.Domain;

namespace LibraryManager.Application.Audit;

public sealed record AuditEventDto(
    Guid Id,
    string EntityType,
    Guid EntityId,
    string Action,
    string ActorId,
    DateTime OccurredAtUtc,
    string CorrelationId,
    JsonElement DataJson)
{
    public static AuditEventDto From(AuditEvent auditEvent)
    {
        using var document = JsonDocument.Parse(auditEvent.DataJson);
        return new(
            auditEvent.Id,
            auditEvent.EntityType,
            auditEvent.EntityId,
            auditEvent.Action,
            auditEvent.ActorId,
            auditEvent.OccurredAtUtc,
            auditEvent.CorrelationId,
            document.RootElement.Clone());
    }
}
