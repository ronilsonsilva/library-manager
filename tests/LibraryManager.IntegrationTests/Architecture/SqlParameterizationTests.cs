using System.Text.RegularExpressions;

namespace LibraryManager.IntegrationTests.Architecture;

public sealed class SqlParameterizationTests
{
    private static readonly Regex InterpolatedRawSql = new(
        @"(?:ExecuteSqlRaw|FromSqlRaw|SqlQueryRaw)(?:Async)?\s*\(\s*\$",
        RegexOptions.Compiled);

    private static readonly Regex ConcatenatedRawSql = new(
        @"(?:ExecuteSqlRaw|FromSqlRaw|SqlQueryRaw)(?:Async)?\s*\([^;]*\+",
        RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex InterpolatedCommandText = new(
        @"CommandText\s*=\s*\$",
        RegexOptions.Compiled);

    private static readonly Regex ConcatenatedSqlLiteral = new(
        @"""(?:SELECT|INSERT|UPDATE|DELETE|WITH)\b[^""]*""\s*\+",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    [Fact]
    public void Production_sql_does_not_concatenate_runtime_values_into_raw_commands()
    {
        foreach (var (path, source) in ProductionSources())
        {
            Assert.DoesNotContain("ExecuteSqlRaw", source, StringComparison.Ordinal);
            Assert.DoesNotContain("FromSqlRaw", source, StringComparison.Ordinal);
            Assert.DoesNotContain("SqlQueryRaw", source, StringComparison.Ordinal);
            Assert.False(
                InterpolatedRawSql.IsMatch(source),
                $"Interpolated Raw SQL is forbidden in '{path}'.");
            Assert.False(
                ConcatenatedRawSql.IsMatch(source),
                $"String-concatenated Raw SQL is forbidden in '{path}'.");
            Assert.False(
                InterpolatedCommandText.IsMatch(source),
                $"Interpolated CommandText is forbidden in '{path}'.");
            Assert.False(
                ConcatenatedSqlLiteral.IsMatch(source),
                $"SQL string concatenation is forbidden in '{path}'.");
        }
    }

    [Fact]
    public void Book_availability_reservation_restore_and_total_copy_sql_stay_parameterized()
    {
        var source = ReadInfrastructure("Persistence", "Repositories", "BookRepository.cs");

        Assert.Contains("ExecuteSqlInterpolatedAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ExecuteSqlRaw", source, StringComparison.Ordinal);
        Assert.Contains("AND is_active = TRUE", source, StringComparison.Ordinal);
        Assert.Contains("AND available_copies > 0", source, StringComparison.Ordinal);
        Assert.Contains("AND available_copies < total_copies", source, StringComparison.Ordinal);
        Assert.Contains("AND {newTotalCopies} >= (total_copies - available_copies)", source, StringComparison.Ordinal);
        Assert.Contains("WHERE id = {bookId}", source, StringComparison.Ordinal);
        Assert.Contains("updated_at_utc = {clock.UtcNow}", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Loan_completion_sql_stays_parameterized()
    {
        var source = ReadInfrastructure("Persistence", "Repositories", "LoanRepository.cs");

        Assert.Contains("ExecuteSqlInterpolatedAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ExecuteSqlRaw", source, StringComparison.Ordinal);
        Assert.Contains("SET status = {status}", source, StringComparison.Ordinal);
        Assert.Contains("WHERE id = {loanId}", source, StringComparison.Ordinal);
        Assert.Contains("AND status = {nameof(LoanStatus.Active)}", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Idempotency_reservation_sql_stays_parameterized()
    {
        var source = ReadInfrastructure("Idempotency", "IdempotencyStore.cs");

        Assert.Contains("ExecuteSqlInterpolatedAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ExecuteSqlRaw", source, StringComparison.Ordinal);
        Assert.Contains("VALUES ({id}, {endpoint}, {key}, {requestHash}, {createdAtUtc})", source, StringComparison.Ordinal);
        Assert.Contains("ON CONFLICT (endpoint, key) DO NOTHING", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Outbox_claim_sql_uses_static_text_and_explicit_parameters()
    {
        var source = ReadInfrastructure("Outbox", "OutboxClaimer.cs");

        Assert.Contains("FOR UPDATE SKIP LOCKED", source, StringComparison.Ordinal);
        Assert.Contains("LIMIT @batchSize", source, StringComparison.Ordinal);
        Assert.Contains("SET locked_by = @workerId", source, StringComparison.Ordinal);
        Assert.Contains("NOW() + (@leaseSeconds * INTERVAL '1 second')", source, StringComparison.Ordinal);
        Assert.Contains("AddParameter(command, \"batchSize\", batchSize)", source, StringComparison.Ordinal);
        Assert.Contains("AddParameter(command, \"workerId\", workerId)", source, StringComparison.Ordinal);
        Assert.Contains("AddParameter(command, \"leaseSeconds\", leaseSeconds)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ExecuteSqlRaw", source, StringComparison.Ordinal);
        Assert.False(
            InterpolatedCommandText.IsMatch(source),
            "Outbox claim CommandText must be a static SQL string.");
    }

    [Fact]
    public void Test_raw_sql_helpers_use_static_literals()
    {
        foreach (var (path, source) in TestSources())
        {
            Assert.False(
                InterpolatedRawSql.IsMatch(source),
                $"Test Raw SQL in '{path}' must be a static literal with explicit parameters.");
            Assert.False(
                ConcatenatedRawSql.IsMatch(source),
                $"Test Raw SQL in '{path}' must not concatenate runtime values.");
        }
    }

    private static string ReadInfrastructure(params string[] segments)
    {
        var path = RepoPath(["src", "LibraryManager.Infrastructure", .. segments]);
        Assert.True(File.Exists(path), $"Expected infrastructure file at {path}.");
        return File.ReadAllText(path);
    }

    private static IEnumerable<(string Path, string Source)> ProductionSources() =>
        CsSources(RepoPath("src"));

    private static IEnumerable<(string Path, string Source)> TestSources() =>
        CsSources(RepoPath("tests"));

    private static IEnumerable<(string Path, string Source)> CsSources(string root)
    {
        Assert.True(Directory.Exists(root), $"Expected source directory at {root}.");

        foreach (var path in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            if (path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            yield return (path, File.ReadAllText(path));
        }
    }

    private static string RepoPath(params string[] segments)
    {
        var path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            Path.Combine(segments)));

        return path;
    }
}
