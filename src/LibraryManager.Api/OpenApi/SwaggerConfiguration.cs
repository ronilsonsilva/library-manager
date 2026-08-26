using Microsoft.OpenApi.Models;

namespace LibraryManager.Api.OpenApi;

public static class SwaggerConfiguration
{
    public const string OAuthClientId = "library-manager-swagger";
    public const string OAuth2SchemeId = "oauth2";

    public static IServiceCollection AddLibraryManagerSwagger(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var authority = configuration["Authentication:Authority"]?.TrimEnd('/');

        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Library Manager API",
                Version = "v1"
            });

            if (string.IsNullOrWhiteSpace(authority))
            {
                return;
            }

            options.AddSecurityDefinition(OAuth2SchemeId, new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.OAuth2,
                Description = "Keycloak Authorization Code with PKCE. Client library-manager-swagger.",
                Flows = new OpenApiOAuthFlows
                {
                    AuthorizationCode = new OpenApiOAuthFlow
                    {
                        AuthorizationUrl = new Uri($"{authority}/protocol/openid-connect/auth"),
                        TokenUrl = new Uri($"{authority}/protocol/openid-connect/token"),
                        Scopes = new Dictionary<string, string>
                        {
                            ["openid"] = "OpenID Connect",
                            ["profile"] = "Profile"
                        }
                    }
                }
            });
        });

        return services;
    }

    public static WebApplication UseLibraryManagerSwagger(this WebApplication app)
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "Library Manager API");
            options.OAuthClientId(OAuthClientId);
            options.OAuthUsePkce();
            options.OAuthScopes("openid", "profile");
            options.OAuthAppName("Library Manager");
        });

        return app;
    }
}
