# Feature Specification: Library Manager API

**Feature Branch**: `001-library-manager`

**Created**: 2026-08-25

**Status**: Draft

**Input**: User description: "Build library-manager, a secure production-oriented REST API for managing a library catalog, users and concurrent book loans, including unique ISBN books with logical deactivation, member registration and loan history, lend/return/cancel with durable history, multi-replica last-copy correctness, loan idempotency, durable audit, external identity-provider authentication, availability caching that never authorizes loans, recoverable post-change processing, observability, health endpoints, automated tests, a local composed runtime, basic orchestration manifests, and an English README."

## Clarifications

### Session 2026-08-25

- Q: How is book inventory modeled, how are books deactivated, and how do loan states change? → A: Inventory is TotalCopies and AvailableCopies with no per-copy identifiers; DELETE /books/{id} logically deactivates so no new loans; loan states are Active, Returned, and Cancelled; only Active can transition; concurrent or repeated return/cancel restores inventory at most once; DueAtUtc must be later than BorrowedAtUtc.
- Q: How does a loan name the borrower versus the signed-in caller? → A: User is the domain borrower/reader; the authenticated caller is a separate security identity; POST /loans identifies the borrower by UserId; audit actor is the JWT subject.
- Q: How does local authentication and authorization work for mutations versus health checks? → A: Keycloak realm library-manager, audience library-manager-api, Swagger client library-manager-swagger with Authorization Code and PKCE; librarian role in a flat roles claim; mutations require the Librarian policy (HTTP 401 if unauthenticated, HTTP 403 if authenticated without the role); health is anonymous; the API never accepts passwords or issues JWTs.
- Q: What happens when loan creation is retried or a transaction fails after claiming an idempotency key? → A: Idempotency-Key is mandatory on POST /loans; same key plus same canonical request returns HTTP 201 with the stored loan body; same key plus different canonical request returns HTTP 409; unexpected transaction failures roll back idempotency ownership.
- Q: Where do cache, Outbox, and domain boundaries sit? → A: Redis caches GET /books/{id}/availability only and is never used to approve loans; availability-changing transactions write Outbox messages, attempt immediate Redis invalidation after commit without failing the request, then retry via Outbox; invalidation is idempotent with short TTL; Outbox and idempotency records are infrastructure, not Domain; Domain contains Book, User, Loan, and AuditEvent; Outbox uses an Application abstraction, same DbContext transaction, multi-replica claim/lease, at-least-once, retry metadata, and ProcessedAtUtc; no CQRS. FR-046 is the general Outbox rule; FR-043 is that rule applied to availability-changing transactions (Outbox row plus post-commit invalidation).
- Q: Which HTTP statuses distinguish idempotency conflict from business-rule failure, and what does an idempotent replay return? → A: HTTP 409 is only for Idempotency-Key reused with a different canonical POST /loans body. HTTP 422 is for business-rule failures (duplicate ISBN or email, TotalCopies below borrowed copies, inactive/unavailable book, duplicate Active loan, return/cancel when not Active). A same-key same-hash replay of a successful create returns HTTP 201 with the stored loan body.
- Q: Who may list AuditEvents? → A: GET /audit-events requires the Librarian policy (401 if unauthenticated, 403 if authenticated without librarian). Catalog, User, loan, and availability reads remain authenticated without Librarian.
- Q: How is multi-instance last-copy lending proven in tests, and must async work accept cancellation? → A: Integration tests MUST run the last-copy race through two API hosts that share one PostgreSQL (two WebApplicationFactory instances or equivalent replicas). All public async Application, Infrastructure, and Api methods MUST accept and propagate CancellationToken.
- Q: What HTTP status is used when a named book, User, or loan does not exist? → A: HTTP 404. That includes POST /loans with an unknown UserId or BookId. HTTP 422 is only for business-rule failures on existing resources (FR-065).
- Q: How is the 14-day due date computed? → A: DueAtUtc MUST equal BorrowedAtUtc.AddDays(14) in UTC (exactly 14 × 24 hours). This is the meaning of “14 calendar days” for this API; do not use a local civil calendar or a date-only due date.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Maintain the book catalog (Priority: P1)

A staff caller records books in the catalog with title, ISBN, author, TotalCopies, AvailableCopies, and active state. Individual physical copies have no identifiers; inventory is only those two counts. Staff can look up a book, browse the catalog, correct catalog data, and logically deactivate a book with DELETE /books/{id} so it can no longer receive new loans. Past catalog activity remains visible; nothing is erased as if it never existed.

**Why this priority**: There is no lending service without an authoritative catalog.

**Independent Test**: Create, retrieve, list, update, and deactivate books via logical DELETE; confirm a duplicate ISBN is HTTP 422, ISBN does not change on update, TotalCopies below borrowed is HTTP 422, that deactivated books remain retrievable, and that they cannot receive new loans.

**Acceptance Scenarios**:

