# Implementation Plan: Library Manager API

**Branch**: `001-library-manager` | **Date**: 2026-08-25 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/001-library-manager/spec.md`

**Note**: This template is filled in by the `/speckit-plan` command; its definition describes the execution workflow.

## Summary

Build `library-manager`, a production-oriented ASP.NET Core REST API for catalog, User, and loan management. PostgreSQL is the source of truth for inventory, idempotency, audit, and Outbox. Redis caches `GET /books/{id}/availability` only and never participates in loan approval. The API is a JWT Bearer resource server; local identity is Keycloak 26.7.2. Application behavior is explicit UseCase classes (no CQRS, MediatR, Command/Query/Handler types, or Generic Repository).

Create-loan correctness is a single PostgreSQL transaction. User Story 3 reserves the Idempotency-Key and runs the business transaction (validate User, atomically reserve availability, insert Loan, insert AuditEvent, write BookAvailabilityChanged Outbox, commit, then await Redis invalidation). User Story 4 owns request hash, same-hash HTTP 201 replay of the stored loan body, HTTP 409 on key reuse with a different canonical body, and rollback of key ownership on unexpected failure. After commit, await best-effort Redis invalidation; failures are logged and metered and retried by OutboxProcessor. FR-046 is the general Outbox obligation; FR-043 applies it to availability-changing transactions and adds post-commit Redis invalidation.

## Technical Context

**Language/Version**: C# / .NET 10 (`net10.0`)

**Primary Dependencies**: ASP.NET Core Web API; Entity Framework Core 10; Npgsql.EntityFrameworkCore.PostgreSQL; StackExchange.Redis; Microsoft.AspNetCore.Authentication.JwtBearer; Swashbuckle.AspNetCore (Swagger UI + OAuth2 PKCE); OpenTelemetry.Extensions.Hosting plus ASP.NET, Http, Npgsql, and Redis instrumentation; Microsoft.Extensions.Diagnostics.HealthChecks with Npgsql and Redis checks

**Storage**: PostgreSQL (system of record, jsonb audit/outbox payloads); Redis (availability cache-aside)

**Testing**: xUnit; Microsoft.AspNetCore.Mvc.Testing (`WebApplicationFactory`); Testcontainers.PostgreSql; Testcontainers.Redis; test-only authentication scheme (integration tests only)

**Target Platform**: Linux containers (Docker Compose locally; Kubernetes Deployment/Service in non-local environments)

**Project Type**: web-service (Clean Architecture solution)

**Performance Goals**: Last-copy dual-lend remains correct under concurrent requests and 2–11 API replicas; availability cache TTL 60 seconds; loan create latency recorded via `library_manager_loan_duration`

**Constraints**: No in-process or Redis locks for business invariants; no `Task.Run` fire-and-forget cache work; no username/password token endpoint; no Keycloak in Kubernetes manifests; no production secrets in git; RFC Problem Details; async APIs with `CancellationToken`; English nomenclature only

**Scale/Scope**: Four production projects and two test projects as named by the constitution; catalog/users/loans/audit plus idempotency, Outbox, cache, auth, health, telemetry, Compose, and baseline Kubernetes manifests

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Gate | Result | Evidence |
|------|--------|----------|
| I. English project language and required project names | PASS | Paths `src/LibraryManager.*` and `tests/LibraryManager.*`; English types, tables, Docker/K8s names |
| II. Clean Architecture, inward dependencies, explicit UseCases, no CQRS/MediatR/Generic Repository | PASS | Layer map and UseCase folders below; capability-specific repositories |
| III. PostgreSQL-owned correctness; no memory/Redis locks; AuditEvent in the same transaction | PASS | Atomic `UPDATE ... WHERE available_copies > 0`; create-loan transaction list |
| IV. Durable PostgreSQL idempotency; unique Endpoint+Key; hash conflict is 409; rollback on failure | PASS | `IdempotencyEntry` + `INSERT ON CONFLICT`; research.md |
| V. Transactional Outbox, same transaction, multi-replica, at-least-once, crash recovery | PASS | `IOutboxWriter` + `OutboxProcessor` claim/lease |
| Cache: Redis optional; post-commit await invalidation; Outbox retry; bounded TTL | PASS | `IAvailabilityCache`; TTL 60s; metric on failure |
| Security: resource server; JWT validation; Keycloak local; ICurrentUserContext; 401 vs 403 | PASS | JwtBearer Authority/Audience; Librarian policy; realm file |
| Observability: correlation, UTC, structured logs, OTel traces/metrics | PASS | `X-Correlation-ID`; named meters |
| Quality: async, Problem Details, secrets out of source, integration tests | PASS | Health, Testcontainers, appsettings + env |

**Post-design re-check (Phase 1):** PASS. `data-model.md` keeps Book/User/Loan/AuditEvent in Domain and IdempotencyEntry/OutboxMessage in Infrastructure. `contracts/openapi.yaml` encodes Librarian mutations, Librarian GET `/audit-events`, anonymous health, mandatory Idempotency-Key, HTTP 201 create/replay, HTTP 409 only for canonical mismatch, and HTTP 422 for business-rule failures. `quickstart.md` validates Compose, Keycloak PKCE, and Testcontainers tests including two-host last-copy. No new constitution violations.

## Project Structure

### Documentation (this feature)

```text
specs/001-library-manager/
├── plan.md              # This file (/speckit-plan command output)
├── research.md          # Phase 0 output (/speckit-plan command)
├── data-model.md        # Phase 1 output (/speckit-plan command)
├── quickstart.md        # Phase 1 output (/speckit-plan command)
├── contracts/           # Phase 1 output (/speckit-plan command)
│   └── openapi.yaml
└── tasks.md             # Phase 2 output (/speckit-tasks command - NOT created by /speckit-plan)
```

### Source Code (repository root)

```text
LibraryManager.sln
global.json
Directory.Build.props
README.md
docker-compose.yml
Dockerfile

