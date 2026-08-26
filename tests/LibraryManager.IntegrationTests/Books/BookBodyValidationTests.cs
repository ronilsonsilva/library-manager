using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LibraryManager.Api.Security;
using LibraryManager.Infrastructure.Persistence;
using LibraryManager.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LibraryManager.IntegrationTests.Books;

[Collection(DatabaseCollection.Name)]
public sealed class BookBodyValidationTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly CustomWebApplicationFactory _factory;
    private HttpClient _librarian = null!;

    public BookBodyValidationTests(DatabaseFixture database)
    {
        _factory = new CustomWebApplicationFactory(database);
    }

    public Task InitializeAsync()
    {
        _librarian = _factory.CreateClient().WithTestAuth("book-body-validation", LibrarianPolicy.Role);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _librarian.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Empty_title_returns_400_validation_problem_and_does_not_create_a_book()
    {
        var isbn = UniqueIsbn();

        var response = await _librarian.PostAsJsonAsync(
            "/books",
            new { title = "", isbn, author = "Frank Herbert", totalCopies = 1 });

        await AssertBodyValidationAsync(response, "Title", isbn);
    }

    [Fact]
    public async Task Empty_isbn_returns_400_validation_problem_and_does_not_create_a_book()
    {
        var isbn = UniqueIsbn();

        var response = await _librarian.PostAsJsonAsync(
            "/books",
            new { title = "Dune", isbn = "", author = "Frank Herbert", totalCopies = 1 });

        await AssertBodyValidationAsync(response, "Isbn", isbn);
    }

    [Fact]
    public async Task Total_copies_below_one_returns_400_validation_problem_and_does_not_create_a_book()
    {
        var isbn = UniqueIsbn();

        var response = await _librarian.PostAsJsonAsync(
            "/books",
            new { title = "Dune", isbn, author = "Frank Herbert", totalCopies = 0 });

        await AssertBodyValidationAsync(response, "TotalCopies", isbn);
    }

    private async Task AssertBodyValidationAsync(
        HttpResponseMessage response,
        string modelStateKey,
        string isbn)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(JsonOptions);
        Assert.NotNull(problem);
        Assert.Equal((int)HttpStatusCode.BadRequest, problem.Status);
        Assert.Contains(modelStateKey, problem.Errors.Keys);
        Assert.True(problem.Extensions.ContainsKey("correlationId"));

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
        Assert.Equal(0, await db.Books.CountAsync(book => book.Isbn == isbn));
    }

    private static string UniqueIsbn() => Guid.NewGuid().ToString("N")[..12];
}
