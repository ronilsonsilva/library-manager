namespace LibraryManager.IntegrationTests.Architecture;

public sealed class NuGetAuditPropsTests
{
    [Fact]
    public void Directory_build_props_enables_transitive_nuget_audit_and_fails_high_critical()
    {
        var props = File.ReadAllText(RepoPath("Directory.Build.props"));

        Assert.Contains("<NuGetAudit>true</NuGetAudit>", props, StringComparison.Ordinal);
        Assert.Contains("<NuGetAuditMode>all</NuGetAuditMode>", props, StringComparison.Ordinal);
        Assert.DoesNotContain("<NuGetAudit>false</NuGetAudit>", props, StringComparison.Ordinal);
        Assert.DoesNotContain("NU1903", props, StringComparison.Ordinal);
        Assert.DoesNotContain("NU1904", props, StringComparison.Ordinal);
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
