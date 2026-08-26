---
description: "Task list for Library Manager API implementation"
---

# Tasks: Library Manager API

**Input**: Design documents from `/specs/001-library-manager/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: Included. Spec FR-052 and `quickstart.md` require unit and integration tests. Write story tests first and ensure they fail before implementation.

**Organization**: Tasks are grouped by user story so each story can be implemented, tested, and demonstrated independently after Phase 2.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: User story label (`US1`–`US10`) for story phases only
- Every task includes an exact file path

## Path Conventions

- Production: `src/LibraryManager.Domain/`, `src/LibraryManager.Application/`, `src/LibraryManager.Infrastructure/`, `src/LibraryManager.Api/`
- Tests: `tests/LibraryManager.UnitTests/`, `tests/LibraryManager.IntegrationTests/`

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Solution, projects, and shared build settings

- [X] T001 Create `LibraryManager.sln` and directories `src/`, `tests/`, `infrastructure/keycloak/`, `deploy/kubernetes/` at the repository root
- [X] T002 [P] Create `src/LibraryManager.Domain/LibraryManager.Domain.csproj` targeting `net10.0` with no NuGet packages
- [X] T003 [P] Create `src/LibraryManager.Application/LibraryManager.Application.csproj` targeting `net10.0` referencing Domain only
- [X] T004 [P] Create `src/LibraryManager.Infrastructure/LibraryManager.Infrastructure.csproj` targeting `net10.0` with EF Core 10, Npgsql, and StackExchange.Redis
- [X] T005 [P] Create `src/LibraryManager.Api/LibraryManager.Api.csproj` targeting `net10.0` with JwtBearer, Swashbuckle, health checks, and OpenTelemetry packages
- [X] T006 [P] Create `tests/LibraryManager.UnitTests/LibraryManager.UnitTests.csproj` with xUnit referencing Domain and Application
- [X] T007 [P] Create `tests/LibraryManager.IntegrationTests/LibraryManager.IntegrationTests.csproj` with xUnit, `Microsoft.AspNetCore.Mvc.Testing`, Testcontainers.PostgreSql, and Testcontainers.Redis
- [X] T008 Add `global.json`, `Directory.Build.props` (nullable, implicit usings), `.editorconfig`, and add all projects to `LibraryManager.sln`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Domain model, persistence, auth, correlation, health, and test host that every story needs

**CRITICAL**: No user story work can begin until this phase is complete

- [X] T009 [P] Implement `Book` in `src/LibraryManager.Domain/Book.cs` per `data-model.md`
- [X] T010 [P] Implement `User` in `src/LibraryManager.Domain/User.cs` per `data-model.md`
- [X] T011 [P] Implement `Loan` and `LoanStatus` in `src/LibraryManager.Domain/Loan.cs` and `src/LibraryManager.Domain/LoanStatus.cs`
- [X] T012 [P] Implement `AuditEvent` in `src/LibraryManager.Domain/AuditEvent.cs`
- [X] T013 Create abstractions `IBookRepository`, `IUserRepository`, `ILoanRepository`, `IAuditRepository`, `IUnitOfWork`, `IIdempotencyStore`, `IOutboxWriter`, `IAvailabilityCache`, `ICurrentUserContext`, `ICorrelationContext`, `IClock` in `src/LibraryManager.Application/Abstractions/` (every public async method takes and propagates `CancellationToken`)
- [X] T014 Create `LibraryDbContext` in `src/LibraryManager.Infrastructure/Persistence/LibraryDbContext.cs`
- [X] T015 [P] Add `BookConfiguration` in `src/LibraryManager.Infrastructure/Persistence/Configurations/BookConfiguration.cs`
- [X] T016 [P] Add `UserConfiguration` in `src/LibraryManager.Infrastructure/Persistence/Configurations/UserConfiguration.cs`
- [X] T017 [P] Add `LoanConfiguration` with partial unique Active index in `src/LibraryManager.Infrastructure/Persistence/Configurations/LoanConfiguration.cs`
- [X] T018 [P] Add `AuditEventConfiguration` with jsonb `DataJson` in `src/LibraryManager.Infrastructure/Persistence/Configurations/AuditEventConfiguration.cs`
- [X] T019 [P] Add Infrastructure `IdempotencyEntry` entity and configuration in `src/LibraryManager.Infrastructure/Idempotency/IdempotencyEntry.cs` and `IdempotencyEntryConfiguration.cs`
- [X] T020 [P] Add Infrastructure `OutboxMessage` entity and configuration in `src/LibraryManager.Infrastructure/Outbox/OutboxMessage.cs` and `OutboxMessageConfiguration.cs`
- [X] T021 Implement repositories in `src/LibraryManager.Infrastructure/Persistence/Repositories/BookRepository.cs`, `UserRepository.cs`, `LoanRepository.cs`, `AuditRepository.cs` (async methods take `CancellationToken`)
- [X] T022 Implement `IUnitOfWork` in `src/LibraryManager.Infrastructure/Persistence/UnitOfWork.cs` (`SaveChangesAsync` / transaction APIs take `CancellationToken`)
- [X] T023 Implement `IIdempotencyStore` with `INSERT ON CONFLICT` in `src/LibraryManager.Infrastructure/Idempotency/IdempotencyStore.cs` (async methods take `CancellationToken`; US3 may call reserve only; hash/replay/409 is T064)
- [X] T024 Implement `IOutboxWriter` using the current `LibraryDbContext` transaction in `src/LibraryManager.Infrastructure/Outbox/OutboxWriter.cs` (async methods take `CancellationToken`)
- [X] T025 Implement `IAvailabilityCache` in `src/LibraryManager.Infrastructure/Caching/RedisAvailabilityCache.cs` (key `library-manager:books:{bookId}:availability`, TTL 60s; async methods take `CancellationToken`)
- [X] T026 [P] Implement `IClock` in `src/LibraryManager.Infrastructure/Time/SystemClock.cs`
- [X] T027 [P] Implement `ICurrentUserContext` from JWT `sub` in `src/LibraryManager.Api/Security/CurrentUserContext.cs`
- [X] T028 [P] Implement `ICorrelationContext` in `src/LibraryManager.Api/Middleware/CorrelationContext.cs`
- [X] T029 Configure JwtBearer (`Authentication:Authority`, `Authentication:Audience`, issuer/audience/signature/lifetime, `MapInboundClaims = false`, `RoleClaimType = roles`) and Librarian policy in `src/LibraryManager.Api/Program.cs` and `src/LibraryManager.Api/Security/LibrarianPolicy.cs`
- [X] T030 Add `CorrelationIdMiddleware` for `X-Correlation-ID` in `src/LibraryManager.Api/Middleware/CorrelationIdMiddleware.cs`
- [X] T031 Configure RFC Problem Details and `src/LibraryManager.Api/appsettings.json` plus `appsettings.Development.json`
- [X] T032 Map anonymous `GET /health/live` (process-only, 200 if the process is up) and `GET /health/ready` (Postgres + Redis, HTTP 503 when a dependency is down) in `src/LibraryManager.Api/Health/HealthEndpoints.cs` and `Program.cs`
- [X] T033 Register composition in `src/LibraryManager.Infrastructure/DependencyInjection.cs` and `src/LibraryManager.Api/Program.cs`
- [X] T034 Add `CustomWebApplicationFactory` with Testcontainers PostgreSQL and Redis in `tests/LibraryManager.IntegrationTests/Infrastructure/CustomWebApplicationFactory.cs`. The factory MUST support two (or more) host instances that share one PostgreSQL connection string and one Redis connection string; apply EF migrations once; do not start a second database container per host.
- [X] T035 Add test-only authentication scheme gated by `Testing:UseTestAuth` in `tests/LibraryManager.IntegrationTests/Infrastructure/TestAuthHandler.cs`
- [X] T036 Add initial EF Core migration in `src/LibraryManager.Infrastructure/Persistence/Migrations/`

**Checkpoint**: Foundation ready — user story implementation can begin

---

## Phase 3: User Story 1 - Maintain the book catalog (Priority: P1) MVP

**Goal**: Staff can create, retrieve, list, update, and logically deactivate books (ISBN unique, no per-copy ids).

**Independent Test**: Librarian test token can POST/GET/PUT/DELETE `/books`; duplicate ISBN is HTTP 422; PUT leaves ISBN unchanged; TotalCopies below borrowed is HTTP 422; DELETE deactivates and book remains GET-able; deactivated book cannot be used for new loans once US3 exists.

### Tests for User Story 1

> Write these tests FIRST and ensure they FAIL before implementation

- [X] T037 [P] [US1] Add Domain unit tests for Book invariants in `tests/LibraryManager.UnitTests/Domain/BookTests.cs`
- [X] T038 [P] [US1] Add failing integration tests for create/list/get/update/deactivate, duplicate ISBN (HTTP 422), ISBN unchanged after PUT, TotalCopies below borrowed (HTTP 422), GET unknown book (HTTP 404), and list pagination (`page` default 1, `pageSize` default 20, max 100) in `tests/LibraryManager.IntegrationTests/Books/BookCatalogTests.cs`

### Implementation for User Story 1

- [X] T039 [P] [US1] Implement `CreateBookUseCase` in `src/LibraryManager.Application/Books/CreateBook/CreateBookUseCase.cs`
- [X] T040 [P] [US1] Implement `GetBookUseCase` in `src/LibraryManager.Application/Books/GetBook/GetBookUseCase.cs`
- [X] T041 [P] [US1] Implement `ListBooksUseCase` with `page` (default 1) and `pageSize` (default 20, maximum 100) in `src/LibraryManager.Application/Books/ListBooks/ListBooksUseCase.cs`
- [X] T042 [P] [US1] Implement `UpdateBookUseCase` so ISBN cannot change and TotalCopies below borrowed copies is rejected with HTTP 422 via atomic SQL in `src/LibraryManager.Application/Books/UpdateBook/UpdateBookUseCase.cs` and `BookRepository`
- [X] T043 [US1] Implement `DeactivateBookUseCase` in `src/LibraryManager.Application/Books/DeactivateBook/DeactivateBookUseCase.cs`
- [X] T044 [US1] Implement `BooksController` (Librarian mutations, authenticated reads) in `src/LibraryManager.Api/Controllers/BooksController.cs` matching `specs/001-library-manager/contracts/openapi.yaml`
- [X] T045 [US1] Persist BookCreated/BookUpdated/BookDeactivated `AuditEvent` in the same transaction in `src/LibraryManager.Application/Books/CreateBook/CreateBookUseCase.cs`, `UpdateBookUseCase.cs`, and `DeactivateBookUseCase.cs`

**Checkpoint**: Catalog is independently testable with the integration test host

---

## Phase 4: User Story 2 - Register Users (Priority: P1)

**Goal**: Staff register domain Users (borrowers). User is not the JWT caller.

**Independent Test**: POST `/users` creates a User; duplicate email is HTTP 422; GET `/users/{id}/loans` returns an empty page for a new User.

### Tests for User Story 2

- [X] T046 [P] [US2] Add Domain unit tests for User in `tests/LibraryManager.UnitTests/Domain/UserTests.cs`
- [X] T047 [P] [US2] Add failing integration tests for create User, duplicate email (HTTP 422), GET `/users/{id}/loans` empty first page, and unknown UserId (HTTP 404) in `tests/LibraryManager.IntegrationTests/Users/UserRegistrationTests.cs`

### Implementation for User Story 2

- [X] T048 [P] [US2] Implement `CreateUserUseCase` in `src/LibraryManager.Application/Users/CreateUser/CreateUserUseCase.cs`
- [X] T049 [P] [US2] Implement `GetUserLoansUseCase` with `page` (default 1) and `pageSize` (default 20, maximum 100) in `src/LibraryManager.Application/Users/GetUserLoans/GetUserLoansUseCase.cs`
- [X] T050 [US2] Implement `UsersController` in `src/LibraryManager.Api/Controllers/UsersController.cs`
- [X] T051 [US2] Persist UserCreated `AuditEvent` in the same transaction in `src/LibraryManager.Application/Users/CreateUser/CreateUserUseCase.cs`

**Checkpoint**: Users can be registered independently of lending

---

## Phase 5: User Story 3 - Lend an available copy (Priority: P1)

**Goal**: Create Active loans with PostgreSQL atomic reservation; last-copy races produce one winner; DueAtUtc = BorrowedAtUtc.AddDays(14) in UTC.

**Independent Test**: Lend with spare copies succeeds; two concurrent last-copy requests through two `CustomWebApplicationFactory` hosts sharing one Testcontainers PostgreSQL yield one HTTP 201 and one HTTP 422 unavailable; AvailableCopies never negative; Redis is not read during approval. Idempotent replay and HTTP 409 hash mismatch are proven in US4, not this story.

### Tests for User Story 3

- [X] T052 [P] [US3] Add Domain unit tests for Loan and DueAtUtc (`BorrowedAtUtc.AddDays(14)` in UTC) in `tests/LibraryManager.UnitTests/Domain/LoanTests.cs`
- [X] T053 [P] [US3] Add failing integration tests for successful loan, unknown UserId/BookId (HTTP 404), and concurrent last-copy using two `CustomWebApplicationFactory` instances that share one Testcontainers PostgreSQL (from T034) in `tests/LibraryManager.IntegrationTests/Loans/CreateLoanTests.cs`

### Implementation for User Story 3

- [X] T054 [US3] Implement atomic `TryReserveAvailability` SQL on `BookRepository` in `src/LibraryManager.Infrastructure/Persistence/Repositories/BookRepository.cs`
- [X] T055 [US3] Implement `CreateLoanUseCase` business transaction (reserve idempotency key only — do not complete hash/replay/409; those are T063–T065; validate User, reserve availability, insert Loan with DueAtUtc = BorrowedAtUtc.AddDays(14) in UTC, LoanCreated AuditEvent, BookAvailabilityChanged Outbox, commit) in `src/LibraryManager.Application/Loans/CreateLoan/CreateLoanUseCase.cs`
- [X] T056 [US3] After commit, `await` Redis invalidation without failing the HTTP result in `src/LibraryManager.Application/Loans/CreateLoan/CreateLoanUseCase.cs`
- [X] T057 [US3] Implement `LoansController` POST `/loans` with required `Idempotency-Key` in `src/LibraryManager.Api/Controllers/LoansController.cs`
- [X] T058 [US3] Implement `GetBookLoanHistoryUseCase` and GET `/books/{id}/loans` with `page` (default 1) and `pageSize` (default 20, maximum 100) in `src/LibraryManager.Application/Loans/GetBookLoanHistory/GetBookLoanHistoryUseCase.cs` and `BooksController`
- [X] T059 [US3] Record `library_manager_loans_created`, `library_manager_loans_unavailable`, and `library_manager_loan_duration` in `src/LibraryManager.Api/Telemetry/LibraryManagerMetrics.cs` from the create-loan path
- [X] T060 [US3] Reject inactive book, zero copies, and duplicate Active (UserId, BookId) with HTTP 422 Problem Details; reject unknown UserId or BookId with HTTP 404 in `src/LibraryManager.Application/Loans/CreateLoan/CreateLoanUseCase.cs`

**Checkpoint**: Lending is correct for sequential and last-copy concurrent cases, including two hosts sharing PostgreSQL. Full idempotency (201 replay, 409 mismatch, hash, rollback) is US4.

---

## Phase 6: User Story 4 - Retry a lend without duplicating it (Priority: P1)

**Goal**: Mandatory Idempotency-Key; same canonical hash replays as HTTP 201 with stored body; different hash is HTTP 409; unexpected failure rolls back ownership.

**Independent Test**: Repeat POST `/loans` with same key and body returns HTTP 201 and the stored loan body; different body with same key returns HTTP 409; concurrent same key creates one loan; unexpected failure after key reserve rolls back ownership so the same key can create a loan on retry.

### Tests for User Story 4

- [X] T061 [P] [US4] Add failing tests for missing key (400), sequential replay (HTTP 201 + stored body), concurrent same key, different payload HTTP 409, and unexpected failure after key reserve (ownership rolled back; retry with the same key creates the loan, not a replay) in `tests/LibraryManager.IntegrationTests/Loans/IdempotencyTests.cs` and `tests/LibraryManager.UnitTests/Application/CreateLoanIdempotencyRollbackTests.cs`
- [X] T062 [P] [US4] Add unit tests for canonical JSON + SHA-256 in `tests/LibraryManager.UnitTests/Application/IdempotencyCanonicalizationTests.cs`

### Implementation for User Story 4

- [X] T063 [US4] Canonicalize `{bookId,userId}` and compute SHA-256 in `src/LibraryManager.Infrastructure/Idempotency/LoanRequestCanonicalizer.cs`
- [X] T064 [US4] Wire HTTP 201 replay vs HTTP 409 vs reserve in `src/LibraryManager.Infrastructure/Idempotency/IdempotencyStore.cs` and `src/LibraryManager.Application/Loans/CreateLoan/CreateLoanUseCase.cs`
- [X] T065 [US4] Ensure unexpected exceptions roll back idempotency ownership with the ambient EF transaction in `src/LibraryManager.Application/Loans/CreateLoan/CreateLoanUseCase.cs` (T061 rollback tests must pass)
- [X] T066 [US4] Increment `library_manager_idempotency_replays` in `src/LibraryManager.Api/Telemetry/LibraryManagerMetrics.cs`

**Checkpoint**: Idempotency guarantees are independently proven

---

## Phase 7: User Story 5 - Restrict changes to authenticated, authorized staff (Priority: P1)

**Goal**: Resource server JWT; Librarian mutations; 401 vs 403; anonymous health; local Keycloak + Swagger PKCE.

**Independent Test**: Mutation without token is 401; token without `librarian` is 403; librarian succeeds; health works without token; Swagger OAuth uses PKCE against realm `library-manager`.

### Tests for User Story 5

- [X] T067 [P] [US5] Add failing 401/403/success mutation tests in `tests/LibraryManager.IntegrationTests/Security/AuthorizationTests.cs`
- [X] T068 [P] [US5] Add failing test that AuditEvent.ActorId equals authenticated subject in `tests/LibraryManager.IntegrationTests/Security/AuditActorTests.cs`

### Implementation for User Story 5

- [X] T069 [US5] Create importable realm `infrastructure/keycloak/library-manager-realm.json` (realm `library-manager`, clients `library-manager-api` and `library-manager-swagger`, librarian role, flat `roles` claim, redirect `http://localhost:8080/swagger/oauth2-redirect.html` only, no production secrets)
- [X] T070 [US5] Configure Swagger UI Authorization Code + PKCE for `library-manager-swagger` in `src/LibraryManager.Api/OpenApi/SwaggerConfiguration.cs` and `Program.cs`
- [X] T071 [US5] Ensure no username/password token endpoint exists in `src/LibraryManager.Api/`
- [X] T072 [US5] Keep health endpoints `[AllowAnonymous]` in `src/LibraryManager.Api/Health/HealthEndpoints.cs`

