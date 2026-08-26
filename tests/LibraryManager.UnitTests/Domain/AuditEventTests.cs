using LibraryManager.Domain;

namespace LibraryManager.UnitTests.Domain;

public sealed class AuditEventTests
{
    [Fact]
    public void Create_stores_utc_actor_and_correlation()
    {
        var occurredAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);

        var audit = AuditEvent.Create(
            "Loan",
            Guid.NewGuid(),
            "LoanCreated",
            "subject-from-token",
            occurredAt,
            "correlation-1",
            """{"bookId":"00000000-0000-0000-0000-000000000001"}""");

        Assert.Equal("Loan", audit.EntityType);
        Assert.Equal("LoanCreated", audit.Action);
        Assert.Equal("subject-from-token", audit.ActorId);
        Assert.Equal("correlation-1", audit.CorrelationId);
        Assert.Equal(occurredAt, audit.OccurredAtUtc);
        Assert.NotEqual(Guid.Empty, audit.Id);
    }

    [Fact]
    public void Create_rejects_missing_actor()
    {
        var exception = Assert.Throws<DomainException>(() =>
            AuditEvent.Create(
                "Loan",
                Guid.NewGuid(),
                "LoanCreated",
                " ",
                DateTime.UtcNow,
                "correlation-1",
                "{}"));

        Assert.Contains("ActorId", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_rejects_empty_entity_id()
    {
        var exception = Assert.Throws<DomainException>(() =>
            AuditEvent.Create(
                "Loan",
                Guid.Empty,
                "LoanCreated",
                "subject",
                DateTime.UtcNow,
                "correlation-1",
                "{}"));

        Assert.Contains("EntityId", exception.Message, StringComparison.Ordinal);
    }
}