1. **Given** a staff caller is authorized, **When** they register a book with a unique ISBN and a positive TotalCopies, **Then** the book is stored as active with AvailableCopies equal to TotalCopies and with no per-copy identifiers.
2. **Given** a book already exists with an ISBN, **When** a staff caller registers another book with the same ISBN, **Then** the system rejects the registration with HTTP 422 and no second book is created.
3. **Given** books exist in the catalog, **When** a staff caller lists or retrieves a book, **Then** they see the current title, ISBN, author, TotalCopies, AvailableCopies, and active state.
4. **Given** an active book, **When** a staff caller updates title, author, or TotalCopies (without going below borrowed copies), **Then** later retrievals show the new values and ISBN is unchanged.
5. **Given** an active book with copies on loan, **When** a staff caller sets TotalCopies below the number currently borrowed, **Then** the update is rejected with HTTP 422 and inventory is unchanged.
6. **Given** an active book, **When** a staff caller deactivates it with DELETE /books/{id}, **Then** the book remains retrievable as inactive and cannot receive new loans.
7. **Given** a deactivated book with prior activity, **When** anyone queries the catalog or related history, **Then** the book and its history are still present.

---

### User Story 2 - Register Users (Priority: P1)

A staff caller registers a User, the domain borrower/reader (formerly referred to as library member). That User may receive loans. The User is not the same identity as the authenticated caller who is signed in to the API. POST /loans names the borrower with UserId.

**Why this priority**: Loans require a registered User distinct from the authenticated caller.

**Independent Test**: Register Users and retrieve them; confirm a duplicate email is HTTP 422; create a loan whose UserId is not the caller's security identity.

**Acceptance Scenarios**:

1. **Given** a staff caller is authorized, **When** they register a User with a unique email and a name, **Then** that User can be named as borrower on later loans via UserId.
2. **Given** a User already exists with an email, **When** a staff caller registers the same email again, **Then** the system rejects the registration with HTTP 422.
3. **Given** a staff caller creates a loan for a User, **When** audit and loan records are inspected, **Then** the borrower is the User identified by UserId and the audit actor is the authenticated JWT subject.

---

### User Story 3 - Lend an available copy (Priority: P1)

A staff caller lends an available, active book to a registered User. The loan records the book, the User, BorrowedAtUtc, DueAtUtc (later than BorrowedAtUtc), and lifecycle status Active. When only one copy remains and two independent lend attempts arrive at the same time, exactly one loan is created, exactly one attempt is rejected, AvailableCopies become zero, and AvailableCopies never go below zero. This remains true when several copies of the service handle traffic at once.

**Why this priority**: Correct lending under contention is the core business value of the product.

**Independent Test**: Lend a book with spare copies; then run two simultaneous last-copy lends through two API hosts that share one PostgreSQL and assert a single winner (HTTP 201), a single unavailable rejection (HTTP 422), a single Active loan, and non-negative AvailableCopies.

**Acceptance Scenarios**:

1. **Given** an active book with AvailableCopies of at least one and a registered User with no Active loan for that book, **When** a staff caller requests a loan with that UserId, **Then** an Active loan is created, AvailableCopies decrease by one, and DueAtUtc equals BorrowedAtUtc.AddDays(14) in UTC.
2. **Given** an active book with zero AvailableCopies, **When** a staff caller requests a loan, **Then** the request is rejected with HTTP 422, no loan is created, and AvailableCopies stay zero.
3. **Given** an inactive book with AvailableCopies remaining, **When** a staff caller requests a loan, **Then** the request is rejected with HTTP 422 and no loan is created.
4. **Given** a User already has an Active loan for a book, **When** a staff caller requests another loan of that book for the same UserId, **Then** the request is rejected with HTTP 422.
5. **Given** exactly one AvailableCopies and two independent lend requests at the same time, **When** both complete, **Then** exactly one request succeeds with HTTP 201, exactly one is rejected with HTTP 422, exactly one loan exists, AvailableCopies are zero, and AvailableCopies are not negative.
6. **Given** several service instances serving traffic, **When** the last-copy scenario is repeated, **Then** the same single-winner outcome holds.
7. **Given** a UserId or BookId that does not exist, **When** a staff caller requests a loan, **Then** the response is HTTP 404 and no loan is created.

---

### User Story 4 - Retry a lend without duplicating it (Priority: P1)

POST /loans requires a client-supplied Idempotency-Key. The same key plus the same canonical request returns HTTP 201 with the stored loan body and does not create another loan, reduce availability again, or duplicate business side effects. The same key plus a different canonical request returns HTTP 409. If the business transaction fails unexpectedly, idempotency ownership is rolled back so the key can be used again.

**Why this priority**: Network retries are expected; duplicate loans would corrupt inventory and history.

**Independent Test**: Submit the same lend twice with one key and expect HTTP 201 plus the stored loan body on replay; submit a different lend with that key and expect HTTP 409; confirm one loan, one availability decrement, and rollback of key ownership after an unexpected transaction failure.

**Acceptance Scenarios**:

