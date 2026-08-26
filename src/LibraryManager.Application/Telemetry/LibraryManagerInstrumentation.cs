using System.Diagnostics;

namespace LibraryManager.Application.Telemetry;

public static class LibraryManagerInstrumentation
{
    public const string ActivitySourceName = "LibraryManager";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
}
