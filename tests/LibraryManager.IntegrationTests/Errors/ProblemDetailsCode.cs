using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace LibraryManager.IntegrationTests.Errors;

internal static class ProblemDetailsCode
{
    public static string? Read(ProblemDetails problem)
    {
        if (!problem.Extensions.TryGetValue("code", out var value) || value is null)
        {
            return null;
        }

        return value switch
        {
            JsonElement element => element.GetString(),
            string text => text,
            _ => value.ToString()
        };
    }
}
