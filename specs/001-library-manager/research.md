# Research: Library Manager API

## Clean Architecture without CQRS or MediatR

- **Decision**: Four projects (`Domain`, `Application`, `Infrastructure`, `Api`) with explicit `*UseCase` classes and capability-specific repositories. Ban Command/Query/Handler/`IRequest`/MediatR/Generic Repository types and names.
- **Rationale**: Constitution II and FR-064 require readable, evaluation-friendly application flow. Controllers call one UseCase. Dependencies point inward.
- **Alternatives considered**: MediatR pipeline (rejected: hides flow, banned terminology). Generic `IRepository<T>` (rejected: invents unused methods and weakens intent). Vertical-slice folders that still name types Command/Handler (rejected: CQRS terminology).

## .NET 10 and ASP.NET Core Web API

- **Decision**: Target `net10.0`. Host with ASP.NET Core controllers or minimal APIs grouped by resource; prefer controllers for Swagger operation IDs and filter conventions. Package versions aligned to 10.0.x for ASP.NET, EF Core, JwtBearer, and MVC.Testing.
- **Rationale**: Stakeholder-mandated LTS stack. `net10.0` is the correct TFM.
- **Alternatives considered**: .NET 8/9 (rejected: stack specifies .NET 10). Minimal APIs only (acceptable later if OpenAPI and filters stay equivalent; controllers are the default for this plan).

## PostgreSQL atomic availability

- **Decision**: Reserve copies with a single conditional `UPDATE books SET available_copies = available_copies - 1 WHERE id = @bookId AND is_active = TRUE AND available_copies > 0`. Return/cancel use `UPDATE loans SET status = ... WHERE id = @id AND status = 'Active'` and increment availability only when that update affects one row. TotalCopies changes compute borrowed = `total_copies - available_copies` in the same statement and reject `new_total < borrowed`.
- **Rationale**: Row-level atomicity is replica-safe. `ExecuteUpdateAsync` or Npgsql parameterized SQL via `IBookRepository`/`ILoanRepository` keeps the invariant out of process memory.
- **Alternatives considered**: Read then write in EF (TOCTOU). `SemaphoreSlim` or Redis locks (constitution-forbidden). `SERIALIZABLE` everywhere (unnecessary if the conditional update is the reservation).

## Unique Active loan per User and Book

- **Decision**: Partial unique index `ux_loans_user_book_active ON loans (user_id, book_id) WHERE status = 'Active'`.
- **Rationale**: Enforces FR-017 under concurrency without application locks.
- **Alternatives considered**: Application `AnyAsync` check only (racy). One-loan-per-user-global (rejected: spec allows many Active loans across books).

## Durable idempotency

- **Decision**: Infrastructure entity `IdempotencyEntry` with unique `(endpoint, key)`, `request_hash` (SHA-256 hex of canonical JSON `{ "bookId", "userId" }` with stable property order), `response_status`, `response_body`, timestamps. `INSERT ... ON CONFLICT (endpoint, key) DO NOTHING RETURNING` then `SELECT`. Same hash replays HTTP 201 with the stored loan body; different hash → HTTP 409. Completing the response updates the row in the same transaction. Rollback removes ownership. User Story 3 only reserves the key; User Story 4 owns hash, replay, 409, and rollback.
- **Rationale**: Constitution IV: uniqueness, not a pre-check, is the concurrency control. SHA-256 matches the mandated hash. HTTP 201 on replay matches create so clients do not treat a retry as a different success class (FR-027).
- **Alternatives considered**: Memory cache of keys (lost on restart, split-brain). Compare raw body strings without canonicalization (JSON whitespace false conflicts). Keep ownership after failure (blocks legitimate retry). Replay as HTTP 200 (rejected: spec FR-027).

## Transactional Outbox and multi-replica claiming

- **Decision**: `OutboxMessage` mapped in `LibraryDbContext` but not a Domain type. `IOutboxWriter` inserts in the current EF transaction. `OutboxProcessor` claims with `FOR UPDATE SKIP LOCKED`, sets `locked_by` / `locked_until_utc`, commits, then performs Redis I/O. Success sets `processed_at_utc`. Failure increments `attempt_count`, stores `last_error`, sets bounded exponential `next_attempt_at_utc`, clears or expires lease. Lease TTL ~30s. Batch size small (e.g. 10). FR-046 is the general Outbox rule for reliability-sensitive follow-up. FR-043 is FR-046 applied to availability-changing transactions, plus post-commit Redis invalidation.
- **Rationale**: Constitution V and the mandated claim/lease algorithm. Holding a transaction open during Redis would stall PostgreSQL and couple availability of the database to cache latency. Splitting FR-043 and FR-046 avoids two overlapping general Outbox requirements.
- **Alternatives considered**: Process Redis inside the claim transaction (rejected). Kafka/RabbitMQ as the first hop (extra moving parts; Outbox still required). `LISTEN/NOTIFY` only (not durable across partitions).

