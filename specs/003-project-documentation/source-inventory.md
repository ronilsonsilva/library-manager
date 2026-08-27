# Source inventory (verified 2026-08-27)

Discovery for `003-project-documentation`. **Not** the public README. Controllers and configuration win over the original challenge. If sources disagree, the mismatch is listed under [Discrepancies](#discrepancies) — do not invent a blended contract.

Inspection did **not** change `src/`, `tests/`, or `README.md`.

## Discrepancies

| ID | Sources | Finding | README rule |
|----|---------|---------|-------------|
| D1 | Controllers vs `specs/001-library-manager/contracts/openapi.yaml` | **Methods and routes match** (including `PUT /books/{id}`, history alias, 204 deactivate, `POST /loans` statuses). | Use controllers as the public catalog. |
| D2 | 001 YAML `security: bearerAuth` vs `SwaggerConfiguration.cs` | Runtime Swagger UI is **OAuth2 Authorization Code + PKCE**. 001 YAML is HTTP Bearer. | Document the **local UI** as PKCE. Do not tell readers to paste a hand-built JWT for Compose. Do not invent a third scheme. |
| D3 | `specs/002-production-hardening/contracts/openapi.yaml` | Hardening **overlay**, not a full path list. | Do not use 002 YAML as the public API table. |
| D4 | 001 schema name `BookListResponse` vs API type `PagedResponse<T>` | JSON fields are the same: `items`, `page`, `pageSize`, `totalCount`. | Document the JSON envelope, not the C# type name, as the HTTP contract. |
| D5 | Git | `origin` is `https://github.com/ronilsonsilva/library-manager.git`. Current branch is **`main`**, while spec artifacts live under `specs/003-project-documentation/`. | Clone URL is verified. Do not claim the working tree is on branch `003-project-documentation` unless `git branch` says so. |
| D6 | No `PATCH` | Zero `HttpPatch` / YAML `patch:` in the repo. | Do not document PATCH. |

No controller vs 001 method/route mismatch that blocks documentation. No application-code change required.

## Production HTTP (public)

Authorization: **Librarian** = policy `Librarian` + role `librarian`. **Authenticated** = `[Authorize]`. **Anonymous** = no token.

| Method | Route | Auth | Request | Success | Errors (typical) |
|--------|-------|------|---------|---------|------------------|
| POST | `/books` | Librarian | `CreateBookRequest` `{ title, isbn, author, totalCopies }` | **201** `BookResponse` + Location GET `/books/{id}` | 400, 401, 403, 422 (e.g. duplicate ISBN) |
| GET | `/books` | Authenticated | query `page` (default 1), `pageSize` (default 20, max 100 **clamped**), `isActive` | **200** `PagedResponse<BookResponse>` | 401 |
| GET | `/books/{id}` | Authenticated | path Guid | **200** `BookResponse` | 401, 404 |
| GET | `/books/{id}/availability` | Authenticated | path Guid | **200** `BookAvailabilityResponse` | 401, 404 |
| GET | `/books/{id}/loans` | Authenticated | path Guid; page/pageSize | **200** `PagedResponse<LoanResponse>` | 401, 404 |
| GET | `/books/{id}/history` | Authenticated | **Same action** as `/loans` | same | same |
| PUT | `/books/{id}` | Librarian | `UpdateBookRequest` `{ title, author, totalCopies }` (no ISBN; not partial) | **200** `BookResponse` | 400, 401, 403, 404, 422 (totalCopies below borrowed) |
| DELETE | `/books/{id}` | Librarian | path Guid | **204** logical deactivate | 401, 403, 404 |
| POST | `/users` | Librarian | `CreateUserRequest` `{ name, email }` | **201** `UserResponse` (**no** Location) | 400, 401, 403, 422 (duplicate email) |
| GET | `/users/{id}/loans` | Authenticated | path Guid; page/pageSize | **200** `PagedResponse<LoanResponse>` | 401, 404 |
| POST | `/loans` | Librarian | header `Idempotency-Key`; body `{ bookId, userId }` | **201** `LoanResponse` (create **and** same-hash replay) | 400, 401, 403, 404, 409, 422 |
| POST | `/loans/{id}/return` | Librarian | path Guid | **200** `LoanResponse` | 401, 403, 404, 422 (not Active) |
| POST | `/loans/{id}/cancel` | Librarian | path Guid | **200** `LoanResponse` | 401, 403, 404, 422 (not Active) |
| GET | `/audit-events` | Librarian | `page`, `pageSize`, `entityType`, `entityId` | **200** `PagedResponse<AuditEventResponse>` | 401, 403 |
| GET | `/health/live` | Anonymous | — | **200** process-only (default health writer, not Problem Details) | — |
| GET | `/health/ready` | Anonymous | — | **200** if PG+Redis up; **503** if not. Does **not** check Keycloak | 503 |

Global header: `X-Correlation-ID` (echo or generate). JSON camelCase.

`ResultHttpMapper`: Validation 400, NotFound 404, BusinessRule 422, Conflict 409. 401/403 from JWT challenge/forbidden (English titles, not localized).

### Excluded from public table

- `GET /security/me`, `POST /security/librarian-probe` — only if `Testing:UseTestAuth`
- `GET /__test/unexpected-error` — only environment `Testing`

Do not invent: `PATCH /books/{id}`, `GET /users`, `GET /users/{id}`, `GET /loans`, `GET /loans/{id}`, unsuffixed `GET /health`, token endpoints.

## Idempotency-Key (`POST /loans` only)

- Header name: `Idempotency-Key` (`IdempotencyKey.HeaderName`)
- Trim; length 1–128; missing/empty/whitespace/>128 → ModelState **400** before UseCase
- Store uniqueness: endpoint `"POST /loans"` + key (`ON CONFLICT DO NOTHING`)
- Hash: SHA-256 hex lowercase of camelCase JSON `{ bookId, userId }`
- Same hash → **201** stored body, no second loan
- Different hash → **409** `Idempotency.PayloadMismatch`
- In-progress row without stored 201 body → `InvalidOperationException` → unexpected **500** (not a wait API)
- Return/cancel are **not** key-idempotent; they use `UPDATE loans ... WHERE status = 'Active'`

## Environment variables (API)

Verified names (no `Jwt:` / `OTEL_*`):

`ASPNETCORE_ENVIRONMENT`, `ASPNETCORE_URLS`, `ConnectionStrings__Postgres`, `ConnectionStrings__Redis`, `Authentication__Authority`, `Authentication__Audience`, `Authentication__MetadataAddress`, `Authentication__ValidIssuers__0`, `Authentication__ValidIssuers__1`, `Testing__UseTestAuth`, `Database__ApplyMigrations`, `Outbox__ProcessorEnabled`, `Outbox__BatchSize`, `Outbox__LeaseSeconds`, `Outbox__PollIntervalMilliseconds`, `Outbox__MaxBackoffSeconds`, `OpenTelemetry__OtlpEndpoint`.

Optional appsettings: `Logging:LogLevel:*`, `AllowedHosts`.

Compose-only extras: Keycloak `KC_*`, Postgres `POSTGRES_*`.

K8s ConfigMap: Authority, Audience. Secret: Postgres/Redis with `REPLACE_WITH_*`.

Fallbacks if connection strings omitted: localhost postgres/redis in DI/health/cache code. Prefer Compose/K8s values in the README.

`Authentication:Authority` + `Audience` required unless TestAuth. TestAuth **throws** if Production.

`Database:ApplyMigrations` **or** `IsDevelopment()` → `MigrateAsync()`. Compose `true`; K8s `false`. Single migration: `20260826022736_InitialCreate`. Contributor add: `dotnet ef migrations add` against Infrastructure + Api startup (workflow exists; not run on every K8s replica).

## Local URLs (Compose)

| What | URL |
|------|-----|
| API | `http://localhost:8080` |
| Swagger (Development only) | `http://localhost:8080/swagger` |
| Keycloak | `http://localhost:8081` (container 8080) |
| Live | `http://localhost:8080/health/live` |
| Ready | `http://localhost:8080/health/ready` |
| Postgres | `localhost:5432` |
| Redis | `localhost:6379` |

Clone (verified origin): `https://github.com/ronilsonsilva/library-manager.git`

## Keycloak (Compose)

- Image build `infrastructure/keycloak`; command `start-dev --import-realm`
- Realm `library-manager`; role `librarian`; user `librarian` / `librarian-dev-only` (**local only**)
- Clients: `library-manager-api` (resource server, no user flows); `library-manager-swagger` (standard + PKCE S256, redirect `http://localhost:8080/swagger/oauth2-redirect.html`)
- **Every** client `directAccessGrantsEnabled: false`
- Audience `library-manager-api`
- Import skipped if realm already exists in the container
- API never issues tokens

## Concurrency (PostgreSQL)

Reserve (last copy):

```sql
UPDATE books SET available_copies = available_copies - 1, updated_at_utc = @now
WHERE id = @id AND is_active = TRUE AND available_copies > 0
```

Restore (return/cancel): `available_copies = available_copies + 1 WHERE available_copies < total_copies`

Total copies: single conditional UPDATE so new total ≥ borrowed.

Loan complete: `UPDATE loans SET status = ... WHERE id = @id AND status = 'Active'` (one winner).

No process locks, no Redis locks for these invariants. Check constraint `available_copies >= 0`.

## Redis

- Key: `library-manager:books:{bookId}:availability`
- TTL: **60** seconds
- Cache-aside for **GET availability only**
- Loan create never reads Redis; `RemoveAsync` after commit
- `ResilientAvailabilityCacheDecorator`: GET failure → miss; SET/REMOVE failure non-fatal; REMOVE increments `library_manager_cache_invalidation_failures`
- Tests confirm Redis-down GET still matches PostgreSQL; stale Redis cannot authorize/block a loan

## Outbox

- Type `BookAvailabilityChanged`; same EF transaction as mutation
- Claim: `FOR UPDATE SKIP LOCKED`; `locked_by` / `locked_until_utc`; **commit then** Redis `DEL`
- Processor injects keyed raw Redis (not decorator) so failures retry
- Defaults: batch 10, lease 30s, poll 2s, max backoff 60s
- At-least-once; `DEL` idempotent
- Integration WAF: `Outbox:ProcessorEnabled=false`; tests call `ProcessBatchAsync`
- Dual-worker test: two **worker ids**, one processor — not two API hosts

## Custom metrics (recorded)

Meter `LibraryManager`:

| Instrument | Tested? |
|------------|---------|
| `library_manager_loans_created` | Yes |
| `library_manager_loans_unavailable` | Yes |
| `library_manager_loan_duration` | Yes |
| `library_manager_cache_invalidation_failures` | Yes |
| `library_manager_idempotency_replays` | **No test** |
| `library_manager_outbox_processed` | **No test** |
| `library_manager_outbox_failures` | **No test** |
| `library_manager_outbox_pending` (gauge) | **No test** |

`ActivitySource` name `LibraryManager`; cache spans `availability_cache.get|set|remove` (unit-tested). OTLP only if `OpenTelemetry:OtlpEndpoint` set. JSON console logs + UTC. Correlation on HTTP and spans via `X-Correlation-ID`.

## Health

- Live: `Predicate = _ => false` — **200** while process runs, even if PG/Redis down
- Ready: NpgSql + Redis, 2s timeout, tag `ready` → 503 if down
- K8s probes match these paths on port 8080

## Docker Compose

Services: `library-manager-api` (8080), `postgres` (`postgres:16-alpine`, 5432, volume `postgres_data`), `redis` (`redis:7-alpine`, 6379), `keycloak` (8081). API waits for postgres/redis **healthy**, keycloak **started**. Dockerfile: `mcr.microsoft.com/dotnet/sdk:10.0` build, `aspnet:10.0` runtime, `EXPOSE 8080`, `ENTRYPOINT dotnet LibraryManager.Api.dll`.

Reset volumes: `docker compose down -v` (removes `postgres_data`; not previously documented in README).

## Kubernetes (`deploy/kubernetes/`)

| Kind | Name | Notes |
|------|------|--------|
| Deployment | `library-manager-api` | replicas **2**; image `library-manager-api:latest`; CPU 100m/500m; memory 256Mi/512Mi; live/ready probes; `Database__ApplyMigrations=false`; `Testing__UseTestAuth=false`; `ASPNETCORE_ENVIRONMENT=Production` |
| Service | `library-manager-api` | ClusterIP 8080 |
| ConfigMap | `library-manager-api` | Authority, Audience |
| Secret | `library-manager-api` | connection-string placeholders |

No Keycloak workload, no Ingress, no HPA. Manifests assume external PG, Redis, OIDC. **No live cluster in this repo.**

## Unit test areas (`tests/LibraryManager.UnitTests`)

Domain: Book, User, Loan, AuditEvent, Result, DomainGuard. Application: pagination, canonical hash, idempotency rollback, cancellation. API: binder, exception handler. Infrastructure: cache decorator, Redis activities. Assembly smoke.

## Integration scenarios (`tests/LibraryManager.IntegrationTests`)

Matrix rows in `contracts/test-matrix.md` **all have matching tests** (re-verified): last-copy two hosts + repeats; return races; idempotency sequential/409/concurrent/rollback/missing key; cache hit/stale/invalidate/fail/Redis-down/SET fail/deactivation; Outbox persist/process/retry/lease/SKIP LOCKED/idempotent DEL; auth 401/403/librarian probe; audit actor; health live/ready; telemetry four meters; audit query/reject; Accept-Language; contract locations; SQL parameterization; realm DAG; TestAuth production guard.

**Also exist** (not every row needs to be in the README matrix): binder length/trim, book catalog CRUD, body 400s, user registration, Result HTTP mapping, unexpected 500, Docker/K8s file asserts, NuGet audit props, extra localization cases. FR-028: include important existing tests; omit invented rows. Do **not** hard-code suite sizes.

## Known limitations (code/spec backed)

- K8s does not deploy Keycloak/PG/Redis; sample replicas 2; no migrate-on-pod
- `dotnet test` does not start Keycloak; 401/403 use TestAuth probes
- Ready does not check Keycloak; live has no dependency checks
- Swagger only in Development
- Idempotency: `POST /loans` only; no TTL; in-progress key → 500
- GET availability can stay stale until TTL/Outbox if `Remove` fails; loans still use PostgreSQL
- Outbox at-least-once; hosted processor off in integration hosts
- 401/403 titles hardcoded English
- Keycloak import skipped if realm already exists
- No patron UI, payments, reservations, per-copy barcodes (001 out of scope)
- Tests do not instantiate 11 hosts
- Local Compose credentials are **dev-only**

## Ignore files (setup verification)

`.gitignore` and `.dockerignore` already exist with `bin/`, `obj/`, `.env*`. No changes.

## Catalog comparison result

- `contracts/endpoint-catalog.md` matches controllers + health + 001 paths.
- `contracts/configuration-catalog.md` matches appsettings, Compose, K8s, and bound code keys.
- `contracts/test-matrix.md` rows map to real tests; it is a **subset** of the suite (intentional).
