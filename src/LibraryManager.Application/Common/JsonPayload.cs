using System.Text.Json;

namespace LibraryManager.Application.Common;

internal static class JsonPayload
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static string Serialize(object value) => JsonSerializer.Serialize(value, Options);
}
