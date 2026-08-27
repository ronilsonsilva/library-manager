using System.Text;
using System.Text.Json;
using LibraryManager.Api.Errors;
using LibraryManager.Api.Middleware;
using LibraryManager.Api.Resources;
using LibraryManager.Application.Abstractions;
using LibraryManager.Application.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace LibraryManager.UnitTests.Api;

public sealed class ApiExceptionHandlerTests
{
    [Fact]
    public async Task Operation_canceled_is_not_handled()
    {
        var (handler, logger) = CreateHandler();
        var httpContext = CreateHttpContext("canceled-correlation");

        var handled = await handler.TryHandleAsync(
            httpContext,
            new OperationCanceledException(),
            CancellationToken.None);

        Assert.False(handled);
        Assert.Equal(StatusCodes.Status200OK, httpContext.Response.StatusCode);
        Assert.Empty(ReadBody(httpContext));
        Assert.Empty(logger.Entries);
    }

    [Fact]
    public async Task Task_canceled_is_not_handled()
    {
        var (handler, logger) = CreateHandler();
        var httpContext = CreateHttpContext("task-canceled-correlation");

        var handled = await handler.TryHandleAsync(
            httpContext,
            new TaskCanceledException(),
            CancellationToken.None);

        Assert.False(handled);
        Assert.Empty(logger.Entries);
        Assert.Empty(ReadBody(httpContext));
    }

    [Fact]
    public async Task Unexpected_exception_writes_generic_problem_logs_english_and_omits_internals()
    {
        const string correlationId = "handler-unit-correlation";
        const string secret =
            "Host=db.example; SELECT password FROM users; redis:6379; at LibraryManager.Api";
        var (handler, logger) = CreateHandler();
        var httpContext = CreateHttpContext(correlationId);
        httpContext.Request.Path = "/__test/unexpected-error";

        var handled = await handler.TryHandleAsync(
            httpContext,
            new InvalidOperationException(secret),
            CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status500InternalServerError, httpContext.Response.StatusCode);
        Assert.StartsWith("application/problem+json", httpContext.Response.ContentType);

        var body = ReadBody(httpContext);
        Assert.DoesNotContain(secret, body, StringComparison.Ordinal);
        Assert.DoesNotContain("SELECT", body, StringComparison.Ordinal);
        Assert.DoesNotContain("redis:6379", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Host=db.example", body, StringComparison.Ordinal);
        Assert.DoesNotContain("InvalidOperationException", body, StringComparison.Ordinal);
        Assert.DoesNotContain("at LibraryManager", body, StringComparison.Ordinal);

        var problem = JsonSerializer.Deserialize<ProblemDetails>(body, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        Assert.NotNull(problem);
        Assert.Equal("Problem_Unexpected_Title", problem.Title);
        Assert.Equal("Problem_Unexpected_Detail", problem.Detail);
        Assert.False(problem.Extensions.ContainsKey("exception"));
        Assert.False(problem.Extensions.ContainsKey("code"));
        Assert.Equal(correlationId, ReadCorrelationId(problem));

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Equal("Unhandled exception while processing the request.", entry.Message);
        Assert.IsType<InvalidOperationException>(entry.Exception);
        Assert.Equal(secret, entry.Exception!.Message);
    }

    [Fact]
    public void Expected_failure_exception_types_are_removed_from_application()
    {
        var typeNames = typeof(PagedResult<>).Assembly.GetTypes().Select(type => type.Name).ToHashSet();

        Assert.DoesNotContain("EntityNotFoundException", typeNames);
        Assert.DoesNotContain("BusinessRuleException", typeNames);
        Assert.DoesNotContain("IdempotencyConflictException", typeNames);
    }

    private static (ApiExceptionHandler Handler, CapturingLogger Logger) CreateHandler()
    {
        var logger = new CapturingLogger();
        var handler = new ApiExceptionHandler(logger, new StaticLocalizer());
        return (handler, logger);
    }

    private static DefaultHttpContext CreateHttpContext(string correlationId)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ICorrelationContext>(new CorrelationContext { CorrelationId = correlationId });

        return new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider(),
            Response = { Body = new MemoryStream() }
        };
    }

    private static string ReadBody(HttpContext httpContext)
    {
        httpContext.Response.Body.Position = 0;
        using var reader = new StreamReader(httpContext.Response.Body, Encoding.UTF8, leaveOpen: true);
        return reader.ReadToEnd();
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

    private sealed class StaticLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, name);

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }

    private sealed class CapturingLogger : ILogger<ApiExceptionHandler>
    {
        public List<(LogLevel Level, Exception? Exception, string Message)> Entries { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add((logLevel, exception, formatter(state, exception)));
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose()
            {
            }
        }
    }
}
