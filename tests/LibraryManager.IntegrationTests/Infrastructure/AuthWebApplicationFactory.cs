using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace LibraryManager.IntegrationTests.Infrastructure;

public sealed class AuthWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Postgres", "Host=127.0.0.1;Port=1;Database=unused;Username=x;Password=x");
        builder.UseSetting("ConnectionStrings:Redis", "localhost:1");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = "Host=127.0.0.1;Port=1;Database=unused;Username=x;Password=x",
                ["ConnectionStrings:Redis"] = "localhost:1"
            });
        });

        TestHostConfiguration.ApplyTestAuthentication(builder);
    }
}
