# Feature Specification: Project Documentation

**Feature Branch**: `003-project-documentation`

**Created**: 2026-08-27

**Status**: Draft

**Input**: User description: "Create a new feature named project-documentation for library-manager. The purpose of this feature is to create a comprehensive production-quality root README.md that serves simultaneously as the technical challenge delivery guide, local execution guide, API usage guide, architecture and engineering decision document, test strategy and verification guide, and operational deployment overview."

## Clarifications

### Session 2026-08-27

- Q: What may this feature create or change, and in which language? → A: Only the repository-root `README.md` is created or substantially modified, unless a real documentation/code inconsistency is discovered. English only.
- Q: What is the source of truth when the original challenge, OpenAPI, and the repository disagree? → A: Document actual implemented behavior, not merely the challenge’s suggested API. Current code, OpenAPI, tests, and configuration take precedence over original challenge choices. If the repository and OpenAPI disagree, do not guess—report the discrepancy before documenting it.
- Q: What belongs in the public API and authentication sections? → A: Every production HTTP endpoint exposed by current controllers, each with method, route, purpose, authentication/authorization, headers, request, successful status, and relevant error statuses. Use the implemented book-update method (do not assume PATCH). Document `GET /books/{id}/history` if it remains an intentional production alias. Test-only `/security` probes are not public API. Keycloak is the local OIDC provider; Swagger uses Authorization Code with PKCE; Direct Access Grants are not documented or used; the API is a resource server and never issues tokens.
- Q: How must tests, cache, outbox, localization, and replica correctness be documented? → A: Distinguish unit from integration tests; treat integration tests as first-class challenge evidence; document Testcontainers PostgreSQL and Redis; dedicated two-host last-copy subsection (two WebApplicationFactory hosts if still implemented); sequential and concurrent idempotency separately; document Redis-resilience and Outbox tests that exist after production-hardening; do not hard-code test counts unless automatically current. PostgreSQL is always source of truth; Redis is only a performance optimization; document cache failure/fallback if tests confirm it. Transactional Outbox is an implemented reliability decision, not a future idea. Document en-US and pt-BR only if implemented and tested. The 2–11 replica section explains database and Outbox guarantees, not merely Kubernetes replica count.
- Q: What remain as style, security-doc, limitation, and AI rules? → A: Clickable table of contents; tables for endpoint, environment-variable, metric, and test matrices; concise diagrams for architecture, loan concurrency, and Outbox; avoid excessive repetition; detailed but practical. Environment variables come from actual application configuration and Docker/Kubernetes files; no invented secrets; local-only values only when already in development configuration. SQL docs distinguish safe interpolated parameterization from unsafe raw concatenation; dependency-vulnerability policy matches production-hardening. Known limitations must be real—do not manufacture them. Short AI-assisted-development disclosure; the engineer remains responsible for understanding and defending every implementation decision. Clean Architecture is documented because it is implemented, emphasizing the problems it solves rather than scoring criteria; state that CQRS and MediatR are not used.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Use the README as the challenge delivery and local run guide (Priority: P1)

A technical evaluator who has never opened the source tree can read the root delivery guide, understand what the service is, what the challenge evaluates, and start the complete local stack. The guide lists prerequisites, clone and build steps, how to start and stop the local stack, how to reset local data volumes when needed, and the local addresses for the service, interactive API explorer, local identity provider, and health checks.

**Why this priority**: Delivery fails if an evaluator cannot run the system from the root guide alone.

**Independent Test**: Follow only the root delivery guide from a clean checkout. Confirm prerequisites, start/stop/reset commands, and local addresses match the repository. Confirm the guide does not invent services or ports.

**Acceptance Scenarios**:

1. **Given** a clean checkout, **When** an evaluator reads the introduction, **Then** they can state what the service does, which engineering problems the challenge evaluates, the primary design goals, and that later production-oriented hardening exists beyond the original challenge.
2. **Given** the getting-started section, **When** an evaluator follows it, **Then** they have complete prerequisites and commands to clone, enter the repository, build, start the full local stack, stop it, and reset local volumes.
3. **Given** a running local stack as described, **When** an evaluator looks up addresses, **Then** the documented service, interactive explorer, identity provider, and health-check addresses match the repository’s local configuration.

