using LibraryManager.Api.Resources;
using LibraryManager.Domain;
using Microsoft.Extensions.Localization;

namespace LibraryManager.Api.Localization;

public sealed class ErrorLocalizer(IStringLocalizer<SharedResource> localizer)
{
    public string Localize(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);

        var key = "Error_" + error.Code.Replace('.', '_');
        var localized = error.Arguments is { Length: > 0 }
            ? localizer[key, error.Arguments]
            : localizer[key];

        return localized.ResourceNotFound ? error.Code : localized.Value;
    }
}
