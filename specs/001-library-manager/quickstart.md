# Quickstart: Library Manager API

Validation guide for `001-library-manager`. Implementation lives in `src/` and `tests/`; this file only describes how to prove the system works.

## Prerequisites

- Docker and Docker Compose
- .NET 10 SDK (`net10.0`)
- Repository checkout at the solution root

## Local stack

From the repository root:

```bash
docker compose up --build
```

Expected services: `library-manager-api` (http://localhost:8080), `postgres`, `redis`, `keycloak` (`quay.io/keycloak/keycloak:26.7.2` on http://localhost:8081).

Keycloak imports `infrastructure/keycloak/library-manager-realm.json` on first start (`start-dev --import-realm`). If the realm already exists in the volume, delete the Keycloak volume to reimport.

### Health

```bash
curl -sS http://localhost:8080/health/live
curl -sS http://localhost:8080/health/ready
```

Expected: HTTP 200 without an access token.

## Authentication (Swagger)

1. Open http://localhost:8080/swagger
2. Authorize with client `library-manager-swagger` (Authorization Code + PKCE)
3. Keycloak realm `library-manager`, audience `library-manager-api`
4. Sign in as `librarian` / `librarian-dev-only` (local Compose only; see README)
5. Redirect URI must be `http://localhost:8080/swagger/oauth2-redirect.html` only

The API never accepts username/password to mint JWTs. Direct Access Grants are disabled. Do not use Resource Owner Password Credentials against Keycloak. After Swagger Authorize (Authorization Code + PKCE), the UI sends `Authorization: Bearer` automatically.

Mutations without a token must be HTTP 401. Token without `librarian` in the flat `roles` claim must be HTTP 403.

## Migrations

On API startup in Development/Compose, apply EF Core migrations to PostgreSQL (or run the documented `dotnet ef database update` command from README). Expected: tables `books`, `users`, `loans`, `audit_events`, `idempotency_entries`, `outbox_messages`.

## Smoke: catalog, user, loan

Replace `$TOKEN` and ids.

```bash
curl -sS -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"title":"Dune","isbn":"9780441172719","author":"Frank Herbert","totalCopies":1}' \
  http://localhost:8080/books

curl -sS -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"name":"Ada Lovelace","email":"ada@example.com"}' \
  http://localhost:8080/users

curl -sS -H "Authorization: Bearer $TOKEN" -H "Idempotency-Key: smoke-1" \
  -H "Content-Type: application/json" -H "X-Correlation-ID: smoke-corr-1" \
  -d '{"bookId":"<book-id>","userId":"<user-id>"}' \
  http://localhost:8080/loans
```

Expected:

- First loan: HTTP 201, `status=Active`, `availableCopies` of the book becomes 0
- Repeat the same body and `Idempotency-Key`: HTTP 201, same loan id, availability stays 0
- Same key, different `userId` or `bookId`: HTTP 409
- Duplicate ISBN or duplicate email: HTTP 422
- Second concurrent last-copy loan (different key): one HTTP 201 and one HTTP 422 unavailable; availability never negative
- `GET /books/{id}/availability` may be cached; loan decisions still follow PostgreSQL
- `GET /users/{id}/loans` includes Returned/Cancelled after `POST /loans/{id}/return` or `/cancel`
- `GET /audit-events` requires a librarian token; it shows `ActorId` equal to JWT `sub` and `CorrelationId` `smoke-corr-1`
- Response header `X-Correlation-ID` is present

## Automated tests

```bash
dotnet test tests/LibraryManager.UnitTests
dotnet test tests/LibraryManager.IntegrationTests
```

Integration tests start real PostgreSQL and Redis via Testcontainers. They must not require a human Keycloak login. A test authentication scheme is allowed only when the test host sets `Testing:UseTestAuth`.

Required integration coverage (must fail if the guarantee regresses):

- 401 without authentication
- 403 without librarian authorization (mutations and GET `/audit-events`)
- successful librarian authorization
- authenticated subject persisted as AuditEvent actor
- Duplicate ISBN (HTTP 422)
- ISBN unchanged after PUT
- TotalCopies below borrowed copies (HTTP 422)
- duplicate email (HTTP 422)
- successful loan (HTTP 201)
- unknown UserId or BookId on POST `/loans` (HTTP 404)
- concurrent final-copy loan through two API hosts sharing PostgreSQL (one 201, one 422)
- sequential idempotency replay (HTTP 201, stored body)
- concurrent same Idempotency-Key
- same key with different payload (HTTP 409)
- unexpected failure after Idempotency-Key reserve rolls back ownership
- return
- concurrent duplicate return
- cancellation
- not-Active return/cancel (HTTP 422)
- unknown loan id on return/cancel (HTTP 404)
- historical preservation
- list pagination (page default 1, pageSize default 20, max 100)
- Redis cache hit/miss
- immediate cache invalidation
- transactional Outbox persistence
- Outbox processing
- Outbox retry
- expired Outbox lease recovery
- multiple Outbox workers
- live HTTP 200 when ready is HTTP 503
- cancelled CancellationToken is observed by a public async Application method

## Kubernetes baseline

Apply `deploy/kubernetes/` against a cluster that already has PostgreSQL, Redis, and an external OIDC provider. Set Authority and Audience via ConfigMap/Secret. Do not deploy Keycloak from these manifests.

Probes: liveness `GET /health/live`, readiness `GET /health/ready`.

## Multi-replica check

Scale the API to 2 (and later up to 11) replicas. Repeat last-copy and idempotency races. Expected: still exactly one winning loan per last copy (loser HTTP 422), one owner per Idempotency-Key, same-hash replay HTTP 201, inventory restored at most once on return, Redis never deciding approval. Integration tests prove the last-copy race with two in-process API hosts sharing one PostgreSQL.

## README

The English README must document execution, authentication, migrations, tests, architecture, concurrency, idempotency, audit, caching, Outbox, observability, and why 2–11 replicas remain correct (see plan.md).
