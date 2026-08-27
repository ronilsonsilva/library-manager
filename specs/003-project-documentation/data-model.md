# Data Model: Project Documentation

This feature does **not** change PostgreSQL tables, Redis keys, or Domain entities. Those remain as in `specs/001-library-manager/data-model.md` and production-hardening Result types.

The documentation “model” is the root `README.md`: structured sections whose rows must match the running system.

## Document

| Field | Rules |
|-------|--------|
| Path | Repository-root `README.md` only |
| Language | English |
| Navigation | Clickable table of contents covering every H2 |
| Empty sections | Forbidden |
| Secrets | No production secrets; Compose local-only credentials labeled as such |

## Section

| Field | Rules |
|-------|--------|
| Heading | From [contracts/readme-outline.md](./contracts/readme-outline.md) |
| Level | H2 unless listed as H3 under Testing Strategy |
| Content | Derived from inspection; no invented guarantees |

## EndpointRow (API summary + details)

| Field | Rules |
|-------|--------|
| Method | From `[Http*]` / `MapHealthChecks` |
| Route | Including controller `[Route]` prefix |
| Authorization | Anonymous / Authenticated / Librarian (`librarian` role) |
| Description | Implemented purpose |
| Headers | `Idempotency-Key` on `POST /loans`; `X-Correlation-ID` optional inbound |
| Success status | Actual (`201` create/replay, `200` reads/updates, `204` deactivate) |
| Error statuses | Only those the API actually returns for that operation |
| Public | `false` for `/security/*` and `/__test/unexpected-error` — omit from table |

Relationships: many EndpointRows belong to API Reference. Create Loan EndpointRow must include the idempotency behavior table.

## ConfigRow

| Field | Rules |
|-------|--------|
| Variable | Exact `__` or `:` name from code/Compose/K8s |
| Purpose | What the process uses it for |
| Required | Yes/No as bound (e.g. Authority+Audience required unless TestAuth) |
| Secret | Yes/No |
| Local example/source | Only if present in Compose or Development appsettings |

See [contracts/configuration-catalog.md](./contracts/configuration-catalog.md). Do not invent rows.

## TestScenarioRow

| Field | Rules |
|-------|--------|
| Area | e.g. Loans, Idempotency, Cache, Outbox, Auth, Health, Telemetry |
| Scenario | Name matching an existing test method’s intent |
| Infrastructure | Testcontainers PostgreSQL and/or Redis; one or two WAF hosts; AuthWAF |
| Guarantee | What the test asserts |

Omit rows with no corresponding test. See [contracts/test-matrix.md](./contracts/test-matrix.md).

## DecisionRow (closing table)

| Field | Rules |
|-------|--------|
| Problem | Engineering problem the challenge evaluates |
| Decision | What the repository actually does |
| Trade-off | Cost of that choice |

## Documented business entities (unchanged)

README Domain section **describes** these; it does not redefine schema:

- **Book**: title, isbn, author, total/available copies, active flag; last-copy via atomic `UPDATE ... AND available_copies > 0`
- **User** (reader): library member, distinct from JWT `sub`
- **Loan**: Active / Returned / Cancelled; one active loan per user+book as implemented
- **AuditEvent**: action, entity, entity id, actor (`sub`), UTC, correlation id, contextual JSON; same transaction as mutation
- **IdempotencyEntry** / **OutboxMessage**: Infrastructure persistence, not Domain entities

### Audited actions (from `AuditMetadata`)

`BookCreated`, `BookUpdated`, `BookDeactivated`, `UserCreated`, `LoanCreated`, `LoanReturned`, `LoanCancelled`

### Idempotency states

| Scenario | HTTP | Database |
|----------|------|----------|
| Same key + same canonical hash | 201 replay | No second loan |
| Same key + different hash | 409 | Second lend not applied |
| Concurrent same key (two hosts) | One create, others replay/conflict as implemented | One loan |
| Unexpected failure after reserve | Retry can succeed | Key ownership rolled back (`CreateLoanIdempotencyRollbackTests` / integration unexpected-failure test) |

### Cache states (availability only)

Hit → return Redis payload (may be stale). Miss → PostgreSQL then SET TTL 60s. Redis unavailable → miss/fallback to PostgreSQL (`ResilientAvailabilityCacheDecorator`). Mutation/deactivation → post-commit `RemoveAsync` + Outbox `BookAvailabilityChanged`. Loan create never reads Redis to authorize.

## State transitions (documentation workflow)

```text
inspect sources → draft README → validate catalogs → build/test unchanged
```

If a genuine code/OpenAPI mismatch appears: stop documenting that endpoint, report it, resolve in code or contract, then document.