1. **Given** a POST /loans request that omits Idempotency-Key, **When** it is submitted, **Then** the system rejects it and creates no loan.
2. **Given** a successful lend with a key, **When** the same canonical request is repeated with that key, **Then** the caller receives HTTP 201 with the stored loan body, no second loan exists, AvailableCopies are not decremented again, and business side effects are not duplicated.
3. **Given** a key already used for one canonical request, **When** a different canonical request is submitted with that key, **Then** the system returns HTTP 409 and does not apply the second lend.
4. **Given** two concurrent identical lends sharing one new key, **When** both complete, **Then** only one loan is created and both callers observe a consistent outcome for that key.
5. **Given** a lend that claims a key and then fails the business transaction unexpectedly, **When** the operation ends, **Then** idempotency ownership is rolled back and a later retry with that key is not treated as a committed prior result.

---

### User Story 5 - Restrict changes to authenticated, authorized staff (Priority: P1)

Callers prove who they are with a JWT Bearer access token issued by the external OpenID Connect provider. This service never accepts username/password credentials and never issues JWTs. Mutation endpoints require the Librarian policy. Missing or invalid authentication returns HTTP 401. Authenticated callers without the required role receive HTTP 403. Health endpoints allow anonymous access. Interactive API documentation in local development authenticates through Keycloak using Authorization Code with PKCE.

**Why this priority**: A production lending API cannot expose mutations anonymously or invent its own login.

**Independent Test**: Attempt mutations with no token (HTTP 401), a valid token without librarian role (HTTP 403), and a Librarian-permitted token (success); confirm health checks work anonymously and that this service does not issue tokens.

**Acceptance Scenarios**:

1. **Given** no access token or an invalid token, **When** a caller attempts a mutating business operation, **Then** the response is HTTP 401 and the change is not applied.
2. **Given** a valid token without the librarian role, **When** they attempt a mutating business operation, **Then** the response is HTTP 403 and the change is not applied.
3. **Given** a valid token that satisfies the Librarian policy, **When** they perform an allowed mutation, **Then** the change is applied and the audit actor is the JWT subject claim.
4. **Given** any caller, **When** they search this service for a username/password credential exchange that issues JWTs, **Then** no such capability exists.
5. **Given** the local development environment, **When** a staff caller uses Swagger UI, **Then** they authenticate through Keycloak with Authorization Code and PKCE and can invoke Librarian-permitted operations.
6. **Given** no access token, **When** a caller queries health endpoints, **Then** access is allowed.

---

### User Story 6 - Return or cancel a loan without erasing history (Priority: P2)

Staff can transition an Active loan to Returned or Cancelled. Only Active loans can make those transitions. Returned and Cancelled loans stay permanently visible in User loan history. AvailableCopies increases by one when an Active loan is returned or cancelled, without exceeding TotalCopies. Concurrent or repeated return/cancellation must not restore inventory more than once.

**Why this priority**: Circulation is incomplete without return/cancel, and history is a compliance requirement.

**Independent Test**: Lend, then return; lend, then cancel; repeat or race return/cancel; query User history and confirm inventory restored once and both loans remain with their final statuses.

**Acceptance Scenarios**:

1. **Given** an Active loan, **When** a staff caller returns it, **Then** the status is Returned, AvailableCopies increase by one, and the loan remains queryable.
2. **Given** an Active loan, **When** a staff caller cancels it, **Then** the status is Cancelled, AvailableCopies increase by one, and the loan remains queryable.
3. **Given** a Returned or Cancelled loan, **When** a staff caller queries that User's loan history, **Then** the loan is present with its final status.
4. **Given** a Returned or Cancelled loan, **When** a staff caller tries to return or cancel it again, **Then** the system rejects the action with HTTP 422 and does not change copy counts.
5. **Given** two concurrent return or cancel requests for the same Active loan, **When** both complete, **Then** inventory is restored at most once and the loan has a single terminal status.
6. **Given** a book that was deactivated after loans occurred, **When** history is queried, **Then** those loans remain visible and can still be returned or cancelled if still Active.

---

### User Story 7 - Leave a durable audit trail (Priority: P2)

Relevant business changes persist an AuditEvent with entity type, entity identifier, action, authenticated actor, UTC time, correlation identifier, and contextual change information. The actor is the authenticated JWT subject claim, not the User named on a loan. If the business change succeeds, the AuditEvent is stored; if the business change does not succeed, a matching success AuditEvent is not stored. Correlation identifiers appear on the caller response and on the AuditEvent.

**Why this priority**: Evaluators and operators must explain who changed what, when, and under which request.

**Independent Test**: Perform a successful mutation and a rejected mutation; verify AuditEvent presence/absence, JWT subject actor, UTC time, and matching correlation identifiers. GET /audit-events without Librarian is HTTP 403 (or 401 if unauthenticated).

**Acceptance Scenarios**:

