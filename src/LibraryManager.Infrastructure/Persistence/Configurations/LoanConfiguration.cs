using LibraryManager.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LibraryManager.Infrastructure.Persistence.Configurations;

public sealed class LoanConfiguration : IEntityTypeConfiguration<Loan>
{
    public void Configure(EntityTypeBuilder<Loan> builder)
    {
        builder.ToTable("loans", table =>
        {
            table.HasCheckConstraint("ck_loans_due_after_borrowed", "due_at_utc > borrowed_at_utc");
        });

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.BookId).HasColumnName("book_id").IsRequired();
        builder.Property(e => e.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(e => e.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(e => e.BorrowedAtUtc).HasColumnName("borrowed_at_utc").IsRequired();
        builder.Property(e => e.DueAtUtc).HasColumnName("due_at_utc").IsRequired();
        builder.Property(e => e.ReturnedAtUtc).HasColumnName("returned_at_utc");
        builder.Property(e => e.CancelledAtUtc).HasColumnName("cancelled_at_utc");

        builder.HasOne<Book>()
            .WithMany()
            .HasForeignKey(e => e.BookId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();

        builder.HasIndex(e => new { e.UserId, e.BookId })
            .IsUnique()
            .HasFilter("status = 'Active'")
            .HasDatabaseName("ux_loans_user_book_active");
    }
}
