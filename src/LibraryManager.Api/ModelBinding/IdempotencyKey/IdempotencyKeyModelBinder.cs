using LibraryManager.Api.Contracts.Common;
using LibraryManager.Api.Resources;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Localization;

namespace LibraryManager.Api.ModelBinding;

public sealed class IdempotencyKeyModelBinder(IStringLocalizer<SharedResource> localizer) : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        ArgumentNullException.ThrowIfNull(bindingContext);
        bindingContext.HttpContext.RequestAborted.ThrowIfCancellationRequested();

        if (!bindingContext.HttpContext.Request.Headers.TryGetValue(IdempotencyKey.HeaderName, out var headerValues)
            || headerValues.Count == 0)
        {
            AddError(bindingContext, "Validation_IdempotencyKey_Required");
            return Task.CompletedTask;
        }

        var raw = headerValues[0];
        if (string.IsNullOrWhiteSpace(raw))
        {
            AddError(bindingContext, "Validation_IdempotencyKey_Required");
            return Task.CompletedTask;
        }

        var normalized = raw.Trim();
        if (normalized.Length > IdempotencyKey.MaxLength)
        {
            AddError(bindingContext, "Validation_IdempotencyKey_MaxLength");
            return Task.CompletedTask;
        }

        bindingContext.Result = ModelBindingResult.Success(new IdempotencyKey(normalized));
        return Task.CompletedTask;
    }

    private void AddError(ModelBindingContext bindingContext, string resourceKey)
    {
        bindingContext.ModelState.AddModelError(resourceKey, localizer[resourceKey]);
        bindingContext.Result = ModelBindingResult.Failed();
    }
}
