using LibraryManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LibraryManager.Api.Persistence;

internal static class DatabaseStartup
{
    public static async Task ApplyMigrationsIfConfiguredAsync(
        this WebApplication app,
        CancellationToken cancellationToken = default)
    {
        var apply =
            app.Environment.IsDevelopment()
            || app.Configuration.GetValue("Database:ApplyMigrations", false);
        if (!apply)
        {
            return;
        }

        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
        await db.Database.MigrateAsync(cancellationToken);
    }
}
