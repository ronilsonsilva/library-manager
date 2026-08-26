# Feature Specification: Production Hardening

**Feature Branch**: `002-production-hardening`

**Created**: 2026-08-26

**Status**: Draft

**Input**: User description: "Create a new feature named production-hardening for library-manager. This feature improves production security, maintainability, HTTP validation, localization, failure handling and infrastructure resilience without changing the core library lending behavior."

## Clarifications

### Session 2026-08-26

- Q: Where do HTTP transport contracts live, and may controllers declare them? → A: All transport contracts belong to `LibraryManager.Api`. No controller source file may contain request or response type declarations.
- Q: How is request-body validation enforced? → A: Request-body validation uses ASP.NET Core `[ApiController]` model validation. DataAnnotations are acceptable on API request contract types for simple transport constraints. Controllers do not manually inspect ModelState.
- Q: How is Idempotency-Key bound and validated? → A: Idempotency-Key is HTTP metadata, not a Domain concept. Create a readonly API value named `IdempotencyKey`. CreateLoan receives `IdempotencyKey` instead of `string` or `string?`. Use a dedicated custom `IModelBinder` exposed as `FromIdempotencyKeyAttribute` with approximately `[FromIdempotencyKey] IdempotencyKey idempotencyKey`. Do not put `Required` or `StringLength` on the action parameter, do not manually validate inside `LoansController`, and do not use middleware, an action filter, or `IParsable` alone. Missing, empty, whitespace-only, and longer-than-128-character values are ModelState failures; valid values are trimmed; invalid binding never executes `CreateLoanUseCase`; `[ApiController]` returns HTTP 400; messages are localized.
- Q: How are expected failures and domain validation modeled? → A: Expected Domain/Application failures use `Result`/`Result<T>` with stable language-neutral codes and `ErrorType` Validation, NotFound, BusinessRule, Conflict mapped to HTTP 400/404/422/409. Expected failures are not exceptions. Unexpected technical exceptions continue to `IExceptionHandler`. Domain validation uses reusable lightweight rules with no ASP.NET Core or localization dependency. `AuditEvent.Create` no longer throws `DomainException` for expected field validation.
- Q: Where does localization happen? → A: `en-US` is default; `pt-BR` is additionally supported; `Accept-Language` is the primary culture selector; localization occurs at the API boundary; Domain/Application errors remain language-neutral; operational logs remain English.
- Q: How do cache resilience and book deactivation interact with Redis and Outbox? → A: Application contains no broad Redis recovery try/catch. Infrastructure implements cache resilience using the Decorator Pattern. Redis GET failure is a cache miss; SET and REMOVE failures are non-fatal for the committed request; Outbox handles durable invalidation retry; `OperationCanceledException` is never swallowed. `DeactivateBook` invalidates the availability cache after commit and persists a `BookAvailabilityChanged` Outbox message transactionally.
- Q: What SQL, dependency, identity, and regression rules apply? → A: `ExecuteSqlInterpolatedAsync` is accepted as parameterized SQL; runtime string concatenation into SQL is forbidden; raw APIs may use runtime values only through explicit database parameters. Direct and transitive NuGet packages are audited; NU1903 and NU1904 must fail the build; do not globally disable NuGet auditing; upgrade compatible OpenTelemetry stable packages where appropriate; avoid prerelease Redis OpenTelemetry instrumentation if internal `ActivitySource` instrumentation can replace it. Direct Access Grants are disabled; Swagger remains Authorization Code with PKCE only. Existing concurrency, idempotency, Outbox, JWT, and HTTP status behavior remains compatible unless this feature explicitly changes transport validation behavior.
- Q: Are list and history HTTP envelopes Application `PagedResult` types? → A: No. Controllers MUST return API `PagedResponse<TResponse>` (or equivalent named list types) under `Contracts/`. Application `PagedResult<TDto>` stays in Application and is mapped at the API boundary. JSON field names remain `items`, `page`, `pageSize`, `totalCount`.
- Q: How is password-grant rejection proven without a live Keycloak in `dotnet test`? → A: CI asserts every client in `infrastructure/keycloak/library-manager-realm.json` has `directAccessGrantsEnabled: false`, and that `README.md`, `specs/001-library-manager/quickstart.md`, and tests contain no Keycloak ROPC (`grant_type=password` to the realm token endpoint). Tests that POST password grant to **this API** to prove it has no token endpoint remain allowed. This feature's `quickstart.md` MAY include a clearly labeled operator-only Compose curl; that curl is not a `dotnet test` requirement and MUST NOT be copied into README or 001 docs.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Keep shipped packages free of known high and critical flaws (Priority: P1)