**Checkpoint**: Authn/z semantics and local OIDC are demonstrable

---

## Phase 8: User Story 6 - Return or cancel a loan without erasing history (Priority: P2)

**Goal**: Only Active → Returned/Cancelled; inventory restored at most once; history remains queryable.

**Independent Test**: Return and cancel restore one copy; duplicate/concurrent return does not double increment; GET user loans still lists terminal loans.

### Tests for User Story 6

- [X] T073 [P] [US6] Add failing tests for return, cancel, concurrent duplicate return, history, not-Active return/cancel HTTP 422, and unknown loan id HTTP 404 in `tests/LibraryManager.IntegrationTests/Loans/ReturnAndCancelTests.cs`

### Implementation for User Story 6

- [X] T074 [US6] Implement conditional Active→Returned on `LoanRepository` in `src/LibraryManager.Infrastructure/Persistence/Repositories/LoanRepository.cs`
- [X] T075 [US6] Implement conditional Active→Cancelled on `LoanRepository` in `src/LibraryManager.Infrastructure/Persistence/Repositories/LoanRepository.cs`
- [X] T076 [US6] Implement `ReturnLoanUseCase` with availability increment, AuditEvent, Outbox, post-commit cache invalidation in `src/LibraryManager.Application/Loans/ReturnLoan/ReturnLoanUseCase.cs`
- [X] T077 [US6] Implement `CancelLoanUseCase` similarly in `src/LibraryManager.Application/Loans/CancelLoan/CancelLoanUseCase.cs`
- [X] T078 [US6] Add POST `/loans/{id}/return` and POST `/loans/{id}/cancel` in `src/LibraryManager.Api/Controllers/LoansController.cs`

