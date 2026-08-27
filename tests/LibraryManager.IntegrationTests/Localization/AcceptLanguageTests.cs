using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LibraryManager.Api.Middleware;
using LibraryManager.Api.Security;
using LibraryManager.Application.Books;
using LibraryManager.Application.Users;
using LibraryManager.Domain;
using LibraryManager.IntegrationTests;
using LibraryManager.IntegrationTests.Errors;
using LibraryManager.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace LibraryManager.IntegrationTests.Localization;

[Collection(DatabaseCollection.Name)]
public sealed class AcceptLanguageTests : IAsyncLifetime
{
    private const string EnglishValidationTitle = "One or more validation errors occurred.";
    private const string PortugueseValidationTitle = "Ocorreram um ou mais erros de validação.";
    private const string EnglishTitleRequired = "Title is required.";
    private const string PortugueseTitleRequired = "O título é obrigatório.";
    private const string EnglishIdempotencyRequired = "Idempotency-Key is required.";
    private const string PortugueseIdempotencyRequired = "A chave de idempotência é obrigatória.";
    private const string EnglishBookNotFound = "Book was not found.";
    private const string PortugueseBookNotFound = "Livro não encontrado.";
    private const string EnglishBookInactive = "Book is not active.";
    private const string PortugueseBookInactive = "O livro não está ativo.";
    private const string EnglishUnexpected = "An unexpected error occurred.";
    private const string PortugueseUnexpected = "Ocorreu um erro inesperado.";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly CustomWebApplicationFactory _factory;

