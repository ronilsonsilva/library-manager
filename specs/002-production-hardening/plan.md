# Implementation Plan: Production Hardening

**Branch**: `002-production-hardening` | **Date**: 2026-08-26 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/002-production-hardening/spec.md`

**Note**: This template is filled in by the `/speckit-plan` command; its definition describes the execution workflow.

## Summary

Harden `library-manager` without changing last-copy lending, durable PostgreSQL idempotency, audit/Outbox pairing, or JWT resource-server behavior. Move HTTP transport types into `LibraryManager.Api/Contracts`, bind `Idempotency-Key` with a custom model binder, validate request bodies with `[ApiController]` + DataAnnotations, replace expected Domain/Application exceptions with `Result`/`Result<T>`, localize user-facing HTTP text (`en-US`, `pt-BR`), restrict `IExceptionHandler` to unexpected failures, isolate Redis failures behind `ResilientAvailabilityCacheDecorator`, fail the build on NU1903/NU1904, disable Keycloak Direct Access Grants, and keep parameterized SQL.

No CQRS, Commands, Queries, Handlers, MediatR, or Generic Repository. Application still calls Redis only through `IAvailabilityCache`.

## Technical Context

**Language/Version**: C# / .NET 10 (`net10.0`)

**Primary Dependencies**: ASP.NET Core Web API (`[ApiController]`, `IExceptionHandler`, `IModelBinder`, `RequestLocalization`, `IStringLocalizer`); Entity Framework Core 10 / Npgsql; StackExchange.Redis; JwtBearer; Swashbuckle (Authorization Code + PKCE); OpenTelemetry 1.12.x stable packages only (remove `OpenTelemetry.Instrumentation.StackExchangeRedis`)

**Storage**: PostgreSQL remains system of record; Redis remains optional availability cache-aside

**Testing**: xUnit; `WebApplicationFactory`; Testcontainers PostgreSQL and Redis; architecture/file assertions for contracts and realm JSON; existing TestAuth scheme (no ROPC)

**Target Platform**: Linux containers (Compose locally; Kubernetes unchanged except that this feature does not add Keycloak)

**Project Type**: web-service (existing Clean Architecture solution)

**Performance Goals**: Unchanged last-copy correctness under concurrency and 2–11 replicas; availability cache TTL remains 60 seconds; GET availability remains correct when Redis is down

**Constraints**: No CQRS/MediatR/Generic Repository; no Application Redis `try/catch`; no `IParsable`-only Idempotency-Key path; no ModelState inspection in controllers; no expected Result → exception conversion; no stack traces in HTTP; no ROPC; no blanket NuGet audit suppression; no rewrite of safe `ExecuteSqlInterpolatedAsync`; `CancellationToken` always propagated; English log templates and error codes

**Scale/Scope**: Same four production projects and two test projects; additive Api Contracts/ModelBinding/Localization folders; Domain Result primitives; Infrastructure cache decorator; Directory.Build.props audit; Keycloak realm + README

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Gate | Result | Evidence |
|------|--------|----------|
| I. English nomenclature and required project names | PASS | No new projects; English type/resource key names; localized *text* only at API boundary |
| II. Clean Architecture, UseCases, no CQRS/MediatR/Generic Repository | PASS | Explicit UseCases retained; Result is a value type, not a mediator |
| III. PostgreSQL-owned correctness; AuditEvent same transaction | PASS | Lending SQL and transactions unchanged; `DeactivateBook` still commits mutation + audit + Outbox together |
| IV. Durable PostgreSQL idempotency | PASS | Binder only normalizes the key; uniqueness remains `IdempotencyEntry` |
| V. Transactional Outbox | PASS | `BookAvailabilityChanged` still same transaction; decorator failures leave Outbox retry |
| VI. API contracts | PASS | `Contracts/<Feature>/Requests\|Responses`; no transport types in controller files |
| VII. HTTP input validation | PASS | `[ApiController]` + DataAnnotations; no controller ModelState inspection |
| VIII. Strongly typed HTTP metadata | PASS | `IdempotencyKey` + `IModelBinder` + `FromIdempotencyKeyAttribute` |
| IX. Expected failures as Result | PASS | Domain/Application Result; HTTP map 400/404/422/409 |
| X. Domain validation | PASS | Reusable Domain rules; no ASP.NET/localization in Domain |
| XI. Localization | PASS | `en-US`/`pt-BR`; API-only; codes/logs stay English |
| XII. IExceptionHandler unexpected-only | PASS | Result mapping separate; cancellation not swallowed |
| XIII. SQL safety | PASS | Keep parameterized Interpolated SQL; no concatenation |
| Cache resilience decorator | PASS | `ResilientAvailabilityCacheDecorator` around `RedisAvailabilityCache` |
| Security / Keycloak PKCE | PASS | DAG disabled; Swagger PKCE retained; no ROPC docs/tests |
| Dependency security | PASS | `NuGetAuditMode=all`; NU1903/NU1904 errors |
| Quality: async, Problem Details, tests | PASS | Existing suites plus hardening tests |

**Post-design re-check (Phase 1):** PASS. `data-model.md` adds Result/Error/`IdempotencyKey` without moving Outbox/idempotency into Domain. `contracts/` documents ValidationProblemDetails, Result problem `code`, Accept-Language, and unchanged 201/409/422/404 lending semantics. `quickstart.md` validates localization, binder 400s, Redis fallback, PKCE-only auth, and regression tests. `InvariantGlobalization=false` is limited to the API host so `.resx` works. No new constitution violations.

## Project Structure

### Documentation (this feature)

```text
specs/002-production-hardening/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── error-codes.md
│   └── openapi.yaml
├── checklists/
│   ├── requirements.md
│   └── hardening.md
└── tasks.md
```

### Source Code (repository root)

```text
Directory.Build.props          # NuGetAudit=true; NuGetAuditMode=all; NU1903/NU1904 as errors
README.md                      # localization, Result, binder, cache, audit, SQL, PKCE-only

