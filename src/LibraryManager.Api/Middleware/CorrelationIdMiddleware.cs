using System.Text.RegularExpressions;

namespace LibraryManager.Api.Middleware;

public sealed class CorrelationIdMiddleware(RequestDelegate next)
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
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        await next(context);
    }
}
