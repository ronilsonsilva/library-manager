using LibraryManager.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LibraryManager.Infrastructure.Persistence.Configurations;

public sealed class BookConfiguration : IEntityTypeConfiguration<Book>
{
    public void Configure(EntityTypeBuilder<Book> builder)
    {
        builder.ToTable("books", table =>
        {
            table.HasCheckConstraint("ck_books_available_copies_nonnegative", "available_copies >= 0");
            table.HasCheckConstraint("ck_books_available_copies_lte_total", "available_copies <= total_copies");
            table.HasCheckConstraint("ck_books_total_copies_positive", "total_copies >= 1");
        });

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.Title).HasColumnName("title").HasMaxLength(Book.TitleMaxLength).IsRequired();
        builder.Property(e => e.Isbn).HasColumnName("isbn").HasMaxLength(Book.IsbnMaxLength).IsRequired();
        builder.Property(e => e.Author).HasColumnName("author").HasMaxLength(Book.AuthorMaxLength).IsRequired();
        builder.Property(e => e.TotalCopies).HasColumnName("total_copies").IsRequired();
        builder.Property(e => e.AvailableCopies).HasColumnName("available_copies").IsRequired();
        builder.Property(e => e.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(e => e.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(e => e.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();
        builder.Ignore(e => e.BorrowedCopies);

        builder.HasIndex(e => e.Isbn).IsUnique().HasDatabaseName("ux_books_isbn");
    }
}
