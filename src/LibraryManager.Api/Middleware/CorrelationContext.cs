using LibraryManager.Application.Abstractions;

namespace LibraryManager.Api.Middleware;

public sealed class CorrelationContext : ICorrelationContext
{
    public string CorrelationId { get; set; } = string.Empty;
}
