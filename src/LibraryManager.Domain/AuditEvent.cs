using LibraryManager.Domain.Validation;

namespace LibraryManager.Domain;

public sealed class AuditEvent
{
    private AuditEvent()
    {
        EntityType = string.Empty;
        Action = string.Empty;
        ActorId = string.Empty;
        CorrelationId = string.Empty;
        DataJson = "{}";
    }

    public Guid Id { get; private set; }

    public string EntityType { get; private set; }

    public Guid EntityId { get; private set; }

    public string Action { get; private set; }

    public string ActorId { get; private set; }

    public DateTime OccurredAtUtc { get; private set; }

    public string CorrelationId { get; private set; }

    public string DataJson { get; private set; }

    public static Result<AuditEvent> Create(
        string entityType,
        Guid entityId,
        string action,
        string actorId,
        DateTime occurredAtUtc,
        string correlationId,
        string dataJson)
    {
        var guard = new DomainGuard();
        guard.Required(entityType, ErrorCodes.AuditEntityTypeRequired, out var normalizedEntityType);
        guard.RequiredGuid(entityId, ErrorCodes.AuditEntityIdRequired);
        guard.Required(action, ErrorCodes.AuditActionRequired, out var normalizedAction);
        guard.Required(actorId, ErrorCodes.AuditActorIdRequired, out var normalizedActorId);
        guard.Required(correlationId, ErrorCodes.AuditCorrelationIdRequired, out var normalizedCorrelationId);
        guard.Required(dataJson, ErrorCodes.AuditDataJsonRequired, out var normalizedDataJson);

        return guard.ToResult(() => new AuditEvent
        {
            Id = Guid.NewGuid(),
            EntityType = normalizedEntityType,
            EntityId = entityId,
            Action = normalizedAction,
            ActorId = normalizedActorId,
            OccurredAtUtc = occurredAtUtc,
            CorrelationId = normalizedCorrelationId,
            DataJson = normalizedDataJson
        });
    }
}