    public AcceptLanguageTests(DatabaseFixture database)
    {
        _factory = new CustomWebApplicationFactory(database);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task Omitted_accept_language_uses_en_US_for_body_validation_and_preserves_correlation_id()
    {
        using var client = CreateLibrarian(acceptLanguage: null);
        const string correlationId = "loc-default-correlation";
        var problem = await PostInvalidBookAsync(client, correlationId);

        AssertEnglishBodyValidation(problem, correlationId);
        Assert.Equal("en-US", ReadContentLanguage(_lastResponse!));
    }

    [Fact]
    public async Task Explicit_en_US_uses_english_for_body_validation()
    {
        using var client = CreateLibrarian("en-US");
        const string correlationId = "loc-en-correlation";
        var problem = await PostInvalidBookAsync(client, correlationId);

        AssertEnglishBodyValidation(problem, correlationId);
        Assert.Equal("en-US", ReadContentLanguage(_lastResponse!));
    }

    [Fact]
    public async Task Pt_BR_uses_portuguese_for_body_validation()
    {
        using var client = CreateLibrarian("pt-BR");
        const string correlationId = "loc-pt-body-correlation";
        var problem = await PostInvalidBookAsync(client, correlationId);

        Assert.Equal(PortugueseValidationTitle, problem.Title);
        Assert.Contains(PortugueseTitleRequired, FlattenErrors(problem));
        Assert.DoesNotContain(EnglishTitleRequired, FlattenErrors(problem), StringComparer.Ordinal);
        Assert.Equal(correlationId, ReadExtension(problem, "correlationId"));
        Assert.Equal("pt-BR", ReadContentLanguage(_lastResponse!));
    }

    [Fact]
    public async Task Unsupported_accept_language_falls_back_to_en_US()
    {
        using var client = CreateLibrarian("de-DE");
        var problem = await PostInvalidBookAsync(client, "loc-fallback-correlation");

        AssertEnglishBodyValidation(problem, "loc-fallback-correlation");
        Assert.Equal("en-US", ReadContentLanguage(_lastResponse!));
    }

    [Fact]
    public async Task Idempotency_key_errors_are_localized_in_en_US_and_pt_BR()
    {
        var english = await PostLoanWithoutIdempotencyKeyAsync("en-US", "loc-idem-en");
        Assert.Equal(EnglishValidationTitle, english.Problem.Title);
        Assert.Contains(EnglishIdempotencyRequired, FlattenErrors(english.Problem));
        Assert.Contains("Validation_IdempotencyKey_Required", english.Problem.Errors.Keys);
        Assert.Equal("loc-idem-en", ReadExtension(english.Problem, "correlationId"));
        Assert.Equal("en-US", ReadContentLanguage(english.Response));

        var portuguese = await PostLoanWithoutIdempotencyKeyAsync("pt-BR", "loc-idem-pt");
        Assert.Equal(PortugueseValidationTitle, portuguese.Problem.Title);
        Assert.Contains(PortugueseIdempotencyRequired, FlattenErrors(portuguese.Problem));
        Assert.DoesNotContain(EnglishIdempotencyRequired, FlattenErrors(portuguese.Problem), StringComparer.Ordinal);
        Assert.Contains("Validation_IdempotencyKey_Required", portuguese.Problem.Errors.Keys);
        Assert.Equal("loc-idem-pt", ReadExtension(portuguese.Problem, "correlationId"));
        Assert.Equal("pt-BR", ReadContentLanguage(portuguese.Response));
    }

    [Fact]
    public async Task Result_not_found_is_localized_and_keeps_english_error_code()
    {
        using var englishClient = CreateLibrarian("en-US");
        var english = await GetUnknownBookAsync(englishClient, "loc-notfound-en");
        Assert.Equal(HttpStatusCode.NotFound, english.Response.StatusCode);
        Assert.Equal("Not Found", english.Problem.Title);
        Assert.Equal(EnglishBookNotFound, english.Problem.Detail);
        Assert.Equal(ErrorCodes.BookNotFound, ProblemDetailsCode.Read(english.Problem));
        Assert.Equal("loc-notfound-en", ReadExtension(english.Problem, "correlationId"));
        Assert.Equal("en-US", ReadContentLanguage(english.Response));

        using var portugueseClient = CreateLibrarian("pt-BR");
        var portuguese = await GetUnknownBookAsync(portugueseClient, "loc-notfound-pt");
        Assert.Equal(HttpStatusCode.NotFound, portuguese.Response.StatusCode);
        Assert.Equal("Não encontrado", portuguese.Problem.Title);
        Assert.Equal(PortugueseBookNotFound, portuguese.Problem.Detail);
        Assert.Equal(ErrorCodes.BookNotFound, ProblemDetailsCode.Read(portuguese.Problem));
        Assert.Equal("loc-notfound-pt", ReadExtension(portuguese.Problem, "correlationId"));
        Assert.Equal("pt-BR", ReadContentLanguage(portuguese.Response));
    }

    [Fact]
    public async Task Business_rule_errors_are_localized_in_en_US_and_pt_BR()
    {
        var english = await PostLoanForInactiveBookAsync("en-US", "loc-biz-en");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, english.Response.StatusCode);
        Assert.Equal("Unprocessable Entity", english.Problem.Title);
        Assert.Equal(EnglishBookInactive, english.Problem.Detail);
        Assert.Equal(ErrorCodes.BookInactive, ProblemDetailsCode.Read(english.Problem));
        Assert.Equal("loc-biz-en", ReadExtension(english.Problem, "correlationId"));
        Assert.Equal("en-US", ReadContentLanguage(english.Response));

        var portuguese = await PostLoanForInactiveBookAsync("pt-BR", "loc-biz-pt");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, portuguese.Response.StatusCode);
        Assert.Equal("Entidade não processável", portuguese.Problem.Title);
        Assert.Equal(PortugueseBookInactive, portuguese.Problem.Detail);
        Assert.Equal(ErrorCodes.BookInactive, ProblemDetailsCode.Read(portuguese.Problem));
        Assert.Equal("loc-biz-pt", ReadExtension(portuguese.Problem, "correlationId"));
        Assert.Equal("pt-BR", ReadContentLanguage(portuguese.Response));
    }

    [Fact]
    public async Task Unexpected_errors_are_localized_and_preserve_correlation_id()
    {
        using var englishClient = CreateAnonymous("en-US");
        var english = await GetUnexpectedAsync(englishClient, "loc-500-en");
        Assert.Equal(HttpStatusCode.InternalServerError, english.Response.StatusCode);
        Assert.Equal(EnglishUnexpected, english.Problem.Title);
        Assert.Equal(EnglishUnexpected, english.Problem.Detail);
        Assert.Equal("loc-500-en", ReadExtension(english.Problem, "correlationId"));
        Assert.Null(ProblemDetailsCode.Read(english.Problem));
        Assert.Equal("en-US", ReadContentLanguage(english.Response));

        using var portugueseClient = CreateAnonymous("pt-BR");
        var portuguese = await GetUnexpectedAsync(portugueseClient, "loc-500-pt");
        Assert.Equal(HttpStatusCode.InternalServerError, portuguese.Response.StatusCode);
        Assert.Equal(PortugueseUnexpected, portuguese.Problem.Title);
        Assert.Equal(PortugueseUnexpected, portuguese.Problem.Detail);
        Assert.Equal("loc-500-pt", ReadExtension(portuguese.Problem, "correlationId"));
        Assert.Equal("pt-BR", ReadContentLanguage(portuguese.Response));
    }

    private HttpResponseMessage? _lastResponse;

    private HttpClient CreateLibrarian(string? acceptLanguage)
    {
        var client = _factory.CreateClient().WithTestAuth("loc-librarian", LibrarianPolicy.Role);
        ApplyLanguage(client, acceptLanguage);
        return client;
    }

    private HttpClient CreateAnonymous(string? acceptLanguage)
    {
        var client = _factory.CreateClient();
        ApplyLanguage(client, acceptLanguage);
        return client;
    }

    private static void ApplyLanguage(HttpClient client, string? acceptLanguage)
    {
        client.DefaultRequestHeaders.AcceptLanguage.Clear();
        if (acceptLanguage is not null)
        {
            client.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue(acceptLanguage));
        }
    }

    private async Task<ValidationProblemDetails> PostInvalidBookAsync(HttpClient client, string correlationId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/books")
        {
            Content = JsonContent.Create(new
            {
                title = "",
                isbn = Guid.NewGuid().ToString("N")[..12],
                author = "Frank Herbert",
                totalCopies = 1
            })
        };
        request.Headers.TryAddWithoutValidation(CorrelationIdMiddleware.HeaderName, correlationId);
        _lastResponse = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, _lastResponse.StatusCode);
        var problem = await _lastResponse.Content.ReadFromJsonAsync<ValidationProblemDetails>(JsonOptions);
        Assert.NotNull(problem);
        return problem;
    }

    private async Task<(HttpResponseMessage Response, ValidationProblemDetails Problem)> PostLoanWithoutIdempotencyKeyAsync(
        string acceptLanguage,
        string correlationId)
    {
        using var client = CreateLibrarian(acceptLanguage);
        var book = await CreateBookAsync(client);
        var user = await CreateUserAsync(client);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/loans")
        {
            Content = JsonContent.Create(new { bookId = book.Id, userId = user.Id })
        };
        request.Headers.TryAddWithoutValidation(CorrelationIdMiddleware.HeaderName, correlationId);
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(JsonOptions);
        Assert.NotNull(problem);
        return (response, problem);
    }

    private async Task<(HttpResponseMessage Response, ProblemDetails Problem)> GetUnknownBookAsync(
        HttpClient client,
        string correlationId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/books/{Guid.NewGuid()}");
        request.Headers.TryAddWithoutValidation(CorrelationIdMiddleware.HeaderName, correlationId);
        var response = await client.SendAsync(request);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        Assert.NotNull(problem);
        return (response, problem);
    }

    private async Task<(HttpResponseMessage Response, ProblemDetails Problem)> PostLoanForInactiveBookAsync(
        string acceptLanguage,
        string correlationId)
    {
        using var client = CreateLibrarian(acceptLanguage);
        var book = await CreateBookAsync(client);
        var user = await CreateUserAsync(client);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/books/{book.Id}")).StatusCode);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/loans")
        {
            Content = JsonContent.Create(new { bookId = book.Id, userId = user.Id })
        };
        request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.NewGuid().ToString("N"));
        request.Headers.TryAddWithoutValidation(CorrelationIdMiddleware.HeaderName, correlationId);
        var response = await client.SendAsync(request);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        Assert.NotNull(problem);
        return (response, problem);
    }

    private static async Task<(HttpResponseMessage Response, ProblemDetails Problem)> GetUnexpectedAsync(
        HttpClient client,
        string correlationId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/__test/unexpected-error");
        request.Headers.TryAddWithoutValidation(CorrelationIdMiddleware.HeaderName, correlationId);
        var response = await client.SendAsync(request);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        Assert.NotNull(problem);
        return (response, problem);
    }

    private static async Task<BookDto> CreateBookAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/books",
            new
            {
                title = "Dune",
                isbn = Guid.NewGuid().ToString("N")[..12],
                author = "Frank Herbert",
                totalCopies = 1
            });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var book = await response.Content.ReadFromJsonAsync<BookDto>(JsonOptions);
        Assert.NotNull(book);
        return book;
    }

    private static async Task<UserDto> CreateUserAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/users",
            new { name = "Localization Borrower", email = $"{Guid.NewGuid():N}@example.com" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var user = await response.Content.ReadFromJsonAsync<UserDto>(JsonOptions);
        Assert.NotNull(user);
        return user;
    }

    private static void AssertEnglishBodyValidation(ValidationProblemDetails problem, string correlationId)
    {
        Assert.Equal(EnglishValidationTitle, problem.Title);
        Assert.Contains(EnglishTitleRequired, FlattenErrors(problem));
        Assert.Equal(correlationId, ReadExtension(problem, "correlationId"));
    }

    private static IEnumerable<string> FlattenErrors(ValidationProblemDetails problem) =>
        problem.Errors.SelectMany(pair => pair.Value);

    private static string? ReadContentLanguage(HttpResponseMessage response)
    {
        if (response.Content.Headers.ContentLanguage.Count > 0)
        {
            return response.Content.Headers.ContentLanguage.ToString();
        }

        return response.Headers.TryGetValues("Content-Language", out var values)
            ? values.FirstOrDefault()
            : null;
    }

    private static string? ReadExtension(ProblemDetails problem, string key)
    {
        if (!problem.Extensions.TryGetValue(key, out var value) || value is null)
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
