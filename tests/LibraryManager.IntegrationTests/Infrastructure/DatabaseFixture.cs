using LibraryManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace LibraryManager.IntegrationTests.Infrastructure;

public sealed class DatabaseFixture : IAsyncLifetime
{
    public PostgreSqlContainer Postgres { get; } = new PostgreSqlBuilder("postgres:16-alpine").Build();

    public RedisContainer Redis { get; } = new RedisBuilder("redis:7-alpine").Build();

    public string PostgresConnectionString => Postgres.GetConnectionString();

    public string RedisConnectionString => Redis.GetConnectionString();

    public async Task InitializeAsync()
    {
        await Task.WhenAll(Postgres.StartAsync(), Redis.StartAsync());

        var options = new DbContextOptionsBuilder<LibraryDbContext>()
            .UseNpgsql(PostgresConnectionString)
            .Options;

        await using var db = new LibraryDbContext(options);
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await Postgres.DisposeAsync();
        await Redis.DisposeAsync();
    }
}