src/LibraryManager.Domain/
  Book.cs
  User.cs
  Loan.cs
  LoanStatus.cs
  AuditEvent.cs

src/LibraryManager.Application/
  Abstractions/
    IBookRepository.cs
    IUserRepository.cs
    ILoanRepository.cs
    IAuditRepository.cs
    IUnitOfWork.cs
    IIdempotencyStore.cs
    IOutboxWriter.cs
    IAvailabilityCache.cs
    ICurrentUserContext.cs
    ICorrelationContext.cs
    IClock.cs
  Books/CreateBook/CreateBookUseCase.cs
  Books/GetBook/GetBookUseCase.cs
  Books/ListBooks/ListBooksUseCase.cs
  Books/UpdateBook/UpdateBookUseCase.cs
  Books/DeactivateBook/DeactivateBookUseCase.cs
  Books/GetBookAvailability/GetBookAvailabilityUseCase.cs
  Users/CreateUser/CreateUserUseCase.cs
  Users/GetUserLoans/GetUserLoansUseCase.cs
  Loans/CreateLoan/CreateLoanUseCase.cs
  Loans/ReturnLoan/ReturnLoanUseCase.cs
  Loans/CancelLoan/CancelLoanUseCase.cs
  Loans/GetBookLoanHistory/GetBookLoanHistoryUseCase.cs
  Audit/GetAuditEvents/GetAuditEventsUseCase.cs

src/LibraryManager.Infrastructure/
  Persistence/LibraryDbContext.cs
  Persistence/Configurations/
  Persistence/Migrations/
  Persistence/Repositories/
  Persistence/UnitOfWork.cs
  Time/SystemClock.cs
  Idempotency/IdempotencyEntry.cs
  Idempotency/IdempotencyStore.cs
  Idempotency/LoanRequestCanonicalizer.cs
  Outbox/OutboxMessage.cs
  Outbox/OutboxWriter.cs
  Outbox/OutboxClaimer.cs
  Outbox/OutboxProcessor.cs
  Caching/RedisAvailabilityCache.cs
  DependencyInjection.cs

src/LibraryManager.Api/
  Program.cs
  appsettings.json
  appsettings.Development.json
  Controllers/
  Middleware/CorrelationIdMiddleware.cs
  Middleware/CorrelationContext.cs
  Security/CurrentUserContext.cs
  Security/LibrarianPolicy.cs
  OpenApi/
  Health/
  Telemetry/LibraryManagerMetrics.cs
  Telemetry/OpenTelemetryConfiguration.cs

tests/LibraryManager.UnitTests/
tests/LibraryManager.IntegrationTests/
  Infrastructure/CustomWebApplicationFactory.cs
  Infrastructure/TestAuthHandler.cs

infrastructure/keycloak/library-manager-realm.json
deploy/kubernetes/
  deployment.yaml
  service.yaml
  configmap.yaml
  secret.yaml
