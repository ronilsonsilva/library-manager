using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LibraryManager.Api.Security;
using LibraryManager.Application.Books;
using LibraryManager.Application.Users;
using LibraryManager.Domain;
using LibraryManager.IntegrationTests;
using LibraryManager.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace LibraryManager.IntegrationTests.Errors;

[Collection(DatabaseCollection.Name)]
public sealed class ResultHttpMappingTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly CustomWebApplicationFactory _factory;
    private HttpClient _librarian = null!;

    public ResultHttpMappingTests(DatabaseFixture database)
    {
        _factory = new CustomWebApplicationFactory(database);
    }

    public Task InitializeAsync()
    {
        _librarian = _factory.CreateClient().WithTestAuth("result-mapping", LibrarianPolicy.Role);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _librarian.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Unknown_book_returns_404_with_stable_code()
    {
        var response = await _librarian.GetAsync($"/books/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        Assert.NotNull(problem);
        Assert.Equal("Not Found", problem.Title);
        Assert.Equal(ErrorCodes.BookNotFound, ProblemDetailsCode.Read(problem));
        Assert.True(problem.Extensions.ContainsKey("correlationId"));
    }

    [Fact]
    public async Task Inactive_book_loan_returns_422_with_stable_code()
    {
        var book = await CreateBookAsync(totalCopies: 1);
        var user = await CreateUserAsync("Inactive Mapping Borrower");
        Assert.Equal(HttpStatusCode.NoContent, (await _librarian.DeleteAsync($"/books/{book.Id}")).StatusCode);

        var response = await PostLoanAsync(book.Id, user.Id, Guid.NewGuid().ToString("N"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        Assert.NotNull(problem);
        Assert.Equal("Unprocessable Entity", problem.Title);
        Assert.Equal(ErrorCodes.BookInactive, ProblemDetailsCode.Read(problem));
        Assert.Contains("not active", problem.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Idempotency_payload_mismatch_returns_409_with_stable_code()
    {
        var book = await CreateBookAsync(totalCopies: 2);
        var otherBook = await CreateBookAsync(totalCopies: 2);
        var user = await CreateUserAsync("Conflict Mapping Borrower");
        var otherUser = await CreateUserAsync("Other Mapping Borrower");
        var key = Guid.NewGuid().ToString("N");

        Assert.Equal(HttpStatusCode.Created, (await PostLoanAsync(book.Id, user.Id, key)).StatusCode);

        var conflict = await PostLoanAsync(otherBook.Id, otherUser.Id, key);

        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        var problem = await conflict.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        Assert.NotNull(problem);
        Assert.Equal("Conflict", problem.Title);
        Assert.Equal(ErrorCodes.IdempotencyPayloadMismatch, ProblemDetailsCode.Read(problem));
        Assert.Contains("Idempotency-Key", problem.Detail, StringComparison.Ordinal);
    }

    private async Task<BookDto> CreateBookAsync(int totalCopies)
    {
        var response = await _librarian.PostAsJsonAsync(
            "/books",
            new
            {
                title = "Dune",
                isbn = Guid.NewGuid().ToString("N")[..12],
                author = "Frank Herbert",
                totalCopies
            });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var book = await response.Content.ReadFromJsonAsync<BookDto>(JsonOptions);
        Assert.NotNull(book);
        return book;
    }

    private async Task<UserDto> CreateUserAsync(string name)
    {
        var response = await _librarian.PostAsJsonAsync(
            "/users",
            new { name, email = $"{Guid.NewGuid():N}@example.com" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var user = await response.Content.ReadFromJsonAsync<UserDto>(JsonOptions);
        Assert.NotNull(user);
        return user;
    }

    private async Task<HttpResponseMessage> PostLoanAsync(Guid bookId, Guid userId, string idempotencyKey)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/loans")
        {
            Content = JsonContent.Create(new { bookId, userId })
        };
        request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
        return await _librarian.SendAsync(request);
    }
}
