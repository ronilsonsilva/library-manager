using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LibraryManager.Infrastructure.Outbox;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.Type).HasColumnName("type").HasMaxLength(128).IsRequired();
        builder.Property(e => e.PayloadJson).HasColumnName("payload_json").HasColumnType("jsonb").IsRequired();
        builder.Property(e => e.OccurredAtUtc).HasColumnName("occurred_at_utc").IsRequired();
        builder.Property(e => e.ProcessedAtUtc).HasColumnName("processed_at_utc");
        builder.Property(e => e.AttemptCount).HasColumnName("attempt_count").IsRequired();
        builder.Property(e => e.NextAttemptAtUtc).HasColumnName("next_attempt_at_utc").IsRequired();
        builder.Property(e => e.LockedUntilUtc).HasColumnName("locked_until_utc");
        builder.Property(e => e.LockedBy).HasColumnName("locked_by").HasMaxLength(128);
        builder.Property(e => e.LastError).HasColumnName("last_error");

        builder.HasIndex(e => new { e.ProcessedAtUtc, e.NextAttemptAtUtc, e.LockedUntilUtc })
            .HasDatabaseName("ix_outbox_messages_claim");
    }
}