1. **Given** a successful book, User, or loan mutation, **When** the operation completes, **Then** an AuditEvent exists with entity type, entity id, action, JWT subject actor, UTC timestamp, correlation id, and change context.
2. **Given** a rejected mutation, **When** the operation completes, **Then** no success AuditEvent is stored for a change that did not happen.
3. **Given** a successful mutation, **When** the response, logs, and AuditEvent are compared, **Then** they share the same correlation identifier.
4. **Given** any recorded time on loans or AuditEvents, **When** inspected, **Then** the time is in UTC.
5. **Given** an authenticated caller without the librarian role, **When** they GET /audit-events, **Then** the response is HTTP 403.

---

### User Story 8 - Treat cached availability as a hint, not as authority (Priority: P3)

GET /books/{id}/availability may be served from Redis as a fast view. Lending decisions always use the durable catalog and never consult Redis. Availability-changing transactions write durable Outbox messages. After commit, immediate Redis invalidation is attempted; if that fails, the committed business request still succeeds and Outbox processing retries until invalidation completes. Invalidation is idempotent. Cached availability uses a short bounded TTL.

**Why this priority**: Speed is useful; over-lending from a stale or locked cache is unacceptable.

**Independent Test**: Lend while Redis is stale or unreachable; confirm the durable last-copy rule still holds, the HTTP loan succeeds, and GET /books/{id}/availability eventually matches the catalog.

**Acceptance Scenarios**:

1. **Given** a stale Redis value that still shows a copy available, **When** the durable catalog has zero AvailableCopies, **Then** a new loan is rejected without consulting Redis to approve it.
2. **Given** a successful loan, **When** immediate Redis invalidation fails, **Then** the loan remains committed, the caller is not failed for the cache error, and Outbox processing retries invalidation.
3. **Given** multiple service instances, **When** loans complete on different instances, **Then** availability views converge without double-lending.
4. **Given** the same invalidation message delivered more than once, **When** Outbox consumers run, **Then** cache invalidation remains safe (idempotent).

---

### User Story 9 - Finish reliability-sensitive follow-up work after success (Priority: P3)

Outbox records are technical infrastructure records, not Domain entities. They are written through an Application abstraction implemented by Infrastructure, in the same DbContext transaction as the business mutation. Processors may run on multiple replicas, use database-backed claim/lease semantics so a crashed worker cannot permanently own a message, assume at-least-once delivery, and recover after a crash. Failed messages retain attempt count, next retry timestamp, and error information. Successful messages record ProcessedAtUtc.

**Why this priority**: Side effects that are not recorded with the business change are lost on failure.

**Independent Test**: Commit a loan, interrupt follow-up, resume with more than one processor including a crashed lease holder; confirm the loan is unchanged, a new worker can claim the message, and follow-up completes once logically.

**Acceptance Scenarios**:

1. **Given** a successful loan, return, or cancel, **When** the business change is stored, **Then** the Outbox message is stored in the same transaction.
2. **Given** two processors claiming follow-up work at the same time, **When** processing completes, **Then** business state is not corrupted and consumers tolerate duplicates.
3. **Given** a processor crash while it holds a claim/lease, **When** the lease expires, **Then** another replica can claim the message and remaining work completes.
4. **Given** a processing failure, **When** the message is retained, **Then** it has attempt count, next retry timestamp, and error information.
5. **Given** successful processing, **When** the message is inspected, **Then** ProcessedAtUtc is recorded.

---

### User Story 10 - Operate, observe, test, and document the service (Priority: P3)

Operators can distinguish a live process from a process ready to serve traffic via anonymous health checks. They can follow a request through structured logs, traces, and metrics, including counts of loans created, loans rejected for unavailability, idempotent replays, loan duration, cache-refresh failures, and follow-up processing. The repository starts a full local stack (application, durable store, availability cache, Keycloak). Basic orchestration manifests describe how to run the service with configuration, secrets, resource budgets, and health probes. Automated tests cover domain rules, persistence, last-copy contention including two API hosts sharing PostgreSQL, idempotency, return, cancel, history preservation, authentication, authorization, audit actor identity, caching, durable follow-up processing, and CancellationToken propagation. The README is English-only and explains how to run, authenticate, migrate, test, and reason about architecture, concurrency, idempotency, audit, caching, follow-up processing, observability, and multi-instance correctness.

**Why this priority**: The product is not operable or evaluable without health, telemetry, tests, runtime packaging, and documentation.

**Independent Test**: Exercise live and ready checks anonymously; inspect telemetry after a lend; start the local stack; review manifests and README; run the required automated tests.

**Acceptance Scenarios**:

1. **Given** a running process that is not yet safe to receive traffic, **When** operators query liveness and readiness without a token, **Then** liveness can succeed while readiness fails.
2. **Given** a process that can serve traffic, **When** operators query readiness without a token, **Then** readiness succeeds.
3. **Given** a completed lend, unavailable rejection, idempotent replay, or follow-up failure, **When** operators inspect telemetry, **Then** the corresponding metric moved and traces/logs carry the correlation id.
4. **Given** a clean checkout, **When** operators start the documented local stack, **Then** the application, durable store, availability cache, and Keycloak are available together.
5. **Given** the documented automated tests, **When** they are run, **Then** they cover the scenarios listed in this story and fail if those guarantees regress.

