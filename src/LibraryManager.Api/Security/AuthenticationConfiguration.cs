using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using LibraryManager.Application.Abstractions;

namespace LibraryManager.Api.Security;

public static class AuthenticationConfiguration
{
    public static IServiceCollection AddLibraryManagerAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserContext, CurrentUserContext>();

        services.AddAuthorization(options =>
        {
            options.AddPolicy(LibrarianPolicy.Name, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireRole(LibrarianPolicy.Role);
            });
        });

        if (configuration.GetValue("Testing:UseTestAuth", false))
        {
            services.AddAuthentication(TestAuthDefaults.SchemeName);
            return services;
        }

        var authority = configuration["Authentication:Authority"]?.TrimEnd('/');
        var audience = configuration["Authentication:Audience"];
        var metadataAddress = configuration["Authentication:MetadataAddress"]?.Trim();
        if (string.IsNullOrWhiteSpace(authority) || string.IsNullOrWhiteSpace(audience))
        {
            throw new InvalidOperationException(
                "Authentication:Authority and Authentication:Audience must be configured.");
        }

        var validIssuers = new HashSet<string>(StringComparer.Ordinal) { authority };
        var extraIssuers = configuration.GetSection("Authentication:ValidIssuers").Get<string[]>();
        if (extraIssuers is not null)
        {
            foreach (var issuer in extraIssuers)
            {
                if (!string.IsNullOrWhiteSpace(issuer))
                {
                    validIssuers.Add(issuer.Trim().TrimEnd('/'));
                }
            }
        }

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = authority;
                options.Audience = audience;
                if (!string.IsNullOrWhiteSpace(metadataAddress))
                {
                    options.MetadataAddress = metadataAddress;
                }

                var metadataUrl = string.IsNullOrWhiteSpace(metadataAddress) ? authority : metadataAddress;
                options.RequireHttpsMetadata =
                    !environment.IsDevelopment()
                    && !metadataUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase);
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuers = validIssuers.ToArray(),
                    ValidAudience = audience,
                    NameClaimType = "sub",
                    RoleClaimType = "roles"
                };
            });

        return services;
    }
}