A security reviewer can audit every direct and transitive package the service uses. Known high (NU1903) and critical (NU1904) flaws fail the build. Compatible fixed versions are applied when they exist, including compatible OpenTelemetry stable packages. Audit warnings are not globally silenced, and NuGet auditing is not globally disabled. Runtime prerelease packages are not kept when a stable or project-owned telemetry alternative can preserve the same operator-visible traces and metrics, including availability-cache instrumentation.

**Why this priority**: Unpatched high or critical package flaws and silenced audits are production blockers independent of catalog or lending rules.

**Independent Test**: Enable repository-wide transitive package auditing, fail a build that contains a high or critical finding, confirm no global warning suppression, inventory runtime prerelease packages, and assert (automated) that cache Get/Set/Remove still create `LibraryManager` `ActivitySource` activities and that cache-invalidation metrics remain after removing the prerelease Redis instrumentation package.

**Acceptance Scenarios**:

1. **Given** the repository build, **When** a known high (NU1903) or critical (NU1904) package vulnerability is present, **Then** the build fails.
2. **Given** a compatible fixed version of a vulnerable package, **When** hardening is applied, **Then** the service uses the fixed version and the finding is gone.
3. **Given** the repository configuration, **When** a reviewer inspects package-audit settings, **Then** direct and transitive dependencies are included, NU1903 and NU1904 fail the build, and auditing is not globally disabled.
4. **Given** a runtime prerelease package whose operator-visible traces and metrics can be preserved by the service's own telemetry, **When** hardening is applied, **Then** that prerelease package is not present at runtime and the equivalent traces and metrics remain.

---

### User Story 2 - Reject invalid HTTP input before business work starts (Priority: P1)

A staff caller who sends an incomplete or out-of-range request body, or a missing, empty, whitespace-only, or overlong Idempotency-Key, receives HTTP 400 with problem details in the request culture. Create Loan does not run. A valid Idempotency-Key is trimmed and then used. HTTP request and response shapes are first-class review artifacts owned by the API and living outside controller files. Controllers only route, authorize, bind, invoke the use case, and map the HTTP result. Controllers do not inspect ModelState by hand.

**Why this priority**: Invalid input must never reach loan or catalog work, and the Create Loan key must fail the same way as a bad request body.

**Independent Test**: Submit POST /loans without Idempotency-Key, with whitespace, and with a 129-character value and assert HTTP 400 plus no loan; submit a valid key with surrounding spaces and assert the trimmed key is used; submit a request body that violates a required/length/range/format constraint and assert HTTP 400 before business work; inspect that controllers declare no transport types and that list/history/audit actions return API `PagedResponse<T>` rather than Application `PagedResult<T>` or DTOs.

**Acceptance Scenarios**:

1. **Given** a POST /loans request that omits Idempotency-Key, **When** it is submitted, **Then** the response is HTTP 400, no loan is created, and Create Loan does not execute.
2. **Given** a POST /loans request whose Idempotency-Key is empty or whitespace-only, **When** it is submitted, **Then** the response is HTTP 400 and no loan is created.
3. **Given** a POST /loans request whose Idempotency-Key exceeds 128 characters, **When** it is submitted, **Then** the response is HTTP 400 and no loan is created.
4. **Given** a POST /loans request whose Idempotency-Key has leading or trailing spaces and is otherwise valid, **When** it is submitted, **Then** the stored key is the trimmed value and lending proceeds under existing idempotency rules.
5. **Given** a mutating request whose body violates a required, length, range, or format constraint that the HTTP contract can express, **When** it is submitted, **Then** the response is HTTP 400 before the matching use case executes.
6. **Given** the HTTP API source, **When** a reviewer inspects controllers, **Then** request, response, and other transport types are not declared inside controller files, controllers do not manually inspect ModelState, and list/history/audit collection actions return API page envelopes (`PagedResponse<TResponse>` or named list types under `Contracts/`) rather than Application `PagedResult<T>` or DTOs.

---

### User Story 3 - Report expected failures as outcomes, not crashes (Priority: P1)

When a named book, User, or loan does not exist, a business rule fails, an idempotency conflict occurs, or domain validation fails, the caller receives the existing HTTP statuses from the library-manager specification (HTTP 404, HTTP 422, HTTP 409, or HTTP 400) with a stable language-neutral error code. The service does not throw merely to produce that HTTP response. Unexpected technical failures still pass through one HTTP error boundary, include the correlation identifier, and omit stack traces and other sensitive internals.

**Why this priority**: Callers and operators must distinguish expected business outcomes from unexpected technical failure without leaking internals.

**Independent Test**: Trigger not-found, business-rule, conflict, and transport-validation paths and assert status codes plus error codes without unhandled exceptions; trigger an unexpected failure and assert one problem response with correlation id and without stack traces. Cache Redis-recovery `try/catch` is out of this story (User Story 5).

**Acceptance Scenarios**:

