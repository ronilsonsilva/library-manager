using LibraryManager.Api.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace LibraryManager.IntegrationTests.Security;

public sealed class TestAuthProductionGuardTests
{
    [Fact]
    public void Production_rejects_testing_use_test_auth()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Testing:UseTestAuth"] = "true",
                ["Authentication:Authority"] = "http://localhost:8081/realms/library-manager",
                ["Authentication:Audience"] = "library-manager-api"
            })
            .Build();
        var environment = new StubHostEnvironment { EnvironmentName = Environments.Production };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddLibraryManagerAuthentication(configuration, environment));

        Assert.Contains("Testing:UseTestAuth", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Production", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Testing_environment_allows_test_auth()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Testing:UseTestAuth"] = "true"
            })
            .Build();
        var environment = new StubHostEnvironment { EnvironmentName = "Testing" };

        var result = services.AddLibraryManagerAuthentication(configuration, environment);

        Assert.Same(services, result);
    }

    private sealed class StubHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;

        public string ApplicationName { get; set; } = "LibraryManager.Api";

        public string ContentRootPath { get; set; } = ".";

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
