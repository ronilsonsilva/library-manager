---
description: "Task list for production-hardening implementation"
---

# Tasks: Production Hardening

**Input**: Design documents from `/specs/002-production-hardening/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: Included. Spec FR-051, US9, `plan.md`, and `quickstart.md` require unit and integration coverage for every hardening requirement. Write story tests first and ensure they fail before implementation.

**Organization**: Tasks are grouped by user story so each story can be implemented, tested, and demonstrated independently after Phase 2. No CQRS, MediatR, Command/Query/Handler, or Generic Repository work.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete work)
- **[Story]**: `US1`–`US9` on user-story phases only
- Every task includes an exact file path

## Path Conventions

- Production: `src/LibraryManager.Domain/`, `src/LibraryManager.Application/`, `src/LibraryManager.Infrastructure/`, `src/LibraryManager.Api/`
- Tests: `tests/LibraryManager.UnitTests/`, `tests/LibraryManager.IntegrationTests/`

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Folders and host settings localization needs before any story

- [X] T001 Create `src/LibraryManager.Api/Contracts/Common/`, `Contracts/Books/Requests/`, `Contracts/Books/Responses/`, `Contracts/Users/Requests/`, `Contracts/Users/Responses/`, `Contracts/Loans/Requests/`, `Contracts/Loans/Responses/`, `Contracts/Audit/Responses/`, `ModelBinding/IdempotencyKey/`, `Localization/`, and `Resources/` directories
- [X] T002 [P] Set `InvariantGlobalization` to `false` and include `Resources/*.resx` in `src/LibraryManager.Api/LibraryManager.Api.csproj`
- [X] T003 [P] Create test folders `tests/LibraryManager.UnitTests/Api/`, `tests/LibraryManager.UnitTests/Infrastructure/`, `tests/LibraryManager.IntegrationTests/Architecture/`, `tests/LibraryManager.IntegrationTests/Localization/`, and `tests/LibraryManager.IntegrationTests/Errors/`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Result primitives, DomainGuard, localization host, and HTTP Result mapper that every story uses

**CRITICAL**: No user story work can begin until this phase is complete

- [X] T004 [P] Implement `ErrorType` and `Error` (`Code`, `Type`, `Arguments`) in `src/LibraryManager.Domain/ErrorType.cs` and `src/LibraryManager.Domain/Error.cs`
- [X] T005 [P] Implement `Result` and `Result<T>` in `src/LibraryManager.Domain/Result.cs` and `src/LibraryManager.Domain/ResultT.cs`
- [X] T006 [P] Add stable English codes from `data-model.md` in `src/LibraryManager.Domain/ErrorCodes.cs`
- [X] T007 Implement `DomainGuard` (required string, non-empty Guid, positive int, UTC timestamp) in `src/LibraryManager.Domain/Validation/DomainGuard.cs`
- [X] T008 [P] Add `SharedResource` marker in `src/LibraryManager.Api/Localization/SharedResource.cs`
- [X] T009 [P] Create `src/LibraryManager.Api/Resources/SharedResource.resx`, `SharedResource.en-US.resx`, and `SharedResource.pt-BR.resx` with keys from `plan.md` (including `Validation_IdempotencyKey_Required`, `Validation_IdempotencyKey_MaxLength`, `Validation_Title_Required`, `Validation_Isbn_Required`, `Validation_TotalCopies_Range`, `Error_Book_NotFound`, `Error_Book_Unavailable`, `Error_Loan_InvalidState`, `Error_Idempotency_Conflict`, `Problem_Validation_Title`, `Problem_Unexpected_Title`)
- [X] T010 Implement `ErrorLocalizer` mapping `Error.Code` to `Error_*` resources in `src/LibraryManager.Api/Localization/ErrorLocalizer.cs`
- [X] T011 Configure `AddLocalization`, `AddDataAnnotationsLocalization`, `RequestLocalization` (`en-US` default, `pt-BR`, `Accept-Language`), and `Content-Language` in `src/LibraryManager.Api/Localization/LocalizationConfiguration.cs` and `src/LibraryManager.Api/Program.cs` (CorrelationId → RequestLocalization → ExceptionHandler)
- [X] T012 Implement `ToActionResult`, `ToCreatedResult`, and `ToNoContentResult` in `src/LibraryManager.Api/Results/ResultHttpMapper.cs` (map Validation/NotFound/BusinessRule/Conflict to 400/404/422/409; set `code` + `correlationId`; never throw)
- [X] T013 [P] Add unit tests for `Result`/`Error` in `tests/LibraryManager.UnitTests/Domain/ResultTests.cs`
- [X] T014 [P] Add unit tests for `DomainGuard` in `tests/LibraryManager.UnitTests/Domain/DomainGuardTests.cs`

**Checkpoint**: Foundation ready — user story implementation can begin

---

## Phase 3: User Story 1 - Keep shipped packages free of known high and critical flaws (Priority: P1) 🎯 MVP

**Goal**: Transitive NuGet audit on; NU1903/NU1904 fail the build; no blanket suppression; drop prerelease Redis OTel if own `ActivitySource` covers it.

**Independent Test**: `Directory.Build.props` has `NuGetAuditMode=all` and does not demote NU1903/NU1904; Api csproj has no `OpenTelemetry.Instrumentation.StackExchangeRedis`; cache Get/Set/Remove create `LibraryManager` activities `availability_cache.get` / `.set` / `.remove`; `dotnet package list --vulnerable --include-transitive` is clean or residual findings have documented treatment.

### Tests for User Story 1

> Write these tests FIRST and ensure they FAIL before implementation

- [X] T015 [P] [US1] Add failing file assertion for `NuGetAudit=true`, `NuGetAuditMode=all`, and NU1903/NU1904 as errors in `tests/LibraryManager.IntegrationTests/Architecture/NuGetAuditPropsTests.cs` reading `Directory.Build.props`
- [X] T016 [P] [US1] Change `OpenTelemetry.Instrumentation.StackExchangeRedis` assertion to require the package **absent** in `tests/LibraryManager.IntegrationTests/Security/DockerStackTests.cs`; add failing `ActivityListener` tests that cache Get/Set/Remove start `LibraryManager` activities named `availability_cache.get`, `availability_cache.set`, and `availability_cache.remove` in `tests/LibraryManager.UnitTests/Infrastructure/RedisCacheActivityTests.cs`

### Implementation for User Story 1

- [X] T017 [US1] Set `NuGetAudit=true`, `NuGetAuditMode=all`, and remove `NU1903;NU1904` from `WarningsNotAsErrors` in `Directory.Build.props` (do not globally disable auditing)
- [X] T018 [US1] Remove `OpenTelemetry.Instrumentation.StackExchangeRedis` and `AddRedisInstrumentation` from `src/LibraryManager.Api/LibraryManager.Api.csproj` and `src/LibraryManager.Api/Telemetry/OpenTelemetryConfiguration.cs`
- [X] T019 [US1] Start `LibraryManager` `ActivitySource` activities named `availability_cache.get`, `availability_cache.set`, and `availability_cache.remove` for cache Get/Set/Remove in `src/LibraryManager.Infrastructure/Caching/RedisAvailabilityCache.cs` (make T016 activity tests pass)
- [X] T020 [US1] Upgrade compatible OpenTelemetry stable packages if required in `src/LibraryManager.Api/LibraryManager.Api.csproj`
- [X] T021 [US1] Remediate remaining high/critical findings by upgrading direct parent packages listed by `dotnet nuget why` (edit the owning `*.csproj` under `src/` or `tests/`; no `NoWarn` for NU1903/NU1904)

**Checkpoint**: Dependency audit policy is independently verifiable

---

## Phase 4: User Story 2 - Reject invalid HTTP input before business work starts (Priority: P1)

**Goal**: Transport types live under `Contracts/`; Create Loan binds `IdempotencyKey`; body DataAnnotations; HTTP 400 ValidationProblemDetails; controllers do not inspect ModelState.

**Independent Test**: Missing/empty/whitespace/129-char Idempotency-Key → 400 and no loan; 128-char accepted; trim works; empty title → 400 before use case; controller files contain no transport records; list, loan-history, user-loans, and audit-list actions return API `PagedResponse<T>` (JSON `items`/`page`/`pageSize`/`totalCount`) and never Application `PagedResult<T>` or DTOs.

### Tests for User Story 2

- [X] T022 [P] [US2] Add failing architecture tests that `src/LibraryManager.Api/Controllers/*.cs` declare no `public sealed record` transport types and that list/history/audit actions do not return Application `PagedResult<T>` or Application DTOs as HTTP contracts in `tests/LibraryManager.IntegrationTests/Architecture/ControllerContractLocationTests.cs`
- [X] T023 [P] [US2] Add failing Idempotency-Key tests (missing, empty, whitespace, 129, 128, trim, no loan side effect) in `tests/LibraryManager.IntegrationTests/Loans/IdempotencyKeyBindingTests.cs`
- [X] T024 [P] [US2] Add failing body-validation tests (required title/ISBN, TotalCopies range) expecting HTTP 400 ValidationProblemDetails in `tests/LibraryManager.IntegrationTests/Books/BookBodyValidationTests.cs`
- [X] T025 [P] [US2] Add failing binder unit tests (missing/empty/max length/trim, no throw, no swallow of `OperationCanceledException`) in `tests/LibraryManager.UnitTests/Api/IdempotencyKeyModelBinderTests.cs`

### Implementation for User Story 2

- [X] T026 [P] [US2] Implement readonly `IdempotencyKey` with normalized `Value` in `src/LibraryManager.Api/Contracts/Common/IdempotencyKey.cs`
- [X] T027 [P] [US2] Implement `IModelBinder` (header `Idempotency-Key`, localized ModelState keys, no expected throws) in `src/LibraryManager.Api/ModelBinding/IdempotencyKey/IdempotencyKeyModelBinder.cs`
- [X] T028 [P] [US2] Implement `FromIdempotencyKeyAttribute` in `src/LibraryManager.Api/ModelBinding/IdempotencyKey/FromIdempotencyKeyAttribute.cs`
- [X] T029 [P] [US2] Add `CreateBookRequest` and `UpdateBookRequest` with DataAnnotations in `src/LibraryManager.Api/Contracts/Books/Requests/CreateBookRequest.cs` and `UpdateBookRequest.cs`
- [X] T030 [P] [US2] Add `CreateUserRequest` with DataAnnotations in `src/LibraryManager.Api/Contracts/Users/Requests/CreateUserRequest.cs`
- [X] T031 [P] [US2] Add `CreateLoanRequest` with DataAnnotations in `src/LibraryManager.Api/Contracts/Loans/Requests/CreateLoanRequest.cs`
- [X] T032 [P] [US2] Add `PagedResponse<T>` (`Items`, `Page`, `PageSize`, `TotalCount` → JSON `items`, `page`, `pageSize`, `totalCount`) in `src/LibraryManager.Api/Contracts/Common/PagedResponse.cs`; add `BookResponse` and `BookAvailabilityResponse` (map from Application DTOs; same JSON as today) in `src/LibraryManager.Api/Contracts/Books/Responses/BookResponse.cs` and `BookAvailabilityResponse.cs`
- [X] T033 [P] [US2] Add `UserResponse`, `LoanResponse`, and `AuditEventResponse` in `src/LibraryManager.Api/Contracts/Users/Responses/UserResponse.cs`, `Contracts/Loans/Responses/LoanResponse.cs`, and `Contracts/Audit/Responses/AuditEventResponse.cs`
- [X] T034 [US2] Configure `[ApiController]` `InvalidModelStateResponseFactory` for localized `Problem_Validation_Title` plus `correlationId` in `src/LibraryManager.Api/Localization/LocalizationConfiguration.cs`
- [X] T035 [US2] Update `src/LibraryManager.Api/Controllers/BooksController.cs` to use Contracts only (no local records, no ModelState inspection); `List` returns `PagedResponse<BookResponse>`; `GetLoanHistory` returns `PagedResponse<LoanResponse>` (map from Application `PagedResult<TDto>`)
- [X] T036 [US2] Update `src/LibraryManager.Api/Controllers/UsersController.cs` to use Contracts only; `GetLoans` returns `PagedResponse<LoanResponse>`
- [X] T037 [US2] Update `Create` in `src/LibraryManager.Api/Controllers/LoansController.cs` to `[FromIdempotencyKey] IdempotencyKey idempotencyKey`, pass `idempotencyKey.Value`, no `Required`/`StringLength` on the parameter, no manual key checks
- [X] T038 [US2] Update `src/LibraryManager.Api/Controllers/AuditEventsController.cs` to return `PagedResponse<AuditEventResponse>` (map from Application `PagedResult<AuditEventDto>`)
- [X] T039 [US2] Align existing missing-key assertions with ValidationProblemDetails in `tests/LibraryManager.IntegrationTests/Loans/CreateLoanTests.cs` and `tests/LibraryManager.IntegrationTests/Loans/IdempotencyTests.cs`

**Checkpoint**: HTTP transport validation is independently testable

---

## Phase 5: User Story 3 - Report expected failures as outcomes, not crashes (Priority: P1)

**Goal**: Expected Domain/Application failures use `Result`/`Result<T>`; HTTP 400/404/422/409 stay correct; `IExceptionHandler` is unexpected-only with safe Problem Details and `correlationId`.

**Independent Test**: Missing book → 404 with `code` `Book.NotFound` without unhandled exception; business rule → 422; idempotency mismatch → 409; `AuditEvent.Create` invalid fields return Result; forced unexpected exception → 500 without stack. Redis recovery try/catch is User Story 5, not this story.

### Tests for User Story 3

- [X] T040 [P] [US3] Extend `tests/LibraryManager.UnitTests/Domain/AuditEventTests.cs` so expected field validation returns `Result` and does not throw `DomainException`
- [X] T041 [P] [US3] Add failing Result HTTP mapping tests (404/422/409/`code` extension, not unhandled) in `tests/LibraryManager.IntegrationTests/Errors/ResultHttpMappingTests.cs`
- [X] T042 [P] [US3] Add failing unexpected-handler tests (generic 500, `correlationId`, no stack/SQL/Redis) in `tests/LibraryManager.IntegrationTests/Errors/UnexpectedExceptionTests.cs`

### Implementation for User Story 3

- [X] T043 [US3] Change `AuditEvent.Create` to `Result<AuditEvent>` via `DomainGuard` in `src/LibraryManager.Domain/AuditEvent.cs`
- [X] T044 [P] [US3] Change expected `Book` factory/update validation to `Result`/`Result<Book>` in `src/LibraryManager.Domain/Book.cs`
- [X] T045 [P] [US3] Change expected `User.Create` validation to `Result<User>` in `src/LibraryManager.Domain/User.cs`
- [X] T046 [P] [US3] Change expected `Loan.Create` / `MarkReturned` / `MarkCancelled` validation to `Result` in `src/LibraryManager.Domain/Loan.cs`
- [X] T047 [US3] Propagate `Result` from book UseCases in `src/LibraryManager.Application/Books/` (`CreateBookUseCase.cs`, `GetBookUseCase.cs`, `ListBooksUseCase.cs`, `UpdateBookUseCase.cs`, `DeactivateBookUseCase.cs`, `GetBookAvailabilityUseCase.cs`)
- [X] T048 [US3] Propagate `Result` from `src/LibraryManager.Application/Users/CreateUser/CreateUserUseCase.cs` and `GetUserLoans/GetUserLoansUseCase.cs`
- [X] T049 [US3] Propagate `Result` from loan UseCases including `Idempotency.PayloadMismatch` in `src/LibraryManager.Application/Loans/CreateLoan/CreateLoanUseCase.cs`, `ReturnLoan/ReturnLoanUseCase.cs`, `CancelLoan/CancelLoanUseCase.cs`, `GetBookLoanHistory/GetBookLoanHistoryUseCase.cs`, and `CompleteActiveLoan.cs`
- [X] T050 [US3] Propagate `Result` from `src/LibraryManager.Application/Audit/GetAuditEvents/GetAuditEventsUseCase.cs`
- [X] T051 [US3] Map all controller actions through `ResultHttpMapper` in `src/LibraryManager.Api/Controllers/BooksController.cs`, `UsersController.cs`, `LoansController.cs`, and `AuditEventsController.cs` (success bodies remain API `*Response` / `PagedResponse<T>`; never serialize Application DTOs or `PagedResult<T>`)
- [X] T052 [US3] Restrict `src/LibraryManager.Api/Errors/ApiExceptionHandler.cs` to unexpected failures (localized `Problem_Unexpected_Title`, no `exception.Message`, preserve `correlationId`, return `false` for `OperationCanceledException`)
- [X] T053 [US3] Remove expected `EntityNotFoundException` / `BusinessRuleException` / `IdempotencyConflictException` throw sites from `src/LibraryManager.Application/`
- [X] T054 [US3] Update Domain unit tests in `tests/LibraryManager.UnitTests/Domain/BookTests.cs`, `UserTests.cs`, and `LoanTests.cs` for Result instead of `DomainException`
- [X] T055 [US3] Update integration assertions that expected DomainException problem titles for 404/422/409 in `tests/LibraryManager.IntegrationTests/Books/`, `Users/`, `Loans/`, and `Security/`

**Checkpoint**: Expected failures are Results; unexpected failures are safe HTTP 500s

---

## Phase 6: User Story 5 - Keep availability correct when the fast cache is unavailable (Priority: P1)

**Goal**: Infrastructure decorator owns Redis failures; Application has no Redis recovery `try/catch`; GET availability still returns PostgreSQL data.

**Independent Test**: Redis down → GET `/books/{id}/availability` is catalog-correct; SET failure does not fail the read; REMOVE failure does not fail committed HTTP success and records `library_manager_cache_invalidation_failures`; cancellation still propagates; Application use cases have no Redis recovery try/catch.

### Tests for User Story 5

- [ ] T056 [P] [US5] Add failing unit tests for GET-miss / SET-non-fatal / REMOVE-non-fatal / cancellation / REMOVE failure calling `ILibraryManagerMetrics.RecordCacheInvalidationFailure` in `tests/LibraryManager.UnitTests/Infrastructure/ResilientAvailabilityCacheDecoratorTests.cs`
- [ ] T057 [P] [US5] Add failing integration tests for Redis-unavailable GET fallback and non-fatal SET in `tests/LibraryManager.IntegrationTests/Caching/CacheResilienceTests.cs`

### Implementation for User Story 5

- [ ] T058 [US5] Implement `ResilientAvailabilityCacheDecorator` catching Redis infrastructure exceptions (not `Exception`) in `src/LibraryManager.Infrastructure/Caching/ResilientAvailabilityCacheDecorator.cs`: GET miss on Redis failure; SET/REMOVE non-fatal; REMOVE logs a structured English warning and calls `ILibraryManagerMetrics.RecordCacheInvalidationFailure`; rethrow `OperationCanceledException`
- [ ] T059 [US5] Register `RedisAvailabilityCache` + decorator as `IAvailabilityCache` in `src/LibraryManager.Infrastructure/DependencyInjection.cs` (fix OTel/connection hooks to target the concrete cache)
- [ ] T060 [US5] Remove Redis `try/catch` from `src/LibraryManager.Application/Books/GetBookAvailability/GetBookAvailabilityUseCase.cs`
- [ ] T061 [US5] Delete `src/LibraryManager.Application/Common/AvailabilityCacheInvalidation.cs` and call `IAvailabilityCache.RemoveAsync` from `CreateLoanUseCase.cs`, `CompleteActiveLoan.cs`, `UpdateBookUseCase.cs`, and `DeactivateBookUseCase.cs`
- [ ] T062 [US5] Adjust `tests/LibraryManager.IntegrationTests/Caching/AvailabilityCacheTests.cs` and `tests/LibraryManager.IntegrationTests/Infrastructure/CallbackAvailabilityCache.cs` for decorator registration

**Checkpoint**: Cache misses and Redis outages no longer live in Application

---

## Phase 7: User Story 6 - Stop serving a stale available view after book deactivation (Priority: P1)

**Goal**: Successful `DeactivateBook` writes `BookAvailabilityChanged` in the same PostgreSQL transaction as mutation + `AuditEvent`, invalidates cache after commit, and does not leave a stale active cache value.

**Independent Test**: Cache an active availability, DELETE `/books/{id}`, then GET availability no longer shows the previous active cached value; Outbox row exists.

### Tests for User Story 6

- [ ] T063 [P] [US6] Add failing deactivation cache/Outbox tests in `tests/LibraryManager.IntegrationTests/Books/DeactivateBookCacheTests.cs`

### Implementation for User Story 6

- [ ] T064 [US6] Ensure `DeactivateBookUseCase` writes `BookAvailabilityChanged` via `IOutboxWriter` before `SaveChangesAsync` and `RemoveAsync` after commit in `src/LibraryManager.Application/Books/DeactivateBook/DeactivateBookUseCase.cs`
- [ ] T065 [US6] Confirm deactivated `isActive` is what GET availability returns after invalidation in `src/LibraryManager.Application/Books/GetBookAvailability/GetBookAvailabilityUseCase.cs`

**Checkpoint**: Deactivation cannot leave a stale active availability view

---

## Phase 8: User Story 8 - Stop password-grant shortcuts in local identity setup (Priority: P1)

**Goal**: Direct Access Grants disabled; no ROPC docs/smoke; Swagger PKCE unchanged.

**Independent Test**: Realm JSON has every client `directAccessGrantsEnabled=false`; `README.md` and `specs/001-library-manager/quickstart.md` have no Keycloak `grant_type=password`; tests have no Keycloak token-endpoint ROPC (API 404 probes in `AuthorizationTests` remain allowed); Swagger still documents Authorization Code + PKCE. `dotnet test` does not host Keycloak.

### Tests for User Story 8

- [ ] T066 [P] [US8] Add failing assertions that every client in `infrastructure/keycloak/library-manager-realm.json` has `directAccessGrantsEnabled: false`, and that `README.md`, `specs/001-library-manager/quickstart.md`, and test sources contain no Keycloak realm token-endpoint password grant (allow `AuthorizationTests` API-path 404 probes) in `tests/LibraryManager.IntegrationTests/Security/KeycloakRealmImportTests.cs`

### Implementation for User Story 8

- [ ] T067 [US8] Set `directAccessGrantsEnabled` to `false` on `library-manager-swagger` in `infrastructure/keycloak/library-manager-realm.json`
- [ ] T068 [US8] Remove Resource Owner Password Credentials curl/examples from `README.md`
- [ ] T069 [US8] Remove password-grant smoke example from `specs/001-library-manager/quickstart.md` (keep PKCE instructions)

**Checkpoint**: Local identity setup is PKCE-only

---

## Phase 9: User Story 4 - Serve user-facing errors in English or Brazilian Portuguese (Priority: P2)

**Goal**: `Accept-Language` selects `en-US` (default) or `pt-BR` for binder, body, Result, and unexpected HTTP text; logs/codes stay English.

**Independent Test**: Same 400/404/422 with `Accept-Language: pt-BR` vs omitted/`en-US`; `Content-Language` matches; `code` and log templates remain English.

### Tests for User Story 4

- [X] T070 [P] [US4] Add failing Accept-Language tests (default en-US, en-US, pt-BR, Idempotency-Key, body, Result, unexpected, `correlationId`) in `tests/LibraryManager.IntegrationTests/Localization/AcceptLanguageTests.cs`

### Implementation for User Story 4

- [X] T071 [US4] Attach DataAnnotations resource keys on request contracts in `src/LibraryManager.Api/Contracts/Books/Requests/`, `Users/Requests/CreateUserRequest.cs`, and `Loans/Requests/CreateLoanRequest.cs`
- [X] T072 [US4] Complete `Error_*` entries for every `ErrorCodes` value in `src/LibraryManager.Api/Resources/SharedResource.resx`, `SharedResource.en-US.resx`, and `SharedResource.pt-BR.resx`
- [X] T073 [US4] Ensure `ErrorLocalizer` and ValidationProblemDetails use `IStringLocalizer<SharedResource>` in `src/LibraryManager.Api/Localization/ErrorLocalizer.cs` and `src/LibraryManager.Api/Results/ResultHttpMapper.cs`

**Checkpoint**: User-facing HTTP text is localized; diagnostics are not

---

## Phase 10: User Story 7 - Keep database commands free of concatenated runtime input (Priority: P2)

**Goal**: Runtime SQL values stay parameterized; existing `ExecuteSqlInterpolatedAsync` is not churned.

**Independent Test**: Production source has no Raw SQL concatenation; Book/Loan/Idempotency interpolated SQL still compiles and last-copy tests pass.

### Tests for User Story 7

- [ ] T074 [P] [US7] Add source assertion that `src/LibraryManager.Infrastructure/` does not concatenate runtime values into `ExecuteSqlRaw`/`FromSqlRaw` in `tests/LibraryManager.IntegrationTests/Architecture/SqlParameterizationTests.cs`

### Implementation for User Story 7

- [ ] T075 [US7] Leave parameterized `ExecuteSqlInterpolatedAsync` in `src/LibraryManager.Infrastructure/Persistence/Repositories/BookRepository.cs`, `LoanRepository.cs`, and `src/LibraryManager.Infrastructure/Idempotency/IdempotencyStore.cs`; replace only genuinely unsafe Raw SQL if the audit finds any

**Checkpoint**: SQL safety is documented by test and unchanged safe Interpolated calls

---

## Phase 11: User Story 9 - Preserve existing lending, audit, and operations behavior (Priority: P1)

**Goal**: Last-copy, idempotency, return/cancel, Outbox, JWT, and health stay green except where transport validation explicitly changed.

**Independent Test**: `dotnet test` passes for concurrency, idempotency, Outbox, authentication, and health projects/classes listed in `quickstart.md`.

### Tests for User Story 9

- [ ] T076 [US9] Re-run and fix leftovers in `tests/LibraryManager.IntegrationTests/Loans/CreateLoanTests.cs`, `IdempotencyTests.cs`, `ReturnAndCancelTests.cs`, `tests/LibraryManager.IntegrationTests/Outbox/OutboxProcessorTests.cs`, `tests/LibraryManager.IntegrationTests/Security/AuthorizationTests.cs`, `tests/LibraryManager.IntegrationTests/Health/HealthEndpointTests.cs`, and `tests/LibraryManager.IntegrationTests/Telemetry/ObservabilityTests.cs` (keep `library_manager_cache_invalidation_failures`)
- [ ] T077 [US9] Update `tests/LibraryManager.UnitTests/Application/CancellationTokenPropagationTests.cs` if UseCase signatures now return `Result`/`Result<T>`
- [ ] T078 [US9] Confirm two-host last-copy coverage still runs via `tests/LibraryManager.IntegrationTests/Loans/CreateLoanTests.cs` and `tests/LibraryManager.IntegrationTests/Infrastructure/CustomWebApplicationFactory.cs`

**Checkpoint**: Hardening has not regressed 001 guarantees

---

## Phase 12: Polish & Cross-Cutting Concerns

**Purpose**: Docs, dead code, and quickstart walkthrough

- [ ] T079 [P] Document localization, Result mapping, Idempotency-Key binding, cache decorator, NuGet audit, SQL parameterization, and PKCE-only Keycloak in `README.md`
- [ ] T080 [P] Delete unused `src/LibraryManager.Application/Common/EntityNotFoundException.cs`, `BusinessRuleException.cs`, and `IdempotencyConflictException.cs` if no remaining references
- [ ] T081 Walk `specs/002-production-hardening/quickstart.md` (Compose health, PKCE-only auth, binder 400s, Accept-Language, Redis-down availability, `dotnet test`)
- [ ] T082 [P] Confirm `ControllerContractLocationTests` still forbids Application `PagedResult<T>` / DTO return types on list/history/audit actions in `tests/LibraryManager.IntegrationTests/Architecture/ControllerContractLocationTests.cs` after Result mapping (T051)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **Foundational (Phase 2)**: Depends on Setup — BLOCKS all user stories
- **US1 (Phase 3)**: After Foundational — MVP (audit/OTel); no dependency on other stories
- **US2 (Phase 4)**: After Foundational — uses localization keys from Phase 2
- **US3 (Phase 5)**: After Foundational — uses Result + `ResultHttpMapper`; should follow US2 so controllers already use Contracts
- **US5 (Phase 6)**: After Foundational — should follow US3 if `GetBookAvailabilityUseCase` already returns `Result`
- **US6 (Phase 7)**: After US5 (decorator `RemoveAsync` must exist)
- **US8 (Phase 8)**: After Foundational — independent of Result/cache
- **US4 (Phase 9)**: After US2 + US3 (binder + Result messages to localize)
- **US7 (Phase 10)**: After Foundational — independent
- **US9 (Phase 11)**: After stories you intend to ship
- **Polish (Phase 12)**: After desired stories

### User Story Dependencies

- **US1**: Independent after Phase 2
- **US2**: Independent after Phase 2 (binder + contracts)
- **US3**: Best after US2 (same controllers); independently testable via Result HTTP tests
- **US5**: Independent of US1/US8; after US3 if availability returns Result
- **US6**: Depends on US5
- **US8**: Independent
- **US4**: Depends on US2 + US3
- **US7**: Independent
- **US9**: Depends on implemented stories

### Within Each User Story

- Tests MUST be written and FAIL before implementation
- Types/binders before controllers
- UseCases before controller mapping
- Story complete before the next priority when staffing is serial

### Parallel Opportunities

- T002/T003; T004–T006; T008/T009; T013/T014
- US1 tests T015/T016
- US2 tests T022–T025 and contract files T026–T033
- US3 tests T040–T042 and Domain files T044–T046
- US5 tests T056/T057
- US1 and US8 can run in parallel after Phase 2 while another developer does US2

---

## Parallel Example: User Story 2

```bash
# Tests together:
Task: "ControllerContractLocationTests.cs"
Task: "IdempotencyKeyBindingTests.cs"
Task: "BookBodyValidationTests.cs"
Task: "IdempotencyKeyModelBinderTests.cs"

# Contract files together:
Task: "IdempotencyKey.cs"
Task: "CreateBookRequest.cs / UpdateBookRequest.cs"
Task: "CreateUserRequest.cs"
Task: "CreateLoanRequest.cs"
Task: "BookResponse.cs / LoanResponse.cs / ..."
```

---

## Parallel Example: User Story 3

```bash
# Domain factories together after AuditEvent:
Task: "Book.cs Result validation"
Task: "User.cs Result validation"
Task: "Loan.cs Result validation"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational
3. Complete Phase 3: User Story 1 (NuGet audit + remove prerelease Redis OTel)
4. **STOP and VALIDATE**: props tests + `dotnet package list --vulnerable --include-transitive`

### Incremental Delivery

1. Setup + Foundational
2. US1 → audit gate
3. US2 → HTTP 400 binder/body
4. US3 → Result + safe 500s
5. US5 → Redis decorator
6. US6 → deactivation cache
7. US8 → Keycloak DAG
8. US4 → pt-BR/en-US
9. US7 → SQL assertion
10. US9 + Polish → full `dotnet test` + README

### Parallel Team Strategy

1. Team completes Setup + Foundational
2. Then:
   - Developer A: US1 + US8 + US7
   - Developer B: US2 then US4
   - Developer C: US3 then US5 then US6
3. Everyone: US9

---

## Notes

- [P] = different files, no incomplete dependencies
- Do not introduce CQRS, MediatR, Command/Query/Handler, or Generic Repository
- Do not rewrite safe `ExecuteSqlInterpolatedAsync` for style
- Do not inspect ModelState in controllers
- Do not catch `Exception` for Redis when `RedisException` suffices
- Do not swallow `OperationCanceledException`
- Do not return Application `PagedResult<T>` or Application DTOs from controllers; map to `PagedResponse<TResponse>`
- Commit after each task or logical group
- Stop at checkpoints to validate the story independently