**Checkpoint**: Circulation and historical preservation work independently

---

## Phase 9: User Story 7 - Leave a durable audit trail (Priority: P2)

**Goal**: Query AuditEvents; actor is JWT sub; correlation matches; rejected mutations have no success audit.

**Independent Test**: Librarian GET `/audit-events` lists LoanCreated with ActorId = test subject and matching `X-Correlation-ID`; authenticated non-librarian GET is HTTP 403; failed mutation adds no success row.

### Tests for User Story 7

- [X] T079 [P] [US7] Add failing audit query, Librarian-required GET `/audit-events` (HTTP 403 without librarian), and rejected-mutation tests in `tests/LibraryManager.IntegrationTests/Audit/AuditEventTests.cs`

### Implementation for User Story 7

- [X] T080 [US7] Implement `GetAuditEventsUseCase` with `page` (default 1) and `pageSize` (default 20, maximum 100) in `src/LibraryManager.Application/Audit/GetAuditEvents/GetAuditEventsUseCase.cs`
- [X] T081 [US7] Implement `AuditEventsController` GET `/audit-events` (Librarian) in `src/LibraryManager.Api/Controllers/AuditEventsController.cs`
- [X] T082 [US7] Copy `ICorrelationContext` and UTC `IClock` into `AuditEvent` in all mutation use cases under `src/LibraryManager.Application/`