1. **Given** a request that names a missing book, User, or loan, **When** it completes, **Then** the caller receives HTTP 404 with a language-neutral error code and the process does not treat the miss as an unhandled exception.
2. **Given** a request that violates a catalog or lending business rule, **When** it completes, **Then** the caller receives HTTP 422 with a language-neutral error code.
3. **Given** an Idempotency-Key reused with a different canonical POST /loans body, **When** it completes, **Then** the caller receives HTTP 409 with a language-neutral error code.
4. **Given** expected domain validation failure during a factory or state change, **When** it completes, **Then** the outcome is an expected failure result with an error code, not a thrown validation failure used as normal flow.
5. **Given** an unexpected technical failure, **When** the HTTP boundary handles it, **Then** the caller receives a generic problem response that includes the correlation identifier and does not include a stack trace or sensitive technical details.
6. **Given** a cancelled request, **When** cancellation is signaled, **Then** cancellation always propagates and is never swallowed.

---

### User Story 4 - Serve user-facing errors in English or Brazilian Portuguese (Priority: P2)

A caller can request English (United States) or Portuguese (Brazil) for user-facing validation and error text through Accept-Language. English is the default. Localization happens at the API boundary. Domain and Application errors stay language-neutral. Structured log templates, metric names, trace identifiers, error codes, and operational logs stay stable English values.

**Why this priority**: Callers in the two supported locales must understand HTTP errors, while operators keep one English diagnostic vocabulary.

**Independent Test**: Repeat the same invalid body, invalid Idempotency-Key, not-found, business-rule, conflict, and unexpected-error requests with Accept-Language en-US, pt-BR, and omitted; assert message language and unchanged English error codes, log templates, metric names, and correlation identifiers.

**Acceptance Scenarios**:

1. **Given** Accept-Language `pt-BR`, **When** a caller receives a user-facing validation or Result-based error, **Then** the visible message text is Portuguese (Brazil).
2. **Given** Accept-Language `en-US` or no accepted supported culture, **When** a caller receives a user-facing error, **Then** the visible message text is English (United States).
3. **Given** any supported or default culture, **When** operators inspect logs, metrics, traces, and error codes, **Then** those values remain stable English identifiers and operational logs are not translated.
4. **Given** request-body validation and Idempotency-Key binding failures, **When** problem details are returned, **Then** their user-facing text follows the selected culture.

---

### User Story 5 - Keep availability correct when the fast cache is unavailable (Priority: P1)

GET /books/{id}/availability continues to return the durable catalog answer when Redis is down. A failed cache read behaves as a miss. A failed cache write does not fail an otherwise valid catalog-backed read. A failed immediate invalidation is logged, does not roll back committed catalog state, and remains retryable through the Transactional Outbox. Application contains no broad Redis recovery try/catch; Infrastructure isolates cache failure handling with the Decorator Pattern around the existing availability-cache abstraction. Cancellation is never swallowed.

**Why this priority**: Redis is optional. Callers and last-copy lending must not depend on cache health.

**Independent Test**: Stop or break Redis, then GET availability and assert catalog-correct data; fail cache SET during a catalog-backed read and assert the read still succeeds; fail immediate REMOVE after a committed mutation and assert the HTTP success, Outbox retry path, and cache-invalidation failure metric remain; confirm Application use cases have no Redis recovery try/catch.

**Acceptance Scenarios**:

1. **Given** Redis is unavailable, **When** a caller GET /books/{id}/availability, **Then** the response matches the PostgreSQL catalog and is not failed because the cache is down.
2. **Given** a Redis GET failure, **When** availability is resolved, **Then** the miss path loads from PostgreSQL.
3. **Given** a Redis SET failure after a PostgreSQL availability read, **When** the read completes, **Then** the caller still receives the catalog data.
4. **Given** a Redis REMOVE failure after a committed availability-changing mutation, **When** the request completes, **Then** the business change remains committed, the failure is logged, `library_manager_cache_invalidation_failures` is recorded, and Outbox processing remains responsible for durable retry.
5. **Given** Application use cases that read or invalidate availability, **When** a reviewer inspects them, **Then** they do not contain technical cache-recovery exception handling.

---

### User Story 6 - Stop serving a stale available view after book deactivation (Priority: P1)

After a successful logical book deactivation (`DeactivateBook`), the availability cache is invalidated after commit. The matching BookAvailabilityChanged Outbox message is stored in the same PostgreSQL transaction as the domain mutation and AuditEvent. A previously cached active availability value is not observable as the current view after that success.

**Why this priority**: A deactivated book must not appear available because of a leftover cache entry.

**Independent Test**: Cache an active availability view, deactivate the book, then GET availability and assert it no longer presents the prior active cached value; inspect the same-transaction Outbox message and post-commit invalidation.

**Acceptance Scenarios**:

