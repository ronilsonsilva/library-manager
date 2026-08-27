# Library Manager

ASP.NET Core REST API for catalog, member, and loan management. PostgreSQL is the source of truth for inventory, idempotency, audit, and the transactional Outbox. Redis caches `GET /books/{id}/availability` only. The API is a JWT resource server; it never accepts username/password and never issues tokens.

## Local execution

Prerequisites: Docker, Docker Compose, and the .NET 10 SDK for tests.

```bash
docker compose up --build
```

Services:

| Service | URL / port | Image |
| --- | --- | --- |
| `library-manager-api` | http://localhost:8080 | built from `Dockerfile` (`.NET 10`) |
| `postgres` | localhost:5432 | `postgres:16-alpine` |
| `redis` | localhost:6379 | `redis:7-alpine` |
| `keycloak` | http://localhost:8081 | `quay.io/keycloak/keycloak:26.7.2` |

Keycloak is built from `quay.io/keycloak/keycloak:26.7.2` with `infrastructure/keycloak/library-manager-realm.json` copied into `/opt/keycloak/data/import/` and started with `start-dev --import-realm` (realm `library-manager`). Import is skipped if that realm already exists in the container. Recreate the Keycloak container to reimport.

Health (anonymous):

```bash
curl -sS http://localhost:8080/health/live
curl -sS http://localhost:8080/health/ready
```

`GET /health/live` is process-only and stays HTTP 200 while the process is running. `GET /health/ready` checks PostgreSQL and Redis and returns HTTP 503 when a dependency is down.

## Local-only development credentials

**Local Docker Compose only. Do not use these values in production, CI secrets, or Kubernetes.** Production identity is an external OIDC provider. This repository does not ship production passwords.

| Use | Username | Password |
| --- | --- | --- |
| Keycloak Admin Console (`http://localhost:8081`) | `admin` | `admin-dev-only` |
| Librarian (Swagger Authorization Code + PKCE) | `librarian` | `librarian-dev-only` |
| PostgreSQL (`library_manager`) | `postgres` | `postgres` |
| Redis | (none) | (none) |

The librarian user has the realm role `librarian`. Audience on access tokens is `library-manager-api`.

## Authentication and Swagger PKCE

1. Open http://localhost:8080/swagger
2. Authorize with client `library-manager-swagger`
3. Flow: Authorization Code with PKCE (`S256`)
4. Redirect URI (the only registered value): `http://localhost:8080/swagger/oauth2-redirect.html`
5. Sign in as `librarian` / `librarian-dev-only`

The browser talks to Keycloak at `http://localhost:8081/realms/library-manager`. The API container fetches OIDC metadata from the Compose DNS name `keycloak` (`Authentication:MetadataAddress`) and validates JWT `iss` as `http://localhost:8081/realms/library-manager`. Keycloak `hostname-backchannel-dynamic` keeps JWKS reachable on the internal network.

The API never exposes a login or token endpoint. Direct Access Grants are disabled on every client in `infrastructure/keycloak/library-manager-realm.json`. Do not use Resource Owner Password Credentials against Keycloak. Obtain a Bearer token only through Swagger: Keycloak login, Authorization Code + PKCE S256, then call the API.

Mutations without a Bearer token return HTTP 401. An authenticated token without the flat `roles` claim value `librarian` returns HTTP 403.

## Migrations

Compose sets `Database__ApplyMigrations=true`. The API applies EF Core migrations on startup in Development or when that flag is set.

Tables: `books`, `users`, `loans`, `audit_events`, `idempotency_entries`, `outbox_messages`.

From the host, with PostgreSQL reachable:

```bash
dotnet ef database update --project src/LibraryManager.Infrastructure --startup-project src/LibraryManager.Api
```

## Tests

```bash
dotnet test tests/LibraryManager.UnitTests
dotnet test tests/LibraryManager.IntegrationTests
```

Integration tests use Testcontainers PostgreSQL and Redis. They register a test authentication scheme only when `Testing:UseTestAuth` is true. That setting is false in Compose. Coverage includes last-copy concurrency, sequential and concurrent idempotency, return/cancel, Outbox, JWT, health, HTTP contract locations, Idempotency-Key binding, Result mapping, localization, cache resilience, SQL parameterization, and Keycloak realm configuration (`directAccessGrantsEnabled=false`). See `specs/001-library-manager/quickstart.md` and `specs/002-production-hardening/quickstart.md`.

## Architecture

Four production projects, dependencies pointing inward:

