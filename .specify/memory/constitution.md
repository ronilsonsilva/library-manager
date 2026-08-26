<!--
Sync Impact Report
- Version change: 1.0.0 → 1.1.0
- Modified principles: none renamed; principles I–V retained verbatim
- Added sections:
  - VI. API Contracts
  - VII. HTTP Input Validation
  - VIII. Strongly Typed HTTP Metadata
  - IX. Expected Failures
  - X. Domain Validation
  - XI. Localization
  - XII. Exception Handling
  - XIII. SQL Safety
  - Quality Gates / Dependency Security
  - Cache expanded with Redis resilience rules (no existing cache rule removed)
  - Security expanded with Direct Access Grants and ROPC prohibitions
    (existing JWT, Keycloak, and PKCE rules retained)
  - Governance compliance review expanded to cover the new rules
- Removed sections: none
- Follow-up TODOs: none
-->

# Library Manager Constitution

## Core Principles

### I. English Project Language

All project nomenclature MUST be written in English. This includes source
code, namespaces, class names, method names, variable names, database
objects, configuration names, Docker resources, Kubernetes resources, logs,
errors, documentation, tests, and Git-related names.

The repository name MUST be `library-manager`.

Production .NET projects MUST use these names:

- `LibraryManager.Domain`
- `LibraryManager.Application`
- `LibraryManager.Infrastructure`
- `LibraryManager.Api`

Test projects MUST use these names:

- `LibraryManager.UnitTests`
- `LibraryManager.IntegrationTests`

Rationale: a single language and a fixed project map keep reviews, tests,
and technical evaluation unambiguous.

### II. Clean Architecture and Explicit Use Cases

The solution MUST use Clean Architecture. Dependency direction MUST always
point inward.

`LibraryManager.Domain` MUST have no dependency on Application,
Infrastructure, ASP.NET Core, Entity Framework Core, Redis, authentication
frameworks, or OpenTelemetry.

`LibraryManager.Application` MAY depend only on `LibraryManager.Domain` and
framework-neutral abstractions.

`LibraryManager.Infrastructure` MUST implement persistence, distributed
caching, idempotency storage, transactional outbox, and technical
integrations.

`LibraryManager.Api` MUST own HTTP concerns, authentication, authorization,
OpenAPI, middleware, health endpoints, telemetry configuration, and
dependency composition.

The solution MUST NOT use CQRS. The codebase MUST NOT introduce Command,
Query, Handler, `IRequest`, `IRequestHandler`, mediator, or MediatR
concepts or terminology.

Application behavior MUST be represented through explicit UseCase classes.

The solution MUST NOT use a Generic Repository. Repository abstractions
MUST expose the concrete capabilities needed by business use cases.

Rationale: explicit use cases and capability-specific repositories keep
business intent readable without mediator or generic-persistence indirection.

### III. PostgreSQL-Owned Correctness

PostgreSQL is the source of truth for every business invariant.

Redis MUST NEVER determine whether a loan can succeed.

The system MUST remain correct with multiple API replicas.

Process memory, `SemaphoreSlim`, application locks, and Redis distributed
locks MUST NEVER be used to protect business invariants.

Concurrency-sensitive state changes MUST be resolved atomically by
PostgreSQL.

`AuditEvent` and the corresponding business mutation MUST share the same
PostgreSQL transaction.

Rationale: replica-local memory and cache locks cannot enforce invariants
that must survive scale-out, restart, and concurrent requests.

### IV. Durable PostgreSQL-Backed Idempotency

`POST /loans` MUST use durable PostgreSQL-backed idempotency.

Application-level pre-checks alone are insufficient for idempotency
correctness and MUST NOT be treated as the enforcement mechanism.

PostgreSQL unique constraints MUST enforce `Idempotency-Key` ownership
under concurrency.

Rationale: only a durable uniqueness guarantee can make retries safe when
multiple replicas handle the same key at the same time.

### V. Transactional Outbox

Reliability-sensitive asynchronous side effects MUST use a Transactional
Outbox.

Outbox messages MUST be persisted in the same PostgreSQL transaction as
the originating business state change.

Outbox processing MUST be safe with multiple API replicas.

Outbox processing MUST assume at-least-once delivery.

Outbox consumers MUST be idempotent.

Outbox processing MUST recover from worker crashes.

Rationale: side effects that leave the business transaction MUST be
durable, replayable, and safe under duplication.

### VI. API Contracts

HTTP request and response contracts MUST be explicit top-level API types.

API transport contracts MUST be organized under:

- `LibraryManager.Api/Contracts/<Feature>/Requests`
- `LibraryManager.Api/Contracts/<Feature>/Responses`

Controllers MUST NEVER declare request records, response records, DTOs, or
other transport types inside controller source files.

