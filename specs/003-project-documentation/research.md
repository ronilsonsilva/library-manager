# Research: Project Documentation

Phase 0 resolves documentation source-of-truth, README shape, and inspection findings. No `NEEDS CLARIFICATION` remains in Technical Context.

## Source of truth for the README

- **Decision**: Document actual implemented behavior. Precedence: current code, runtime Swashbuckle (controllers), tests, appsettings/Compose/Kubernetes. Then constitution and feature specs. Original challenge text is last. If repository and OpenAPI disagree, report and do not guess.
- **Rationale**: Spec FR-002–FR-004, SC-001, SC-012. A false delivery guide is worse than a shorter true one.
- **Alternatives considered**: Follow the original challenge’s PATCH/suggested API (rejected). Blend controller and OpenAPI when they drift (forbidden).

## Inspection finding: no implementation change required

- **Decision**: Do not change business code, OpenAPI files, realm JSON, or tests in this feature unless implement-time re-inspection finds a new mismatch.
- **Rationale**: Controllers use `PUT /books/{id}` and 001 `openapi.yaml` also uses `put` (not PATCH). `GET /books/{id}/history` is a second `[HttpGet]` on the same action as `/loans`. Test-only routes are gated (`Testing:UseTestAuth`, `Testing` environment).
- **Alternatives considered**: “Fix” 002 `contracts/openapi.yaml` to be a full catalog (out of scope; it is an additive hardening overlay, not the public catalog).

## README is the only substantial deliverable

- **Decision**: Rewrite root `README.md` only. Spec-kit artifacts stay under `specs/003-project-documentation/`.
- **Rationale**: Spec FR-001.
- **Alternatives considered**: Split into wiki/docs site (rejected). Duplicate a second API catalog in `specs/003/.../contracts/openapi.yaml` as the public contract (rejected: evaluators read README + live Swagger).

## Section outline and allowed merge

- **Decision**: Use the plan.md heading table. Nest Integration Test Coverage, Concurrent Last-Copy, Idempotency Tests, and Cache/Outbox Tests as H3 under Testing Strategy. Fold auth and observability **test** narratives into the matrix / Observability section so there are no empty H2s.
- **Rationale**: User allowed merging closely related sections; FR-040 forbids duplicated major sections.
- **Alternatives considered**: 38 separate H2s (readable TOC but heavy duplication of the test matrix).

## Idempotency-Key header name (checklist CHK017)

- **Decision**: Document the header as `Idempotency-Key` (`IdempotencyKey.HeaderName`), max length 128, binder `[FromIdempotencyKey]`, uniqueness on endpoint `"POST /loans"` + key.
- **Rationale**: Code is explicit; FR-017 “loan-create key header” is the spec alias for this header.
- **Alternatives considered**: Generic “idempotency header” without the name (fails user/checklist clarity).

## Outbox claim mechanism (checklist CHK018)

- **Decision**: Name `FOR UPDATE SKIP LOCKED`, `locked_by` / `locked_until_utc`, claim transaction commits **before** Redis `RemoveAsync` (`OutboxProcessor.ProcessBatchAsync` claims in a scope, then consumes).
- **Rationale**: `OutboxClaimer` SQL and processor loop. User required SKIP LOCKED/claim behavior.
- **Alternatives considered**: “Database lock” without SKIP LOCKED (too vague).

## Authentication and health in the test matrix (checklist CHK036)

- **Decision**: One integration-test matrix with Area column values including Authentication and Health. Do not invent separate named matrix files. Include only scenarios that exist (`AuthorizationTests`, `AuditActorTests`, `HealthEndpointTests`, etc.).
- **Rationale**: FR-028 is a generic matrix of existing tests; user asked those areas to appear.
- **Alternatives considered**: Extra empty “Authentication Matrix” / “Health Matrix” H2s (duplication).

## Clone URL and Compose as primary path

- **Decision**: Document `git clone https://github.com/ronilsonsilva/library-manager.git` from current `origin`. Primary run path is `docker compose up --build`. Reset is `docker compose down -v`.
- **Rationale**: User required clone + Compose + clean-volume. Remote verified 2026-08-27; re-check at implement if origin changes.
- **Alternatives considered**: Host-only `dotnet run` as primary (weaker evaluator path; Keycloak/Postgres/Redis would be extra work).

## Environment variables

- **Decision**: Table only keys bound in code or set in Compose/Kubernetes. Nested JSON becomes `__` names. No invented names. Local examples only when already in development Compose/appsettings. Kubernetes Secret values stay `REPLACE_WITH_*` placeholders.
- **Rationale**: FR-012, SC-002. Compose already documents local postgres/keycloak passwords as **local-only**.
- **Alternatives considered**: Document every Keycloak `KC_*` in the API table (wrong: those are the identity container, not the API). Mention them in Docker/Keycloak sections instead.

## Test counts

- **Decision**: Commands and scenario names only. Never hard-code suite sizes.
- **Rationale**: FR-027, SC-008.
- **Alternatives considered**: Snapshot “78 unit / 117 integration” (stales immediately).

## Two-host last-copy vs 11 replicas

