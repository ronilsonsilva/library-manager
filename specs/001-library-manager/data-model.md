# Data Model: Library Manager API

## Domain entities

These types live in `LibraryManager.Domain`. They have no EF, Redis, or HTTP attributes.

### Book

| Field | Type | Rules |
|-------|------|--------|
| Id | Guid | Generated on create |
| Title | string | Required, trimmed, max 500 |
| Isbn | string | Required, unique, immutable after create, max 32 |
| Author | string | Required, trimmed, max 500 |
| TotalCopies | int | >= 1 on create; later updates must stay >= currently borrowed copies |
| AvailableCopies | int | On create equals TotalCopies; never negative; never greater than TotalCopies |
| IsActive | bool | true on create; DELETE /books/{id} sets false; never hard-deleted |
| CreatedAtUtc | DateTime | IClock.UtcNow |
| UpdatedAtUtc | DateTime | IClock.UtcNow on mutation |

No per-copy identifiers. Inventory is only TotalCopies and AvailableCopies.

Borrowed copies (derived, not stored): `TotalCopies - AvailableCopies`.

### User

Domain borrower/reader. Not the JWT caller.

| Field | Type | Rules |
|-------|------|--------|
| Id | Guid | Generated on create; this is POST /loans `userId` |
| Name | string | Required, trimmed, max 200 |
| Email | string | Required, unique, case-insensitive unique index, max 320 |
| CreatedAtUtc | DateTime | UTC |

### LoanStatus

Enum (persisted as string): `Active`, `Returned`, `Cancelled`.

### Loan

| Field | Type | Rules |
|-------|------|--------|
| Id | Guid | Generated on create |
| BookId | Guid | FK books |
| UserId | Guid | FK users (borrower) |
| Status | LoanStatus | Create = Active |
| BorrowedAtUtc | DateTime | IClock.UtcNow |
| DueAtUtc | DateTime | Must be later than BorrowedAtUtc; on create = BorrowedAtUtc.AddDays(14) in UTC |
| ReturnedAtUtc | DateTime? | Set when Returned |
| CancelledAtUtc | DateTime? | Set when Cancelled |

Transitions: `Active → Returned`, `Active → Cancelled` only. Terminal states do not transition. At most one Active loan per (UserId, BookId).

### AuditEvent

Business entity, same transaction as the mutation.

| Field | Type | Rules |
|-------|------|--------|
| Id | Guid | Generated |
| EntityType | string | e.g. Book, User, Loan |
| EntityId | Guid | Target entity |
| Action | string | e.g. BookCreated, BookUpdated, BookDeactivated, UserCreated, LoanCreated, LoanReturned, LoanCancelled |
| ActorId | string | JWT `sub` from ICurrentUserContext |
| OccurredAtUtc | DateTime | IClock.UtcNow |
| CorrelationId | string | ICorrelationContext |
| DataJson | string | JSON object of contextual fields; stored as jsonb |

Rejected mutations do not write a success AuditEvent.

## Infrastructure persistence (not Domain)

### IdempotencyEntry

Table `idempotency_entries`. Owned by Infrastructure.

| Field | Type | Rules |
|-------|------|--------|
| Id | Guid | Generated |
| Endpoint | string | `POST /loans` |
| Key | string | Idempotency-Key header, max 128 |
| RequestHash | string | SHA-256 hex of canonical request |
| ResponseStatus | int? | Set when completed; successful create and same-hash replay store and return HTTP 201 |
| ResponseBody | string? | JSON snapshot for replay (same body as the original 201) |
| CreatedAtUtc | DateTime | UTC |
| CompletedAtUtc | DateTime? | UTC |

Unique `(Endpoint, Key)`. Inserted in the business transaction; rolled back on unexpected failure.

Canonical request JSON (stable property order):

```json
{"bookId":"<guid>","userId":"<guid>"}
```

UTF-8 bytes → SHA-256 → lowercase hex.

### OutboxMessage

Table `outbox_messages`. Owned by Infrastructure. Written through `IOutboxWriter`.

| Field | Type | Rules |
|-------|------|--------|
| Id | Guid | Generated |
| Type | string | `BookAvailabilityChanged` |
| PayloadJson | string | jsonb; includes bookId and correlationId |
| OccurredAtUtc | DateTime | UTC |
| ProcessedAtUtc | DateTime? | Set on success |
| AttemptCount | int | Default 0 |
| NextAttemptAtUtc | DateTime | Default OccurredAtUtc |
| LockedUntilUtc | DateTime? | Lease |
| LockedBy | string? | Worker instance id |
| LastError | string? | Truncated error text |

## Relationships

```text
User 1 ──< Loan >── 1 Book
Loan mutations ──< AuditEvent (EntityType=Loan)
Book mutations ──< AuditEvent (EntityType=Book)
User mutations ──< AuditEvent (EntityType=User)
Availability-changing transactions ──< OutboxMessage (Type=BookAvailabilityChanged)
POST /loans ── 1 IdempotencyEntry (per Endpoint+Key)
```

## PostgreSQL constraints and indexes

- `books.isbn` UNIQUE
- `users.email` UNIQUE (lower(email) unique index)
- `loans_user_book_active` UNIQUE (user_id, book_id) WHERE status = 'Active'
- `loans.book_id` and `loans.user_id` FK RESTRICT (no cascade delete)
- `audit_events.occurred_at_utc` btree; optional (entity_type, entity_id)
- `idempotency_entries` UNIQUE (endpoint, key)
- `outbox_messages` index on (`processed_at_utc`, `next_attempt_at_utc`, `locked_until_utc`) for claiming
- CHECK `available_copies >= 0`
- CHECK `available_copies <= total_copies`
- CHECK `total_copies >= 1`
- CHECK loan due_at_utc > borrowed_at_utc (defense in depth)

## State machines

### Book.IsActive

```text
true  --DELETE /books/{id}-->  false (terminal for lending)
```

Existing Active loans remain returnable/cancellable.

### Loan.Status

```text
Active --> Returned
Active --> Cancelled
Returned --> (none)
Cancelled --> (none)
```

Inventory restore happens only when the status UPDATE affects exactly one Active row.

## Validation summary

| Operation | Reject when |
|-----------|-------------|
| Create book | Missing title/isbn/author; TotalCopies < 1; duplicate ISBN (HTTP 422) |
| Update book | ISBN change; TotalCopies < borrowed copies (HTTP 422) |
| Deactivate | Already inactive (idempotent 200/204 allowed) |
| Create user | Missing name/email; duplicate email |
| Create loan | Missing Idempotency-Key (400); unknown UserId/BookId (404); inactive book, zero availability, duplicate Active (user, book) (422); hash conflict (409) |
| Return/cancel | Loan missing (404); not Active (422) |

## Cache document

Key: `library-manager:books:{bookId}:availability`

Value: `{ "bookId": "...", "availableCopies": n, "totalCopies": n, "isActive": true }`

TTL: 60 seconds. Not read by CreateLoan/ReturnLoan/CancelLoan.