Controllers MUST contain only HTTP routing, authorization, model binding,
use-case invocation, and HTTP result mapping.

Rationale: transport types belong in a dedicated contract surface so HTTP
shape stays reviewable and controllers stay free of hidden DTO definitions.

### VII. HTTP Input Validation

Transport validation belongs exclusively to `LibraryManager.Api`.

Request-body validation MUST use ASP.NET Core model validation and
`[ApiController]` whenever built-in validation can express the requirement.

DataAnnotations MAY be used on API request contracts for transport
concerns such as required values, lengths, ranges, and formats.

Controllers MUST NOT contain manual transport validation when ASP.NET Core
model binding or model validation can enforce the requirement before the
action executes.

Rationale: the framework validation pipeline is the HTTP input gate;
controllers MUST NOT reimplement that gate after the action starts.

### VIII. Strongly Typed HTTP Metadata

Important HTTP metadata that requires normalization or validation MUST NOT
be represented as loosely validated raw strings in controller actions.

`Idempotency-Key` MUST be represented by a strongly typed API-layer
`IdempotencyKey` value.

`Idempotency-Key` binding and validation MUST use ASP.NET Core custom
model binding.

The Create Loan action MUST receive a valid `IdempotencyKey` rather than
manually validating `string` or `string?`.

Missing or invalid `Idempotency-Key` input MUST produce ModelState
validation errors before controller execution.

`[ApiController]` automatic HTTP 400 behavior MUST remain enabled.

`Idempotency-Key` validation MUST NOT use middleware or an action filter.
The responsibility is HTTP input binding.

Rationale: header metadata that can fail validation is still HTTP input
and MUST fail through the same model-binding path as the request body.

### IX. Expected Failures

Exceptions MUST NOT be used as normal control flow for expected validation
and business outcomes.

Expected Domain and Application failures MUST use `Result` or `Result<T>`.

Result errors MUST contain stable language-neutral error codes.

Error categories MUST distinguish at least:

- Validation
- NotFound
- BusinessRule
- Conflict

`DomainException` MUST NOT be used as the normal validation mechanism.

Exceptions remain appropriate for unexpected or truly exceptional
technical failures.

Rationale: expected outcomes are part of the business contract and MUST be
modeled as values, not as thrown control flow.

### X. Domain Validation

Repetitive domain validation MUST use reusable lightweight validation
rules rather than repeated manual if/throw blocks.

Domain factories and state-changing operations that can fail through
expected validation MAY return `Result` or `Result<T>`.

Domain validation MUST NEVER depend on ASP.NET Core, localization, or
Infrastructure.

Rationale: domain rules stay portable and testable only when they remain
free of presentation and infrastructure concerns.

### XI. Localization

User-facing API validation and error messages MUST support localization.

This principle governs user-facing API message text only. It MUST NOT
localize source-code nomenclature, error codes, log templates, metric
names, or trace identifiers required by English Project Language.

Supported cultures initially MUST be:

- `en-US`
- `pt-BR`

`en-US` MUST be the default culture.

ASP.NET Core `RequestLocalization` and `IStringLocalizer` MUST be used.

DataAnnotations and custom model-binding validation messages MUST be
resource-backed.

Domain and Application layers MUST expose language-neutral error codes
rather than localized text.

Structured log templates, metric names, trace identifiers, and error
codes MUST remain stable English values and MUST NOT be localized.

Rationale: callers receive language-appropriate HTTP messages while
operators keep stable English diagnostics and codes.

### XII. Exception Handling

`LibraryManager.Api` MUST use ASP.NET Core `IExceptionHandler` as the
single HTTP boundary for unhandled exceptions.

Expected Result failures MUST NOT be converted into exceptions merely to
reach `IExceptionHandler`.

`LibraryManager.Application` MUST NOT contain broad technical try/catch
blocks used to recover infrastructure failures.

Exceptions MUST be caught only where a valid recovery action exists.

`OperationCanceledException` MUST NEVER be swallowed.

Rationale: unexpected failures surface once at the HTTP boundary;
expected failures stay on the Result path, and cancellation MUST propagate.

### XIII. SQL Safety

Every SQL command containing runtime values MUST use database parameters.

Runtime or user-controlled values MUST NEVER be concatenated into SQL
strings.

`ExecuteSqlInterpolated`, `FromSql` interpolated, and equivalent EF Core
parameterized APIs are permitted.

`ExecuteSqlRaw` and `FromSqlRaw` MAY only receive runtime values through
explicit database parameters.

Existing safe `ExecuteSqlInterpolated` calls MUST NOT be rewritten merely
because interpolation syntax is present.

Rationale: parameterization is the SQL injection control. Interpolated EF
APIs that emit parameters are already safe and MUST NOT be churned.

## Runtime Constraints

### Cache