## Redis cache-aside

- **Decision**: Cache key `library-manager:books:{bookId}:availability`, JSON payload `{ "bookId", "availableCopies", "totalCopies", "isActive" }`, TTL 60 seconds. `GetBookAvailabilityUseCase` reads cache first, on miss loads PostgreSQL and sets cache. Create/return/cancel/total-copy changes never read Redis. After commit, `await RemoveAsync`. Consumer `DEL` is idempotent.
- **Rationale**: Spec cache endpoint + short TTL. 60s is minutes-scale bounded without serving stale availability for long after a missed invalidation.
- **Alternatives considered**: Write-through on loan (still not used for decisions; more complexity). TTL of hours (violates short bound). Fire-and-forget `Task.Run` (forbidden).

## JWT resource server and flat roles

- **Decision**: `AddJwtBearer` with Authority and Audience from configuration. Validate issuer, audience, signature, lifetime. `MapInboundClaims = false`. `RoleClaimType = "roles"`. Actor is claim `sub`. Policy `Librarian` = role `librarian`. Mutations and GET `/audit-events` require Librarian. Other reads are authenticated without Librarian. Health is anonymous. Keycloak protocol mapper emits multivalued flat `roles` on the access token (not nested `realm_access`).
- **Rationale**: Spec authentication block; ASP.NET `IsInRole` does not read nested Keycloak JSON without a mapper. Disabling inbound claim maps keeps `sub` as `sub` for `ICurrentUserContext`.
- **Alternatives considered**: Custom username/password token endpoint (forbidden). Nested `realm_access.roles` parsing in middleware (works but fights the mandated flat claim). IdentityServer in-process (out of scope).

## Keycloak 26.7.2 local import

- **Decision**: Image `quay.io/keycloak/keycloak:26.7.2`. Command `start-dev --import-realm`. Mount `infrastructure/keycloak/library-manager-realm.json` to `/opt/keycloak/data/import/`. Realm `library-manager`. Clients `library-manager-api` (audience/resource) and `library-manager-swagger` (public, standard flow, PKCE). Redirect URI only `http://localhost:8080/swagger/oauth2-redirect.html`. Local user with role `librarian` and a development-only password documented as non-production. Bootstrap admin via Compose env, not committed production secrets.
- **Rationale**: Matches Keycloak 26 container import docs and spec realm/client names. Import is skipped if the realm already exists (documented in README: recreate volume to reimport).
- **Alternatives considered**: Manual Admin Console setup (not reproducible). Wildcard redirect URIs (rejected). Shipping Keycloak in Kubernetes manifests (rejected: production IdP is external).

## Local issuer split-horizon

- **Decision**: Publish Keycloak at `localhost:8081`. Browser/Swagger uses `http://localhost:8081/realms/library-manager`. API container metadata URL may be `http://keycloak:8080/realms/library-manager` with `TokenValidationParameters.ValidIssuer` including the issuer value actually written in tokens (typically the hostname Keycloak is configured to advertise). Configure `KC_HOSTNAME` / `KC_HOSTNAME_PORT` so `iss` is stable and documented in README.
- **Rationale**: JWT `iss` must match validation. Compose DNS names are not browser hostnames.
- **Alternatives considered**: Disable issuer validation in Development (weaker, easy to copy to production). Extra reverse proxy (more moving parts than needed for this baseline).

## Swagger UI PKCE

- **Decision**: Swashbuckle Swagger UI with OAuth2 authorization code, `OAuthUsePkce()`, client id `library-manager-swagger`, scopes as defined in the realm. No client secret in the SPA client.
- **Rationale**: Spec FR-040. Swagger UI is the mandated interactive surface.
- **Alternatives considered**: Scalar or built-in OpenAPI JSON only (no PKCE UI). Confidential Swagger client (cannot safely hold a secret in the browser).

## OpenTelemetry and metrics

- **Decision**: `ActivitySource` name `LibraryManager`. ASP.NET, HttpClient, Npgsql, Redis instrumentation. OTLP exporter when `OpenTelemetry:OtlpEndpoint` is set; otherwise traces still create activities for correlation. Instruments use the exact metric names from the spec.
- **Rationale**: Constitution observability + named counters/histograms/gauges for evaluation.
- **Alternatives considered**: Prometheus-only scrape without OTel (less portable). Serilog-only metrics (not traces).