- `LibraryManager.Domain` — Book, User, Loan, AuditEvent
- `LibraryManager.Application` — explicit UseCase classes and capability-specific repository abstractions
- `LibraryManager.Infrastructure` — EF Core/PostgreSQL, Redis, idempotency, Outbox processor
- `LibraryManager.Api` — HTTP, JWT Bearer, Swagger, health, telemetry

The solution does not use CQRS, MediatR, Command/Query/Handler types, or a Generic Repository. Controllers call one UseCase.

## HTTP contracts

Request and response types live in `LibraryManager.Api` under `Contracts/<Feature>/Requests` and `Contracts/<Feature>/Responses`. Controllers do not declare transport records.

List, loan history, and audit list actions return API `PagedResponse<T>` (`items`, `page`, `pageSize`, `totalCount`). Application `PagedResult<T>` and Application DTOs are mapped at the HTTP boundary and are not serialized as the public contract.

## Transport validation

Simple HTTP constraints are enforced before a UseCase runs:

- Request bodies use DataAnnotations (`Required`, `StringLength`, `Range`) on contract types.
- `[ApiController]` returns HTTP 400 `ValidationProblemDetails` automatically. Controllers do not inspect `ModelState`.
- Invalid `Idempotency-Key` never reaches `CreateLoanUseCase`.

HTTP 400 is transport validation. HTTP 422 is a business rule. HTTP 409 is Idempotency-Key payload mismatch. Those statuses stay distinct.

## Idempotency-Key model binding

`POST /loans` binds a strongly typed `IdempotencyKey` with `[FromIdempotencyKey]`. The binder reads the `Idempotency-Key` header, trims it, and rejects missing, empty, whitespace-only, or values longer than 128 characters as ModelState errors (HTTP 400). A 128-character key is valid. The action parameter is not a nullable `string` and does not use `[Required]` or `[StringLength]`.

Durable ownership remains a unique PostgreSQL row on endpoint + key. The request hash is SHA-256 of canonical JSON `{ "bookId", "userId" }`. Same hash replays HTTP 201 with the stored body. A different hash returns HTTP 409. Unexpected failure rolls back key ownership so a retry can proceed.

## Result Pattern and domain validation

Expected Domain and Application outcomes use `Result` / `Result<T>` with `Error` and `ErrorType`. Stable English codes (`Book.NotFound`, `Book.Unavailable`, `Idempotency.PayloadMismatch`, and others in `ErrorCodes`) travel with the error. Localization happens only at the API boundary.

`DomainGuard` replaces repetitive domain if/throw checks (required strings, non-empty Guid, positive integers, UTC timestamps). Domain has no ASP.NET Core, localization, or Infrastructure dependencies. Expected field validation on factories such as `AuditEvent.Create` returns `Result`; it is not thrown as `DomainException`.

`ResultHttpMapper` maps:

| ErrorType | HTTP |
| --- | --- |
| Validation | 400 |
| NotFound | 404 |
| BusinessRule | 422 |
| Conflict | 409 |

Problem Details include localized title/detail, language-neutral `code`, and `correlationId`.

## Localization

Supported cultures: `en-US` (default) and `pt-BR`. `Accept-Language` selects the culture. Omitted or unsupported values fall back to `en-US`. Responses set `Content-Language`.

User-facing binder, DataAnnotations, Result, and unexpected-error text are localized. Structured log templates, metric names, trace names, error codes, and operational logs stay English.

Example:

```bash
curl -sS -H "Authorization: Bearer $TOKEN" -H "Accept-Language: pt-BR" \
  -H "Content-Type: application/json" \
  -d '{"title":"","isbn":"9780441172719","author":"Frank Herbert","totalCopies":1}' \
  http://localhost:8080/books
```

Expected: HTTP 400, `Content-Language: pt-BR`, Portuguese validation text, English-stable `correlationId`.

## Unexpected exceptions

`ApiExceptionHandler` (`IExceptionHandler`) is the HTTP boundary for unexpected failures only. Expected Result failures are not thrown to reach that handler. Unexpected responses are generic localized HTTP 500 Problem Details with `correlationId`. Stack traces, SQL, Redis, and other internals are not returned. `OperationCanceledException` is not handled as a 500.

## Caching and Redis resilience

Redis key `library-manager:books:{bookId}:availability`, TTL 60 seconds, cache-aside for `GET /books/{id}/availability` only. Loan approval never reads Redis.

Infrastructure wraps Redis with `ResilientAvailabilityCacheDecorator`:

