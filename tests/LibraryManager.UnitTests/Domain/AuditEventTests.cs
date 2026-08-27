using LibraryManager.Domain;

namespace LibraryManager.UnitTests.Domain;

public sealed class AuditEventTests
{
    [Fact]
    public void Create_stores_utc_actor_and_correlation()
    {
        var occurredAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);

        var result = AuditEvent.Create(
            "Loan",
            Guid.NewGuid(),
            "LoanCreated",
            "subject-from-token",
            occurredAt,
            "correlation-1",
            """{"bookId":"00000000-0000-0000-0000-000000000001"}""");

        Assert.True(result.IsSuccess);
        var audit = result.Value;
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
        var result = AuditEvent.Create(
            "Loan",
            Guid.NewGuid(),
            "LoanCreated",
            " ",
            DateTime.UtcNow,
            "correlation-1",
            "{}");

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.AuditActorIdRequired, result.Error.Code);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }

    [Fact]
    public void Create_rejects_empty_entity_id()
    {
        var result = AuditEvent.Create(
            "Loan",
            Guid.Empty,
            "LoanCreated",
            "subject",
            DateTime.UtcNow,
            "correlation-1",
            "{}");

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.AuditEntityIdRequired, result.Error.Code);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }
}
