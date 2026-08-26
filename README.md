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
| Librarian (Swagger / local tokens) | `librarian` | `librarian-dev-only` |
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

The API never exposes a login endpoint. A password grant against Keycloak is a local smoke tool only:

```bash
curl -sS -X POST "http://localhost:8081/realms/library-manager/protocol/openid-connect/token" \
  -d "client_id=library-manager-swagger" \
  -d "grant_type=password" \
  -d "username=librarian" \
  -d "password=librarian-dev-only"
```

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

Integration tests use Testcontainers PostgreSQL and Redis. They register a test authentication scheme only when `Testing:UseTestAuth` is true. That setting is false in Compose. Coverage expectations are listed in `specs/001-library-manager/quickstart.md`.

## Architecture

Four production projects, dependencies pointing inward:

- `LibraryManager.Domain` — Book, User, Loan, AuditEvent
- `LibraryManager.Application` — explicit UseCase classes and capability-specific repository abstractions
- `LibraryManager.Infrastructure` — EF Core/PostgreSQL, Redis, idempotency, Outbox processor
- `LibraryManager.Api` — HTTP, JWT Bearer, Swagger, health, telemetry

The solution does not use CQRS, MediatR, Command/Query/Handler types, or a Generic Repository. Controllers call one UseCase.

## Concurrency

The last remaining copy is reserved with a single conditional update:

`UPDATE books SET available_copies = available_copies - 1 WHERE id = @id AND is_active AND available_copies > 0`

Exactly one concurrent request wins; the other receives HTTP 422. Availability never goes negative. Process memory, `SemaphoreSlim`, and Redis locks are not used for this invariant.

## Idempotency

`POST /loans` requires `Idempotency-Key`. Ownership is a unique PostgreSQL row on endpoint + key. The request hash is SHA-256 of canonical JSON `{ "bookId", "userId" }`. Same hash replays HTTP 201 with the stored body. A different hash returns HTTP 409. Unexpected failure rolls back key ownership so a retry can proceed.

## Audit

Successful mutations persist `AuditEvent` in the same PostgreSQL transaction. Actor is JWT `sub`. Correlation id comes from `X-Correlation-ID` (or a generated value) and is returned on the response. Rejected mutations do not write a success audit for that operation.

`GET /audit-events` is librarian-only.

## Caching

Redis key `library-manager:books:{bookId}:availability`, TTL 60 seconds, cache-aside for the availability GET only. Loan approval never reads Redis. After commit the API awaits `RemoveAsync`; Outbox retries missed invalidations. `Task.Run` fire-and-forget is not used.

## Outbox

Availability-changing transactions write an Outbox row in the same DbContext transaction. `OutboxProcessor` claims with `FOR UPDATE SKIP LOCKED`, stores `LockedBy` / `LockedUntilUtc`, commits, then talks to Redis. Expired leases can be recovered. Delivery is at-least-once; `DEL` is idempotent.

## Observability

Structured console logs, `X-Correlation-ID`, `ActivitySource` name `LibraryManager`. OTLP export when `OpenTelemetry:OtlpEndpoint` is set. Named instruments include loan create, unavailable, idempotency replays, loan duration, cache invalidation failures, and Outbox processed/failures/pending.

## Why 2–11 API replicas remain correct

API instances are stateless. PostgreSQL owns inventory and idempotency uniqueness. Audit and Outbox rows share the business transaction. Outbox workers coordinate with database leases, not in-process locks. Redis never authorizes a loan. Adding replicas cannot create extra last-copy loans, duplicate idempotent loans, or restore inventory twice.

Kubernetes baseline manifests belong under `deploy/kubernetes/` and must not include a Keycloak workload. Local identity is Compose-only.