**Checkpoint**: Audit trail is queryable and consistent with auth identity

---

## Phase 10: User Story 8 - Treat cached availability as a hint (Priority: P3)

**Goal**: GET `/books/{id}/availability` is cache-aside; loans never consult Redis; stale cache cannot approve a loan.

**Independent Test**: Availability endpoint hits Redis after first read; loan with stale cache still uses PostgreSQL; invalidation after loan is attempted.

### Tests for User Story 8

- [X] T083 [P] [US8] Add failing cache hit/miss and stale-cache loan tests in `tests/LibraryManager.IntegrationTests/Caching/AvailabilityCacheTests.cs`

### Implementation for User Story 8

- [X] T084 [US8] Implement `GetBookAvailabilityUseCase` cache-aside in `src/LibraryManager.Application/Books/GetBookAvailability/GetBookAvailabilityUseCase.cs`
- [X] T085 [US8] Add GET `/books/{id}/availability` in `src/LibraryManager.Api/Controllers/BooksController.cs`
- [X] T086 [US8] Ensure Create/Return/Cancel never call cache get for approval in `src/LibraryManager.Application/Loans/CreateLoan/CreateLoanUseCase.cs`, `ReturnLoanUseCase.cs`, and `CancelLoanUseCase.cs`; only `await` invalidate after commit
- [X] T087 [US8] Increment `library_manager_cache_invalidation_failures` on invalidate errors in `src/LibraryManager.Api/Telemetry/LibraryManagerMetrics.cs`

