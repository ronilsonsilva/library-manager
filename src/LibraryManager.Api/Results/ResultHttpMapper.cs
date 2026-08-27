using LibraryManager.Api.Contracts.Common;
using LibraryManager.Api.Localization;
using LibraryManager.Api.Middleware;
using LibraryManager.Api.Resources;
using LibraryManager.Application.Abstractions;
using LibraryManager.Application.Common;
using LibraryManager.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace LibraryManager.Api.ResultMapping;

public static class ResultHttpMapper
{
    public static ActionResult ToActionResult<TValue, TResponse>(
        this Result<TValue> result,
        ControllerBase controller,
        Func<TValue, TResponse> map)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(controller);
        ArgumentNullException.ThrowIfNull(map);

        if (result.IsFailure)
        {
            return ToProblem(controller, result.Error);
        }

        return controller.Ok(map(result.Value));
    }

    public static ActionResult ToActionResult<TDto, TResponse>(
        this Result<PagedResult<TDto>> result,
        ControllerBase controller,
        Func<TDto, TResponse> mapItem)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(controller);
        ArgumentNullException.ThrowIfNull(mapItem);

        if (result.IsFailure)
        {
            return ToProblem(controller, result.Error);
        }

        return controller.Ok(PagedResponse<TResponse>.From(result.Value, mapItem));
    }

    public static ActionResult ToCreatedResult<TValue, TResponse>(
        this Result<TValue> result,
        ControllerBase controller,
        Func<TValue, TResponse> map)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(controller);
        ArgumentNullException.ThrowIfNull(map);

        if (result.IsFailure)
        {
            return ToProblem(controller, result.Error);
        }

        return new ObjectResult(map(result.Value))
        {
            StatusCode = StatusCodes.Status201Created
        };
    }

    public static ActionResult ToCreatedAtAction<TValue, TResponse>(
        this Result<TValue> result,
        ControllerBase controller,
        string actionName,
        Func<TValue, object> routeValues,
        Func<TValue, TResponse> map)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(controller);
        ArgumentNullException.ThrowIfNull(actionName);
        ArgumentNullException.ThrowIfNull(routeValues);
        ArgumentNullException.ThrowIfNull(map);

        if (result.IsFailure)
        {
            return ToProblem(controller, result.Error);
        }

        return controller.CreatedAtAction(actionName, routeValues(result.Value), map(result.Value));
    }

    public static ActionResult ToNoContentResult(this Result result, ControllerBase controller)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(controller);

        if (result.IsFailure)
        {
            return ToProblem(controller, result.Error);
        }

        return controller.NoContent();
    }

    private static ActionResult ToProblem(ControllerBase controller, Error error)
    {
        var services = controller.HttpContext.RequestServices;
        var titles = services.GetRequiredService<IStringLocalizer<SharedResource>>();
        var errorLocalizer = services.GetRequiredService<ErrorLocalizer>();
        var correlationId = services.GetService<ICorrelationContext>()?.CorrelationId
            ?? controller.HttpContext.Response.Headers[CorrelationIdMiddleware.HeaderName].FirstOrDefault();

        var (status, titleKey) = error.Type switch
        {
            ErrorType.Validation => (StatusCodes.Status400BadRequest, "Problem_Validation_Title"),
            ErrorType.NotFound => (StatusCodes.Status404NotFound, "Problem_NotFound_Title"),
            ErrorType.BusinessRule => (StatusCodes.Status422UnprocessableEntity, "Problem_BusinessRule_Title"),
            ErrorType.Conflict => (StatusCodes.Status409Conflict, "Problem_Conflict_Title"),
            _ => (StatusCodes.Status500InternalServerError, "Problem_Unexpected_Title")
        };

        var problem = new ProblemDetails
        {
            Status = status,
            Title = titles[titleKey].Value,
            Detail = errorLocalizer.Localize(error),
            Instance = controller.HttpContext.Request.Path
        };
        problem.Extensions["code"] = error.Code;
        problem.Extensions["correlationId"] = correlationId;

        return new ObjectResult(problem)
        {
            StatusCode = status,
            ContentTypes = { "application/problem+json" }
        };
    }
}