```

**Structure Decision**: Constitution-mandated Clean Architecture solution. Domain has no framework packages. Application depends only on Domain plus framework-neutral abstractions. Infrastructure implements EF Core/Npgsql, Redis, idempotency, and Outbox. Api owns HTTP, JWT, Swagger, middleware, health, and composition. Tests split unit vs Testcontainers integration.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

No constitution violations. Four production projects and two test projects are required by Constitution I/II, not extra complexity.

## Architecture and Runtime Design

### Layer rules

- **Domain**: `Book`, `User`, `Loan`, `LoanStatus` (`Active`, `Returned`, `Cancelled`), `AuditEvent` and their invariants only. No EF, ASP.NET, Redis, JWT, or OpenTelemetry types.
- **Application**: one public UseCase class per folder listed above. No Command, Query, Handler, `IRequest`, `IRequestHandler`, mediator, or MediatR types.
- **Infrastructure**: `LibraryDbContext` maps Domain entities plus `IdempotencyEntry` and `OutboxMessage`. Same DbContext instance is enlisted in the business transaction.
- **Api**: resource server only. `ICurrentUserContext.ActorId` is JWT `sub`. Application never references `HttpContext`.

### Authentication and Keycloak

- JwtBearer from `Authentication:Authority` and `Authentication:Audience`.
- Validate issuer, audience, signature, and lifetime.
- `MapInboundClaims = false`; `RoleClaimType = "roles"`; `NameClaimType = "sub"`.
- Policy `Librarian` requires role `librarian`.
- Mutations: `[Authorize(Policy = "Librarian")]`. Reads of catalog, Users, loans, and availability: `[Authorize]`. GET `/audit-events`: `[Authorize(Policy = "Librarian")]`. Health: `[AllowAnonymous]`.
- Missing/invalid token → HTTP 401. Authenticated without role → HTTP 403.
- Local image `quay.io/keycloak/keycloak:26.7.2`, `start-dev --import-realm`, realm file `infrastructure/keycloak/library-manager-realm.json`.
- Realm `library-manager`; API audience client `library-manager-api`; Swagger public client `library-manager-swagger` with Authorization Code + PKCE.
- Swagger redirect URI exactly `http://localhost:8080/swagger/oauth2-redirect.html` (no wildcards).
- Kubernetes does not deploy Keycloak; production Authority/Audience come from ConfigMap/Secret.

### HTTP status mapping

- HTTP 409: only `POST /loans` when an Idempotency-Key is reused with a different canonical request (FR-065, FR-028).
- HTTP 422: business-rule failures (duplicate ISBN or email, TotalCopies below borrowed copies, inactive or unavailable book, duplicate Active loan, return or cancel when the loan is not Active).
- HTTP 404: named book, User, or loan does not exist, including `POST /loans` with unknown `UserId` or `BookId`.
- HTTP 201: successful `POST /loans` and same-key same-hash replay of a stored successful create (FR-027). Do not use HTTP 200 for that replay.
- HTTP 401 vs 403: missing/invalid token vs authenticated without the required policy (Librarian on mutations and on GET `/audit-events`).

### Create loan transaction (single PostgreSQL transaction)

User Story 3 implements steps 1 (reserve key only), 2–6, and 8 of the business transaction. User Story 4 completes hash comparison, stored-response replay (HTTP 201), HTTP 409, step 7, and rollback of ownership.

1. Reserve `Idempotency-Key` (`Endpoint` + `Key` unique). Hash, replay, and 409 are User Story 4.
2. Validate User exists.
3. Atomically reserve availability (see SQL below).
4. Create `Loan` (`Active`, `DueAtUtc = BorrowedAtUtc.AddDays(14)` in UTC).
5. Create `LoanCreated` `AuditEvent` (actor = JWT `sub`, `CorrelationId` from `ICorrelationContext`).
6. Write `BookAvailabilityChanged` Outbox record via `IOutboxWriter` (FR-046 general Outbox; FR-043 for this availability change plus post-commit invalidation).
7. Complete idempotency stored response (HTTP 201 status and body) — User Story 4.
8. Commit.

If any unexpected failure occurs before commit, the transaction rolls back including idempotency ownership.

After commit: `await` Redis invalidation (`library-manager:books:{bookId}:availability`). On failure: log, increment `library_manager_cache_invalidation_failures`, return success for the loan. Do not use `Task.Run`. All public async Application, Infrastructure, and Api methods take and propagate `CancellationToken`.

