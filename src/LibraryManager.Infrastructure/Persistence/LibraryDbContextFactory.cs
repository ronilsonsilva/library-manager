using LibraryManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace LibraryManager.Infrastructure.Persistence;

public sealed class LibraryDbContextFactory : IDesignTimeDbContextFactory<LibraryDbContext>
{
    public LibraryDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("LIBRARY_MANAGER_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=library_manager;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<LibraryDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new LibraryDbContext(options);
    }
}
