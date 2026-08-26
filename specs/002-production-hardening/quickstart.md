# Quickstart: Production Hardening

Validation guide for `002-production-hardening`. Implementation lives in `src/` and `tests/`. Resource JSON and lending rules stay compatible with `001-library-manager` except where HTTP transport validation is specified here.

## Prerequisites

- Docker and Docker Compose
- .NET 10 SDK (`net10.0`)
- Repository checkout at the solution root

## Local stack

```bash
docker compose up --build
```

Expected: `library-manager-api` (http://localhost:8080), `postgres`, `redis`, `keycloak` (http://localhost:8081).

If the Keycloak volume already has a realm, delete it so `directAccessGrantsEnabled=false` on `library-manager-swagger` is imported.

### Health (unchanged)

```bash
curl -sS http://localhost:8080/health/live
curl -sS http://localhost:8080/health/ready
```

Expected: HTTP 200, no token.

## Authentication (PKCE only)

1. Open http://localhost:8080/swagger
2. Authorize with `library-manager-swagger` (Authorization Code + PKCE S256)
3. Realm `library-manager`, audience `library-manager-api`
4. Local user `librarian` / `librarian-dev-only` (Compose only)

Do **not** use Resource Owner Password Credentials or `grant_type=password` against Keycloak as a documented login path. The API still never issues tokens.

Integration tests continue to use the test authentication scheme, not password grant. `dotnet test` does not start Keycloak; CI proves Direct Access Grants are disabled by asserting `infrastructure/keycloak/library-manager-realm.json`.

### Optional operator check (Compose only, not `dotnet test`)

After a fresh realm import, this should **not** return an access token (typically HTTP 400/401 from Keycloak):

```bash
curl -sS -D - -o /dev/null \
  -d "grant_type=password" \
  -d "client_id=library-manager-swagger" \
  -d "username=librarian" \
  -d "password=librarian-dev-only" \
  http://localhost:8081/realms/library-manager/protocol/openid-connect/token
```

## Package audit

From the solution root:

```bash
dotnet package list --vulnerable --include-transitive
dotnet build
```

Expected: build fails on NU1903/NU1904; no `OpenTelemetry.Instrumentation.StackExchangeRedis` package reference.

## Idempotency-Key binding

Replace `$TOKEN`. These must return **HTTP 400** ValidationProblemDetails and create **no loan**:

```bash
curl -sS -D - -o /dev/null -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"bookId":"00000000-0000-0000-0000-000000000001","userId":"00000000-0000-0000-0000-000000000002"}' \
  http://localhost:8080/loans

curl -sS -D - -o /dev/null -H "Authorization: Bearer $TOKEN" -H "Idempotency-Key: " \
  -H "Content-Type: application/json" \
  -d '{"bookId":"00000000-0000-0000-0000-000000000001","userId":"00000000-0000-0000-0000-000000000002"}' \
  http://localhost:8080/loans

curl -sS -D - -o /dev/null -H "Authorization: Bearer $TOKEN" -H "Idempotency-Key: $(python -c "print('x'*129)")" \
  -H "Content-Type: application/json" \
  -d '{"bookId":"00000000-0000-0000-0000-000000000001","userId":"00000000-0000-0000-0000-000000000002"}' \
  http://localhost:8080/loans
```

A 128-character key is accepted (then normal lending/idempotency rules apply). Surrounding whitespace on a valid key is trimmed.

## Localization

```bash
curl -sS -H "Authorization: Bearer $TOKEN" -H "Accept-Language: pt-BR" \
  -H "Content-Type: application/json" \
  -d '{"title":"","isbn":"9780441172719","author":"Frank Herbert","totalCopies":1}' \
  http://localhost:8080/books
```

Expected: HTTP 400, `Content-Language: pt-BR`, Portuguese validation text, English-stable `correlationId` / no translated metric names.

Omit `Accept-Language` or send `en-US` → English (`en-US`) problem text.

## Result vs unexpected errors

- Unknown book id on GET `/books/{id}` → HTTP 404 problem with `code` `Book.NotFound`, localized detail, `correlationId`.
- Unexpected failure → HTTP 500 generic `Problem_Unexpected_Title`, `correlationId`, no stack trace.

## Cache resilience

With Redis stopped (Compose `redis` down) and PostgreSQL up:

```bash
curl -sS -H "Authorization: Bearer $TOKEN" http://localhost:8080/books/<id>/availability
```

Expected: HTTP 200 with catalog data (or 404 if the book is missing), not 5xx because Redis is down.

Deactivate a book that was cached as active; subsequent availability must not present the stale active view. Outbox still stores `BookAvailabilityChanged` in the same transaction as the deactivation.

## Automated tests

```bash
dotnet test
```

Must stay green for last-copy concurrency, sequential/concurrent idempotency, return/cancel concurrency, Outbox, authentication, and health, except assertions updated for ValidationProblemDetails on transport 400s.

New tests must cover binder 400s, localization, Result HTTP mapping, unexpected handler safety, cache decorator behavior (including invalidation-failure metric), cache `LibraryManager` activities, deactivation invalidation, controller contract locations including `PagedResponse` list envelopes, and Keycloak DAG disabled in realm JSON (no live Keycloak required).

## README

After implementation, README must document Accept-Language, Result mapping, Idempotency-Key binding, cache resilience, NuGet audit, SQL parameterization, and PKCE-only Keycloak — with no password-grant example.