**Checkpoint**: Cache is a hint only

---

## Phase 11: User Story 9 - Finish reliability-sensitive follow-up work (Priority: P3)

**Goal**: `OutboxProcessor` BackgroundService with SKIP LOCKED leases, Redis I/O outside the claim transaction, retry, crash recovery, idempotent consumers.

**Independent Test**: Loan persists Outbox in the same transaction; processor invalidates cache; failed processing retries; expired lease is claimed by another worker; two workers do not corrupt state.

### Tests for User Story 9

- [X] T088 [P] [US9] Add failing tests for Outbox persistence, processing, retry, expired lease, and multiple workers in `tests/LibraryManager.IntegrationTests/Outbox/OutboxProcessorTests.cs`

### Implementation for User Story 9

- [X] T089 [US9] Implement claim SQL (`FOR UPDATE SKIP LOCKED`, `LockedBy`, `LockedUntilUtc`) in `src/LibraryManager.Infrastructure/Outbox/OutboxClaimer.cs`
- [X] T090 [US9] Implement `OutboxProcessor` as `BackgroundService` in `src/LibraryManager.Infrastructure/Outbox/OutboxProcessor.cs` (commit claim, then Redis, then ProcessedAtUtc or backoff)
- [X] T091 [US9] Register hosted `OutboxProcessor` in `src/LibraryManager.Infrastructure/DependencyInjection.cs`
- [X] T092 [US9] Make cache invalidation consumer idempotent in `src/LibraryManager.Infrastructure/Caching/RedisAvailabilityCache.cs`
- [X] T093 [US9] Record `library_manager_outbox_processed`, `library_manager_outbox_failures`, `library_manager_outbox_pending` in `src/LibraryManager.Api/Telemetry/LibraryManagerMetrics.cs`