1. **Given** a successful `DeactivateBook`, **When** the transaction commits, **Then** a BookAvailabilityChanged Outbox message exists in the same PostgreSQL transaction as the domain mutation and AuditEvent.
2. **Given** that commit, **When** immediate cache invalidation runs, **Then** it occurs after commit and does not roll back the deactivation if Redis fails.
3. **Given** a previously cached active availability value, **When** deactivation has succeeded, **Then** later availability observation does not present that stale active value as current.

---

### User Story 7 - Keep database commands free of concatenated runtime input (Priority: P2)

Every SQL command that includes runtime values uses database parameters. Callers cannot inject SQL through concatenated strings. Existing interpolated commands that already emit parameters are left in place.

**Why this priority**: SQL injection is a production defect even when lending rules are unchanged.

**Independent Test**: Audit every raw SQL operation; assert runtime values are parameters; confirm previously safe interpolated commands were not rewritten merely because interpolation syntax is present.

**Acceptance Scenarios**:

1. **Given** a SQL command that includes runtime or user-controlled values, **When** it executes, **Then** those values are passed as database parameters and are not concatenated into the SQL string.
2. **Given** an existing interpolated SQL call that already parameterizes values, **When** hardening is applied, **Then** that call is not rewritten merely because interpolation syntax is present.
3. **Given** an unsafe raw SQL concatenation or interpolation, **When** hardening is applied, **Then** it is replaced with a parameterized form.

---

### User Story 8 - Stop password-grant shortcuts in local identity setup (Priority: P1)

Local Keycloak Direct Access Grants stay disabled. Resource Owner Password Credentials examples and smoke tests are removed. Swagger continues to authenticate only through Authorization Code with PKCE. The API still never accepts passwords or issues tokens.

**Why this priority**: Password-grant examples teach a forbidden local authentication path and conflict with the public Swagger client.

**Independent Test**: Assert every client in the source-controlled realm JSON has Direct Access Grants disabled; search `README.md`, `specs/001-library-manager/quickstart.md`, and tests for Keycloak `grant_type=password` token calls and assert none remain (API-path 404 probes that prove this API issues no tokens are allowed); confirm Swagger PKCE configuration remains. Optional Compose curl against Keycloak may appear only in this feature's `quickstart.md` and is not required inside `dotnet test`.

**Acceptance Scenarios**:

1. **Given** the source-controlled local Keycloak realm, **When** Direct Access Grants are inspected, **Then** they are disabled.
2. **Given** local documentation and smoke tests, **When** a reviewer searches for Resource Owner Password Credentials usage, **Then** none remain.
3. **Given** local Swagger UI, **When** a staff caller authenticates, **Then** they use Authorization Code with PKCE only.

---

### User Story 9 - Preserve existing lending, audit, and operations behavior (Priority: P1)

Catalog, User, loan, last-copy concurrency, durable idempotency, audit, Outbox, JWT, HTTP status, authentication, observability, and health behavior remain functionally compatible with the existing library-manager specification unless this feature explicitly changes transport validation behavior. Automated tests are updated where hardening changes HTTP validation or identity-setup details, and new automated coverage exists for every new hardening requirement in this feature.

**Why this priority**: Hardening has no value if it regresses the lending service.

**Independent Test**: Run the existing unit and integration suites after updates; add tests for each new acceptance scenario in this specification.

**Acceptance Scenarios**:

1. **Given** the existing concurrency, idempotency, Outbox, JWT, HTTP status, authentication, observability, and health tests, **When** they run after hardening, **Then** they pass except where this feature explicitly changes transport validation behavior, plus the updates required by localization, Result mapping, cache-resilience, and identity-setup rules.
2. **Given** each new hardening requirement in this specification, **When** the test suite runs, **Then** automated coverage exists for that requirement.

---

### Edge Cases