---

### Edge Cases

- Lending a book that does not exist, or lending to a UserId that does not exist, returns HTTP 404 and creates no loan. Return or cancel of an unknown loan id returns HTTP 404.
- Creating a book with TotalCopies less than one, or with missing title, ISBN, or author, is rejected.
- Reducing TotalCopies below the number of copies currently on loan is rejected.
- Individual physical copies have no identifiers; operations never require a copy barcode or copy id.
- Deactivating a book with DELETE /books/{id} does not cancel Active loans; those loans can still be returned or cancelled; the book cannot receive new loans.
- An Idempotency-Key reused with a different canonical request returns HTTP 409, not a silent second loan.
- Duplicate ISBN, duplicate email, TotalCopies below borrowed copies, inactive or unavailable book, duplicate Active loan, and return/cancel of a non-Active loan return HTTP 422, not HTTP 409.
- Unexpected transaction failure after claiming an Idempotency-Key rolls back that ownership.
- DueAtUtc that is not later than BorrowedAtUtc is rejected.
- HTTP 401 (missing or invalid authentication) is not interchangeable with HTTP 403 (authenticated without librarian role).
- AvailableCopies never becomes negative, including under concurrent last-copy lending.
- Concurrent or repeated return/cancellation restores AvailableCopies at most once.
- Immediate Redis invalidation failure does not fail a committed loan, return, or cancel.
- Returned and Cancelled loans cannot be deleted through normal staff operations.
- Duplicate Outbox delivery does not create extra loans or extra availability changes; cache invalidation is idempotent.
- A crashed Outbox worker's claim/lease expires so another replica can process the message.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Staff MUST be able to create a book with title, ISBN, author, TotalCopies, AvailableCopies, and active state.
- **FR-002**: ISBN MUST be unique across books; a duplicate ISBN MUST return HTTP 422.
- **FR-003**: Staff MUST be able to retrieve a book by identifier and list books.
- **FR-004**: Staff MUST be able to update allowed book fields.
- **FR-005**: DELETE /books/{id} MUST perform logical deactivation; the book MUST remain retrievable and MUST NOT be physically removed.
- **FR-006**: The system MUST NOT silently delete books, Users, loans, or AuditEvents as part of normal operations.
- **FR-007**: A newly created book MUST be active with AvailableCopies equal to TotalCopies.
- **FR-008**: ISBN MUST be immutable after creation; an update MUST NOT change ISBN.
- **FR-009**: Updates that would set TotalCopies below the number of copies currently on loan MUST be rejected with HTTP 422.
- **FR-010**: Staff MUST be able to register a User with a name and a unique email; a duplicate email MUST return HTTP 422.
- **FR-011**: Staff MUST be able to query a User's full loan history, including Returned and Cancelled loans.
- **FR-012**: User (domain borrower/reader) and authenticated caller identity MUST remain separate concepts.
- **FR-013**: Staff MUST be able to lend an available copy of an active book to a registered User.
- **FR-014**: A loan MUST record the book, the User, BorrowedAtUtc, DueAtUtc, and status Active, Returned, or Cancelled.
- **FR-015**: BorrowedAtUtc and DueAtUtc MUST be stored in UTC.
- **FR-016**: DueAtUtc MUST be later than BorrowedAtUtc; on successful create it MUST equal `BorrowedAtUtc.AddDays(14)` in UTC (exactly 14 × 24 hours).
- **FR-017**: A User MUST NOT have more than one Active loan for the same book; a violating lend MUST return HTTP 422.
- **FR-018**: Lending a deactivated book or a book with zero AvailableCopies MUST be rejected without creating a loan and MUST return HTTP 422.
- **FR-019**: Staff MUST be able to return an Active loan; AvailableCopies MUST increase by one without exceeding TotalCopies.
- **FR-020**: Staff MUST be able to cancel an Active loan; AvailableCopies MUST increase by one without exceeding TotalCopies.
- **FR-021**: Returned and Cancelled loans MUST remain permanently available in historical queries.
- **FR-022**: Only Active loans MAY transition to Returned or Cancelled; return or cancel of a non-Active loan MUST be rejected without changing copy counts and MUST return HTTP 422.
- **FR-023**: When exactly one copy remains and two independent lend requests race, the system MUST accept exactly one, reject exactly one, create exactly one loan, set AvailableCopies to zero, and NEVER allow negative AvailableCopies.
- **FR-024**: The service MUST remain correct for FR-023 when multiple instances handle traffic. Automated integration tests MUST demonstrate FR-023 using two API hosts that share one PostgreSQL database.
- **FR-025**: Loan approval MUST use the durable catalog as the only authority; Redis MUST NEVER be consulted when deciding whether a loan can succeed.
- **FR-026**: POST /loans MUST require a client-supplied Idempotency-Key.
- **FR-027**: Same Idempotency-Key plus the same canonical request MUST return HTTP 201 with the stored loan body (the same status and payload as the original successful completion) and MUST NOT create another loan, decrement availability again, or duplicate audit or other business side effects.
- **FR-028**: Same Idempotency-Key plus a different canonical request MUST return HTTP 409 and MUST NOT apply the new request.
- **FR-029**: Concurrent use of the same new Idempotency-Key MUST result in a single owner and a single business effect.
- **FR-030**: Relevant successful business changes MUST persist an AuditEvent containing entity type, entity identifier, action, authenticated actor, UTC timestamp, correlation identifier, and contextual change information.
- **FR-031**: Audit data for a business change MUST be stored when that change succeeds and MUST NOT be stored as a successful change when the change fails.
- **FR-032**: Audit actor identity MUST come from the authenticated JWT subject claim, not from the User named on a loan.
- **FR-033**: Correlation identifiers MUST propagate through caller-visible responses, logs, traces, and AuditEvents.
- **FR-034**: Callers MUST authenticate with JWT Bearer access tokens issued by an external OpenID Connect identity provider.
- **FR-035**: The API MUST NEVER accept username/password credentials and MUST NEVER issue JWTs itself.
- **FR-036**: Access tokens MUST be rejected unless they are genuine, intended for this service, issued by the expected provider, and still within their lifetime.
- **FR-037**: Mutation endpoints MUST require the Librarian policy. GET /audit-events MUST also require the Librarian policy.
- **FR-038**: Missing or invalid authentication MUST return HTTP 401; authenticated callers without the required role MUST receive HTTP 403; these outcomes MUST NOT be interchangeable.
- **FR-039**: Local development MUST use Keycloak as the OpenID Connect provider, with realm configuration importable from the repository.
- **FR-040**: Swagger UI MUST authenticate through the public OIDC client using Authorization Code with PKCE.
- **FR-041**: Production identity-provider settings MUST be supplied outside source control.
- **FR-042**: Redis MAY cache GET /books/{id}/availability as a performance optimization with a short bounded TTL.
- **FR-043**: Availability-changing transactions are an instance of FR-046: they MUST create a durable Outbox message in the same transaction as the mutation; after commit, immediate Redis invalidation MUST be attempted.
- **FR-044**: Immediate Redis failure MUST NOT fail the committed business request.
- **FR-045**: Outbox processing MUST guarantee eventual retry of cache invalidation; invalidation MUST be idempotent; refresh MUST recover after process restarts and across multiple instances.
- **FR-046**: Reliability-sensitive work after a successful business change MUST use durable Transactional Outbox processing recorded with that change. FR-043 applies FR-046 to availability-changing transactions and additionally requires post-commit Redis invalidation.
- **FR-047**: Outbox processing MUST support multiple replicas, MUST use database-backed claim/lease semantics so a crashed worker cannot permanently own a message, MUST assume at-least-once delivery, MUST use idempotent consumers, MUST retain attempt count, next retry timestamp, and error information on failed messages, and MUST record ProcessedAtUtc on successful messages.
- **FR-048**: Operators MUST be able to follow a request through structured logs, distributed traces, and metrics.
- **FR-049**: Operators MUST be able to count loans created, loans rejected for unavailability, idempotent replays, loan duration, cache-invalidation failures, and Outbox processing health.
- **FR-050**: The system MUST expose GET /health/live and GET /health/ready, and those health endpoints MUST allow anonymous access. GET /health/live MUST succeed when the process is running even if dependencies are down. GET /health/ready MUST fail (HTTP 503) when PostgreSQL or Redis is not reachable.
- **FR-051**: Failure responses MUST use one consistent problem format so callers can handle errors uniformly.
- **FR-052**: Automated unit and integration tests MUST cover domain rules, durable catalog persistence, concurrent last-copy borrowing including two API hosts sharing PostgreSQL, idempotency, loan return, loan cancellation, historical preservation, authentication, authorization, audit actor identity, availability caching, durable Outbox processing, and CancellationToken propagation.
- **FR-053**: Operators MUST be able to start a complete local stack from the repository: the API, the durable catalog store, the availability cache, and Keycloak.
- **FR-054**: The repository MUST include basic container-orchestration manifests covering workload and network exposure, configuration and secret references, CPU and memory requests and limits, and liveness and readiness probes.
- **FR-055**: The README MUST be written entirely in English and MUST document how to run the system, authenticate, apply data migrations, run tests, and understand architecture, concurrency, idempotency, audit, caching, Outbox processing, observability, and multi-instance correctness.
- **FR-056**: Production secrets MUST NOT be stored in source control; runtime configuration MUST come from application settings plus environment or secret services.
- **FR-057**: Individual physical copies MUST NOT require identifiers; book inventory MUST be represented only by TotalCopies and AvailableCopies.
- **FR-058**: POST /loans MUST identify the borrower through UserId.
- **FR-059**: Concurrent or repeated return/cancellation MUST NOT restore inventory more than once.
- **FR-060**: Unexpected transaction failures MUST roll back idempotency ownership.
- **FR-061**: Outbox records MUST be technical infrastructure records, not Domain entities; they MUST be written using an Application abstraction implemented by Infrastructure and MUST share the same DbContext transaction as the business mutation.
- **FR-062**: Idempotency persistence records MUST be Infrastructure concerns, not Domain entities.
- **FR-063**: Domain MUST contain the business entities Book, User, Loan, and AuditEvent.
- **FR-064**: CQRS concepts and terminology MUST NOT be used.
- **FR-065**: HTTP 409 MUST be used only for Idempotency-Key reuse with a different canonical POST /loans request. HTTP 422 MUST be used for business-rule failures (duplicate ISBN, duplicate email, TotalCopies below borrowed copies, inactive or unavailable book, duplicate Active loan for the same User and book, return or cancel of a non-Active loan). HTTP 404 MUST be used when the named book, User, or loan does not exist, including POST /loans with an unknown UserId or BookId.
- **FR-066**: All public asynchronous Application, Infrastructure, and Api methods MUST accept and propagate `CancellationToken`.