---

### User Story 2 - Authenticate and call the public API from the README (Priority: P1)

An evaluator can use the root guide as an API usage guide: local identity is an external sign-in provider; the service never mints access tokens; interactive explorer login uses Authorization Code with proof-of-possession of the code verifier; the Librarian role is required for mutations. A complete public endpoint reference lets them call catalog, member, loan, audit, and health operations with method, route, purpose, auth rules, bodies, headers, success and important error statuses, and short examples.

**Why this priority**: Evaluators must exercise the real API without reverse-engineering controllers.

**Independent Test**: Compare every documented public route to implemented public production endpoints. If controllers and the published HTTP contract disagree, the discrepancy is reported before the route is documented. Confirm password-grant and Direct Access Grants are absent. Confirm test-only `/security` probes are not listed as public production API.

**Acceptance Scenarios**:

1. **Given** the local authentication section, **When** an evaluator follows it, **Then** they understand Keycloak is the local identity provider, the service is a resource server that never issues tokens, Swagger uses Authorization Code with PKCE, the Librarian authorization model, and that Direct Access Grants and Resource Owner Password Credentials are not documented login paths.
2. **Given** the endpoint reference (tables preferred), **When** an evaluator looks up a public production operation, **Then** they see method, route, purpose, authentication/authorization, headers, request, successful status, relevant error statuses, and a short example when useful.
3. **Given** catalog, member, loan, audit, and health operations exposed by current controllers, **When** the guide is reviewed, **Then** each is documented, including `GET /books/{id}/history` if it remains an intentional production alias, and the book update method is the one actually implemented (not an assumed PATCH).
4. **Given** test-only `/security` or unexpected-error probes, **When** the public API reference is read, **Then** those probes are not presented as public production endpoints.

---

### User Story 3 - Understand architecture, consistency, and engineering decisions (Priority: P1)

An evaluator can use the root guide as the architecture and decision document: layered project boundaries (documented because they are implemented, emphasizing the concrete problems they solve rather than scoring criteria), inward dependencies, no CQRS or MediatR, last-copy correctness, durable loan-key uniqueness, audit pairing, PostgreSQL as source of truth with Redis only as a performance optimization, durable invalidation follow-up as an implemented reliability decision, why many replicas stay correct from database and Outbox guarantees, and a closing table mapping problems to decisions and trade-offs.

**Why this priority**: The challenge is judged on engineering judgment, not only on a running process.

**Independent Test**: Read architecture, concurrency, replica, audit, cache, and outbox sections against constitution and implemented behavior. Confirm claims match tests and source; confirm no invented guarantees.

**Acceptance Scenarios**:

1. **Given** the architecture and structure sections, **When** an evaluator reads them, **Then** they can name the four production layers, both test projects, inward dependency direction, the concrete consistency problems the layering solves (not as scoring criteria), and that CQRS and MediatR are not used.
2. **Given** the last-copy and replica sections, **When** an evaluator reads them, **Then** they understand why a read-check-write approach is unsafe, why exactly one of two concurrent last-copy borrowers succeeds, why process memory and cache locks are not used for that invariant, and why two to eleven replicas remain correct because of database and Outbox guarantees rather than replica count alone.
3. **Given** idempotency, audit, cache, and outbox sections, **When** an evaluator reads them, **Then** they can explain loan-key rules, audit versus technical logs, PostgreSQL as source of truth, Redis as a performance optimization only, post-commit invalidation and (if tests confirm it) fallback when the cache is down, and why Transactional Outbox is an implemented reliability decision that shares the business transaction.
4. **Given** the design-decisions summary, **When** an evaluator scans it, **Then** they see a concise mapping from important engineering problems to implemented decisions and trade-offs.

---

### User Story 4 - Verify the system using the documented test strategy (Priority: P1)

An evaluator can use the root guide as the test and verification guide: unit versus integration strategy, integration tests as first-class evidence for challenge requirements, why a fake in-process database is not used to prove PostgreSQL concurrency, Testcontainers PostgreSQL and Redis, two-host last-copy evidence, commands to run all tests or each suite (not fragile hard-coded counts), a matrix of important integration scenarios, sequential versus concurrent idempotency, and Redis-resilience and Outbox tests that exist after production-hardening.

