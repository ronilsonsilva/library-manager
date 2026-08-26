<!--
Sync Impact Report
- Version change: unversioned scaffold → 1.0.0
- Modified principles:
  - [PRINCIPLE_1_NAME] → I. English Project Language
  - [PRINCIPLE_2_NAME] → II. Clean Architecture and Explicit Use Cases
  - [PRINCIPLE_3_NAME] → III. PostgreSQL-Owned Correctness
  - [PRINCIPLE_4_NAME] → IV. Durable PostgreSQL-Backed Idempotency
  - [PRINCIPLE_5_NAME] → V. Transactional Outbox
- Added sections:
  - Runtime Constraints (Cache, Security, Observability)
  - Quality Gates
  - Governance (concrete amendment, versioning, and compliance rules)
- Removed sections: none (placeholder scaffold replaced in full)
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

## Runtime Constraints

### Cache

Redis is a performance optimization only. It MUST NOT become a source of
truth for business decisions.

Availability cache invalidation MUST provide both:

- immediate best-effort post-commit invalidation;
- durable retry through the Transactional Outbox.

Cache invalidation MUST use asynchronous APIs.

Fire-and-forget `Task.Run` MUST NEVER be used for cache invalidation.

Redis failures MUST NEVER rollback committed PostgreSQL business
transactions.

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
  reliability, authentication, or replica safety MUST be rejected unless
  an approved amendment exists.
- Reviewers MUST require an explicit justification for added framework
  complexity.

**Version**: 1.0.0 | **Ratified**: 2026-08-25 | **Last Amended**: 2026-08-25
