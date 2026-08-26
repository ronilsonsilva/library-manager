# Data Model: Production Hardening

This feature does not change PostgreSQL tables, Redis key shape, or Domain entity fields from `001-library-manager`. It adds in-process types for expected failures, HTTP metadata, and localization keys.

Existing Domain entities (`Book`, `User`, `Loan`, `AuditEvent`) and Infrastructure rows (`IdempotencyEntry`, `OutboxMessage`) remain as specified in `specs/001-library-manager/data-model.md`.

## Result primitives (Domain)

### ErrorType

Enum: `Validation`, `NotFound`, `BusinessRule`, `Conflict`.

HTTP mapping (API only): 400, 404, 422, 409. HTTP 409 is only Idempotency-Key canonical mismatch.

### Error

| Field | Type | Rules |
|-------|------|--------|
| Code | string | Required, stable English identifier (`Book.NotFound`). Never localized. |
| Type | ErrorType | Required |
| Arguments | object[]? | Optional format args for the API localizer; not display strings |

### Result / Result&lt;T&gt;

Success or a single `Error` (first validation failure is sufficient; DomainGuard may stop at first failure). No exception payload. Not an HTTP type.

## Error codes

Canonical catalog (extend as UseCases migrate; codes stay English):

| Code | Type | Typical HTTP |
|------|------|----------------|
| `Audit.EntityTypeRequired` | Validation | 400 |
| `Audit.EntityIdRequired` | Validation | 400 |
| `Audit.ActionRequired` | Validation | 400 |
| `Audit.ActorIdRequired` | Validation | 400 |
| `Audit.CorrelationIdRequired` | Validation | 400 |
| `Audit.DataJsonRequired` | Validation | 400 |
| `Book.NotFound` | NotFound | 404 |
| `Book.Unavailable` | BusinessRule | 422 |
| `Book.Inactive` | BusinessRule | 422 |
| `Book.DuplicateIsbn` | BusinessRule | 422 |
| `Book.TotalCopiesBelowBorrowed` | BusinessRule | 422 |
| `User.NotFound` | NotFound | 404 |
| `User.DuplicateEmail` | BusinessRule | 422 |
| `Loan.NotFound` | NotFound | 404 |
| `Loan.InvalidState` | BusinessRule | 422 |
| `Loan.DuplicateActive` | BusinessRule | 422 |
| `Idempotency.PayloadMismatch` | Conflict | 409 |

Transport-only failures (missing body fields, Idempotency-Key) use ModelState keys, not these codes.

## DomainGuard

Lightweight Domain helper (no ASP.NET, no `.resx`):

- required non-empty/whitespace string (trim on success)
- non-empty Guid
- positive int (`>= 1`)
- UTC `DateTime` (`Kind` UTC or unspecified treated as UTC per existing clock rules)

`AuditEvent.Create` returns `Result<AuditEvent>` using these rules. Other in-scope factories return `Result<T>` for expected validation only.

## IdempotencyKey (API, not Domain)

Readonly HTTP value.

| Field | Type | Rules |
|-------|------|--------|
| Value | string | Trimmed; length 1–128 after trim; constructed only after successful binding |

Invalid headers never construct an instance and never reach `CreateLoanUseCase`. Durable store still keys on `Value` plus endpoint `POST /loans`.

## HTTP transport records (API)

Request/response types live under `Contracts/`. Application DTOs and `PagedResult<T>` are unchanged and are **not** HTTP contracts. API responses map field-for-field so JSON stays compatible with 001:

- Common: `IdempotencyKey` (header metadata); `PagedResponse<T>` (`Items`/`Page`/`PageSize`/`TotalCount` → JSON `items`, `page`, `pageSize`, `totalCount`)
- Books: `CreateBookRequest`, `UpdateBookRequest`, `BookResponse`, `BookAvailabilityResponse`; list = `PagedResponse<BookResponse>`
- Users: `CreateUserRequest`, `UserResponse`; user loans = `PagedResponse<LoanResponse>`
- Loans: `CreateLoanRequest`, `LoanResponse`; book loan history = `PagedResponse<LoanResponse>`
- Audit: `AuditEventResponse`; list = `PagedResponse<AuditEventResponse>`

`PagedResponse<T>` is an API envelope in `Contracts/Common/` (same folder rule as `IdempotencyKey`). It is not a Domain type and not Application `PagedResult<T>`. Controllers map `PagedResult<TDto>` → `PagedResponse<TResponse>` at the HTTP boundary.

Request annotations (transport only): required title/ISBN/author/name/email; `TotalCopies` range ≥ 1; string lengths matching Domain maxima (title/author 500, ISBN 32, name 200, email 320).

## Availability cache (unchanged payload)

Key `library-manager:books:{bookId}:availability`. Payload `{ bookId, availableCopies, totalCopies, isActive }`. TTL 60s. Decorator does not change payload; it changes failure behavior (miss / non-fatal write / non-fatal remove). On REMOVE failure the decorator records `library_manager_cache_invalidation_failures` via `ILibraryManagerMetrics`. `RedisAvailabilityCache` starts `LibraryManager` activities `availability_cache.get`, `availability_cache.set`, and `availability_cache.remove`.

## Outbox (unchanged)

`BookAvailabilityChanged` payload remains `{ bookId, correlationId }` (or the existing `AvailabilityOutbox.Payload` shape). `DeactivateBook` must write this message in the same transaction as deactivation + `AuditEvent`.

## Localization resources (API)

Not Domain data. Resource keys are identifiers; values are `en-US` / `pt-BR` strings. Operational logs do not read these files.

## State transitions

Loan/book/user lifecycles unchanged. Hardening only changes how expected validation is *represented* (Result vs throw) and how HTTP metadata is bound.
