using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using LibraryManager.Api.Middleware;
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

        if (environment.IsProduction() && configuration.GetValue("Testing:UseTestAuth", false))
        {
            throw new InvalidOperationException(
                "Testing:UseTestAuth cannot be enabled in the Production environment.");
        }

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
                options.Events = new JwtBearerEvents
                {
                    OnChallenge = context => WriteProblemAsync(
                        context.HttpContext,
                        StatusCodes.Status401Unauthorized,
                        "Unauthorized",
                        "Missing or invalid access token.",
                        () => context.HandleResponse()),
                    OnForbidden = context => WriteProblemAsync(
                        context.HttpContext,
                        StatusCodes.Status403Forbidden,
                        "Forbidden",
                        "Authenticated caller lacks the required role.")
                };
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

    private static async Task WriteProblemAsync(
        HttpContext httpContext,
        int statusCode,
        string title,
        string detail,
        Action? beforeWrite = null)
    {
        beforeWrite?.Invoke();
        if (httpContext.Response.HasStarted)
        {
            return;
        }

        var correlationId = httpContext.Response.Headers[CorrelationIdMiddleware.HeaderName].FirstOrDefault()
            ?? httpContext.Request.Headers[CorrelationIdMiddleware.HeaderName].FirstOrDefault();
        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path
        };
        problem.Extensions["correlationId"] = correlationId;

        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/problem+json";
        await httpContext.Response.WriteAsJsonAsync(problem);
    }
}
