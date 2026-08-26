using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LibraryManager.Api.Security;
using LibraryManager.IntegrationTests.Infrastructure;

namespace LibraryManager.IntegrationTests.Security;

public sealed class AuthorizationTests : IClassFixture<AuthWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly AuthWebApplicationFactory _factory;

    public AuthorizationTests(AuthWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Librarian_mutation_without_token_returns_401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync(SecurityProbeEndpoints.LibrarianProbeRoute, content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Librarian_mutation_with_invalid_token_returns_401()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test", "invalid");

        var response = await client.PostAsync(SecurityProbeEndpoints.LibrarianProbeRoute, content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Librarian_mutation_without_librarian_role_returns_403()
    {
        var client = _factory.CreateClient().WithTestAuth("reader-subject");

        var response = await client.PostAsync(SecurityProbeEndpoints.LibrarianProbeRoute, content: null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Librarian_mutation_with_librarian_role_succeeds()
    {
        var client = _factory.CreateClient().WithTestAuth("librarian-subject", LibrarianPolicy.Role);

        var response = await client.PostAsync(SecurityProbeEndpoints.LibrarianProbeRoute, content: null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Current_user_without_token_returns_401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync(SecurityProbeEndpoints.CurrentUserRoute);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Current_user_actor_id_is_jwt_subject()
    {
        const string subject = "subject-from-token";
        var client = _factory.CreateClient().WithTestAuth(subject);

        var response = await client.GetAsync(SecurityProbeEndpoints.CurrentUserRoute);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CurrentUserResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(subject, body.ActorId);
    }

    [Fact]
    public async Task Health_live_is_anonymous()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("/connect/token")]
    [InlineData("/oauth/token")]
    [InlineData("/token")]
    [InlineData("/auth/token")]
    public async Task Api_does_not_issue_tokens(string path)
    {
        var client = _factory.CreateClient();
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["username"] = "librarian",
            ["password"] = "librarian-dev-only"
        });

        var response = await client.PostAsync(path, content);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private sealed record CurrentUserResponse(string ActorId);
}