**Checkpoint**: Outbox is multi-replica safe and recoverable

---

## Phase 12: User Story 10 - Operate, observe, test, and document (Priority: P3)

**Goal**: OpenTelemetry, Compose stack, Kubernetes baseline without Keycloak, English README covering correctness guarantees.

**Independent Test**: Follow `specs/001-library-manager/quickstart.md`; live/ready work; `docker compose up` brings API, Postgres, Redis, Keycloak; k8s manifests exist; README is English and complete.

### Tests for User Story 10

- [X] T094 [P] [US10] Add failing anonymous live/ready tests in `tests/LibraryManager.IntegrationTests/Health/HealthEndpointTests.cs`: live returns HTTP 200 without a token; ready returns HTTP 200 when Postgres and Redis are up; live still returns HTTP 200 when ready returns HTTP 503 (dependency down)

### Implementation for User Story 10

- [X] T095 [US10] Configure OpenTelemetry (`ActivitySource` `LibraryManager`, OTLP optional) in `src/LibraryManager.Api/Telemetry/OpenTelemetryConfiguration.cs` and `Program.cs`
- [X] T096 [US10] Add structured logging scopes with correlation id in `src/LibraryManager.Api/Middleware/CorrelationIdMiddleware.cs`
- [X] T097 [US10] Create `Dockerfile` using `mcr.microsoft.com/dotnet/sdk:10.0` and `aspnet:10.0`
- [X] T098 [US10] Create `docker-compose.yml` services `library-manager-api`, `postgres`, `redis`, `keycloak` (`quay.io/keycloak/keycloak:26.7.2`, `start-dev --import-realm`)
- [X] T099 [US10] Add `deploy/kubernetes/deployment.yaml` with CPU/memory requests and limits, liveness `/health/live`, readiness `/health/ready`
- [X] T100 [P] [US10] Add `deploy/kubernetes/service.yaml`
- [X] T101 [P] [US10] Add `deploy/kubernetes/configmap.yaml` for Authority/Audience (no Keycloak workload)
- [X] T102 [P] [US10] Add `deploy/kubernetes/secret.yaml` references for connection strings (placeholders only)
- [X] T103 [US10] Write English `README.md` covering execution, authentication, migrations, tests, architecture, concurrency, idempotency, audit, caching, Outbox, observability, and why 2–11 replicas remain correct

