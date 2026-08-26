using LibraryManager.Application.Abstractions;

namespace LibraryManager.Api.Security;

public static class SecurityProbeEndpoints
{
    public const string LibrarianProbeRoute = "/security/librarian-probe";
    public const string CurrentUserRoute = "/security/me";

    public static void MapSecurityProbes(this WebApplication app)
    {
        if (!app.Configuration.GetValue("Testing:UseTestAuth", false))
        {
            return;
        }

        app.MapGet(CurrentUserRoute, (ICurrentUserContext currentUser) =>
                Results.Ok(new CurrentUserResponse(currentUser.ActorId)))
            .RequireAuthorization()
            .ExcludeFromDescription();

        app.MapPost(LibrarianProbeRoute, () => Results.NoContent())
            .RequireAuthorization(LibrarianPolicy.Name)
            .ExcludeFromDescription();
    }

    private sealed record CurrentUserResponse(string ActorId);
}