- Missing, empty, whitespace-only, and over-128-character Idempotency-Key values are ModelState validation failures and HTTP 400, not Create Loan execution.
- A valid Idempotency-Key with surrounding whitespace is accepted only after trim; uniqueness and replay rules use the trimmed value.
- `IdempotencyKey` is a readonly API-layer value. Idempotency-Key is HTTP metadata, not a Domain concept.
- Idempotency-Key validation is HTTP input binding via a dedicated `IModelBinder` and `FromIdempotencyKeyAttribute`. It is not middleware, not an action filter, not `IParsable` alone, and not manual checks inside `LoansController`.
- Request-body constraints that built-in `[ApiController]` model validation can express never fall through to manual controller checks. Controllers do not manually inspect ModelState.
- Expected Result failures are not converted into exceptions merely to reach `IExceptionHandler`. Unexpected technical exceptions continue propagating to that boundary.
- `AuditEvent.Create` does not throw `DomainException` for expected field validation. `DomainException` is not the normal validation mechanism for expected domain or application failures.
- Unsupported or omitted Accept-Language values use `en-US`. Localization occurs at the API boundary.
- User-facing problem text is localized; error codes, log templates, metric names, trace identifiers, and operational logs are never localized.
- Redis GET failure is a miss, not an HTTP 5xx, when PostgreSQL can answer.
- Redis SET failure after a valid PostgreSQL availability read does not fail that read.
- Redis REMOVE failure after commit does not roll back PostgreSQL state; Outbox retries invalidation.
- Cancellation tokens always propagate, including through cache-resilience code. `OperationCanceledException` is never swallowed.
- Successful `DeactivateBook` must not leave a previously cached active availability value observable as current.
- `ExecuteSqlInterpolatedAsync` is accepted as parameterized SQL. Existing parameterized interpolated SQL is not churned. Runtime string concatenation into SQL is forbidden.
- Password-grant token examples are out of bounds even as “local smoke only” shortcuts. Direct Access Grants stay disabled.
- Core lending rules, last-copy correctness, durable idempotency ownership, JWT behavior, and audit/Outbox transaction pairing are unchanged unless this feature explicitly changes transport validation behavior.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Repository-wide NuGet auditing MUST remain enabled, MUST include direct and transitive dependencies, and MUST NOT be globally disabled.
- **FR-002**: High (NU1903) and critical (NU1904) NuGet vulnerability warnings MUST fail the build.
- **FR-003**: Known package vulnerabilities MUST be remediated by upgrade, replacement, or explicit documented risk treatment. Global audit-warning suppression MUST NOT be used.
- **FR-004**: Runtime projects MUST contain no known high or critical vulnerabilities when compatible fixed versions exist.
- **FR-005**: Runtime prerelease dependencies MUST NOT be used when a stable or project-owned instrumentation alternative exists.
- **FR-006**: `OpenTelemetry.Instrumentation.StackExchangeRedis` MUST be removed when Redis traces and metrics can be preserved through the service's own `ActivitySource` and metrics. Cache Get, Set, and Remove MUST start `LibraryManager` activities named `availability_cache.get`, `availability_cache.set`, and `availability_cache.remove`, and automated tests MUST assert those activities. Compatible OpenTelemetry stable packages MUST be upgraded where appropriate.
- **FR-007**: Dependency changes MUST preserve the complete automated regression suite, with updates only where this feature changes observable HTTP, identity-setup, or failure-mapping behavior.
- **FR-008**: All HTTP transport contracts MUST belong to `LibraryManager.Api`. Request and response contracts MUST be explicit top-level API types under `LibraryManager.Api/Contracts/<Feature>/Requests` and `LibraryManager.Api/Contracts/<Feature>/Responses`. List, history, and audit collection endpoints MUST use API page envelopes (`PagedResponse<TResponse>` or named list types under `Contracts/`), mapped from Application `PagedResult<TDto>`. Application DTOs and `PagedResult<T>` MUST NOT be controller action return types. JSON page fields MUST remain `items`, `page`, `pageSize`, and `totalCount`.
- **FR-009**: No controller source file MAY contain request or response type declarations. Controllers MUST NEVER declare request records, response records, DTOs, or other transport types inside controller source files.
- **FR-010**: Controllers MUST contain only HTTP routing, authorization, model binding, use-case invocation, and HTTP result mapping.
- **FR-011**: Transport validation MUST belong exclusively to `LibraryManager.Api`.
- **FR-012**: Request-body validation MUST use ASP.NET Core `[ApiController]` model validation whenever built-in validation can express the requirement. DataAnnotations MAY be used on API request contract types for simple transport constraints such as required values, lengths, ranges, and formats.
- **FR-013**: Controllers MUST NOT perform manual request-body transport validation when model binding or model validation can enforce the requirement before the action executes. Controllers MUST NOT manually inspect ModelState.
- **FR-014**: POST /loans MUST continue to require the `Idempotency-Key` HTTP header. Idempotency-Key is HTTP metadata, not a Domain concept.
- **FR-015**: The Create Loan action MUST receive a readonly API-layer `IdempotencyKey` bound by `[FromIdempotencyKey]` (`FromIdempotencyKeyAttribute`), not `string` or `string?`. Controller syntax MUST remain approximately `[FromIdempotencyKey] IdempotencyKey idempotencyKey`.
- **FR-016**: `Idempotency-Key` binding and validation MUST use a dedicated ASP.NET Core custom `IModelBinder`. The binder MUST read the header, reject missing, empty, whitespace-only, and longer-than-128-character values, trim valid values, and construct a valid `IdempotencyKey`. Binding MUST NOT rely solely on `IParsable`.
- **FR-017**: Invalid `Idempotency-Key` input MUST produce localized ModelState errors. `[ApiController]` automatic HTTP 400 behavior MUST remain enabled so `CreateLoanUseCase` does not execute.
- **FR-018**: `LoansController` MUST contain no manual `Idempotency-Key` validation and MUST NOT place `Required` or `StringLength` attributes on the action parameter.
- **FR-019**: `Idempotency-Key` validation MUST NOT use middleware or an action filter.
- **FR-020**: Expected Domain and Application failures MUST use `Result` or `Result<T>` with `Error` and `ErrorType`. Expected failures MUST NOT be exceptions. Exceptions MUST NOT be used as normal control flow for expected validation and business outcomes.
- **FR-021**: `ErrorType` MUST support Validation, NotFound, BusinessRule, and Conflict.
- **FR-022**: Result errors MUST contain stable language-neutral error codes and MAY contain optional formatting arguments.
- **FR-023**: Expected Result failures MUST be mapped directly to HTTP responses. They MUST NOT be thrown merely to reach `IExceptionHandler`. Unexpected technical exceptions MUST continue propagating to `IExceptionHandler`.
- **FR-024**: HTTP mapping for expected failures MUST preserve: Validation → HTTP 400, NotFound → HTTP 404, BusinessRule → HTTP 422, Conflict → HTTP 409. HTTP 409 remains only for Idempotency-Key canonical-request mismatch.
- **FR-025**: `DomainException` MUST NOT be used as the normal validation mechanism.
- **FR-026**: Domain validation MUST use reusable lightweight rules rather than repeated manual if/throw blocks.
- **FR-027**: `AuditEvent.Create` MUST NOT throw `DomainException` for expected field validation. `AuditEvent.Create` and other domain factories or state-changing operations that fail through expected validation MUST return `Result` or `Result<T>`.
- **FR-028**: Domain validation MUST NEVER depend on ASP.NET Core, localization, or Infrastructure, and MUST expose error codes rather than localized text.
- **FR-029**: User-facing API validation and error messages MUST support `en-US` and `pt-BR`. `en-US` MUST be the default culture. `Accept-Language` MUST be the primary culture selector through ASP.NET Core `RequestLocalization`. Localization MUST occur at the API boundary. Domain and Application errors MUST remain language-neutral.
- **FR-030**: `IStringLocalizer` and `.resx` resources MUST back DataAnnotations, custom model-binding validation, ValidationProblemDetails user-facing text, Result-based validation, not-found, business-rule, and conflict messages, and generic unexpected HTTP error messages.
- **FR-031**: Structured log templates, metric names, trace identifiers, error codes, and operational logs MUST remain stable English values and MUST NOT be localized.
- **FR-032**: `LibraryManager.Api` MUST use ASP.NET Core `IExceptionHandler` as the single HTTP boundary for unhandled exceptions.
- **FR-033**: Unexpected HTTP error responses MUST include `CorrelationId` and MUST NOT expose stack traces or sensitive technical details.
- **FR-034**: `LibraryManager.Application` MUST NOT contain broad Redis recovery try/catch or other technical try/catch blocks used to recover infrastructure failures. Exceptions MUST be caught only where a valid recovery action exists.
- **FR-035**: `OperationCanceledException` MUST NEVER be swallowed.
- **FR-036**: `IAvailabilityCache` MUST remain defined by Application. Redis resilience MUST be implemented at the Infrastructure boundary using the Decorator Pattern around `RedisAvailabilityCache`.
- **FR-037**: Redis GET failure MUST behave as a cache miss when PostgreSQL can satisfy the request.
- **FR-038**: Redis SET failure MUST be non-fatal and MUST NOT fail an otherwise valid PostgreSQL-backed availability read.
- **FR-039**: Redis REMOVE / immediate invalidation failure MUST be non-fatal for the committed request, MUST be logged, MUST record `library_manager_cache_invalidation_failures` via `ILibraryManagerMetrics`, MUST NOT rollback committed PostgreSQL state, and MUST leave durable retry to the Transactional Outbox.
- **FR-040**: GET /books/{id}/availability MUST continue returning correct PostgreSQL data when Redis is unavailable.
- **FR-041**: Successful `DeactivateBook` MUST invalidate the availability cache after commit.
- **FR-042**: `DeactivateBook` MUST persist the `BookAvailabilityChanged` Outbox message in the same PostgreSQL transaction as the domain mutation and `AuditEvent`.
- **FR-043**: A previously cached active availability value MUST NOT remain observable as current after successful book deactivation.
- **FR-044**: Every SQL command containing runtime values MUST use database parameters. Runtime or user-controlled values MUST NEVER be concatenated into SQL strings.
- **FR-045**: `ExecuteSqlInterpolatedAsync` is accepted as parameterized SQL. `ExecuteSqlInterpolated`, interpolated `FromSql`, and equivalent parameterized EF Core APIs are permitted. `ExecuteSqlRaw` and `FromSqlRaw` MAY receive runtime values only through explicit database parameters.
- **FR-046**: Existing safe `ExecuteSqlInterpolated` calls MUST NOT be rewritten merely because interpolation syntax is present. Unsafe raw SQL interpolation or concatenation MUST be replaced with parameterized APIs.
- **FR-047**: Local Keycloak Direct Access Grants MUST remain disabled.
- **FR-048**: Resource Owner Password Credentials flow MUST NOT be used for library-manager local authentication examples or smoke tests.
- **FR-049**: Swagger authentication MUST continue to use Authorization Code with PKCE exclusively.
- **FR-050**: Existing concurrency, idempotency, Outbox, JWT, and HTTP status behavior MUST remain functionally compatible with `001-library-manager` unless this feature explicitly changes transport validation behavior.
- **FR-051**: Automated tests MUST be updated as required and MUST add coverage for every new hardening requirement in this specification.