**Why this priority**: Challenge scoring depends on being able to reproduce and interpret automated proof.

**Independent Test**: Run the documented test commands. Confirm the integration matrix names scenarios that exist in the suite. Confirm the two-host last-copy narrative matches the implemented concurrent test (two WebApplicationFactory hosts if still implemented). Confirm test counts are not hard-coded unless automatically current.

**Acceptance Scenarios**:

1. **Given** the testing strategy section, **When** an evaluator follows the commands, **Then** they can run the full suite, unit tests only, and integration tests only, and they see unit tests distinguished from integration tests.
2. **Given** the integration matrix (table preferred), **When** an evaluator reads a listed scenario, **Then** they see purpose, setup, action, and expected guarantee, and the scenario corresponds to an actual automated test (or is omitted if no such test exists).
3. **Given** the concurrent last-copy subsection, **When** an evaluator reads it, **Then** they understand two independent service hosts share one PostgreSQL database (two WebApplicationFactory hosts if still implemented), one copy remains, two users race, one loan succeeds, one is rejected, inventory never goes negative, and why two hosts are stronger evidence than two in-process tasks against a single business object.
4. **Given** idempotency, cache-resilience, and outbox test subsections, **When** an evaluator reads them, **Then** they understand sequential replay and concurrent same-key tests separately, asserted database state, and Redis-resilience and Outbox integration tests that exist after production-hardening.

---

### User Story 5 - Operate, configure, and deploy from the README (Priority: P2)

An operator or evaluator can configure local or deployed execution from documented settings, understand database migration behavior, container layout, Kubernetes manifests as a baseline (not a claim that a cluster was applied), health probes, observability, localization, error-handling, security, and dependency-audit policy.

**Why this priority**: Operational understanding is required for production-oriented evaluation, after the system can be run and tested.

**Independent Test**: Compare every documented setting name to application configuration and compose/Kubernetes files. Confirm secrets are examples or placeholders only. Confirm Kubernetes text does not claim a live cluster was deployed if only manifests exist.

**Acceptance Scenarios**:

1. **Given** the configuration section, **When** an operator looks up a setting, **Then** they see name, purpose, a safe development example when one exists, whether it is required, and whether it is secret-sensitive, with no real production secrets.
2. **Given** database, container, and Kubernetes sections, **When** an operator reads them, **Then** they understand PostgreSQL’s role, how schema versions are applied locally, container services, and that Kubernetes artifacts are manifests with external data-store and identity assumptions.
3. **Given** health, observability, localization, error-handling, and security sections, **When** an operator reads them, **Then** they can distinguish liveness from readiness, name implemented custom metrics, see en-US/pt-BR only if those cultures are implemented and tested, know expected versus unexpected error handling, know parameterized SQL versus unsafe concatenation, and know the resource-server identity and production-hardening package-audit policy.

---

### User Story 6 - Trust the README as an accurate snapshot (Priority: P1)

A reviewer can treat the root guide as describing the system that is actually implemented. Content is derived from constitution, feature specs, HTTP contracts, source, tests, compose, containers, identity realm, Kubernetes manifests, and configuration. Current code, OpenAPI, tests, and configuration take precedence over the original challenge’s suggested API. English only. Clickable table of contents. No invented endpoints, settings, status codes, or guarantees. Documentation-only unless review finds a real mismatch.

**Why this priority**: An impressive but false guide is worse than a shorter true one.

**Independent Test**: Spot-check documented routes, statuses, settings, cache key/TTL, identity flows, and replica claims against source and tests. Search the guide for password-grant login, Direct Access Grants used as login, invented paths, and hard-coded test counts.

**Acceptance Scenarios**:

1. **Given** any documented public endpoint, setting, or status, **When** a reviewer checks the repository, **Then** it exists in implementation or configuration; undocumented invented items are absent.
2. **Given** the root guide language, **When** a reviewer reads it, **Then** it is entirely English, has a clickable table of contents covering the mandated topics, prefers tables for matrices, uses concise diagrams for architecture, loan concurrency, and Outbox, and stays practical for a reviewer running the project.
3. **Given** a mismatch between repository and OpenAPI, **When** documentation is written, **Then** the discrepancy is reported before it is documented; the guide is not completed by guessing.

