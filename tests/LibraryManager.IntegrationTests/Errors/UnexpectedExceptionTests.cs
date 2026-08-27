using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LibraryManager.Api.Middleware;
using LibraryManager.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace LibraryManager.IntegrationTests.Errors;

[Collection(DatabaseCollection.Name)]
public sealed class UnexpectedExceptionTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly CustomWebApplicationFactory _factory;
    private HttpClient _client = null!;

    public UnexpectedExceptionTests(DatabaseFixture database)
    {
        _factory = new CustomWebApplicationFactory(database);
    }

    public Task InitializeAsync()
    {
        _client = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Unexpected_exception_returns_generic_500_with_correlation_and_no_internals()
    {
        const string correlationId = "unexpected-error-correlation";
        _client.DefaultRequestHeaders.Remove(CorrelationIdMiddleware.HeaderName);
        _client.DefaultRequestHeaders.Add(CorrelationIdMiddleware.HeaderName, correlationId);

        var response = await _client.GetAsync("/__test/unexpected-error");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.DoesNotContain("SELECT", body, StringComparison.Ordinal);
        Assert.DoesNotContain("redis:6379", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Host=db.example", body, StringComparison.Ordinal);
        Assert.DoesNotContain("at LibraryManager", body, StringComparison.Ordinal);
        Assert.DoesNotContain("InvalidOperationException", body, StringComparison.Ordinal);

        var problem = JsonSerializer.Deserialize<ProblemDetails>(body, JsonOptions);
        Assert.NotNull(problem);
        Assert.Equal("An unexpected error occurred.", problem.Title);
        Assert.Equal("An unexpected error occurred.", problem.Detail);
        Assert.Equal(correlationId, ReadCorrelationId(problem));
    }

    private static string? ReadCorrelationId(ProblemDetails problem)
    {
        if (!problem.Extensions.TryGetValue("correlationId", out var value) || value is null)
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