### Key Entities

- **IdempotencyKey**: Readonly API-layer value representing a validated, trimmed `Idempotency-Key` header of at most 128 characters. HTTP metadata, not a Domain concept. Invalid input never becomes a `CreateLoanUseCase` argument.
- **Expected failure result**: `Result` or `Result<T>` carrying `Error` with `ErrorType` (Validation, NotFound, BusinessRule, Conflict), a stable language-neutral code, and optional formatting arguments. Expected failures are not exceptions.
- **Localized user-facing message**: Culture-specific HTTP problem text produced at the API boundary for `en-US` and `pt-BR`, derived from error codes or validation metadata. Not stored as Domain or Application text.
- **Availability view**: Optional Redis cache of GET /books/{id}/availability. Misses and cache failures fall back to PostgreSQL. Invalidated after availability-changing commits, including `DeactivateBook`.
- **BookAvailabilityChanged outbox message**: Infrastructure follow-up recorded by `DeactivateBook` in the same PostgreSQL transaction as the availability-changing mutation and `AuditEvent`.
- **Package vulnerability finding**: A known high (NU1903) or critical (NU1904) flaw in a direct or transitive runtime dependency that MUST fail the build unless remediated.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of release builds are blocked while a known high (NU1903) or critical (NU1904) third-party flaw remains, NuGet auditing is not globally disabled, and 0% of those warnings are globally silenced.
- **SC-002**: 100% of POST /loans requests with missing, empty, whitespace-only, or over-128-character Idempotency-Key values receive HTTP 400 and create no loan.
- **SC-003**: 100% of request-body constraint violations that HTTP model validation can express receive HTTP 400 before the matching use case executes.
- **SC-004**: 100% of expected not-found, business-rule, conflict, and domain-validation outcomes return the existing HTTP statuses with language-neutral error codes and are not reported as unhandled failures.
- **SC-005**: In 100% of comparable trials, Accept-Language `pt-BR` yields Portuguese (Brazil) user-facing error text, omitted or `en-US` yields English (United States), and error codes, log templates, metric names, and trace identifiers remain English.
- **SC-006**: 100% of unexpected error responses include the request correlation identifier, and 0% include stack traces or other sensitive technical details.
- **SC-007**: 100% of GET /books/{id}/availability requests return catalog-correct data when the fast cache is unavailable, and 100% of otherwise valid catalog-backed availability reads succeed when cache writes fail.
- **SC-008**: After 100% of successful book deactivations, a previously cached active availability value is not observable as the current availability view.
- **SC-009**: 100% of SQL commands that include runtime values pass those values as parameters, with no concatenated user-controlled SQL.
- **SC-010**: 100% of clients in the source-controlled local Keycloak realm have Direct Access Grants disabled; 0% of repository documentation and automated tests use Resource Owner Password Credentials against Keycloak; Swagger authentication remains Authorization Code with PKCE. (That realm configuration is what causes Keycloak to reject password-grant token requests when imported.)
- **SC-011**: 100% of existing concurrency, idempotency, Outbox, JWT, HTTP status, authentication, observability, and health automated tests remain passing after required updates except where this feature explicitly changes transport validation behavior, and every new hardening requirement in this specification has automated coverage.

