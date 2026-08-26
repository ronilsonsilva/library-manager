namespace LibraryManager.IntegrationTests.Infrastructure;

public sealed class KubernetesManifestTests
{
    [Fact]
    public void Manifests_define_api_workload_without_keycloak()
    {
        var directory = RepoDirectory("deploy", "kubernetes");
        var files = Directory.GetFiles(directory, "*.yaml").Select(File.ReadAllText).ToArray();
        Assert.True(files.Length >= 4, "Expected Deployment, Service, ConfigMap, and Secret manifests.");

        var combined = string.Join('\n', files);
        Assert.DoesNotContain("keycloak", combined, StringComparison.OrdinalIgnoreCase);

        var deployment = File.ReadAllText(Path.Combine(directory, "deployment.yaml"));
        Assert.Contains("kind: Deployment", deployment, StringComparison.Ordinal);
        Assert.Contains("replicas: 2", deployment, StringComparison.Ordinal);
        Assert.Contains("cpu: 100m", deployment, StringComparison.Ordinal);
        Assert.Contains("cpu: 500m", deployment, StringComparison.Ordinal);
        Assert.Contains("memory: 256Mi", deployment, StringComparison.Ordinal);
        Assert.Contains("memory: 512Mi", deployment, StringComparison.Ordinal);
        Assert.Contains("path: /health/live", deployment, StringComparison.Ordinal);
        Assert.Contains("path: /health/ready", deployment, StringComparison.Ordinal);
        Assert.Contains("configMapRef:", deployment, StringComparison.Ordinal);
        Assert.Contains("secretRef:", deployment, StringComparison.Ordinal);
        Assert.Contains("Testing__UseTestAuth", deployment, StringComparison.Ordinal);

        var service = File.ReadAllText(Path.Combine(directory, "service.yaml"));
        Assert.Contains("kind: Service", service, StringComparison.Ordinal);
        Assert.Contains("library-manager-api", service, StringComparison.Ordinal);

        var configMap = File.ReadAllText(Path.Combine(directory, "configmap.yaml"));
        Assert.Contains("kind: ConfigMap", configMap, StringComparison.Ordinal);
        Assert.Contains("Authentication__Authority", configMap, StringComparison.Ordinal);
        Assert.Contains("Authentication__Audience", configMap, StringComparison.Ordinal);
        Assert.Contains("library-manager-api", configMap, StringComparison.Ordinal);

        var secret = File.ReadAllText(Path.Combine(directory, "secret.yaml"));
        Assert.Contains("kind: Secret", secret, StringComparison.Ordinal);
        Assert.Contains("ConnectionStrings__Postgres", secret, StringComparison.Ordinal);
        Assert.Contains("ConnectionStrings__Redis", secret, StringComparison.Ordinal);
        Assert.Contains("REPLACE_WITH_", secret, StringComparison.Ordinal);
    }

    private static string RepoDirectory(params string[] segments)
    {
        var path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            Path.Combine(segments)));

        Assert.True(Directory.Exists(path), $"Expected directory at {path}.");
        return path;
    }
}
