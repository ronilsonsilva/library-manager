using LibraryManager.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LibraryManager.Infrastructure.Persistence.Configurations;

public sealed class AuditEventConfiguration : IEntityTypeConfiguration<AuditEvent>
{
    public void Configure(EntityTypeBuilder<AuditEvent> builder)
    {
        builder.ToTable("audit_events");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.EntityType).HasColumnName("entity_type").HasMaxLength(64).IsRequired();
        builder.Property(e => e.EntityId).HasColumnName("entity_id").IsRequired();
        builder.Property(e => e.Action).HasColumnName("action").HasMaxLength(64).IsRequired();
        builder.Property(e => e.ActorId).HasColumnName("actor_id").HasMaxLength(256).IsRequired();
        builder.Property(e => e.OccurredAtUtc).HasColumnName("occurred_at_utc").IsRequired();
        builder.Property(e => e.CorrelationId).HasColumnName("correlation_id").HasMaxLength(128).IsRequired();
        builder.Property(e => e.DataJson).HasColumnName("data_json").HasColumnType("jsonb").IsRequired();

        builder.HasIndex(e => e.OccurredAtUtc).HasDatabaseName("ix_audit_events_occurred_at_utc");
        builder.HasIndex(e => new { e.EntityType, e.EntityId }).HasDatabaseName("ix_audit_events_entity");
    }
}