## Assumptions

- Planning and implementation MUST follow the Library Manager Constitution v1.1.0. This feature does not amend or weaken that constitution.
- Core library lending behavior is unchanged: last-copy correctness, durable PostgreSQL-backed idempotency, audit pairing, Outbox pairing, JWT resource-server authentication, and Redis-as-optional-cache remain as specified in `001-library-manager`, except where this feature explicitly changes transport validation behavior.
- HTTP status mapping for expected Result failures is: Validation → HTTP 400, NotFound → HTTP 404, BusinessRule → HTTP 422, Conflict → HTTP 409 (Conflict remains reserved for Idempotency-Key canonical-request mismatch).
- All Domain factories and state-changing operations that currently throw `DomainException` for expected validation (including `Book`, `User`, `Loan`, and `AuditEvent`) move to `Result` / `Result<T>` and reusable validation rules. `AuditEvent.Create` does not throw `DomainException` for expected field validation.
- Unsupported `Accept-Language` values fall back to `en-US`. Localization occurs at the API boundary. Domain and Application errors remain language-neutral. Operational logs remain English.
- Redis observability after removing `OpenTelemetry.Instrumentation.StackExchangeRedis` is preserved with the service's own `ActivitySource` (`availability_cache.get` / `.set` / `.remove`) and metrics (`library_manager_cache_invalidation_failures`); automated tests assert those; no replacement prerelease package is introduced. Compatible OpenTelemetry stable packages are upgraded where appropriate.
- `ExecuteSqlInterpolatedAsync` is accepted as parameterized SQL. Existing usage in persistence and idempotency storage is not rewritten for style. Runtime string concatenation into SQL is forbidden.
- Local Keycloak remains the development identity provider with Direct Access Grants disabled; production identity configuration stays externalized. Swagger remains Authorization Code with PKCE only.
- Automated tests may obtain tokens through a confidential test client or Authorization Code/PKCE helper that does not use Resource Owner Password Credentials and does not require Direct Access Grants. `dotnet test` does not host Keycloak; Direct Access Grants disabled in the imported realm JSON is the CI-proof that password grant is rejected. Optional live curl against Compose Keycloak is operator confirmation only.
- HTTP list/history envelopes are API `PagedResponse<T>` (or named equivalents) under `Contracts/`. Application `PagedResult<TDto>` is not an HTTP contract type.
- `IdempotencyKey` binding uses a dedicated `IModelBinder` and `FromIdempotencyKeyAttribute`. `IParsable` alone is insufficient because missing-header behavior and localized ModelState failures are required.