**Checkpoint**: Operators can run, observe, and explain the system

---

## Phase 13: Polish & Cross-Cutting Concerns

**Purpose**: Contract alignment, leftover tests, CancellationToken tests and audit, and quickstart validation

- [ ] T104 [P] Align remaining OpenAPI responses (HTTP 422 business rules, HTTP 404 missing resources, HTTP 201 replay, Problem Details `correlationId`) in `src/LibraryManager.Api/` with `specs/001-library-manager/contracts/openapi.yaml`
- [ ] T105 Add Domain/Application unit tests for remaining invariants in `tests/LibraryManager.UnitTests/`
- [ ] T106 Ensure production secrets cannot be enabled via `Testing:UseTestAuth` in `src/LibraryManager.Api/Program.cs`
- [X] T107 Run `specs/001-library-manager/quickstart.md` validation (compose health, smoke loan, `dotnet test`)
- [ ] T108 Review English-only nomenclature across `src/`, `tests/`, `deploy/`, and `infrastructure/`
- [ ] T110 [P] Add failing unit tests that a cancelled `CancellationToken` is observed by a public async Application method (`OperationCanceledException` or equivalent) in `tests/LibraryManager.UnitTests/Application/CancellationTokenPropagationTests.cs`
- [ ] T109 Audit that all public async Application, Infrastructure, and Api methods accept and propagate `CancellationToken` (FR-066) under `src/LibraryManager.Application/`, `src/LibraryManager.Infrastructure/`, and `src/LibraryManager.Api/`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies
- **Foundational (Phase 2)**: Depends on Setup — BLOCKS all user stories
- **User Stories (Phases 3–12)**: Depend on Foundational
- **Polish (Phase 13)**: Depends on the stories you intend to ship

