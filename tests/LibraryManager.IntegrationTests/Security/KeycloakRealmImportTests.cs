using System.Text.Json;

namespace LibraryManager.IntegrationTests.Security;

public sealed class KeycloakRealmImportTests
{
    [Fact]
    public void Realm_import_defines_local_oidc_foundation()
    {
        var path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "infrastructure", "keycloak", "library-manager-realm.json"));

        Assert.True(File.Exists(path), $"Realm import was not found at {path}.");

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;

        Assert.Equal("library-manager", root.GetProperty("realm").GetString());

        var realmRoles = root.GetProperty("roles").GetProperty("realm").EnumerateArray()
            .Select(role => role.GetProperty("name").GetString())
            .ToArray();
        Assert.Contains("librarian", realmRoles);

        var clients = root.GetProperty("clients").EnumerateArray().ToArray();
        var apiClient = Assert.Single(clients, client => client.GetProperty("clientId").GetString() == "library-manager-api");
        Assert.False(apiClient.GetProperty("standardFlowEnabled").GetBoolean());
        Assert.False(apiClient.GetProperty("directAccessGrantsEnabled").GetBoolean());

        var swaggerClient = Assert.Single(clients, client => client.GetProperty("clientId").GetString() == "library-manager-swagger");
        Assert.True(swaggerClient.GetProperty("publicClient").GetBoolean());
        Assert.True(swaggerClient.GetProperty("standardFlowEnabled").GetBoolean());
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
}
