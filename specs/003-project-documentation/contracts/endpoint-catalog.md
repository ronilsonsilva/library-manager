# Endpoint catalog (production)

Source: controllers + `HealthEndpoints` as of plan date. Re-verify at implement. **Public README table = this list only.**

Authorization: **Librarian** = policy `Librarian` (role `librarian`). **Authenticated** = `[Authorize]`. **Anonymous** = no token.

| Method | Route | Authorization | Notes |
|--------|-------|---------------|--------|
| POST | `/books` | Librarian | Create; **201** `BookResponse` + Location of GET `/books/{id}` |
| GET | `/books` | Authenticated | Paged `page` (default 1), `pageSize` (default 20, max 100, **clamped**), `isActive` |
| GET | `/books/{id}` | Authenticated | 200 / 404 |
| GET | `/books/{id}/availability` | Authenticated | Cache-aside; non-librarian allowed; 200 / 404 |
| GET | `/books/{id}/loans` | Authenticated | Paged history |
| GET | `/books/{id}/history` | Authenticated | **Alias** of `/loans` (same action) |
| PUT | `/books/{id}` | Librarian | Full update (`title`, `author`, `totalCopies`); **not PATCH** |
| DELETE | `/books/{id}` | Librarian | Logical deactivate; **204** |
| POST | `/users` | Librarian | Create; **201** `UserResponse` (**no** Location) |
| GET | `/users/{id}/loans` | Authenticated | Paged |
| POST | `/loans` | Librarian | `Idempotency-Key` required; see below |
| POST | `/loans/{id}/return` | Librarian | **200** `LoanResponse`; not key-idempotent |
| POST | `/loans/{id}/cancel` | Librarian | **200** `LoanResponse`; not key-idempotent |
| GET | `/audit-events` | Librarian | Paged; `entityType`, `entityId` |
| GET | `/health/live` | Anonymous | Process-only; **200** even if PG/Redis down; default health writer (not Problem Details) |
| GET | `/health/ready` | Anonymous | PostgreSQL + Redis only (not Keycloak); 503 if down |

## POST /loans statuses (implemented)

| Status | When |
|--------|------|
| 201 | Created or same-key same-hash replay (`ToCreatedResult`) |
| 400 | Transport validation / missing or invalid `Idempotency-Key` (binder, `[ApiController]`) |
| 401 | Missing or invalid Bearer token |
| 403 | Authenticated without `librarian` |
| 404 | Book or user not found |
| 409 | Same key, different canonical SHA-256 hash of `{ bookId, userId }` |
| 422 | Business rule (e.g. no copies, inactive book, duplicate active loan) |

Header: `Idempotency-Key` (1–128 after trim). Endpoint uniqueness key: `POST /loans`. Hash: SHA-256 of camelCase JSON `{ bookId, userId }`.

Body: `{ "bookId": "<guid>", "userId": "<guid>" }`.

## Excluded from public table

| Route | Why |
|-------|-----|
| GET `/security/me` | Mapped only if `Testing:UseTestAuth` |
| POST `/security/librarian-probe` | Same |
| GET `/__test/unexpected-error` | Mapped only in `Testing` environment |

Do **not** invent: `PATCH /books/{id}`, `GET /users`, `GET /users/{id}`, `GET /loans`, `GET /loans/{id}`, unsuffixed `GET /health`, or token-issuing routes.

001 `contracts/openapi.yaml` uses PUT for update (agrees with controllers). 002 `contracts/openapi.yaml` is a **hardening overlay**, not a complete path list. Runtime Swagger (Development only) uses OAuth2 Authorization Code + PKCE; the 001 YAML security scheme is HTTP `bearerAuth` — document the **UI** flow as PKCE, not as password grant or hand-built JWTs.
