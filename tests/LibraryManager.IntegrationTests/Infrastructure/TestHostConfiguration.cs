using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LibraryManager.IntegrationTests.Infrastructure;

internal static class TestHostConfiguration
{
    public static void ApplyTestAuthentication(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Testing:UseTestAuth", "true");
        builder.UseSetting("Outbox:ProcessorEnabled", "false");
        builder.UseSetting("Authentication:Authority", "http://localhost:8081/realms/library-manager");
        builder.UseSetting("Authentication:Audience", "library-manager-api");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Testing:UseTestAuth"] = "true",
                ["Outbox:ProcessorEnabled"] = "false",
                ["Authentication:Authority"] = "http://localhost:8081/realms/library-manager",
                ["Authentication:Audience"] = "library-manager-api"
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services
                .AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    TestAuthHandler.SchemeName,
                    _ => { });

            services.PostConfigure<AuthenticationOptions>(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                options.DefaultForbidScheme = TestAuthHandler.SchemeName;
                options.DefaultScheme = TestAuthHandler.SchemeName;
            });
        });
    }
}
