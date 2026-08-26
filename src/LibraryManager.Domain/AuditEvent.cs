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

    public static AuditEvent Create(
        string entityType,
        Guid entityId,
        string action,
        string actorId,
        DateTime occurredAtUtc,
        string correlationId,
        string dataJson)
    {
        if (string.IsNullOrWhiteSpace(entityType))
        {
            throw new DomainException("EntityType is required.");
        }

        if (entityId == Guid.Empty)
        {
            throw new DomainException("EntityId is required.");
        }

        if (string.IsNullOrWhiteSpace(action))
        {
            throw new DomainException("Action is required.");
        }

        if (string.IsNullOrWhiteSpace(actorId))
        {
            throw new DomainException("ActorId is required.");
        }

        if (string.IsNullOrWhiteSpace(correlationId))
        {
            throw new DomainException("CorrelationId is required.");
        }

        if (string.IsNullOrWhiteSpace(dataJson))
        {
            throw new DomainException("DataJson is required.");
        }

        return new AuditEvent
        {
            Id = Guid.NewGuid(),
            EntityType = entityType.Trim(),
            EntityId = entityId,
            Action = action.Trim(),
            ActorId = actorId,
            OccurredAtUtc = occurredAtUtc,
            CorrelationId = correlationId,
            DataJson = dataJson
        };
    }
}