- **Decision**: Document two `CustomWebApplicationFactory` hosts sharing one Testcontainers PostgreSQL, one remaining copy, two users, two `Idempotency-Key` values, one 201 and one 422, one active loan, zero available copies. Explain that shared DB invariants generalize to 2–11 replicas. Do **not** claim tests start 11 hosts. Kubernetes sample `replicas: 2` is a manifest default, not proof of an applied cluster.
- **Rationale**: `CreateLoanTests.Concurrent_last_copy_through_two_hosts_has_one_winner`; FR-019, FR-029, SC-007, SC-010.
- **Alternatives considered**: Equate `kubectl scale --replicas=11` with a passing test (false).

## Observability: document metrics vs tested metrics

- **Decision**: The metrics table may list instruments the service **records** (`LibraryManagerMetrics`). The testing/observability narrative must say which ones **tests assert**: `library_manager_loans_created`, `library_manager_loan_duration`, `library_manager_loans_unavailable`, `library_manager_cache_invalidation_failures`, plus unit `availability_cache.get|set|remove`. Do **not** claim tests cover `library_manager_idempotency_replays`, Outbox meters, OTLP export, or span correlation.
- **Rationale**: FR-025 vs FR-028 / SC-008 / SC-009. Source inspection of `ObservabilityTests` and `RedisCacheActivityTests`.
- **Alternatives considered**: Only document tested meters (under-describes production). Claim all meters are tested (false).

## Swagger UI vs 001 OpenAPI security scheme

- **Decision**: Document local login as Swagger **Authorization Code + PKCE** (`SwaggerConfiguration`, `OAuthUsePkce`). Note that `specs/001-library-manager/contracts/openapi.yaml` uses HTTP `bearerAuth`. That is not a controller/OpenAPI method mismatch (PUT still matches). Do not tell readers to paste a hand-built JWT for the standard Compose flow.
- **Rationale**: User authentication rules; inspection of Swagger vs YAML.
- **Alternatives considered**: Document YAML bearer as the local UI flow (wrong for Swagger UI).

## Known limitations (real only)

- **Decision**: List only current, inspectable limits, for example:
  - Kubernetes manifests assume **external** PostgreSQL, Redis, and OIDC; they do not deploy Keycloak or data stores; sample `replicas: 2`
  - `Database__ApplyMigrations=false` on the Deployment; schema apply is out of band
  - Swagger UI is registered only when `ASPNETCORE_ENVIRONMENT=Development` (Compose sets this; Production host does not serve `/swagger` unless that changes)
  - Integration JWT tests use TestAuth (`Testing:UseTestAuth`), not a live Keycloak container; `dotnet test` does not start Keycloak; DAG-off is proven from realm JSON
  - `GET /health/ready` checks PostgreSQL and Redis only (not Keycloak); live runs no dependency checks; health bodies are the ASP.NET default writer (not Problem Details)
  - Availability GET may stay stale until TTL 60s or Outbox `DEL` if immediate `Remove` fails (asserted after deactivate + REMOVE fail); loans never trust Redis
  - Outbox delivery is at-least-once; integration hosts set `Outbox:ProcessorEnabled=false` and call `ProcessBatchAsync` directly; SKIP LOCKED two-worker test uses two worker ids on **one** processor, not two API hosts
  - Only `POST /loans` is idempotent; entries have no TTL; an in-progress key without a stored 201 body throws (`InvalidOperationException` → unexpected 500), not a wait/409 API
  - JWT 401/403 Problem Details titles are hardcoded English (not `Accept-Language`)
  - Compose Keycloak `--import-realm` skips import if the realm already exists (recreate container/volume to reimport)
  - No `GET /users`, `GET /users/{id}`, `GET /loans`, `GET /loans/{id}`, or unsuffixed `GET /health`
  - No patron-facing UI, payments, reservations, or per-copy barcodes (001 out of scope)
- **Rationale**: FR-037, SC-013. Follow-up source inspection (config, HTTP surface, tests).
- **Alternatives considered**: Generic “not cloud native / needs more monitoring” filler (forbidden).

## Production evolution

- **Decision**: Optional next steps tied to the current design: managed PostgreSQL/Redis/OIDC, OTLP collector in the cluster, autoscaling **after** invariants are understood, rate limiting, secret store instead of placeholder Secret YAML, Outbox/audit retention. Do not list CQRS/MediatR as future work.
- **Rationale**: FR-038.
- **Alternatives considered**: Buzzword backlog (rejected).

## AI disclosure

- **Decision**: Short note: Cursor AI and GitHub Spec Kit assisted implementation; requirements, decisions, code, and tests remain reviewable; the engineer is responsible for every decision.
- **Rationale**: FR-036.
- **Alternatives considered**: Promotional tool marketing (rejected).

## Current README gap

- **Decision**: Treat today’s `README.md` as a hardening-era stub: useful local/auth/cache notes exist, but it lacks TOC, clone command, `down -v`, full API table, domain rules, test matrix, last-copy/idempotency/outbox test subsections, Docker/K8s depth, limitations, evolution, decisions table, and AI note.
- **Rationale**: Compare current README (~231 lines) to FR-001–FR-040.
- **Alternatives considered**: Incremental patch only (would leave mandated sections missing).