Stakeholder-mandated platform mapping (planning MUST honor these names and contracts; they are product constraints, not open design choices):

- HTTP contracts: all transport types in `LibraryManager.Api`; folder `LibraryManager.Api/Contracts/<Feature>/Requests` and `.../Responses`; shared HTTP metadata/envelopes may live in `Contracts/Common/` (`IdempotencyKey`, `PagedResponse<T>`); no request/response declarations in controller source files; controllers MUST NOT return Application `PagedResult<T>` or DTOs
- Body validation: `[ApiController]` model validation and DataAnnotations on request contracts; controllers do not inspect ModelState
- Idempotency header type: readonly API-layer `IdempotencyKey`; HTTP metadata, not Domain
- Idempotency binding: dedicated `IModelBinder` exposed as `FromIdempotencyKeyAttribute`; not `IParsable` alone; not middleware; not an action filter
- Create Loan signature constraint: `[FromIdempotencyKey] IdempotencyKey idempotencyKey`
- Expected-failure model: `Result`, `Result<T>`, `Error`, `ErrorType` with Validation, NotFound, BusinessRule, Conflict mapped to HTTP 400/404/422/409
- Unexpected HTTP boundary: ASP.NET Core `IExceptionHandler`; unexpected technical exceptions continue propagating there
- Localization: at the API boundary; `RequestLocalization`, `IStringLocalizer`, `.resx`; cultures `en-US` (default) and `pt-BR`; culture from `Accept-Language`
- Cache abstraction: Application `IAvailabilityCache`
- Cache resilience: Infrastructure Decorator Pattern around `RedisAvailabilityCache`; no broad Application Redis recovery try/catch
- Deactivation follow-up: `DeactivateBook` writes `BookAvailabilityChanged` in the same PostgreSQL transaction as mutation and `AuditEvent`, then invalidates cache after commit
- SQL: `ExecuteSqlInterpolatedAsync` accepted as parameterized; unsafe raw concatenation forbidden; raw APIs use explicit parameters for runtime values
- Package audit: repository-wide NuGet audit of direct and transitive packages; NU1903 and NU1904 fail the build; auditing not globally disabled; compatible OpenTelemetry stable packages upgraded where appropriate
- Identity: Direct Access Grants disabled; no ROPC examples or smoke tests; Swagger Authorization Code with PKCE

## Out of Scope

- Changes to last-copy lending rules, loan state machine, due-date calculation, catalog inventory model, or audit field set
- Additional cultures beyond `en-US` and `pt-BR`
- Making Redis required or using Redis to authorize loans
- Replacing PostgreSQL-backed idempotency or Transactional Outbox
- Enabling Direct Access Grants or adding an API-issued token endpoint
- CQRS, mediator, generic-repository, or other constitution-forbidden designs
- Blanket NuGet audit suppression or keeping a prerelease Redis instrumentation package when project-owned telemetry can cover it
