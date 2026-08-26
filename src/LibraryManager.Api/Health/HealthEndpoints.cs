using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace LibraryManager.Api.Health;

public static class HealthEndpoints
{
    public static IServiceCollection AddLibraryManagerHealthChecks(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var postgres = configuration.GetConnectionString("Postgres")
            ?? "Host=localhost;Port=5432;Database=library_manager;Username=postgres;Password=postgres";
        var redis = configuration.GetConnectionString("Redis") ?? "localhost:6379";
        var timeout = TimeSpan.FromSeconds(2);

        services.AddHealthChecks()
            .AddNpgSql(postgres, name: "postgres", timeout: timeout, tags: ["ready"])
            .AddRedis(redis, name: "redis", timeout: timeout, tags: ["ready"]);

        return services;
    }

    public static void MapHealthEndpoints(this WebApplication app)
    {
        app.MapHealthChecks("/health/live", new HealthCheckOptions
            {
                Predicate = _ => false
            })
            .AllowAnonymous()
            .WithTags("Health");

        app.MapHealthChecks("/health/ready", new HealthCheckOptions
            {
                Predicate = check => check.Tags.Contains("ready")
            })
            .AllowAnonymous()
            .WithTags("Health");
    }
}