### Key Entities

- **Book**: A catalog title with ISBN (unique, immutable after create), author, TotalCopies, AvailableCopies, and active state. Individual physical copies have no identifiers. AvailableCopies never goes negative and stays consistent with TotalCopies and Active loans. Logical deactivation uses DELETE /books/{id} and blocks new loans.
- **User**: Domain borrower/reader. Distinct from the authenticated API caller. POST /loans names this entity through UserId.
- **Loan**: A borrowing of one undifferentiated copy by one User, with BorrowedAtUtc, DueAtUtc (must be later than BorrowedAtUtc), and status Active, Returned, or Cancelled. Only Active may transition to Returned or Cancelled. Historical statuses remain queryable.
- **AuditEvent**: A durable Domain record of a relevant business change, including entity type, entity id, action, actor (JWT subject), UTC time, correlation id, and change context. Present only when the corresponding change succeeds.
- **Availability view**: Redis-cached GET /books/{id}/availability. Never authoritative for lending. Short bounded TTL. Invalidation is idempotent.
- **Outbox record**: Infrastructure follow-up work, not a Domain entity. Recorded with the business mutation in the same DbContext transaction. Claim/lease, at-least-once processing, retry metadata, ProcessedAtUtc.
- **Idempotency record**: Infrastructure ownership of an Idempotency-Key for POST /loans, not a Domain entity. Rolled back if the business transaction fails unexpectedly.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: An authorized staff caller can add a book and see it in the catalog in under 2 minutes.
- **SC-002**: In 100% of last-copy dual-lend trials, including with multiple service instances, exactly one loan is created, exactly one request is rejected, AvailableCopies equal zero, and AvailableCopies are never negative.
- **SC-003**: In 100% of identical loan-creation retries using the same Idempotency-Key and same canonical request, HTTP 201 is returned with the stored loan body, no extra loan is created, and availability is not reduced twice.
- **SC-004**: In 100% of mismatched Idempotency-Key replays, the second request receives HTTP 409 and inventory is unchanged by that second request.
- **SC-005**: After return or cancel, 100% of User history queries still include that loan with its final status, and 100% of concurrent or repeated return/cancel trials restore inventory at most once.
- **SC-006**: 100% of successful catalog, User, and loan mutations produce a durable AuditEvent whose actor matches the JWT subject and whose correlation id matches the caller-visible request id.
- **SC-007**: 100% of mutation attempts with missing or invalid authentication receive HTTP 401; 100% of mutation attempts with a valid token that lacks the librarian role receive HTTP 403.
- **SC-008**: Operators can determine within 5 seconds whether an instance is merely running versus ready to serve traffic, without presenting a token.
- **SC-009**: A staff caller can complete a lend-then-return cycle on the first attempt without manual data repair in at least 95% of guided trials.
- **SC-010**: When GET /books/{id}/availability is wrong or Redis is unreachable, lending still follows SC-002, the committed loan is not failed for the cache error, and the view becomes consistent with the catalog without reversing a completed loan.
- **SC-011**: A new operator following the README can start the local stack, authenticate, and run the documented test suite without undocumented steps.