## Correlation

- **Decision**: Middleware reads `X-Correlation-ID`. If missing or not a reasonable token (length/charset), generate a GUID. Set response header, `ICorrelationContext`, `BeginScope`, and Activity baggage/tag.
- **Rationale**: FR-033.
- **Alternatives considered**: W3C `traceparent` only (still used for traces, but AuditEvent needs a stable documented header).

## Health checks

- **Decision**: Map `/health/live` to a self-check (or empty liveness). Map `/health/ready` to Npgsql and Redis checks. Both anonymous. Kubernetes probes match these paths.
- **Rationale**: FR-050. Liveness must not fail solely because Redis is down (that would kill pods that could still be restarted uselessly); readiness must fail when the process should not receive traffic.
- **Alternatives considered**: Single `/health` (cannot distinguish live vs ready). Including Keycloak in ready (API is a resource server; IdP outage is 401, not unready, unless explicitly desired later).

## Testing

- **Decision**: Unit tests with fakes for UseCases and Domain, including CancellationToken cancellation. Integration tests: Testcontainers PostgreSQL + Redis, `WebApplicationFactory`, EF migrations on startup, `Testing:UseTestAuth` scheme that issues claims (`sub`, `roles`) only in the test host. Never enable that scheme in Docker/K8s appsettings. Last-copy (FR-023/FR-024) uses two factory hosts against one PostgreSQL container: shared connection strings, migrations once. Public async Application, Infrastructure, and Api methods take `CancellationToken`.
- **Rationale**: Constitution requires real PostgreSQL for concurrency/idempotency/Outbox. Two hosts prove replica-safe inventory without requiring a live Kubernetes cluster in CI. Hitting Keycloak for every 401/403 test is slow and brittle; a test scheme is allowed if strictly test-environment-only. Separate tests may still obtain a real Keycloak token in Compose smoke tests documented in quickstart.
- **Alternatives considered**: In-memory EF provider (does not honor SKIP LOCKED, jsonb, partial indexes). Always-on fake auth in Development (risk of leaking to Compose). Single-host last-copy only (does not satisfy FR-024). One database container per factory host (rejected: does not share PostgreSQL).

## Docker and Kubernetes

- **Decision**: Multi-stage Dockerfile `mcr.microsoft.com/dotnet/sdk:10.0` → `mcr.microsoft.com/dotnet/aspnet:10.0`. Compose: `library-manager-api`, `postgres`, `redis`, `keycloak`. K8s: Deployment, Service, ConfigMap, Secret references, CPU request `100m` / limit `500m`, memory request `256Mi` / limit `512Mi`, probes as above. Replicas documented 2–11; sample replica count 2. No Keycloak manifests.
- **Rationale**: Mandated delivery surface. Resource numbers are a baseline for evaluation, not a capacity plan.
- **Alternatives considered**: Helm (out of scope for “basic manifests”). Including Redis password in git (use Secret refs).

## Problem Details, HTTP mapping, and clocks

- **Decision**: `AddProblemDetails()` and consistent `application/problem+json`. HTTP 409 is reserved for Idempotency-Key canonical mismatch on POST /loans. HTTP 422 is reserved for business-rule failures (duplicate ISBN/email, TotalCopies too low, unavailable/inactive book, duplicate Active loan, non-Active return/cancel). HTTP 404 is reserved for a named book, User, or loan that does not exist (including POST /loans unknown UserId or BookId). Successful POST /loans and same-hash replay return 201. GET `/audit-events` uses the Librarian policy. `IClock.UtcNow` for all timestamps so tests freeze time. DueAtUtc on create = `BorrowedAtUtc.AddDays(14)` in UTC; reject `DueAtUtc <= BorrowedAtUtc`. List `page` default 1, `pageSize` default 20, maximum 100.
- **Rationale**: FR-051, FR-015, FR-016, FR-027, FR-037, FR-065.
- **Alternatives considered**: Ad-hoc JSON error objects. HTTP 409 for duplicate ISBN/email (rejected: collides with idempotency conflict). HTTP 422 for unknown UserId/BookId (rejected: missing resource is 404). `BorrowedAtUtc.Date.AddDays(14)` as a separate calendar algorithm (rejected: equivalent in UTC to AddDays(14); spec uses AddDays(14) only). `DateTime.UtcNow` scattered (hard to test).

## English nomenclature

- **Decision**: All code, tables (`books`, `users`, `loans`, `audit_events`, `idempotency_entries`, `outbox_messages`), Compose service names, and K8s resource names in English as listed in the plan.
- **Rationale**: Constitution I.
- **Alternatives considered**: Portuguese identifiers (rejected).
