using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LibraryManager.Api.Middleware;
using LibraryManager.Api.Security;
using LibraryManager.Application.Common;
using LibraryManager.Application.Loans;
using LibraryManager.Application.Users;
using LibraryManager.Infrastructure.Persistence;
using LibraryManager.IntegrationTests;
using LibraryManager.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LibraryManager.IntegrationTests.Users;

[Collection(DatabaseCollection.Name)]
public sealed class UserRegistrationTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly CustomWebApplicationFactory _factory;
    private HttpClient _librarian = null!;

    public UserRegistrationTests(DatabaseFixture database)
    {
        _factory = new CustomWebApplicationFactory(database);
    }

    public Task InitializeAsync()
    {
        _librarian = _factory.CreateClient().WithTestAuth("user-librarian", LibrarianPolicy.Role);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _librarian.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Create_user_persists_audit_actor_and_correlation()
    {
        const string subject = "user-librarian";
        const string correlationId = "user-create-correlation";
        _librarian.DefaultRequestHeaders.Remove(CorrelationIdMiddleware.HeaderName);
        _librarian.DefaultRequestHeaders.Add(CorrelationIdMiddleware.HeaderName, correlationId);

        var email = $"{Guid.NewGuid():N}@example.com";
        var response = await _librarian.PostAsJsonAsync(
            "/users",
            new { name = "Ada Lovelace", email });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(correlationId, response.Headers.GetValues(CorrelationIdMiddleware.HeaderName).Single());

        var created = await response.Content.ReadFromJsonAsync<UserDto>(JsonOptions);
        Assert.NotNull(created);
        Assert.Equal("Ada Lovelace", created.Name);
        Assert.Equal(email.ToLowerInvariant(), created.Email);

        var loans = await _librarian.GetFromJsonAsync<PagedResult<LoanDto>>(
            $"/users/{created.Id}/loans",
            JsonOptions);
        Assert.NotNull(loans);
        Assert.Equal(1, loans.Page);
        Assert.Equal(20, loans.PageSize);
        Assert.Equal(0, loans.TotalCount);
        Assert.Empty(loans.Items);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
        var audit = await db.AuditEvents.SingleAsync(item =>
            item.EntityId == created.Id && item.Action == AuditMetadata.UserCreated);
        Assert.Equal(subject, audit.ActorId);
        Assert.Equal(correlationId, audit.CorrelationId);

        _librarian.DefaultRequestHeaders.Remove(CorrelationIdMiddleware.HeaderName);
    }

    [Fact]
    public async Task Duplicate_email_returns_422()
    {
        var email = $"{Guid.NewGuid():N}@example.com";
        var first = await _librarian.PostAsJsonAsync("/users", new { name = "Ada Lovelace", email });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var duplicate = await _librarian.PostAsJsonAsync("/users", new { name = "Other", email });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, duplicate.StatusCode);
    }

    [Fact]
    public async Task Unknown_user_loans_returns_404()
    {
        var response = await _librarian.GetAsync($"/users/{Guid.NewGuid()}/loans");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
