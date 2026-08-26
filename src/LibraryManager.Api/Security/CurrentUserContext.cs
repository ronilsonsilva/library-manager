using System.Security.Claims;
using LibraryManager.Application.Abstractions;

namespace LibraryManager.Api.Security;

public sealed class CurrentUserContext(IHttpContextAccessor httpContextAccessor) : ICurrentUserContext
{
    public string ActorId
    {
        get
        {
            var user = httpContextAccessor.HttpContext?.User
                ?? throw new InvalidOperationException("No HTTP context is available.");

            return user.FindFirstValue("sub")
                ?? throw new InvalidOperationException("Authenticated subject claim 'sub' is missing.");
        }
    }
}