## Assumptions

- Planning and implementation MUST follow the Library Manager Constitution v1.0.0.
- Users are registered by staff; there is no patron self-service portal, email verification, or password for Users.
- User unique key is email; name is required; no other patron profile fields are in scope.
- Loan length is 14 days from BorrowedAtUtc, computed as `BorrowedAtUtc.AddDays(14)` in UTC; there are no renewals, holds, fines, reservations, or waitlists.
- Cancel means staff void an Active loan (for example created in error); return means the copy is back. Both restore one AvailableCopies at most once and keep the loan row.
- A User may have many Active loans, but not two Active loans of the same book.
- AvailableCopies are maintained by the system from TotalCopies and Active loans; staff do not set AvailableCopies independently except that create sets AvailableCopies equal to TotalCopies.
- Book title and author may be updated; ISBN may not. Deactivation is logical only via DELETE /books/{id}.
- Canonical loan-creation request for idempotency comparison includes UserId and BookId (and any other material body fields documented by the API).
- Correlation identifiers are generated when the caller does not supply one, returned to the caller, and copied into logs, traces, and AuditEvents.
- Cached availability TTL is short and bounded (minutes-scale) so a missed refresh cannot linger indefinitely.
- Outbox consumers in this release invalidate or refresh GET /books/{id}/availability; they do not send email or call other business systems.
- Catalog, User, and loan lists are paginated: query `page` default 1 (minimum 1), `pageSize` default 20 (minimum 1, maximum 100).
- Read operations of catalog, Users, loans, and availability require authentication. GET /audit-events requires the Librarian policy. Health checks do not.
- HTTP 409 is reserved for POST /loans Idempotency-Key canonical-request mismatch. HTTP 422 is reserved for business-rule failures listed in FR-065. HTTP 404 is reserved for a named book, User, or loan that does not exist.
- Public async Application, Infrastructure, and Api methods accept and propagate CancellationToken.
- There is no public patron UI. Swagger UI is for local development and evaluation only.
- Orchestration manifests are a basic deployment baseline, not a full multi-environment package.

