# Research: Production Hardening

## Result pattern in Domain, not a mediator

- **Decision**: Place framework-neutral `Result`, `Result<T>`, `Error`, and `ErrorType` in `LibraryManager.Domain`. UseCases return `Result`/`Result<T>`. The API maps them with one extension (`ToActionResult` / created overload). No CQRS, MediatR, Command, Query, or Handler types.
- **Rationale**: Constitution II and IX. Domain factories must return Result without depending on Application or ASP.NET Core. Expected failures stay values.
- **Alternatives considered**: FluentResults/ErrorOr packages (extra framework). Application-only Result (Domain factories could not return it). Keep throwing `DomainException`/`BusinessRuleException` into `IExceptionHandler` (forbidden: expected failures must not become exceptions).

## HTTP 400 vs 422 vs 409 mapping

- **Decision**: `ErrorType.Validation` → 400, `NotFound` → 404, `BusinessRule` → 422, `Conflict` → 409. Transport ModelState failures are 400 via `[ApiController]`, not Result. HTTP 409 remains only Idempotency-Key canonical mismatch (`Idempotency.PayloadMismatch`).
- **Rationale**: Spec FR-024 and 001 HTTP contract. Transport input is not a Domain validation Result.
- **Alternatives considered**: Map Domain validation to 422 (rejected: spec maps Validation → 400). Map missing Idempotency-Key through Result (rejected: must fail in model binding before `CreateLoanUseCase`).

## Idempotency-Key custom binder, not IParsable

- **Decision**: Readonly `IdempotencyKey` in `LibraryManager.Api/Contracts/Common`. `IdempotencyKeyModelBinder` implements `IModelBinder`, reads `Idempotency-Key`, writes localized ModelState, never throws for expected validation, never swallows `OperationCanceledException`. `FromIdempotencyKeyAttribute` : `ModelBinderAttribute`. Do not implement `IParsable<IdempotencyKey>` as the validation path.
- **Rationale**: Missing headers and localized ModelState errors are binding concerns. `IParsable` cannot express “header absent” vs “unparsable string” with resource-backed messages.
- **Alternatives considered**: `[FromHeader] string?` plus controller checks (current; forbidden). Middleware/filter (forbidden). `IParsable` plus `[Required]` on the parameter (forbidden on the action parameter).

## API contracts vs Application DTOs

- **Decision**: Move/create HTTP request and response types under `LibraryManager.Api/Contracts/<Feature>/Requests|Responses`. Keep Application DTOs (`BookDto`, `LoanDto`, …) unchanged. Controllers map DTO → `*Response`. JSON property names stay identical to today’s contract so existing clients and tests remain compatible except where transport validation is explicitly changed.
- **Rationale**: Constitution VI. User instruction: do not churn Application DTOs merely to relocate HTTP types.
- **Alternatives considered**: Return Application DTOs from controllers (rejected: those types would remain the HTTP contract). Duplicate Domain entities as API types (unnecessary).

## Localization requires disabling invariant globalization on the API host

- **Decision**: Override `InvariantGlobalization` to `false` on `LibraryManager.Api` (and integration tests that host it). Keep Domain/Application/Infrastructure invariant. Use `AddLocalization`, `AddDataAnnotationsLocalization`, `RequestLocalization` with `en-US` (default) and `pt-BR`, `Accept-Language`, and `Content-Language` on responses. Resources: `SharedResource.resx` (neutral/en-US fallback) plus `SharedResource.en-US.resx` and `SharedResource.pt-BR.resx`.
- **Rationale**: Repository `Directory.Build.props` currently sets `InvariantGlobalization=true`, which prevents culture and satellite resources. Localization is an API-boundary concern (Constitution XI).
- **Alternatives considered**: Keep invariant globalization (makes `.resx` and `Accept-Language` ineffective). Enable globalization on every project (unnecessary for Domain).

## IExceptionHandler only for unexpected failures

- **Decision**: Refactor `ApiExceptionHandler` so it no longer maps `EntityNotFoundException`, `BusinessRuleException`, `IdempotencyConflictException`, or `DomainException` to HTTP. Those paths become Result mapping. Unexpected exceptions yield a generic localized problem (`Problem_Unexpected_Title`) with `correlationId`, no stack, no `exception.Message`, no connection strings/SQL/Redis details. Log the exception with a stable English template. Do not handle `OperationCanceledException` (return `false`).
- **Rationale**: Constitution XII. Current handler uses `exception.Message` as `Detail` and treats expected Application exceptions as HTTP outcomes.
- **Alternatives considered**: Keep exception types as the HTTP bridge (forbidden). Developer exception page in Production (leaks internals).

## Cache resilience decorator

