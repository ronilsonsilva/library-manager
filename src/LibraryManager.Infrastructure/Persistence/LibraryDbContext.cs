using LibraryManager.Domain;
using LibraryManager.Infrastructure.Idempotency;
using LibraryManager.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;

namespace LibraryManager.Infrastructure.Persistence;

public sealed class LibraryDbContext : DbContext
{
    public LibraryDbContext(DbContextOptions<LibraryDbContext> options)
        : base(options)
    {
    }

    public DbSet<Book> Books => Set<Book>();

    public DbSet<User> Users => Set<User>();

    public DbSet<Loan> Loans => Set<Loan>();

    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    public DbSet<IdempotencyEntry> IdempotencyEntries => Set<IdempotencyEntry>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LibraryDbContext).Assembly);
    }
}
