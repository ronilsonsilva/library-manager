using System.Net;
using LibraryManager.Api.Middleware;
using LibraryManager.Application.Abstractions;
using LibraryManager.Application.Common;
using LibraryManager.Domain;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace LibraryManager.Api.Errors;

public sealed class ApiExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, title) = exception switch
        {
            EntityNotFoundException => (HttpStatusCode.NotFound, "Not Found"),
            IdempotencyConflictException => (HttpStatusCode.Conflict, "Conflict"),
            BusinessRuleException => (HttpStatusCode.UnprocessableEntity, "Unprocessable Entity"),
            DomainException => (HttpStatusCode.BadRequest, "Bad Request"),
            _ => (HttpStatusCode.InternalServerError, "Internal Server Error")
        };

        if (status == HttpStatusCode.InternalServerError)
        {
            return false;
        }

        var correlationId = httpContext.RequestServices.GetService<ICorrelationContext>()?.CorrelationId
            ?? httpContext.Response.Headers[CorrelationIdMiddleware.HeaderName].FirstOrDefault();

        var problem = new ProblemDetails
        {
            Status = (int)status,
            Title = title,
            Detail = exception.Message,
            Instance = httpContext.Request.Path
        };
        problem.Extensions["correlationId"] = correlationId;

        httpContext.Response.StatusCode = (int)status;
        httpContext.Response.ContentType = "application/problem+json";
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken: cancellationToken);

        return true;
    }
}
