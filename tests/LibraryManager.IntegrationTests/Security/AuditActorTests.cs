using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LibraryManager.Api.Middleware;
using LibraryManager.Api.Security;
using LibraryManager.Application.Audit;
using LibraryManager.Application.Common;
using LibraryManager.IntegrationTests;
using LibraryManager.IntegrationTests.Infrastructure;

namespace LibraryManager.IntegrationTests.Security;

[Collection(DatabaseCollection.Name)]
public sealed class AuditActorTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly CustomWebApplicationFactory _factory;
    private HttpClient _librarian = null!;

    public AuditActorTests(DatabaseFixture database)
    {
        _factory = new CustomWebApplicationFactory(database);
    }

    public Task InitializeAsync()
    {
        _librarian = _factory.CreateClient().WithTestAuth("audit-actor-subject", LibrarianPolicy.Role);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _librarian.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Audit_event_actor_id_equals_authenticated_jwt_subject()
    {
        const string subject = "audit-actor-subject";
        const string correlationId = "audit-actor-correlation";
        _librarian.DefaultRequestHeaders.Remove(CorrelationIdMiddleware.HeaderName);
        _librarian.DefaultRequestHeaders.Add(CorrelationIdMiddleware.HeaderName, correlationId);

        var response = await _librarian.PostAsJsonAsync(
            "/users",
            new { name = "Audit Actor User", email = $"{Guid.NewGuid():N}@example.com" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var page = await _librarian.GetFromJsonAsync<PagedResult<AuditEventDto>>(
            "/audit-events?entityType=User&pageSize=100",
            JsonOptions);
        Assert.NotNull(page);
        var created = Assert.Single(
            page.Items,
            item => item.Action == AuditMetadata.UserCreated && item.CorrelationId == correlationId);
        Assert.Equal(subject, created.ActorId);
        Assert.Equal(correlationId, created.CorrelationId);
        Assert.Equal("Audit Actor User", created.DataJson.GetProperty("name").GetString());
    }
}