Redis is a performance optimization only. It MUST NOT become a source of
truth for business decisions.

Redis remains an optional performance optimization. The system MUST remain
correct when Redis is unavailable.

Availability cache invalidation MUST provide both:

- immediate best-effort post-commit invalidation;
- durable retry through the Transactional Outbox.

Cache invalidation MUST use asynchronous APIs.

Fire-and-forget `Task.Run` MUST NEVER be used for cache invalidation.

Redis failures MUST NEVER rollback committed PostgreSQL business
transactions.

Redis immediate invalidation failure MUST NOT rollback committed
PostgreSQL state.

Redis read failure MUST behave as a cache miss when PostgreSQL can
satisfy the request.

Redis write failure MUST NOT fail an otherwise valid PostgreSQL-backed
read.

Transactional Outbox remains responsible for durable cache invalidation
retries.

Redis resilience MUST be implemented at the Infrastructure boundary. A
Decorator around `IAvailabilityCache` MUST isolate Redis failure handling.

Cached availability MUST use a bounded TTL.

### Security

The API MUST use standards-based JWT Bearer authentication backed by an
external OpenID Connect/OAuth 2.0 identity provider.

`LibraryManager.Api` MUST act as a resource server.

The API MUST NEVER implement a custom username/password endpoint that
generates access tokens.

Access tokens MUST be validated for signature, issuer, audience, and
lifetime.

Local development MUST use Keycloak as the OpenID Connect identity
provider.

Swagger UI MUST authenticate through Keycloak using Authorization Code
with PKCE.

Keycloak Direct Access Grants MUST remain disabled.

Resource Owner Password Credentials flow MUST NOT be used for
library-manager local authentication examples or smoke tests.

Local Keycloak configuration MUST be importable from source-controlled
development realm configuration.

Production identity-provider configuration MUST be externalized.

Library domain `User` and authenticated caller identity are separate
concepts and MUST NOT be collapsed into one model.

Application MUST access authenticated caller information only through
`ICurrentUserContext`.

Application MUST NEVER reference `HttpContext`.

Audit actor identity MUST come from the authenticated JWT subject.

Mutation endpoints MUST require explicit authorization.

Authentication and authorization MUST preserve correct HTTP 401 and 403
semantics.

### Observability

Correlation IDs MUST propagate through HTTP responses, logs, traces, and
`AuditEvent` records.

All timestamps MUST use UTC.

The system MUST use structured logging.

The system MUST provide OpenTelemetry-compatible traces and metrics.

## Quality Gates

Production code MUST use asynchronous APIs and MUST propagate
`CancellationToken`.

HTTP error responses MUST use consistent RFC Problem Details.

Production credentials and secrets MUST NEVER be stored in source control.

Configuration MUST use appsettings plus environment variables or secret
providers.

Concurrency, idempotency, authentication, and Outbox guarantees MUST have
integration tests.

Every important engineering decision MUST be explainable during technical
evaluation.

The codebase MUST prefer explicit, readable, and maintainable code over
unnecessary framework complexity. Additional frameworks or generic
abstractions MUST NOT be introduced unless they are required to satisfy
this constitution and can be justified in review.

### Dependency Security

NuGet security auditing MUST remain enabled repository-wide.

NuGet auditing MUST include transitive dependencies.

High and critical vulnerability warnings MUST fail the build.

Known vulnerabilities MUST be remediated through package upgrade,
dependency replacement, or explicit documented risk treatment rather than
blanket warning suppression.

Runtime prerelease dependencies MUST NOT be used when a stable or
internal-instrumentation alternative exists.

Dependency changes MUST preserve the complete automated regression suite.

## Governance

This constitution is the governing engineering standard for
`library-manager`. Conflicting local conventions, framework defaults, or
ad hoc shortcuts are invalid unless this document is amended first.

Amendments MUST be recorded in `.specify/memory/constitution.md` with:

- an updated semantic version;
- an updated **Last Amended** date;
- a Sync Impact Report describing principle or section changes.

Versioning policy:

- MAJOR: backward-incompatible removal or redefinition of a principle.
- MINOR: new principle or section, or materially expanded guidance.
- PATCH: clarification, wording, typo fix, or non-semantic refinement.

Compliance review:

- Specifications, plans, tasks, pull requests, and code reviews MUST
  verify compliance with this constitution.
- A change that weakens PostgreSQL-owned correctness, idempotency, Outbox
  reliability, authentication, replica safety, API contract discipline,
  Result-based expected-failure handling, localization boundaries, Redis
  resilience, SQL parameterization, or dependency security MUST be
  rejected unless an approved amendment exists.
- Reviewers MUST require an explicit justification for added framework
  complexity.

**Version**: 1.1.0 | **Ratified**: 2026-08-25 | **Last Amended**: 2026-08-26
