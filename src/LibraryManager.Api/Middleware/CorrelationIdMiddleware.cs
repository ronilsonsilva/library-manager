using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace LibraryManager.Api.Middleware;

public sealed class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-ID";
    private static readonly Regex ValidCorrelationId = new(
        "^[A-Za-z0-9._-]{1,128}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public async Task InvokeAsync(HttpContext context, CorrelationContext correlation)
    {
        var incoming = context.Request.Headers[HeaderName].FirstOrDefault();
        var correlationId = incoming is not null && ValidCorrelationId.IsMatch(incoming)
            ? incoming
            : Guid.NewGuid().ToString("D");

        correlation.CorrelationId = correlationId;
        Activity.Current?.SetTag("correlation.id", correlationId);
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
        {
            await next(context);
        }
    }
}