src/LibraryManager.Domain/
  Result.cs
  ResultT.cs
  Error.cs
  ErrorType.cs
  ErrorCodes.cs
  Validation/DomainGuard.cs
  AuditEvent.cs                # Create → Result<AuditEvent>
  Book.cs / User.cs / Loan.cs  # expected validation → Result where in scope
  DomainException.cs           # retained only for genuinely exceptional states

src/LibraryManager.Application/
  Abstractions/IAvailabilityCache.cs   # unchanged surface
  Books/GetBookAvailability/GetBookAvailabilityUseCase.cs  # no Redis try/catch
  Books/DeactivateBook/DeactivateBookUseCase.cs            # Result; RemoveAsync after commit
  Loans/CreateLoan/CreateLoanUseCase.cs                    # Result<LoanDto>
  Common/AvailabilityCacheInvalidation.cs                  # DELETE (logic moves to decorator)
  Common/*Exception.cs                                     # retire expected-exception types after UseCase migration

src/LibraryManager.Infrastructure/
  Caching/RedisAvailabilityCache.cs
  Caching/ResilientAvailabilityCacheDecorator.cs
  DependencyInjection.cs       # decorator as IAvailabilityCache; concrete Redis registered for wrap
  Persistence/Repositories/    # keep ExecuteSqlInterpolatedAsync
  Idempotency/IdempotencyStore.cs

src/LibraryManager.Api/
  Program.cs                   # localization pipeline; exception handler; no Redis OTel package
  Contracts/Common/IdempotencyKey.cs
  Contracts/Common/PagedResponse.cs
  Contracts/Books/Requests/CreateBookRequest.cs
  Contracts/Books/Requests/UpdateBookRequest.cs
  Contracts/Books/Responses/BookResponse.cs
  Contracts/Books/Responses/BookAvailabilityResponse.cs
  Contracts/Users/Requests/CreateUserRequest.cs
  Contracts/Users/Responses/UserResponse.cs
  Contracts/Loans/Requests/CreateLoanRequest.cs
  Contracts/Loans/Responses/LoanResponse.cs
  Contracts/Audit/Responses/AuditEventResponse.cs
  ModelBinding/IdempotencyKey/IdempotencyKeyModelBinder.cs
  ModelBinding/IdempotencyKey/FromIdempotencyKeyAttribute.cs
  Results/ResultHttpMapper.cs  # ToActionResult / ToCreatedResult; localize here
  Localization/SharedResource.cs
  Localization/ErrorLocalizer.cs
  Localization/LocalizationConfiguration.cs
  Resources/SharedResource.resx
  Resources/SharedResource.en-US.resx
  Resources/SharedResource.pt-BR.resx
  Errors/ApiExceptionHandler.cs
  Controllers/*.cs             # routing, auth, binding, use case, map result only
  Telemetry/OpenTelemetryConfiguration.cs  # drop AddRedisInstrumentation

tests/LibraryManager.UnitTests/
  Domain/ResultTests.cs
  Domain/DomainGuardTests.cs
  Domain/AuditEventTests.cs    # expected validation does not throw
  Api/IdempotencyKeyModelBinderTests.cs
  Infrastructure/RedisCacheActivityTests.cs
  Infrastructure/ResilientAvailabilityCacheDecoratorTests.cs

tests/LibraryManager.IntegrationTests/
  Loans/IdempotencyKeyBindingTests.cs
  Localization/AcceptLanguageTests.cs
  Errors/UnexpectedExceptionTests.cs
  Caching/CacheResilienceTests.cs
  Security/KeycloakRealmImportTests.cs  # every client DAG false; no Keycloak ROPC in README/001/tests
  Architecture/ControllerContractLocationTests.cs  # no controller records; no PagedResult HTTP returns
  Telemetry/ObservabilityTests.cs  # keep library_manager_cache_invalidation_failures

infrastructure/keycloak/library-manager-realm.json  # swagger directAccessGrantsEnabled=false
```

**Structure Decision**: Extend the existing constitution layout. HTTP types move into Api Contracts. Result primitives live in Domain so factories can return them. Redis failure handling is Infrastructure-only via Decorator Pattern.

## Complexity Tracking

No constitution violations. Result types, the cache decorator, and custom model binding are required by Constitution IX, Cache, and VIII — not optional framework sprawl.

## Architecture and Runtime Design

### Layer rules

- **Domain**: entities plus `Result`/`Error`/`ErrorType`/`ErrorCodes`/`DomainGuard`. No ASP.NET, localization, EF, Redis, or OpenTelemetry.
- **Application**: UseCases return `Result`/`Result<T>`. No `HttpContext`, no `IStringLocalizer`, no Redis exception recovery. `IAvailabilityCache` unchanged.
- **Infrastructure**: `RedisAvailabilityCache` talks to Redis; `ResilientAvailabilityCacheDecorator` implements `IAvailabilityCache` and is the type registered for Application. SQL stays parameterized Interpolated APIs.
- **Api**: Contracts, binder, localization, Result HTTP mapping, unexpected `IExceptionHandler`. Controllers do not declare records and do not inspect ModelState.

Forbidden names/types remain: Command, Query, Handler, `IRequest`, `IRequestHandler`, mediator, MediatR, Generic Repository.

### HTTP contracts

| Current (in controller files) | Target |
|-------------------------------|--------|
| `CreateBookRequest`, `UpdateBookRequest` | `Contracts/Books/Requests/` |
| `CreateUserRequest` | `Contracts/Users/Requests/` |
| `CreateLoanRequest` | `Contracts/Loans/Requests/` |
| `ActionResult<PagedResult<BookDto>>` (list books) | `ActionResult<PagedResponse<BookResponse>>` |
| `ActionResult<PagedResult<LoanDto>>` (book history, user loans) | `ActionResult<PagedResponse<LoanResponse>>` |
| `ActionResult<PagedResult<AuditEventDto>>` | `ActionResult<PagedResponse<AuditEventResponse>>` |
| (none for audit item) | `Contracts/Audit/Responses/AuditEventResponse.cs` |
| Application DTOs / `PagedResult<T>` returned as JSON | Map item DTOs to `*Response`; map pages to `PagedResponse<TResponse>` in `Contracts/Common/PagedResponse.cs`. Controllers MUST NOT return Application types. JSON remains `items`, `page`, `pageSize`, `totalCount`. |

Request contracts use DataAnnotations (`[Required]`, `[StringLength]`, `[Range]`) with resource-backed messages. Controllers:

```csharp
[HttpPost]
[Authorize(Policy = LibrarianPolicy.Name)]
public async Task<ActionResult<LoanResponse>> Create(
    [FromIdempotencyKey] IdempotencyKey idempotencyKey,
    CreateLoanRequest request,
    CancellationToken cancellationToken)
{
    var result = await createLoan.ExecuteAsync(
        request.BookId,
        request.UserId,
        idempotencyKey.Value,
        cancellationToken);

    return result.ToCreatedResult(this);
}
```

No `[Required]`/`[StringLength]` on `idempotencyKey`. No ModelState checks.

### Idempotency-Key binder

`IdempotencyKey` is a readonly API struct/class with normalized `Value` (trimmed, length 1–128). It is HTTP metadata, not a Domain type. Use case still receives `string` (`idempotencyKey.Value`).

Binder reads `HttpRequest.Headers["Idempotency-Key"]`:

| Input | ModelState key | HTTP |
|-------|----------------|------|
| missing | `Validation_IdempotencyKey_Required` | 400, use case not run |
| empty / whitespace | `Validation_IdempotencyKey_Required` | 400 |
| length > 128 | `Validation_IdempotencyKey_MaxLength` | 400 |
| length = 128 valid charset/content | trim (no-op if none) and bind | action runs |
| surrounding whitespace, otherwise valid | trim, bind | action runs |

Binder must not throw for those cases and must not catch `OperationCanceledException`. `[ApiController]` automatic 400 remains enabled. Configure `InvalidModelStateResponseFactory` so ValidationProblemDetails use `Problem_Validation_Title` and include `correlationId`.

Durable idempotency (unique Endpoint+Key, SHA-256 canonical body, 201 replay, 409 mismatch, rollback) is unchanged after the trimmed key is passed in.

### Result and HTTP mapping

`Error` fields: `Code` (stable English, e.g. `Book.NotFound`), `Type` (`Validation` / `NotFound` / `BusinessRule` / `Conflict`), `Arguments` (optional format args, not localized strings).

One Api mapper (`ResultHttpMapper` / `ToActionResult` / `ToCreatedResult` / `ToNoContentResult`):

- Success: 200 / 201 / 204 as the action specifies; body is the API response type.
- Failure: localize `Code` via `ErrorLocalizer` → problem `detail`; title from `Problem_*` resources; `extensions.code` = `Error.Code`; `extensions.correlationId`.
- Never `throw` to reach `IExceptionHandler`.

Retire Application `EntityNotFoundException`, `BusinessRuleException`, and `IdempotencyConflictException` from UseCase success paths once mapped.

### Domain validation

`DomainGuard` (or equivalent) collects errors then returns `Result`/`Result<T>`. `AuditEvent.Create` returns `Result<AuditEvent>` with codes `Audit.EntityTypeRequired`, `Audit.EntityIdRequired`, and the other required-field codes. UseCases propagate `Result` from `Create` instead of assuming a thrown `DomainException`.

Apply the same pattern to Book/User/Loan expected validation used by HTTP flows. Do not rewrite Redis/EF/Outbox infrastructure exceptions.

### Localization

`Program.cs`:

1. `AddLocalization` + `AddDataAnnotationsLocalization` (SharedResource)
2. `RequestLocalizationOptions`: default `en-US`; supported `en-US`, `pt-BR`; `AcceptLanguageHeaderRequestCultureProvider`
3. Pipeline: CorrelationId → `UseRequestLocalization` → `UseExceptionHandler` → auth → endpoints
4. Set `Content-Language` to the resolved culture on API responses (middleware or localization option)

`LibraryManager.Api.csproj`: `InvariantGlobalization=false` so resources load. Resource keys include the spec list (`Validation_IdempotencyKey_Required`, `Validation_IdempotencyKey_MaxLength`, `Validation_Title_Required`, `Validation_Isbn_Required`, `Validation_TotalCopies_Range`, `Error_Book_NotFound`, `Error_Book_Unavailable`, `Error_Loan_InvalidState`, `Error_Idempotency_Conflict`, `Problem_Validation_Title`, `Problem_Unexpected_Title`) plus additional `Error_*` keys matching `ErrorCodes` as UseCases migrate.

Logs, metric names, trace ids, and `Error.Code` stay English.

### Unexpected exception handling

`ApiExceptionHandler` handles only unexpected exceptions:

- HTTP 500 problem: localized `Problem_Unexpected_Title`, generic detail (no exception text), `correlationId`
- Log: English template + exception object (operators still see stack in logs, not in HTTP)
- Return `false` for `OperationCanceledException`
- Do not serialize connection strings, SQL, Redis endpoints, or stack traces

### Cache resilience and availability

Registration:

```text
Application  → IAvailabilityCache
Infrastructure → ResilientAvailabilityCacheDecorator → RedisAvailabilityCache
```

Decorator:

- `GetAsync`: propagate cancellation; on Redis infra failure log warning and return null
- `SetAsync`: propagate cancellation; on failure log warning; do not throw to the UseCase
- `RemoveAsync`: propagate cancellation; on failure log a structured English warning, call `ILibraryManagerMetrics.RecordCacheInvalidationFailure`, and do not throw
- Catch Redis infrastructure exceptions (`RedisException` and connection/timeout subtypes), not `Exception`

`GetBookAvailabilityUseCase`: get cache → if present return; else PostgreSQL; `SetAsync`; return. No Redis catch.

`DeactivateBookUseCase` (and other availability mutations): mutate + `AuditEvent` Result + `BookAvailabilityChanged` Outbox in one PostgreSQL transaction; after commit `await cache.RemoveAsync`. Immediate Redis failure does not fail the HTTP success. Outbox remains durable retry.

Start `LibraryManager` activities around cache Get/Set/Remove after removing the StackExchangeRedis OTel package (`availability_cache.get`, `availability_cache.set`, `availability_cache.remove`). Unit tests must assert those activities are created. Keep `library_manager_cache_invalidation_failures`. Resolve `RedisAvailabilityCache` as the inner type so any remaining connection hook is not looking at the decorator as `IAvailabilityCache`.

### SQL safety

Audit remaining `ExecuteSql*`, `FromSql*`. Production inventory is `ExecuteSqlInterpolatedAsync` in `BookRepository`, `LoanRepository`, and `IdempotencyStore` — leave them. Add a test or analyzer-style assertion that production source does not concatenate runtime values into Raw SQL. Test helper `ExecuteSqlRawAsync` with static SQL and no parameters may remain.

### Dependency security

`Directory.Build.props`:

- `NuGetAudit=true`
- `NuGetAuditMode=all`
- Remove `NU1903` and `NU1904` from `WarningsNotAsErrors` (with `TreatWarningsAsErrors=true` they fail the build)
- Do not set `NuGetAudit=false` and do not `NoWarn` NU1903/NU1904

Remediate with compatible upgrades of direct parents. Remove `OpenTelemetry.Instrumentation.StackExchangeRedis`. Update `DockerStackTests` to assert that package is absent. Run `dotnet package list --vulnerable --include-transitive` during implementation; document residual findings only with explicit risk treatment (none expected if upgrades exist).

### Keycloak

- `library-manager-swagger.directAccessGrantsEnabled = false` (API client already false)
- Keep standard flow, PKCE S256, `library-manager-swagger`, librarian role, JWT resource server
- Delete password-grant curl from README and `specs/001-library-manager/quickstart.md`
- Tests (`dotnet test`): every realm client has DAG false; grep proves no Keycloak ROPC in docs/tests. Do **not** require Testcontainers Keycloak. API-path password-grant 404 probes remain allowed.
- Optional operator curl against Compose Keycloak lives only in this feature’s `quickstart.md`

### Testing

Preserve existing tests unless an assertion contradicts the new HTTP validation shape (missing Idempotency-Key is ValidationProblemDetails 400, not `DomainException` problem title).

Add coverage listed in the spec / user request: contract file location (including list/history `PagedResponse` not Application `PagedResult`), binder 400s (missing/empty/whitespace/129 vs 128, trim, no loan side effect), Accept-Language en-US/pt-BR, Result HTTP statuses, unexpected handler safety + correlationId, Redis GET/SET/REMOVE resilience, `LibraryManager` cache activities (`availability_cache.get` / `.set` / `.remove`), REMOVE failure → `library_manager_cache_invalidation_failures`, deactivation invalidation + Outbox, SQL parameterization assertion, Keycloak DAG in realm JSON (no live Keycloak in `dotnet test`), regression of last-copy, idempotency, return/cancel, Outbox, auth, health.

### Documentation

README sections: Accept-Language examples; Result + HTTP mapping; `[ApiController]` + binder; cache decorator; NuGet audit (NU1903/NU1904); SQL parameterization; Keycloak PKCE-only (no password grant).