---

### Edge Cases

- A setting that exists only in Kubernetes manifests or only in local compose is labeled for that environment, not presented as universal.
- Environment-variable names are taken from application configuration and Docker/Kubernetes files; no secret value is invented; local-only development values appear only when already intentionally committed.
- Direct Access Grants and Resource Owner Password Credentials are not documented as login or smoke paths.
- Test-only `/security` HTTP probes are omitted from the public endpoint catalog.
- `GET /books/{id}/history` is documented only if it remains an intentional production alias; if removed, the guide does not keep it.
- Book update documentation uses the HTTP method the service actually implements and MUST NOT assume PATCH because the original challenge suggested it.
- If controllers and OpenAPI disagree, the discrepancy is reported before the endpoint is documented; the author does not guess.
- Kubernetes section describes manifests and assumptions; it does not claim a cluster was deployed unless that is true.
- Custom metrics listed in the guide are only those the service actually records.
- Test counts are not hard-coded unless they are automatically accurate and current; commands and scenario descriptions are preferred.
- Cache failure and PostgreSQL fallback are documented only if current tests confirm that behavior.
- en-US and pt-BR are documented only if production-hardening implemented and tested them.
- Known limitations list only true current limitations; the section is not filled with invented drawbacks.
- Future-evolution items are optional improvements, not implied challenge defects.
- AI-assisted development is disclosed briefly; the engineer remains responsible for every implementation decision.
- Diagrams are concise and used for architecture, loan concurrency, and Outbox; tables are preferred for endpoints, settings, metrics, and test matrices.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The only deliverable this feature creates or substantially modifies is the repository-root `README.md` in English, unless a real documentation/code inconsistency is discovered. The guide MUST serve as challenge delivery guide, local execution guide, API usage guide, architecture and decision document, test strategy and verification guide, and operational deployment overview.
- **FR-002**: The guide MUST document actual implemented behavior, not merely the original challenge’s suggested API. It MUST NOT invent endpoints, settings, status codes, features, or guarantees.
- **FR-003**: Current code, OpenAPI, tests, and configuration MUST take precedence when the original challenge allowed implementation choices. The guide MUST still be derived from the constitution, feature specifications, contracts, source, tests, local stack, container build, local identity configuration, Kubernetes manifests, and application configuration.
- **FR-004**: This feature is documentation-only unless a real mismatch is found. If the repository and OpenAPI disagree, the author MUST report the discrepancy and MUST NOT guess or document a blended invented contract. Only after the discrepancy is resolved MAY the guide describe that endpoint.
- **FR-005**: The guide MUST include a clickable table of contents that reaches every mandated section.
- **FR-006**: The introduction MUST explain what the service is, what engineering problems the challenge evaluates, primary design goals, the technology stack actually used, and production-oriented additions beyond the original challenge.
- **FR-007**: The guide MUST document the technologies the repository actually uses for runtime, HTTP API, persistence, cache, identity, telemetry, tests, containers, and cluster manifests, using the names present in the repository (see Assumptions).
- **FR-008**: The architecture section MUST document Clean Architecture because it is implemented, emphasizing the concrete problems it solves rather than presenting it as challenge scoring criteria. It MUST name the four production layers and both test projects, inward dependency direction, and MUST explicitly state that CQRS and MediatR are not used (and that a generic repository is not used).
- **FR-009**: The guide MUST include a concise repository tree of important directories without listing every source file.
- **FR-010**: Getting started MUST include complete prerequisites and commands to clone, enter the repository, build, start the full local stack, stop it, and reset local volumes when necessary, plus local addresses for the API, interactive explorer, identity provider, and health endpoints.
- **FR-011**: Local authentication MUST document Keycloak as the local OpenID Connect provider, that the API is a resource server and never issues access tokens, that Swagger uses Authorization Code with PKCE, the Librarian role and authorization model. Direct Access Grants MUST NOT be documented or used as a login path. Resource Owner Password Credentials MUST NOT be documented.
- **FR-012**: Configuration MUST list environment variables extracted from actual application configuration and Docker/Kubernetes files. Each entry MUST include name, purpose, safe development example only when already present in development configuration, required vs optional, and secret-sensitivity. No secret value MAY be invented. Real production secrets MUST NOT appear.
- **FR-013**: Database documentation MUST explain PostgreSQL as system of record, how schema versions are kept, automatic apply behavior if the local stack currently does that, the manual schema-update command, and how contributors add a new schema version if that workflow exists.
- **FR-014**: The API reference MUST document every production HTTP endpoint exposed by current controllers. Test-only `/security` endpoints MUST NOT appear in the public API reference.
- **FR-015**: Each public endpoint entry MUST include method, route, purpose, authentication/authorization, headers, request, successful status, and relevant error statuses. Tables are preferred. Short examples MAY be included when useful.
- **FR-016**: Book update MUST be documented with the implemented HTTP method; the guide MUST NOT assume PATCH. `GET /books/{id}/history` MUST be documented if it remains an intentional production alias. The catalog MUST also include the implemented create/list/get/deactivate book, availability, loans-by-book if still exposed, create user, user loans, create/return/cancel loan, audit list, and live/ready health.
- **FR-017**: Idempotency documentation MUST cover the loan-create key header, maximum length, strongly typed binding, canonical hash (SHA-256), PostgreSQL uniqueness, replay, payload conflict, rollback after unexpected failure, and concurrent replica behavior.
- **FR-018**: Concurrency documentation MUST explain the last-copy problem, why read-check-write is wrong, the atomic conditional inventory update conceptually, why exactly one concurrent last-copy borrower wins, why cache and in-process locks are not used for that invariant, transaction boundaries, and concurrency for loan create, return, cancel, and total-copy updates.
- **FR-019**: A dedicated section MUST explain why the API remains correct with 2–11 replicas from database and Outbox guarantees (stateless instances, PostgreSQL inventory and key uniqueness, audit and outbox in the same transaction, competing outbox workers, idempotent outbox consumers, cache never authorizing loans), not merely from Kubernetes replica count.
- **FR-020**: Audit documentation MUST explain business audit versus technical logs, audited actions, actor, entity, entity id, UTC time, correlation id, contextual payload, and same-transaction pairing with the mutation.
- **FR-021**: Cache documentation MUST always describe PostgreSQL as source of truth and Redis exclusively as a performance optimization. It MUST explain cache-aside, key convention, time-to-live, why loan create does not trust cached availability, post-commit invalidation, invalidation after book deactivation, and cache failure/fallback to PostgreSQL if current tests confirm that behavior.
- **FR-022**: Transactional Outbox MUST be documented as an implemented production-reliability decision, not as a future idea, covering the crash window of immediate invalidation alone, same-transaction persistence, background processing, competing-consumer locking as implemented, lease fields, at-least-once delivery, idempotent invalidation, retry/backoff, crash recovery, and multi-replica processing.
- **FR-023**: Error-handling documentation MUST describe HTTP request contracts, body validation, strongly typed loan-create key binding, expected-failure outcomes mapped to HTTP, a single unexpected-failure HTTP boundary, problem details, and status semantics (transport validation versus business rule versus key conflict).
- **FR-024**: Localization documentation MUST describe en-US and pt-BR only if production-hardening has implemented and tested them, including default culture, language selection via the standard language request header, what user-facing text is localized, and why logs, metrics, and stable error codes stay English. If those cultures are not implemented and tested, the section MUST be omitted rather than invented.
- **FR-025**: Observability documentation MUST cover structured logs, correlation ids, tracing source, exported traces and metrics, and a table of implemented custom metrics with what each measures.
- **FR-026**: Health documentation MUST distinguish process liveness from dependency readiness and MUST NOT claim liveness depends on PostgreSQL or Redis if it does not.
- **FR-027**: Testing documentation MUST distinguish Unit Tests from Integration Tests, present Integration Tests as first-class evidence for technical challenge requirements, explain why an in-memory stand-in is not used to prove PostgreSQL concurrency, document Testcontainers PostgreSQL and Redis use, the in-process test host, multi-host tests, and commands for all / unit / integration runs. Test counts MUST NOT be hard-coded unless automatically accurate and current; prefer commands and scenario descriptions.
- **FR-028**: An integration-test matrix (table preferred) MUST document important scenarios that actually exist, each with purpose, setup, action, and expected guarantee. Include Redis-resilience and Outbox integration tests that exist after production-hardening. Omit rows that have no corresponding test.
- **FR-029**: A dedicated concurrent last-copy subsection MUST describe two independent API hosts sharing one PostgreSQL database, the two-WebApplicationFactory-host strategy if still implemented, one remaining copy, two users, one success and one rejection, one active loan, zero remaining copies, no negative inventory, and why two hosts beat in-process dual tasks.
- **FR-030**: The guide MUST document sequential and concurrent idempotency tests separately and what database state is asserted after each.
- **FR-031**: Outbox test documentation MUST cover transactional persistence, successful processing, failure/retry, lease expiration, and multiple-worker safety as actually tested after production-hardening.
- **FR-032**: Container documentation MUST describe the application image definition, compose services (API, PostgreSQL, Redis, local identity), startup, reset, and health validation commands that match the repository.
- **FR-033**: Kubernetes documentation MUST describe Deployment, Service, ConfigMap, Secret references, resource requests/limits, probes, and external PostgreSQL/Redis/identity assumptions, without claiming a cluster was deployed if only manifests exist.
- **FR-034**: Security documentation MUST cover resource-server token validation (issuer, audience, signature, lifetime), Librarian policy, PKCE, no API token issuance, no real secrets committed, and parameterized SQL that distinguishes safe interpolated parameterization from unsafe raw SQL concatenation.
- **FR-035**: Dependency-security documentation MUST reflect the final production-hardening implementation: direct and transitive package auditing, build treatment of high and critical findings, and how to run the audit command.
- **FR-036**: A short AI-assisted development note MUST disclose that AI tooling and Spec Kit assisted implementation because the challenge permits AI usage, that architecture, requirements, tests, and implementation were validated against the challenge and automated tests, and that the engineer remains responsible for understanding and defending every implementation decision, without overemphasizing tooling.
- **FR-037**: Known limitations MUST be based on real current limitations. The section MUST NOT manufacture limitations merely to fill space.
- **FR-038**: Production-evolution notes MAY list realistic future improvements (managed data stores and identity, external telemetry backend, autoscaling, rate limiting, resilience policies, stronger secret management, outbox/audit retention) without implying they are required for challenge correctness, and MUST NOT list architectural buzzwords as empty future work.
- **FR-039**: The guide MUST end with a concise design-decisions table mapping important engineering problems to implemented decisions and trade-offs.
- **FR-040**: The guide MUST prefer tables for endpoint, environment-variable, metric, and test matrices; prefer concise diagrams for architecture, loan concurrency, and Outbox; avoid excessive repetition; remain detailed but practical for a reviewer attempting to run the project; and enable an evaluator to clone, run, authenticate, call the API, and understand decisions without reading the entire source tree.

