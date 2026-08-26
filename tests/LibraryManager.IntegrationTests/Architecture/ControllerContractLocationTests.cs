namespace LibraryManager.IntegrationTests.Architecture;

public sealed class ControllerContractLocationTests
{
    private static readonly string[] ApplicationHttpContractTypeNames =
    [
        "PagedResult<",
        "BookDto",
        "BookAvailabilityDto",
        "UserDto",
        "LoanDto",
        "AuditEventDto"
    ];

    [Fact]
    public void Controller_files_declare_no_public_sealed_record_transport_types()
    {
        foreach (var (path, source) in ControllerSources())
        {
            Assert.False(
                source.Contains("public sealed record", StringComparison.Ordinal),
                $"Controller '{path}' must not declare public sealed record transport types.");
        }
    }

    [Fact]
    public void List_history_and_audit_actions_do_not_return_application_page_or_dto_contracts()
    {
        foreach (var (path, source) in ControllerSources())
        {
            foreach (var typeName in ApplicationHttpContractTypeNames)
            {
                Assert.False(
                    source.Contains(typeName, StringComparison.Ordinal),
                    $"Controller '{path}' must not use Application HTTP contract type '{typeName}'.");
            }
        }
    }

    [Fact]
    public void List_history_and_audit_actions_return_api_paged_response_contracts()
    {
        var books = ControllerSource("BooksController.cs");
        Assert.Contains("Task<ActionResult<PagedResponse<BookResponse>>> List", books, StringComparison.Ordinal);
        Assert.Contains("Task<ActionResult<PagedResponse<LoanResponse>>> GetLoanHistory", books, StringComparison.Ordinal);

        var users = ControllerSource("UsersController.cs");
        Assert.Contains("Task<ActionResult<PagedResponse<LoanResponse>>> GetLoans", users, StringComparison.Ordinal);

        var audit = ControllerSource("AuditEventsController.cs");
        Assert.Contains("Task<ActionResult<PagedResponse<AuditEventResponse>>> List", audit, StringComparison.Ordinal);
    }

    private static string ControllerSource(string fileName)
    {
        var path = Path.Combine(ControllersDirectory(), fileName);
        Assert.True(File.Exists(path), $"Expected controller at {path}.");
        return File.ReadAllText(path);
    }

    private static IEnumerable<(string Path, string Source)> ControllerSources()
    {
        var files = Directory.GetFiles(ControllersDirectory(), "*.cs");
        Assert.NotEmpty(files);

        foreach (var path in files)
        {
            yield return (path, File.ReadAllText(path));
        }
    }

    private static string ControllersDirectory()
    {
        var path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src",
            "LibraryManager.Api",
            "Controllers"));

        Assert.True(Directory.Exists(path), $"Expected controllers directory at {path}.");
        return path;
    }
}
