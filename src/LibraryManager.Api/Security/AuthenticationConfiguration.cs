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

        var authority = configuration["Authentication:Authority"];
        var audience = configuration["Authentication:Audience"];
        if (string.IsNullOrWhiteSpace(authority) || string.IsNullOrWhiteSpace(audience))
        {
            throw new InvalidOperationException(
                "Authentication:Authority and Authentication:Audience must be configured.");
        }

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = authority;
                options.Audience = audience;
                options.RequireHttpsMetadata = !environment.IsDevelopment();
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidAudience = audience,
                    NameClaimType = "sub",
                    RoleClaimType = "roles"
                };
            });

        return services;
    }
}
