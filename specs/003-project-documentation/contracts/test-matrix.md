# Integration test matrix contract

Rows are **existing** tests. README matrix must not add scenarios that are not in the suite. Re-verify method names at implement. Do not publish test counts.

Infrastructure legend: **PG** = Testcontainers PostgreSQL; **Redis** = Testcontainers Redis; **WAF** = `CustomWebApplicationFactory`; **2×WAF** = two factories sharing PG; **AuthWAF** = `AuthWebApplicationFactory`.

| Area | Scenario | Infrastructure | Guarantee verified |
|------|----------|----------------|--------------------|
| Concurrency | Concurrent last copy through two hosts | 2×WAF, PG, Redis | One 201, one 422; one loan; zero available copies |
| Concurrency | Repeated last-copy races | 2×WAF, PG, Redis | Same invariant across repeats |
| Concurrency | Concurrent duplicate return | 2×WAF, PG | Inventory restored once |
| Concurrency | Concurrent return and cancel | 2×WAF, PG | One terminal status; restore once |
| Idempotency | Sequential same-key replay | WAF, PG | 201 stored body; one loan |
| Idempotency | Same key different payload | WAF, PG | 409; second lend not applied |
| Idempotency | Concurrent same key two hosts | 2×WAF, PG | Both 201; **same loan id**; copies decremented once |
| Idempotency | Unexpected failure rolls back key | WAF, PG | Retry can create loan |
| Idempotency | Missing key | WAF | 400; no loan |
| Cache | Miss then hit (stale Redis value) | WAF, PG, Redis | GET can serve cached payload |
| Cache | Stale Redis cannot approve/block loan | WAF, PG, Redis | Loan decision from PostgreSQL |
| Cache | Loan invalidates after commit | WAF, PG, Redis | Cache cleared |
| Cache | Loan succeeds if immediate invalidation fails | WAF, PG | HTTP 201 despite REMOVE failure |
| Cache | Redis unavailable GET | WAF, PG, bad Redis | Availability matches PostgreSQL |
| Cache | Redis SET failure | WAF, PG | GET still from PostgreSQL |
| Cache | Deactivation clears cache + Outbox | WAF, PG, Redis | Cached active value not left as GET result |
| Outbox | Persist unprocessed with loan | WAF, PG | Same database as business row |
| Outbox | Process after claim commit | WAF | Invalidates Redis; marks processed |
| Outbox | Failure retry/backoff then success | WAF | Retry then processed |
| Outbox | Expired lease claimed by another worker | WAF | Lease recovery |
| Outbox | Two workers SKIP LOCKED | WAF, two worker ids on **one** `OutboxProcessor` | Distinct messages (not two API hosts). Integration WAF sets `Outbox:ProcessorEnabled=false`; tests call `ProcessBatchAsync` |
| Outbox | Duplicate invalidation idempotent | WAF | Safe `DEL` |
| Authentication | Mutation without token | AuthWAF | 401 |
| Authentication | Invalid token | AuthWAF | 401 |
| Authentication | No librarian role | AuthWAF | 403 |
| Authentication | Librarian role succeeds | AuthWAF | 204 on probe (test-only route; **explain in tests, not public API table**) |
| Authentication | Audit actor = JWT `sub` | WAF | Actor id matches subject |
| Health | Live anonymous 200 | WAF | No token |
| Health | Ready 200 when PG+Redis up | WAF, PG, Redis | Ready healthy |
| Health | Live 200 when ready 503 | AuthWAF (no deps) | Liveness ≠ readiness |
| Telemetry | Loan created + duration metrics | WAF | `library_manager_loans_created`, `library_manager_loan_duration` |
| Telemetry | Unavailable metric | WAF | `library_manager_loans_unavailable` |
| Telemetry | Cache invalidation failure metric | WAF | `library_manager_cache_invalidation_failures`; loan still 201 |
| Audit | Query LoanCreated with actor/correlation/context | WAF | Librarian list |
| Audit | Rejected mutation writes no success audit | WAF | No false LoanCreated |
| Localization | Accept-Language | WAF | en-US / pt-BR |
| Architecture | Controller contracts location | source | No transport types in controllers |
| Architecture | SQL parameterization | source | No unsafe Raw concat |
| Security | Keycloak realm DAG false | source JSON | No Direct Access Grants |
| Security | TestAuth forbidden in Production | source/config | Guard |

Unit tests (strategy section, not the integration matrix): Domain entities, `Result`/`DomainGuard`, binder, exception handler, cache decorator, Redis activities, cancellation tokens, canonical hash, idempotency rollback.

Public API documentation must still describe 401/403/Librarian on **production** routes even though some auth tests hit `/security/*` probes.

Do **not** claim tests verify `library_manager_idempotency_replays`, Outbox meters, OTLP export, or 11-host scale. Last-copy repeats use two hosts (not 11). AuthWAF uses dummy PG/Redis (`Port=1`) so ready is 503 while live stays 200.
