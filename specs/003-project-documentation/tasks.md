---
description: "Task list for project-documentation implementation"
---

# Tasks: Project Documentation

**Input**: Design documents from `/specs/003-project-documentation/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: Not requested as new automated tests. Spec is documentation-only. Validate by catalog diffs, `specs/003-project-documentation/quickstart.md`, and existing `dotnet build` / `dotnet test`. Do **not** add test projects or hard-coded test counts.

**Organization**: Tasks are grouped by user story. The only substantial deliverable is `README.md`. Do not change `src/` or `tests/` unless a genuine controller/OpenAPI mismatch is found (then **stop, report, do not guess**).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete work)
- **[Story]**: `US1`–`US6` on user-story phases only
- Every task includes an exact file path

## Path Conventions

- Deliverable: `README.md`
- Catalogs: `specs/003-project-documentation/contracts/`
- Production: `src/LibraryManager.Api/`, `src/LibraryManager.Application/`, `src/LibraryManager.Infrastructure/`
- Tests: `tests/LibraryManager.UnitTests/`, `tests/LibraryManager.IntegrationTests/`
- Local stack: `docker-compose.yml`, `Dockerfile`, `infrastructure/keycloak/`
- Cluster baseline: `deploy/kubernetes/`

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Confirm this feature rewrites only the root guide

- [X] T001 Confirm documentation-only scope (no new projects/packages) using `specs/003-project-documentation/plan.md` and `.specify/memory/constitution.md`
- [X] T002 [P] Confirm the clone URL still matches `git remote` for the Quick Start command that will be written in `README.md`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Re-verify planning catalogs against source so README authoring does not invent contracts

**CRITICAL**: No user story work can begin until this phase is complete. If a catalog disagrees with code or OpenAPI, report and stop; do not guess.

- [X] T003 Compare `specs/003-project-documentation/contracts/endpoint-catalog.md` to `src/LibraryManager.Api/Controllers/BooksController.cs`, `UsersController.cs`, `LoansController.cs`, `AuditEventsController.cs`, and `src/LibraryManager.Api/Health/HealthEndpoints.cs` (PUT not PATCH; history alias; exclude `/security/*` and `/__test/unexpected-error`)
- [X] T004 [P] Compare `specs/003-project-documentation/contracts/configuration-catalog.md` to `src/LibraryManager.Api/appsettings.json`, `src/LibraryManager.Api/appsettings.Development.json`, `docker-compose.yml`, and `deploy/kubernetes/`
- [X] T005 [P] Compare `specs/003-project-documentation/contracts/test-matrix.md` to test methods under `tests/LibraryManager.IntegrationTests/` (omit rows with no test; concurrent same-key is both 201; Outbox dual-worker is two worker ids, not two hosts)

**Checkpoint**: Catalogs match the repository — README section authoring can begin

---

## Phase 3: User Story 1 - Use the README as the challenge delivery and local run guide (Priority: P1) 🎯 MVP

**Goal**: An evaluator who never opens the source tree understands what the service is and can start, stop, and reset the local stack from `README.md` alone.

**Independent Test**: Follow only `README.md` from a clean checkout. Prerequisites, clone/build/compose/up/down/`down -v`, and local addresses match `docker-compose.yml`. No invented services or ports.

### Implementation for User Story 1

- [X] T006 [US1] Write H1 `library-manager`, Overview, Challenge Goals, and Key Engineering Highlights in `README.md` (books, users, loans, concurrency, idempotency, audit, Redis, observability, containers; do not open with a long architecture essay)
- [X] T007 [US1] Write Technology Stack in `README.md` using names from `LibraryManager.sln`, `Directory.Build.props`, `Dockerfile`, and `docker-compose.yml`
- [X] T008 [US1] Write Prerequisites and Quick Start in `README.md`: `git clone https://github.com/ronilsonsilva/library-manager.git`, `cd library-manager`, `docker compose up --build`, `docker compose down`, `docker compose down -v`, API `http://localhost:8080`, Swagger `http://localhost:8080/swagger`, Keycloak `http://localhost:8081`, `GET /health/live` and `GET /health/ready`, plus `dotnet build` / `dotnet test` alternatives
- [X] T009 [US1] Add a clickable Table of Contents in `README.md` covering every H2 present after T006–T008 (expand the TOC as later stories add headings)

**Checkpoint**: US1 is independently usable as a local run guide

---

## Phase 4: User Story 2 - Authenticate and call the public API from the README (Priority: P1)

**Goal**: Document Keycloak as local OIDC, Swagger Authorization Code + PKCE, Librarian mutations, and every production endpoint from controllers.

**Independent Test**: Diff the public API table in `README.md` against `specs/003-project-documentation/contracts/endpoint-catalog.md` and controllers. No PATCH, no ROPC/DAG login, no `/security` or `/__test` rows.

### Implementation for User Story 2

- [X] T010 [US2] Write Local Authentication with Keycloak in `README.md` from `docker-compose.yml`, `infrastructure/keycloak/library-manager-realm.json`, and `src/LibraryManager.Api/OpenApi/SwaggerConfiguration.cs` (resource server never issues tokens; PKCE S256; librarian role; Compose-only credentials labeled local-only; do not require hand-built JWTs; do not document Password Grant or Direct Access Grants as login)
- [X] T011 [US2] Write the API Reference **summary** table (Method, Route, Authorization, Description) in `README.md` from `specs/003-project-documentation/contracts/endpoint-catalog.md`
- [X] T012 [US2] Write detailed subsections in `README.md` for Create Book, Update Book (`PUT`), Create User, Create Loan, Return Loan, Cancel Loan, Get Availability, and Audit Events; `POST /loans` must include `Idempotency-Key`, body `{ bookId, userId }`, and statuses 201/400/401/403/404/409/422 with implemented semantics from `src/LibraryManager.Api/Controllers/LoansController.cs` and `src/LibraryManager.Api/Results/ResultHttpMapper.cs`

**Checkpoint**: US2 is independently usable as an API usage guide

---

## Phase 5: User Story 3 - Understand architecture, consistency, and engineering decisions (Priority: P1)

**Goal**: Document Clean Architecture (problems solved, not scoring), last-copy, idempotency, audit, Redis, Outbox, 2–11 replica correctness, and a decisions table.

**Independent Test**: Read those `README.md` sections against `.specify/memory/constitution.md` and source. No app/Redis locks for inventory; no claim that tests start 11 hosts.

### Implementation for User Story 3

- [X] T013 [US3] Write Architecture (Mermaid or ASCII: Api → Application → Domain; Infrastructure implements Application abstractions; composition root `src/LibraryManager.Api/Program.cs`) and Repository Structure in `README.md`; state no CQRS, MediatR, or Generic Repository
- [X] T014 [US3] Write Domain and Business Rules in `README.md` from `src/LibraryManager.Domain/` (Book, User, Loan, AuditEvent; JWT `sub` is not the library User)
- [X] T015 [US3] Write Concurrency and Consistency in `README.md` with the replica diagram and atomic SQL from `src/LibraryManager.Infrastructure/Persistence/Repositories/BookRepository.cs`; explain why read-modify-write, application locks, and Redis locks are not used
- [X] T016 [US3] Write Idempotency in `README.md` from `src/LibraryManager.Api/Contracts/Common/IdempotencyKey.cs`, `src/LibraryManager.Application/Loans/CreateLoan/LoanRequestCanonicalizer.cs`, and `src/LibraryManager.Infrastructure/Idempotency/IdempotencyStore.cs` (header name, `POST /loans` uniqueness, SHA-256, behavior table including in-progress 500 limitation)
- [X] T017 [US3] Write Domain Audit Trail in `README.md` from `src/LibraryManager.Application/Common/AuditMetadata.cs` (actions, actor, UTC, `X-Correlation-ID`, same transaction)
- [X] T018 [US3] Write Redis Caching Strategy in `README.md` from `src/LibraryManager.Infrastructure/Caching/RedisAvailabilityCache.cs` and `ResilientAvailabilityCacheDecorator.cs` (key, TTL 60s, hit/miss/unavailable, invalidation, deactivation, loans never read Redis)
- [X] T019 [US3] Write Transactional Outbox in `README.md` from `src/LibraryManager.Infrastructure/Outbox/OutboxClaimer.cs` and `OutboxProcessor.cs` (`FOR UPDATE SKIP LOCKED`, lease, Redis I/O after claim commit, at-least-once, idempotent `DEL`)
- [X] T020 [US3] Write Why the System Remains Correct with 2–11 Replicas in `README.md` (stateless API, PostgreSQL inventory/idempotency, same-transaction audit/outbox, competing workers; two-host tests generalize; do not claim 11-host tests)
- [ ] T021 [US3] Write Engineering Decisions Summary (problem → decision → trade-off table) in `README.md`

**Checkpoint**: US3 is independently usable as an architecture and decision document

---

## Phase 6: User Story 4 - Verify the system using the documented test strategy (Priority: P1)

**Goal**: Substantial testing documentation: unit vs integration, Testcontainers, WAF, two-host last-copy, matrices from real tests only.

**Independent Test**: Commands in `README.md` match `tests/LibraryManager.UnitTests` and `tests/LibraryManager.IntegrationTests`. Matrix rows exist in `specs/003-project-documentation/contracts/test-matrix.md`. No hard-coded suite sizes.

### Implementation for User Story 4

- [X] T022 [US4] Write Testing Strategy in `README.md` (unit vs integration; why not EF InMemory for concurrency; Testcontainers PostgreSQL/Redis; `CustomWebApplicationFactory`; commands `dotnet test`, `dotnet test tests/LibraryManager.UnitTests`, `dotnet test tests/LibraryManager.IntegrationTests`)
- [X] T023 [US4] Write Integration Test Coverage table in `README.md` from `specs/003-project-documentation/contracts/test-matrix.md` (include Auth and Health rows; note probe routes are test-only)
- [X] T024 [US4] Write Concurrent Last-Copy Test in `README.md` from `tests/LibraryManager.IntegrationTests/Loans/CreateLoanTests.cs` (two WAF hosts, one copy, two users, two keys, one 201 / one 422, one loan, zero copies)
- [X] T025 [US4] Write Idempotency Tests in `README.md` from `tests/LibraryManager.IntegrationTests/Loans/IdempotencyTests.cs` (sequential replay, concurrent same key both 201 same loan id, different payload 409, rollback)
- [X] T026 [US4] Write Cache and Outbox Tests in `README.md` from `tests/LibraryManager.IntegrationTests/Caching/` and `tests/LibraryManager.IntegrationTests/Outbox/OutboxProcessorTests.cs` (including processor disabled in WAF; SKIP LOCKED two worker ids)

**Checkpoint**: US4 is independently usable as a verification guide

---

## Phase 7: User Story 5 - Operate, configure, and deploy from the README (Priority: P2)

**Goal**: Configuration table, migrations, Docker, Kubernetes manifests (not a live cluster), health, observability, localization, errors, security, NuGet audit.

**Independent Test**: Every env name in `README.md` exists in `specs/003-project-documentation/contracts/configuration-catalog.md`. Kubernetes text does not claim a cluster was applied. No invented secrets.

### Implementation for User Story 5

- [X] T027 [US5] Write Configuration (Variable, Purpose, Required, Secret, Local Example/Source) in `README.md` from `specs/003-project-documentation/contracts/configuration-catalog.md` (no `Jwt:` / `OTEL_*` names)
- [X] T028 [US5] Write Database and EF Core Migrations in `README.md` from `src/LibraryManager.Api/Persistence/DatabaseStartup.cs` (Development or `Database__ApplyMigrations`; Compose `true`; K8s `false`; `dotnet ef database update --project src/LibraryManager.Infrastructure --startup-project src/LibraryManager.Api`)
- [X] T029 [US5] Write Error Handling and Validation plus Localization in `README.md` from `src/LibraryManager.Api/Results/ResultHttpMapper.cs` and `src/LibraryManager.Api/Localization/` (400 vs 422 vs 409; en-US/pt-BR; `Accept-Language`; codes/logs English; 401/403 titles hardcoded English)
- [X] T030 [US5] Write Observability and Health Checks in `README.md` from `src/LibraryManager.Api/Telemetry/LibraryManagerMetrics.cs` and `src/LibraryManager.Api/Health/HealthEndpoints.cs` (list recorded meters; state which tests assert; live vs ready; health writer is not Problem Details; ready does not check Keycloak)
- [X] T031 [US5] Write Docker in `README.md` from `Dockerfile` and `docker-compose.yml` (services, ports, Keycloak `start-dev --import-realm`, named volume `postgres_data`)
- [X] T032 [US5] Write Kubernetes in `README.md` from `deploy/kubernetes/deployment.yaml`, `service.yaml`, `configmap.yaml`, and `secret.yaml` (Deployment, Service, ConfigMap, Secret refs, requests/limits, probes; external PG/Redis/OIDC; **manifests only, no live cluster claim**; `REPLACE_WITH_*` placeholders)
- [X] T033 [US5] Write Security Considerations and Dependency Security in `README.md` from `src/LibraryManager.Api/Security/AuthenticationConfiguration.cs`, `tests/LibraryManager.IntegrationTests/Architecture/SqlParameterizationTests.cs`, and `Directory.Build.props` (JWT iss/aud/sig/lifetime, PKCE, parameterized SQL vs unsafe concat, `NuGetAuditMode=all`, `dotnet package list --vulnerable --include-transitive`)

**Checkpoint**: US5 is independently usable as an operations overview

---

## Phase 8: User Story 6 - Trust the README as an accurate snapshot (Priority: P1)

**Goal**: Complete remaining honesty sections and make the whole guide an accurate snapshot. Depends on US1–US5 content existing in `README.md`.

**Independent Test**: Spot-check routes, env names, cache key/TTL, PKCE, replica claims against source. Search `README.md` for password grant, DAG-as-login, PATCH, `/security` as public API, and hard-coded test counts.

### Implementation for User Story 6

- [ ] T034 [US6] Write AI-Assisted / Spec-Driven Development, Known Limitations, and Production Evolution in `README.md` from `specs/003-project-documentation/research.md` (real limitations only; short non-promotional AI note; evolution not implied defects)
- [ ] T035 [US6] Refresh the Table of Contents in `README.md` so every H2 in `specs/003-project-documentation/contracts/readme-outline.md` is clickable and no major section is duplicated
- [X] T036 [US6] Accuracy pass on `README.md` vs `specs/003-project-documentation/contracts/endpoint-catalog.md`, `configuration-catalog.md`, and `test-matrix.md`; English only; no invented endpoints/settings/statuses/secrets; if controllers and OpenAPI disagree, report in the working notes and do not document a blended contract

**Checkpoint**: US6 leaves a guide that can be trusted as a snapshot of the implemented system

---

## Phase 9: Polish & Cross-Cutting Concerns

**Purpose**: Feature quickstart validation and proof that documentation work did not alter the application

- [X] T037 Walk `specs/003-project-documentation/quickstart.md` against `README.md` (TOC, commands, diagrams, tables)
- [X] T038 Markdown sanity on `README.md` (heading anchors, fenced commands, no empty sections)
- [X] T039 Run `dotnet build` and `dotnet test` from the solution root (`LibraryManager.sln`) and confirm this feature did not change application behavior

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies
- **Foundational (Phase 2)**: Depends on Setup — **BLOCKS** all user stories
- **US1 (Phase 3)**: Depends on Foundational — MVP
- **US2 (Phase 4)**: Depends on US1 TOC/H1 existing in `README.md` (same file)
- **US3 (Phase 5)**: Depends on US1; can follow US2
- **US4 (Phase 6)**: Depends on US1 commands; independently readable after T022–T026
- **US5 (Phase 7)**: P2; depends on Foundational; same-file sequential after earlier stories recommended
- **US6 (Phase 8)**: P1 accuracy story; **depends on US1–US5** because it validates the complete guide
- **Polish (Phase 9)**: Depends on US6

All story implementation tasks edit `README.md`, so they are **sequential** for a single author. Do not mark them `[P]`.

### User Story Dependencies

- **User Story 1 (P1)**: After Phase 2 — MVP run guide
- **User Story 2 (P1)**: After US1 headings exist
- **User Story 3 (P1)**: After Phase 2; sequential after US1 in `README.md`
- **User Story 4 (P1)**: After Phase 2; sequential in `README.md`
- **User Story 5 (P2)**: After Phase 2; sequential in `README.md`
- **User Story 6 (P1)**: After US1–US5 content is present

### Parallel Opportunities

- T002 with T001
- T004 and T005 after T003 starts (or together after T003 if one person; together if two people inspect different trees)
- No parallel README section writes (single file)

### Parallel Example: Foundational

```bash
# After T003, inspect config and tests together:
Task: "Compare configuration-catalog.md to appsettings.json, docker-compose.yml, deploy/kubernetes/"
Task: "Compare test-matrix.md to tests/LibraryManager.IntegrationTests/"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Phase 1 Setup
2. Phase 2 Foundational (catalog re-verify)
3. Phase 3 User Story 1
4. **STOP**: evaluator can clone and `docker compose up --build` from `README.md`

### Incremental Delivery

1. Setup + Foundational
2. US1 → local run MVP
3. US2 → API + PKCE
4. US3 → architecture and invariants
5. US4 → tests as evidence
6. US5 → ops/deploy
7. US6 → honesty + TOC + snapshot check
8. Polish → quickstart + build/test

### Parallel Team Strategy

Not effective for section drafting (one `README.md`). Parallelize only Phase 2 inspections.

---

## Notes

- `[P]` only when files differ
- `[Story]` on US phases only
- Do not create empty README sections; write a heading only when filling it
- Merge test H3s under Testing Strategy per `plan.md`
- Commit only if the user asks
- Stop at any checkpoint to validate the story independently