### User Story Dependencies

- **US1 (P1)**: After Phase 2 — no story dependencies (catalog MVP)
- **US2 (P1)**: After Phase 2 — parallel with US1
- **US3 (P1)**: After US1 and US2 (needs Book and User)
- **US4 (P1)**: After US3 (extends create-loan idempotency)
- **US5 (P1)**: After Phase 2 — can parallel US1/US2; Keycloak/Swagger can land before US3
- **US6 (P2)**: After US3
- **US7 (P2)**: After US3 (needs AuditEvent rows); controller can be built earlier
- **US8 (P3)**: After US1; invalidation assertions after US3
- **US9 (P3)**: After US3 (Outbox rows exist)
- **US10 (P3)**: After health (Phase 2) and ideally after US5 realm file; Compose/K8s/README can start once API boots

### Within Each User Story

- Tests MUST be written and fail before implementation
- Use cases before controllers
- Story complete before the next dependent story

### Parallel Opportunities

- T002–T007 after T001
- T009–T012, T015–T020, T026–T028 after abstractions/DbContext sequencing
- US1 and US2 after Phase 2
- US5 (tests + realm + Swagger) in parallel with catalog work
- T100–T102 Kubernetes files in parallel

---

## Parallel Example: User Story 1

```bash
# Tests in parallel:
Task: "Domain unit tests in tests/LibraryManager.UnitTests/Domain/BookTests.cs"
Task: "Integration tests in tests/LibraryManager.IntegrationTests/Books/BookCatalogTests.cs"

# Use cases in parallel after tests exist:
Task: "CreateBookUseCase in src/LibraryManager.Application/Books/CreateBook/CreateBookUseCase.cs"
Task: "GetBookUseCase in src/LibraryManager.Application/Books/GetBook/GetBookUseCase.cs"
Task: "ListBooksUseCase in src/LibraryManager.Application/Books/ListBooks/ListBooksUseCase.cs"
Task: "UpdateBookUseCase in src/LibraryManager.Application/Books/UpdateBook/UpdateBookUseCase.cs"
```

## Parallel Example: User Story 2

```bash
Task: "UserTests in tests/LibraryManager.UnitTests/Domain/UserTests.cs"
Task: "UserRegistrationTests in tests/LibraryManager.IntegrationTests/Users/UserRegistrationTests.cs"
Task: "CreateUserUseCase in src/LibraryManager.Application/Users/CreateUser/CreateUserUseCase.cs"
Task: "GetUserLoansUseCase in src/LibraryManager.Application/Users/GetUserLoans/GetUserLoansUseCase.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational
3. Complete Phase 3: User Story 1 (catalog)
4. **STOP and VALIDATE** with `BookCatalogTests`
5. Demo catalog CRUD with a librarian test token

### Product-critical slice (recommended next)

1. US2 Users
2. US3 Create loan (last-copy)
3. US4 Idempotency
4. US5 401/403 + Keycloak/Swagger

### Incremental Delivery

1. Setup + Foundational → foundation
2. US1 → catalog MVP
3. US2 → borrowers
4. US3–US5 → secure concurrent lending
5. US6–US7 → return/cancel + audit query
6. US8–US10 → cache, Outbox processor, Compose/K8s/README
7. Polish → quickstart.md

### Parallel Team Strategy

1. Team completes Setup + Foundational
2. Then:
   - Developer A: US1
   - Developer B: US2 + US5
   - Developer C: waits for US1/US2 then US3/US4
3. US6–US10 after lending exists

---

## Notes

- [P] tasks = different files, no incomplete dependencies
- [USn] maps to spec user stories 1–10
- Do not introduce CQRS, MediatR, Command/Query/Handler types, or Generic Repository
- Do not use `Task.Run` for cache invalidation
- HTTP 409 is only Idempotency-Key canonical mismatch; HTTP 422 is business-rule failure; HTTP 404 is missing book/User/loan; successful loan replay is HTTP 201
- Verify tests fail before implementing
- Commit after each task or logical group
- Stop at checkpoints to validate independently
