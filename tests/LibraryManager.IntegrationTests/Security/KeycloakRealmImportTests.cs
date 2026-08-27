using System.Text.Json;

namespace LibraryManager.IntegrationTests.Security;

public sealed class KeycloakRealmImportTests
{
    private static readonly string PasswordGrantForm = "grant_type" + "=" + "password";
    private static readonly string KeycloakRealmTokenPath =
        "realms/library-manager/protocol/" + "openid-connect/token";

    [Fact]
    public void Realm_import_defines_local_oidc_foundation()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(RealmPath()));
        var root = document.RootElement;

        Assert.Equal("library-manager", root.GetProperty("realm").GetString());

        var realmRoles = root.GetProperty("roles").GetProperty("realm").EnumerateArray()
            .Select(role => role.GetProperty("name").GetString())
            .ToArray();
        Assert.Contains("librarian", realmRoles);

        var clients = root.GetProperty("clients").EnumerateArray().ToArray();
        Assert.All(
            clients,
            client => Assert.False(
                client.GetProperty("directAccessGrantsEnabled").GetBoolean(),
                $"{client.GetProperty("clientId").GetString()} must have Direct Access Grants disabled."));

        var apiClient = Assert.Single(clients, client => client.GetProperty("clientId").GetString() == "library-manager-api");
        Assert.False(apiClient.GetProperty("standardFlowEnabled").GetBoolean());
        Assert.False(apiClient.GetProperty("directAccessGrantsEnabled").GetBoolean());

        var swaggerClient = Assert.Single(clients, client => client.GetProperty("clientId").GetString() == "library-manager-swagger");
        Assert.True(swaggerClient.GetProperty("publicClient").GetBoolean());
        Assert.True(swaggerClient.GetProperty("standardFlowEnabled").GetBoolean());
        Assert.False(swaggerClient.GetProperty("implicitFlowEnabled").GetBoolean());
        Assert.False(swaggerClient.GetProperty("directAccessGrantsEnabled").GetBoolean());
        Assert.Equal("S256", swaggerClient.GetProperty("attributes").GetProperty("pkce.code.challenge.method").GetString());

        var redirectUris = swaggerClient.GetProperty("redirectUris").EnumerateArray()
            .Select(uri => uri.GetString())
            .ToArray();
        Assert.Single(redirectUris);
        Assert.Equal("http://localhost:8080/swagger/oauth2-redirect.html", redirectUris[0]);

        var mappers = swaggerClient.GetProperty("protocolMappers").EnumerateArray().ToArray();
        var rolesMapper = Assert.Single(mappers, mapper => mapper.GetProperty("name").GetString() == "roles");
        Assert.Equal("oidc-usermodel-realm-role-mapper", rolesMapper.GetProperty("protocolMapper").GetString());
        Assert.Equal("roles", rolesMapper.GetProperty("config").GetProperty("claim.name").GetString());

        var audienceMapper = Assert.Single(mappers, mapper => mapper.GetProperty("protocolMapper").GetString() == "oidc-audience-mapper");
        Assert.Equal(
            "library-manager-api",
            audienceMapper.GetProperty("config").GetProperty("included.client.audience").GetString());

        var librarian = Assert.Single(
            root.GetProperty("users").EnumerateArray(),
            user => user.GetProperty("username").GetString() == "librarian");
        Assert.Contains(
            librarian.GetProperty("realmRoles").EnumerateArray().Select(role => role.GetString()),
            role => role == "librarian");
    }

    [Fact]
    public void Docs_and_tests_do_not_use_keycloak_resource_owner_password_credentials()
    {
        AssertNoPasswordGrantForm(RepoPath("README.md"));
        AssertNoPasswordGrantForm(RepoPath("specs", "001-library-manager", "quickstart.md"));

        foreach (var path in Directory.GetFiles(RepoPath("tests"), "*.cs", SearchOption.AllDirectories))
        {
            if (IsBuildOutput(path)
                || path.EndsWith("KeycloakRealmImportTests.cs", StringComparison.Ordinal))
            {
                continue;
            }

            var source = File.ReadAllText(path);
            var postsPasswordGrantToThisApi = path.EndsWith("AuthorizationTests.cs", StringComparison.Ordinal)
                && source.Contains("/connect/token", StringComparison.Ordinal)
                && source.Contains("/oauth/token", StringComparison.Ordinal);

            Assert.False(
                HasPasswordGrantForm(source) && source.Contains(KeycloakRealmTokenPath, StringComparison.Ordinal),
                $"'{path}' must not use Resource Owner Password Credentials against the Keycloak token endpoint.");

            if (!postsPasswordGrantToThisApi)
            {
                Assert.False(
                    HasPasswordGrantForm(source),
                    $"'{path}' must not use Resource Owner Password Credentials.");
            }
        }
    }

    private static void AssertNoPasswordGrantForm(string path)
    {
        Assert.True(File.Exists(path), $"Expected file at {path}.");
        Assert.DoesNotContain(PasswordGrantForm, File.ReadAllText(path));
    }

    private static bool HasPasswordGrantForm(string source) =>
        source.Contains(PasswordGrantForm, StringComparison.Ordinal)
        || source.Contains("[\"grant_type\"] = \"password\"", StringComparison.Ordinal);

    private static bool IsBuildOutput(string path) =>
        path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
        || path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

    private static string RealmPath() => RepoPath("infrastructure", "keycloak", "library-manager-realm.json");

    private static string RepoPath(params string[] segments)
    {
        var path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            Path.Combine(segments)));

        Assert.True(
            File.Exists(path) || Directory.Exists(path),
            $"Expected repository path at {path}.");
        return path;
    }
}
