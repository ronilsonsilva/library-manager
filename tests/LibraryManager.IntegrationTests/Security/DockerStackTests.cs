using System.Text.RegularExpressions;

namespace LibraryManager.IntegrationTests.Security;

public sealed class DockerStackTests
{
    [Fact]
    public void Dockerfile_uses_dotnet_10_sdk_and_aspnet_images()
    {
        var dockerfile = File.ReadAllText(RepoPath("Dockerfile"));

        Assert.Contains("mcr.microsoft.com/dotnet/sdk:10.0", dockerfile, StringComparison.Ordinal);
        Assert.Contains("mcr.microsoft.com/dotnet/aspnet:10.0", dockerfile, StringComparison.Ordinal);
        Assert.Contains("NuGet.config", dockerfile, StringComparison.Ordinal);
    }

    [Fact]
    public void Compose_starts_required_english_services_and_imports_keycloak_realm()
    {
        var compose = File.ReadAllText(RepoPath("docker-compose.yml"));

        Assert.Contains("library-manager-api:", compose, StringComparison.Ordinal);
        Assert.Contains("postgres:", compose, StringComparison.Ordinal);
        Assert.Contains("redis:", compose, StringComparison.Ordinal);
        Assert.Contains("keycloak:", compose, StringComparison.Ordinal);
        Assert.Contains("library-manager-keycloak:26.7.2", compose, StringComparison.Ordinal);
        Assert.Contains("start-dev", compose, StringComparison.Ordinal);
        Assert.Contains("--import-realm", compose, StringComparison.Ordinal);
        Assert.Contains("infrastructure/keycloak", compose, StringComparison.Ordinal);
        Assert.Contains("KC_HOSTNAME_BACKCHANNEL_DYNAMIC", compose, StringComparison.Ordinal);

        var keycloakDockerfile = File.ReadAllText(RepoPath("infrastructure", "keycloak", "Dockerfile"));
        Assert.Contains("quay.io/keycloak/keycloak:26.7.2", keycloakDockerfile, StringComparison.Ordinal);
        Assert.Contains("/opt/keycloak/data/import/library-manager-realm.json", keycloakDockerfile, StringComparison.Ordinal);
        Assert.Contains("http://localhost:8081", compose, StringComparison.Ordinal);
        Assert.Contains("http://localhost:8080/swagger/oauth2-redirect.html", File.ReadAllText(
            RepoPath("infrastructure", "keycloak", "library-manager-realm.json")));
    }

    [Fact]
    public void Swagger_ui_uses_authorization_code_with_pkce()
    {
        var swagger = File.ReadAllText(RepoPath(
            "src", "LibraryManager.Api", "OpenApi", "SwaggerConfiguration.cs"));

        Assert.Contains("OAuthUsePkce()", swagger, StringComparison.Ordinal);
        Assert.Contains("AuthorizationCode", swagger, StringComparison.Ordinal);
        Assert.Contains("library-manager-swagger", swagger, StringComparison.Ordinal);
        Assert.Contains("http://localhost:8080/swagger/oauth2-redirect.html", swagger, StringComparison.Ordinal);
        Assert.DoesNotMatch(new Regex("OAuthClientSecret", RegexOptions.CultureInvariant), swagger);
    }

    private static string RepoPath(params string[] segments)
    {
        var relative = Path.Combine(segments);
        var path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            relative));

        Assert.True(File.Exists(path), $"Expected repository file at {path}.");
        return path;
    }
}
