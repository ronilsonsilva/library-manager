using System.Net;
using LibraryManager.IntegrationTests.Infrastructure;

namespace LibraryManager.IntegrationTests.Health;

[Collection(DatabaseCollection.Name)]
public sealed class HealthEndpointTests : IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;
    private HttpClient _client = null!;

    public HealthEndpointTests(DatabaseFixture database)
    {
        _factory = new CustomWebApplicationFactory(database);
    }

    public Task InitializeAsync()
    {
        _client = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Live_returns_200_without_a_token()
    {
        var response = await _client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Ready_returns_200_when_postgres_and_redis_are_up()
    {
        var response = await _client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Live_stays_200_when_ready_returns_503()
    {
        await using var host = new AuthWebApplicationFactory();
        var client = host.CreateClient();

        var live = await client.GetAsync("/health/live");
        var ready = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);
    }
}