- GET failure is treated as a cache miss; PostgreSQL still serves availability.
- SET failure is non-fatal after committed catalog work.
- REMOVE failure is non-fatal, logs an English warning, and increments `library_manager_cache_invalidation_failures`.
- `OperationCanceledException` is never swallowed.

Application UseCases do not catch Redis exceptions. After commit the API awaits `RemoveAsync`; the transactional Outbox retries missed invalidations. `Task.Run` fire-and-forget is not used.

Successful `DeactivateBook` writes the mutation, `AuditEvent`, and `BookAvailabilityChanged` Outbox row in one PostgreSQL transaction, then invalidates the availability cache after commit. A previously cached active value is not left as the observable GET result.

## Outbox

Availability-changing transactions write an Outbox row in the same DbContext transaction. `OutboxProcessor` claims with `FOR UPDATE SKIP LOCKED`, stores `LockedBy` / `LockedUntilUtc`, commits, then talks to Redis. Expired leases can be recovered. Delivery is at-least-once; `DEL` is idempotent.

## NuGet audit

`Directory.Build.props` sets `NuGetAudit=true` and `NuGetAuditMode=all`. With `TreatWarningsAsErrors`, NU1903 (high) and NU1904 (critical) fail the build for direct and transitive packages. Auditing is not globally disabled and those codes are not suppressed with `NoWarn`. Remediation is upgrade, replacement, or documented risk treatment. The API does not reference `OpenTelemetry.Instrumentation.StackExchangeRedis`; cache spans use the `LibraryManager` `ActivitySource` (`availability_cache.get`, `.set`, `.remove`).

```bash
dotnet package list --vulnerable --include-transitive
dotnet build
```

## SQL parameterization

Runtime SQL values use database parameters. Production code does not concatenate user or runtime input into `ExecuteSqlRaw` / `FromSqlRaw`. Existing `ExecuteSqlInterpolatedAsync` calls in `BookRepository`, `LoanRepository`, and `IdempotencyStore` are parameterized and are not rewritten for style.

## Concurrency

The last remaining copy is reserved with a single conditional update:

`UPDATE books SET available_copies = available_copies - 1 WHERE id = @id AND is_active AND available_copies > 0`

Exactly one concurrent request wins; the other receives HTTP 422. Availability never goes negative. Process memory, `SemaphoreSlim`, and Redis locks are not used for this invariant.

## Audit

Successful mutations persist `AuditEvent` in the same PostgreSQL transaction. Actor is JWT `sub`. Correlation id comes from `X-Correlation-ID` (or a generated value) and is returned on the response. Rejected mutations do not write a success audit for that operation.

`GET /audit-events` is librarian-only.

## Observability

Structured console logs, `X-Correlation-ID`, `ActivitySource` name `LibraryManager`. OTLP export when `OpenTelemetry:OtlpEndpoint` is set. Named instruments include loan create, unavailable, idempotency replays, loan duration, cache invalidation failures, and Outbox processed/failures/pending.

## Why 2–11 API replicas remain correct

API instances are stateless. PostgreSQL owns inventory and idempotency uniqueness. Audit and Outbox rows share the business transaction. Outbox workers coordinate with database leases, not in-process locks. Redis never authorizes a loan. Adding replicas cannot create extra last-copy loans, duplicate idempotent loans, or restore inventory twice.

Kubernetes baseline manifests belong under `deploy/kubernetes/` and must not include a Keycloak workload. Local identity is Compose-only.

## Kubernetes

These manifests assume PostgreSQL, Redis, and an **external** OIDC provider already exist in the cluster. They do not deploy Keycloak.

1. Set `Authentication__Authority` (issuer URL) and `Authentication__Audience` in `deploy/kubernetes/configmap.yaml`.
2. Replace the `REPLACE_WITH_*` placeholders in `deploy/kubernetes/secret.yaml` in the cluster. Do not commit production passwords.
3. Point `deployment.yaml` at a built `library-manager-api` image in your registry.
4. Apply:

```bash
kubectl apply -f deploy/kubernetes/
```

Sample replica count is **2**. Scale between **2 and 11**:

```bash
kubectl scale deployment library-manager-api --replicas=2
kubectl scale deployment library-manager-api --replicas=11
```

Probes: liveness `GET /health/live`, readiness `GET /health/ready`. CPU request `100m` / limit `500m`; memory request `256Mi` / limit `512Mi`.

That range stays correct because API pods are stateless, PostgreSQL owns inventory and idempotency, Outbox uses database leases, and Redis never authorizes a loan. Apply EF Core migrations out of band; the Deployment does not run them on every replica.