### Key Entities

- **Root delivery guide**: The single English root document that an evaluator uses to run, call, test, and understand the service.
- **Public production endpoint**: An HTTP operation exposed for real use (catalog, members, loans, audit, health), not a test-only probe.
- **Documented setting**: A named configuration value with purpose, environment, required/optional, and secret-sensitivity.
- **Integration scenario row**: A named automated proof with purpose, setup, action, and guarantee, corresponding to a real test.
- **Design decision**: A challenge problem mapped to the chosen approach and its trade-off.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of public production HTTP operations documented in the root guide exist on current controllers; 0% of documented public operations are invented, assumed from the original challenge (for example PATCH), or are test-only `/security` probes presented as production API.
- **SC-002**: 100% of documented environment-variable names exist in application configuration or Docker/Kubernetes files; 0% are invented; 0% of secret values are invented; 0% of real production secrets appear.
- **SC-003**: An evaluator who follows only the root guide can start the local stack, open Swagger, complete Authorization Code with PKCE as documented, and identify health-check addresses without reading other documents.
- **SC-004**: 100% of mandated guide topics are reachable from a clickable table of contents.
- **SC-005**: 0% of the guide is written in a language other than English.
- **SC-006**: 0% of documented local login or smoke paths use Resource Owner Password Credentials or Direct Access Grants.
- **SC-007**: After reading the last-copy and replica sections, a reviewer can correctly state that exactly one of two concurrent last-copy borrowers succeeds, that cache/in-process locks do not own that invariant, and that 2–11 replica correctness comes from database and Outbox guarantees.
- **SC-008**: 100% of integration-matrix rows correspond to an automated test that exists; Redis-resilience and Outbox tests that exist after production-hardening are included; 0% of test counts are hard-coded unless automatically current.
- **SC-009**: 100% of custom metrics named in the guide are metrics the service actually records.
- **SC-010**: Kubernetes documentation does not claim a live cluster was deployed when the repository only contains manifests.
- **SC-011**: A reviewer can complete a first-pass evaluation (run, authenticate, call a mutation and a query, run tests, explain last-copy and loan-key uniqueness) using the root guide without reading the entire source tree.
- **SC-012**: If controllers and OpenAPI disagree, 100% of such discrepancies are reported before those endpoints are documented (0% guessed).
- **SC-013**: Known-limitations contains 0 manufactured items; en-US/pt-BR appear only if implemented and tested.

