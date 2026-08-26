using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LibraryManager.Api.Contracts.Common;
using LibraryManager.Api.Security;
using LibraryManager.Application.Books;
using LibraryManager.Application.Loans;
using LibraryManager.Application.Users;
using LibraryManager.Infrastructure.Persistence;
using LibraryManager.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LibraryManager.IntegrationTests.Loans;

[Collection(DatabaseCollection.Name)]
public sealed class IdempotencyKeyBindingTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly CustomWebApplicationFactory _factory;
    private HttpClient _librarian = null!;

    public IdempotencyKeyBindingTests(DatabaseFixture database)
    {
        _factory = new CustomWebApplicationFactory(database);
    }

    public Task InitializeAsync()
    {
        _librarian = _factory.CreateClient().WithTestAuth("idempotency-binder", LibrarianPolicy.Role);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _librarian.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Missing_key_returns_400_validation_problem_and_does_not_create_a_loan()
    {
        var (bookId, userId) = await SeedBookAndUserAsync();

        var response = await _librarian.PostAsJsonAsync("/loans", new { bookId, userId });

        await AssertBindingFailureAsync(response, "Validation_IdempotencyKey_Required", bookId);
    }

    [Fact]
    public async Task Empty_key_returns_400_validation_problem_and_does_not_create_a_loan()
    {
        var (bookId, userId) = await SeedBookAndUserAsync();

        var response = await PostLoanAsync(bookId, userId, idempotencyKey: string.Empty);

        await AssertBindingFailureAsync(response, "Validation_IdempotencyKey_Required", bookId);
    }

    [Fact]
    public async Task Whitespace_key_returns_400_validation_problem_and_does_not_create_a_loan()
    {
        var (bookId, userId) = await SeedBookAndUserAsync();

        var response = await PostLoanAsync(bookId, userId, " \t  ");

        await AssertBindingFailureAsync(response, "Validation_IdempotencyKey_Required", bookId);
    }

    [Fact]
    public async Task Key_longer_than_128_characters_returns_400_and_does_not_create_a_loan()
    {
        var (bookId, userId) = await SeedBookAndUserAsync();
        var key = new string('a', IdempotencyKey.MaxLength + 1);

        var response = await PostLoanAsync(bookId, userId, key);

        await AssertBindingFailureAsync(response, "Validation_IdempotencyKey_MaxLength", bookId);
    }

    [Fact]
    public async Task Key_of_128_characters_is_accepted()
    {
        var (bookId, userId) = await SeedBookAndUserAsync();
        var key = Guid.NewGuid().ToString("N").PadRight(IdempotencyKey.MaxLength, 'x');
        Assert.Equal(IdempotencyKey.MaxLength, key.Length);

        var response = await PostLoanAsync(bookId, userId, key);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(1, await CountLoansForBookAsync(bookId));
    }

    [Fact]
    public async Task Surrounding_whitespace_is_trimmed_before_idempotency_ownership()
    {
        var (bookId, userId) = await SeedBookAndUserAsync();
        var normalized = Guid.NewGuid().ToString("N");

        var first = await PostLoanAsync(bookId, userId, $"  {normalized}  ");
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var created = await first.Content.ReadFromJsonAsync<LoanDto>(JsonOptions);
        Assert.NotNull(created);

        var replay = await PostLoanAsync(bookId, userId, normalized);
        Assert.Equal(HttpStatusCode.Created, replay.StatusCode);
        var replayed = await replay.Content.ReadFromJsonAsync<LoanDto>(JsonOptions);
        Assert.NotNull(replayed);
        Assert.Equal(created.Id, replayed.Id);
        Assert.Equal(1, await CountLoansForBookAsync(bookId));
    }

    private async Task<(Guid BookId, Guid UserId)> SeedBookAndUserAsync()
    {
        var bookResponse = await _librarian.PostAsJsonAsync(
            "/books",
            new { title = "Dune", isbn = Guid.NewGuid().ToString("N")[..12], author = "Frank Herbert", totalCopies = 2 });
        Assert.Equal(HttpStatusCode.Created, bookResponse.StatusCode);
        var book = await bookResponse.Content.ReadFromJsonAsync<BookDto>(JsonOptions);
        Assert.NotNull(book);

        var userResponse = await _librarian.PostAsJsonAsync(
            "/users",
            new { name = "Binder Borrower", email = $"{Guid.NewGuid():N}@example.com" });
        Assert.Equal(HttpStatusCode.Created, userResponse.StatusCode);
        var user = await userResponse.Content.ReadFromJsonAsync<UserDto>(JsonOptions);
        Assert.NotNull(user);

        return (book.Id, user.Id);
    }

    private async Task<HttpResponseMessage> PostLoanAsync(Guid bookId, Guid userId, string idempotencyKey)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/loans")
        {
            Content = JsonContent.Create(new { bookId, userId })
        };
        request.Headers.TryAddWithoutValidation(IdempotencyKey.HeaderName, idempotencyKey);
        return await _librarian.SendAsync(request);
    }

    private async Task AssertBindingFailureAsync(
        HttpResponseMessage response,
        string modelStateKey,
        Guid bookId)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(JsonOptions);
        Assert.NotNull(problem);
        Assert.Equal((int)HttpStatusCode.BadRequest, problem.Status);
        Assert.Contains(modelStateKey, problem.Errors.Keys);
        Assert.True(problem.Extensions.ContainsKey("correlationId"));
        Assert.Equal(0, await CountLoansForBookAsync(bookId));
    }

    private async Task<int> CountLoansForBookAsync(Guid bookId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
        return await db.Loans.CountAsync(item => item.BookId == bookId);
    }
}