- **Decision**: Keep `IAvailabilityCache` in Application. Keep `RedisAvailabilityCache` as the Redis client. Register `ResilientAvailabilityCacheDecorator` as `IAvailabilityCache` wrapping the concrete cache. Catch `RedisException` (and related StackExchange.Redis connection/timeout types), not `Exception`. GET → null (miss); SET/REMOVE → log warning and do not throw; REMOVE also calls `ILibraryManagerMetrics.RecordCacheInvalidationFailure` (`library_manager_cache_invalidation_failures`). Always rethrow `OperationCanceledException`. Decorator unit tests MUST assert the metric on REMOVE failure. Delete `AvailabilityCacheInvalidation.TryRemoveAsync`. UseCases call `RemoveAsync`/`GetAsync`/`SetAsync` directly.
- **Rationale**: Constitution Cache + FR-034/036. Application currently uses `catch (Exception)` around Redis.
- **Alternatives considered**: Polly in Application (wrong layer). Catch `Exception` in the decorator (too broad). Keep TryRemove in Application (forbidden).

## Redis observability without prerelease instrumentation

- **Decision**: Remove `OpenTelemetry.Instrumentation.StackExchangeRedis` (1.12.0-beta.2). Start `LibraryManager` `ActivitySource` activities around cache Get/Set/Remove named `availability_cache.get`, `availability_cache.set`, and `availability_cache.remove`. Keep `library_manager_cache_invalidation_failures`. Do not add another prerelease Redis instrumentation package. Align remaining OpenTelemetry 1.12.x packages if compatible upgrades exist. Automated tests MUST assert those activity names (unit) and keep the invalidation metric (existing `ObservabilityTests` plus decorator unit tests).
- **Rationale**: Constitution Dependency Security and FR-006. `DockerStackTests` currently asserts the beta package is present; invert that assertion. Package absence alone does not prove traces remain.
- **Alternatives considered**: Keep the beta package (forbidden when own ActivitySource can cover it). Add a different prerelease Redis exporter (forbidden). Rely only on a package-absent assertion (insufficient).

## NuGet audit as build errors

- **Decision**: In `Directory.Build.props`: `NuGetAudit=true`, `NuGetAuditMode=all`, treat NU1903 and NU1904 as errors. Remove `NU1903;NU1904` from `WarningsNotAsErrors` (may keep NU1901/NU1902 as non-errors). Audit with `dotnet package list --vulnerable --include-transitive`. Prefer upgrading the direct parent. Do not add blanket `NoWarn` for NU1903/NU1904.
- **Rationale**: Spec FR-001–FR-004. Current props demote all NU190x to warnings.
- **Alternatives considered**: `NuGetAudit=false` (forbidden). Suppress NU1903 globally (forbidden).

## Domain validation helper scope

- **Decision**: Add a small Domain `Validation`/`DomainGuard` helper for required strings, non-empty Guids, positive numbers, and UTC timestamps. `AuditEvent.Create` returns `Result<AuditEvent>`. Apply the same pattern to other Domain factories/state changes whose current `DomainException` is expected caller-visible validation (`Book.Create`/`Update`, `User.Create`, `Loan.Create`/`MarkReturned`/`MarkCancelled`) because UseCases already treat those as expected HTTP 400/422. Do not rewrite infrastructure or truly impossible-state exceptions.
- **Rationale**: Spec FR-026/FR-027 plus “do not mechanically rewrite every exception.”
- **Alternatives considered**: Only change `AuditEvent.Create` (leaves Book/User/Loan throwing into HTTP). FluentValidation package (extra framework; Domain must stay infrastructure-free).

## Keycloak Direct Access Grants

- **Decision**: Set `directAccessGrantsEnabled` to `false` on `library-manager-swagger` (API client is already false). Remove README/001-quickstart password-grant curl. `dotnet test` proves SC-010 by (1) asserting every client in `library-manager-realm.json` has DAG false (Keycloak rejects password grant when that realm is imported) and (2) asserting no Keycloak token-endpoint ROPC remains in README, 001 quickstart, or tests. `AuthorizationTests.Api_does_not_issue_tokens` may still POST `grant_type=password` to **this API** (`/connect/token`, etc.) expecting 404. Do not add Testcontainers Keycloak. Optional operator curl against Compose Keycloak may live only in `specs/002-production-hardening/quickstart.md`.
- **Rationale**: Spec FR-047–FR-049 and SC-010. Password grant on the public Swagger client is the remaining DAG enablement. CI cannot assume a live Keycloak process.
- **Alternatives considered**: Leave DAG true “for local smoke” (forbidden). Use ROPC in tests instead of TestAuth (forbidden). Require live Keycloak in `dotnet test` (not CI-testable).

## SQL parameterization

- **Decision**: Production SQL stays on `ExecuteSqlInterpolatedAsync` (already parameterized). Do not rewrite those calls. `ExecuteSqlRawAsync` in tests may remain only with static SQL and no runtime concatenation. Ban `FromSqlRaw`/`ExecuteSqlRaw` with interpolated user input.
- **Rationale**: Constitution XIII. Inventory found no production Raw SQL with runtime values.
- **Alternatives considered**: Convert Interpolated to Raw+NpgsqlParameter for style (forbidden churn).