## Assumptions

- The only deliverable created or substantially modified is the repository root `README.md`, unless a real documentation/code inconsistency is discovered and then corrected.
- Current code, OpenAPI, tests, and configuration take precedence over the original challenge’s suggested API (including not assuming PATCH for book update).
- If the repository and OpenAPI disagree, the discrepancy is reported before documentation continues for that item.
- The current stack to name accurately includes: .NET 10, ASP.NET Core Web API, Entity Framework Core, Npgsql, PostgreSQL, Redis, JWT Bearer, OpenID Connect, Keycloak for local development, OpenTelemetry, xUnit, WebApplicationFactory, Testcontainers, Docker, and Kubernetes manifests.
- Production layer names remain `LibraryManager.Domain`, `LibraryManager.Application`, `LibraryManager.Infrastructure`, and `LibraryManager.Api`. Test projects remain `LibraryManager.UnitTests` and `LibraryManager.IntegrationTests`.
- Book update is HTTP PUT in the current implementation. Book deactivate is HTTP DELETE (logical). `GET /books/{id}/loans` and `GET /books/{id}/history` are both implemented aliases and both MUST be documented while they remain intentional production routes.
- Local compose currently applies schema on API startup when configured to do so; the guide MUST reflect whatever the repository actually does.
- Local development identity values already committed for Compose may be documented as local-only and not for production. No secret may be invented.
- Direct Access Grants and password-grant are forbidden in the guide even as a “smoke” login example. An existing operator-only negative check in a feature quickstart MUST NOT be copied into the root guide as a login path.
- Integration-matrix rows are omitted when no corresponding test exists. Redis-resilience and Outbox tests that exist after production-hardening are included. Test counts are not hard-coded unless automatically current.
- en-US and pt-BR are documented because production-hardening implemented and tested them; if that ever ceased to be true, the section would be omitted.
- PostgreSQL is always source of truth; Redis is exclusively a performance optimization. Cache failure/fallback is documented because current tests confirm it.
- Transactional Outbox is implemented, not a future idea.
- Clean Architecture is documented because it is implemented; the guide emphasizes problems it solves, not scoring.
- Diagrams may use the repository’s usual markdown diagram format (architecture, loan concurrency, Outbox). Tables are preferred for endpoints, environment variables, metrics, and test matrices.
- No new product capability is in scope except accuracy fixes discovered while documenting.

## Out of Scope

- New lending rules, endpoints, identity flows, or operational features
- Rewriting specs, OpenAPI, or tests except to correct a proven mismatch after it is reported
- Guessing a contract when controllers and OpenAPI disagree
- Deploying or claiming a live Kubernetes cluster
- Documenting Resource Owner Password Credentials or Direct Access Grants as authentication
- Translating the root guide out of English
- Hard-coding fragile test counts
- Manufacturing known limitations
- Embedding a separate checklist inside the README
