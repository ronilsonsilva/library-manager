namespace LibraryManager.Api.Health;

public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this WebApplication app)
    {
        app.MapGet("/health/live", () => Results.Ok())
            .AllowAnonymous()
            .WithTags("Health");
    }
}
