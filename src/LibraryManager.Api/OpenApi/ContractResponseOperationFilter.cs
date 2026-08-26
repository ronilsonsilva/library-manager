using LibraryManager.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace LibraryManager.Api.OpenApi;

public sealed class ContractResponseOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var path = "/" + (context.ApiDescription.RelativePath ?? string.Empty);
        var method = context.ApiDescription.HttpMethod ?? string.Empty;

        AddProblem(operation, "401", "Missing or invalid access token");

        if (HasLibrarianPolicy(context))
        {
            AddProblem(operation, "403", "Authenticated caller lacks librarian role");
        }

        if (path.Contains("{id}", StringComparison.OrdinalIgnoreCase))
        {
            AddProblem(operation, "404", "Named book, User, or loan does not exist");
        }

        var isLoanCreate = method.Equals("POST", StringComparison.OrdinalIgnoreCase)
            && path.Equals("/loans", StringComparison.OrdinalIgnoreCase);
        if (isLoanCreate)
        {
            AddJson(operation, "201", "Loan created, or idempotent replay of a prior successful create");
            AddProblem(operation, "400", "Missing Idempotency-Key or invalid body");
            AddProblem(operation, "404", "Unknown UserId or BookId");
            AddProblem(operation, "409", "Idempotency-Key reused with a different canonical request");
            AddProblem(operation, "422", "Book inactive, no copies, or User already has Active loan for the book");
            return;
        }

        if (method.Equals("POST", StringComparison.OrdinalIgnoreCase)
            && (path.Contains("/return", StringComparison.OrdinalIgnoreCase)
                || path.Contains("/cancel", StringComparison.OrdinalIgnoreCase)))
        {
            AddProblem(operation, "422", "Loan is not Active");
            return;
        }

        if (method.Equals("POST", StringComparison.OrdinalIgnoreCase)
            || method.Equals("PUT", StringComparison.OrdinalIgnoreCase))
        {
            AddProblem(operation, "400", "Validation or domain problem");
            AddProblem(operation, "422", "Business-rule failure");
        }

        if (method.Equals("POST", StringComparison.OrdinalIgnoreCase)
            && !path.Contains("/return", StringComparison.OrdinalIgnoreCase)
            && !path.Contains("/cancel", StringComparison.OrdinalIgnoreCase))
        {
            AddJson(operation, "201", "Created");
        }
    }

    private static bool HasLibrarianPolicy(OperationFilterContext context) =>
        context.ApiDescription.ActionDescriptor.EndpointMetadata
            .OfType<AuthorizeAttribute>()
            .Any(attribute => string.Equals(attribute.Policy, LibrarianPolicy.Name, StringComparison.Ordinal));

    private static void AddProblem(OpenApiOperation operation, string status, string description)
    {
        operation.Responses.TryAdd(status, new OpenApiResponse
        {
            Description = description,
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["application/problem+json"] = new()
                {
                    Schema = ProblemSchema()
                }
            }
        });
    }

    private static void AddJson(OpenApiOperation operation, string status, string description)
    {
        operation.Responses.TryAdd(status, new OpenApiResponse
        {
            Description = description
        });
    }

    private static OpenApiSchema ProblemSchema() =>
        new()
        {
            Type = "object",
            Properties = new Dictionary<string, OpenApiSchema>
            {
                ["type"] = new() { Type = "string" },
                ["title"] = new() { Type = "string" },
                ["status"] = new() { Type = "integer" },
                ["detail"] = new() { Type = "string" },
                ["instance"] = new() { Type = "string" },
                ["traceId"] = new() { Type = "string" },
                ["correlationId"] = new() { Type = "string" }
            }
        };
}
