using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace LibraryManager.IntegrationTests.Infrastructure;

public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _postgresConnectionString;
    private readonly string _redisConnectionString;

    public CustomWebApplicationFactory(DatabaseFixture fixture)
        : this(fixture.PostgresConnectionString, fixture.RedisConnectionString)
    {
    }

    public CustomWebApplicationFactory(string postgresConnectionString, string redisConnectionString)
    {
        _postgresConnectionString = postgresConnectionString;
        _redisConnectionString = redisConnectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Postgres", _postgresConnectionString);
        builder.UseSetting("ConnectionStrings:Redis", _redisConnectionString);
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = _postgresConnectionString,
                ["ConnectionStrings:Redis"] = _redisConnectionString
            });
        });

        TestHostConfiguration.ApplyTestAuthentication(builder);
    }
}