**Availability reservation (conceptual):**

```sql
UPDATE books
SET available_copies = available_copies - 1
WHERE id = @bookId
  AND is_active = TRUE
  AND available_copies > 0;
```

One affected row succeeds; zero means unavailable. Never read-check-write in process memory.

Reject a second Active loan for the same User and Book with a partial unique index, not an app-tier lock.

### Idempotency

- Persist `IdempotencyEntry` in Infrastructure only.
- Unique constraint `(endpoint, key)` with `endpoint = 'POST /loans'`.
- Canonical JSON of material fields (`userId`, `bookId`) → SHA-256 hex `RequestHash`.
- `INSERT ... ON CONFLICT (endpoint, key) DO NOTHING` then read the winner.
- Same key + same hash → HTTP 201 with stored loan body; increment `library_manager_idempotency_replays`.
- Same key + different hash → HTTP 409.
- Missing header → HTTP 400 Problem Details.

### Return and cancellation

Conditional update: only `Active` becomes `Returned` or `Cancelled`. Only the session that updates exactly one loan row increments `available_copies` (never above `total_copies`). Same transaction writes `AuditEvent` and `BookAvailabilityChanged` Outbox. Concurrent duplicate return restores inventory at most once.

### TotalCopies updates

Atomic SQL: borrowed copies = `total_copies - available_copies`; reject if new total < borrowed; set `available_copies = new_total - borrowed`. Races with borrow are resolved by PostgreSQL, not memory.

### Outbox processor

`OutboxProcessor` : `BackgroundService`. Claim a small batch with `FOR UPDATE SKIP LOCKED`, set `LockedBy` and `LockedUntilUtc`, **commit the claim**. Then talk to Redis **outside** that transaction. Success: `ProcessedAtUtc`, clear lease. Failure: increment `AttemptCount`, store `LastError`, bounded exponential `NextAttemptAtUtc`, allow lease expiry. Crashed worker: lease expires; another replica claims. Consumers are idempotent (`DEL` / overwrite of the same cache key).

### Correlation and observability

- Header `X-Correlation-ID`: preserve valid incoming value or generate; echo on response; `ICorrelationContext`; logs, Activity tags, `AuditEvent.CorrelationId`.
- Structured `ILogger`; `ActivitySource`; `System.Diagnostics.Metrics`; OpenTelemetry exporter configurable via env.
- Metrics: `library_manager_loans_created`, `library_manager_loans_unavailable`, `library_manager_idempotency_replays`, `library_manager_loan_duration`, `library_manager_cache_invalidation_failures`, `library_manager_outbox_processed`, `library_manager_outbox_failures`, `library_manager_outbox_pending`.

### Health

- `GET /health/live`: process liveness only, anonymous, HTTP 200 while the process is running even if PostgreSQL or Redis is down.
- `GET /health/ready`: PostgreSQL and Redis, anonymous, HTTP 503 when a dependency is not reachable.

### Testing

xUnit unit tests for Domain rules and UseCase orchestration with fakes, including CancellationToken propagation (cancelled token is observed). Integration tests use `WebApplicationFactory` + Testcontainers PostgreSQL and Redis. `CustomWebApplicationFactory` MUST be constructible more than once against the **same** PostgreSQL (and Redis) container: shared connection string, migrations applied once. The last-copy race (FR-023/FR-024) MUST run through two factory hosts. Test auth scheme is registered only when `Testing:UseTestAuth` is true. Coverage list is in spec FR-052 and `quickstart.md`. Public async APIs propagate `CancellationToken`. List endpoints use `page` default 1 and `pageSize` default 20 (max 100).

### Compose and Kubernetes

Compose services: `library-manager-api`, `postgres`, `redis`, `keycloak` (26.7.2, realm import). Kubernetes: Deployment, Service, ConfigMap, Secret references, CPU/memory requests and limits, liveness `/health/live`, readiness `/health/ready`. No Keycloak resource in `deploy/kubernetes`.

### Multi-replica correctness (2–11 replicas)

API instances are stateless. PostgreSQL owns inventory (`UPDATE ... available_copies > 0`) and idempotency uniqueness. `AuditEvent` and Outbox rows share the business transaction. Outbox uses database leases (`FOR UPDATE SKIP LOCKED` + `LockedUntilUtc`). Consumers are idempotent. Redis never owns loan correctness. Therefore adding replicas cannot create extra last-copy loans, duplicate idempotent loans, or double inventory restore.
