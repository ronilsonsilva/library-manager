using System.Globalization;
using LibraryManager.Api.Middleware;
using LibraryManager.Api.Resources;
using LibraryManager.Application.Abstractions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace LibraryManager.Api.Errors;

public sealed class ApiExceptionHandler(
    ILogger<ApiExceptionHandler> logger,
    IStringLocalizer<SharedResource> localizer) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException)
        {
            return false;
        }

        logger.LogError(exception, "Unhandled exception while processing the request.");

        var correlationId = httpContext.RequestServices.GetService<ICorrelationContext>()?.CorrelationId
            ?? httpContext.Response.Headers[CorrelationIdMiddleware.HeaderName].FirstOrDefault();

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = localizer["Problem_Unexpected_Title"],
            Detail = localizer["Problem_Unexpected_Detail"],
            Instance = httpContext.Request.Path
        };
        problem.Extensions["correlationId"] = correlationId;

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        httpContext.Response.Headers.ContentLanguage = CultureInfo.CurrentUICulture.Name;
        await httpContext.Response.WriteAsJsonAsync(
            problem,
            options: null,
            contentType: "application/problem+json",
            cancellationToken);

        return true;
    }
}
