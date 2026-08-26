using System.Net.Http.Headers;
using LibraryManager.IntegrationTests.Infrastructure;

namespace LibraryManager.IntegrationTests;

internal static class TestAuthClientExtensions
{
    public static HttpClient WithTestAuth(
        this HttpClient client,
        string subject,
        params string[] roles)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test", "token");
        client.DefaultRequestHeaders.Remove(TestAuthHandler.SubjectHeaderName);
        client.DefaultRequestHeaders.Add(TestAuthHandler.SubjectHeaderName, subject);
        client.DefaultRequestHeaders.Remove(TestAuthHandler.RolesHeaderName);

        foreach (var role in roles)
        {
            client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeaderName, role);
        }

        return client;
    }
}
