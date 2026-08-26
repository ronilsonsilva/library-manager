using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LibraryManager.IntegrationTests.Infrastructure;

public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _postgresConnectionString;
    private readonly string _redisConnectionString;
    private readonly Action<IServiceCollection>? _configureTestServices;

    public CustomWebApplicationFactory(DatabaseFixture fixture)
        : this(fixture.PostgresConnectionString, fixture.RedisConnectionString)
    {
    }

    public CustomWebApplicationFactory(
        string postgresConnectionString,
        string redisConnectionString,
        Action<IServiceCollection>? configureTestServices = null)
    {
        _postgresConnectionString = postgresConnectionString;
        _redisConnectionString = redisConnectionString;
        _configureTestServices = configureTestServices;
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

        if (_configureTestServices is not null)
        {
            builder.ConfigureTestServices(_configureTestServices);
        }
    }
}