Stakeholder-mandated platform mapping (planning MUST honor these names and contracts; they are product constraints, not open design choices):

- Durable catalog and business invariants: PostgreSQL
- Fast availability view: Redis cache of GET /books/{id}/availability; never consulted for loan approval
- Access tokens: JWT Bearer from an external OpenID Connect/OAuth 2.0 provider, validated for signature, issuer, audience, and lifetime
- Local identity provider: Keycloak
- Keycloak realm name: `library-manager`
- API resource identifier/audience: `library-manager-api`
- Swagger public OIDC client: `library-manager-swagger`
- Swagger authentication: Authorization Code with PKCE
- Development realm: contains a `librarian` role
- Authorization role claims: exposed in a flat `roles` claim consumable by ASP.NET Core
- Mutation authorization: Librarian policy
- Health endpoints: anonymous GET /health/live and GET /health/ready
- Unauthenticated or invalid token: HTTP 401
- Authenticated without required role: HTTP 403
- Business-rule failure (duplicate ISBN/email, TotalCopies too low, unavailable/inactive book, duplicate Active loan, non-Active return/cancel): HTTP 422
- Named book, User, or loan does not exist (including POST /loans unknown UserId or BookId): HTTP 404
- Idempotency-Key reused with a different canonical POST /loans body: HTTP 409
- Successful POST /loans and same-key same-hash replay: HTTP 201 with the stored loan body
- GET /audit-events: Librarian policy
- Token issuance: this API never accepts username/password and never issues JWTs
- Loan creation contract: POST /loans requiring Idempotency-Key and UserId; HTTP 201 on create and on same-hash replay; HTTP 409 on key reuse with a different canonical request; HTTP 422 on business-rule failure; HTTP 404 when UserId or BookId does not exist
- Book deactivation contract: DELETE /books/{id} (logical)
- Failure document format: RFC Problem Details
- Reliable follow-up: Transactional Outbox as infrastructure (Application abstraction, Infrastructure implementation, same DbContext transaction as the mutation), at-least-once, idempotent consumers, database-backed claim/lease, crash recovery, multi-replica safe, failed-message retry metadata, ProcessedAtUtc
- Domain entities: Book, User, Loan, AuditEvent only for business entities; idempotency and Outbox persistence are Infrastructure, not Domain
- CQRS: not permitted (no Command, Query, Handler, IRequest, IRequestHandler, mediator, or MediatR concepts or terminology)
- Telemetry: structured logs plus OpenTelemetry-compatible traces and metrics, including loan-created, unavailable-loan, idempotency-replay, loan latency, cache-invalidation-failure, and Outbox processing
- Local runtime: Docker Compose providing the API, PostgreSQL, Redis, and Keycloak
- Orchestration: Kubernetes Deployment, Service, configuration references, secret references, CPU and memory requests and limits, liveness probe, and readiness probe
- Automated tests: unit and integration coverage for domain rules, PostgreSQL persistence, concurrent last-copy borrowing including two API hosts sharing PostgreSQL, idempotency (201 replay / 409 mismatch), loan return, loan cancellation, historical preservation, authentication, authorization, audit actor identity, Redis caching, durable Outbox processing, and CancellationToken propagation
- README topics: execution, authentication, migrations, tests, architecture, concurrency, idempotency, audit, caching, Outbox processing, observability, and multi-replica correctness, entirely in English

## Out of Scope

- Patron-facing web or mobile applications, and any login that this API issues tokens for
- Payments, fines, reservations, renewals, multi-copy loans in a single request, and inter-library loan
- Physical inventory, barcodes, per-copy identifiers, and branch/location management
- Hard deletion or anonymization workflows beyond logical deactivation and refusing silent deletes
- Custom identity-provider implementation or social login built into this API
- Advanced multi-cluster orchestration, autoscaling policies, and service mesh configuration
- CQRS, mediator, and generic-repository designs
