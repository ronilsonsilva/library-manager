using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LibraryManager.Infrastructure.Idempotency;

public sealed class IdempotencyEntryConfiguration : IEntityTypeConfiguration<IdempotencyEntry>
{
    public void Configure(EntityTypeBuilder<IdempotencyEntry> builder)
    {
        builder.ToTable("idempotency_entries");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.Endpoint).HasColumnName("endpoint").HasMaxLength(64).IsRequired();
        builder.Property(e => e.Key).HasColumnName("key").HasMaxLength(128).IsRequired();
        builder.Property(e => e.RequestHash).HasColumnName("request_hash").HasMaxLength(64).IsRequired();
        builder.Property(e => e.ResponseStatus).HasColumnName("response_status");
        builder.Property(e => e.ResponseBody).HasColumnName("response_body");
        builder.Property(e => e.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(e => e.CompletedAtUtc).HasColumnName("completed_at_utc");

        builder.HasIndex(e => new { e.Endpoint, e.Key })
            .IsUnique()
            .HasDatabaseName("ux_idempotency_entries_endpoint_key");
    }
}
