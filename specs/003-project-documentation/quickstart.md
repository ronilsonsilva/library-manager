# Quickstart: Project Documentation

Validation guide for the rewritten root `README.md`. Do not treat this file as the evaluator’s delivery guide—that is `README.md`.

## Prerequisites

- Docker and Docker Compose
- .NET 10 SDK
- Repository checkout at the solution root

## What “done” looks like

An evaluator who uses **only** `README.md` can clone, start Compose, open Swagger, sign in with Authorization Code + PKCE, hit health URLs, and explain last-copy, idempotency, audit, cache, and Outbox—without reading `specs/` or source.

## After README rewrite

### 1. Follow Quick Start from a clean mental model

Commands in README must match:

```bash
git clone https://github.com/ronilsonsilva/library-manager.git
cd library-manager
docker compose up --build
```

```bash
docker compose down -v
```

Addresses: API `http://localhost:8080`, Swagger `http://localhost:8080/swagger`, Keycloak `http://localhost:8081`, `GET /health/live` and `GET /health/ready`.

Optional live stack is not required for `dotnet test`.

### 2. Catalog diffs (no guessing)

Compare README to:

- [contracts/endpoint-catalog.md](./contracts/endpoint-catalog.md) and controllers
- [contracts/configuration-catalog.md](./contracts/configuration-catalog.md) and Compose/K8s/appsettings
- [contracts/test-matrix.md](./contracts/test-matrix.md) and test methods
- [contracts/readme-outline.md](./contracts/readme-outline.md) vs TOC

Book update must be **PUT**. History alias documented if still present. No `/security` or `/__test` in the public API table. No PATCH. No Password Grant login path.

### 3. Application unchanged

```bash
dotnet build
dotnet test
dotnet test tests/LibraryManager.UnitTests
dotnet test tests/LibraryManager.IntegrationTests
dotnet package list --vulnerable --include-transitive
```

Expect success without code changes from this feature. If documentation work accidentally edited `src/` or `tests/`, revert unless a genuine mismatch was approved.

### 4. Markdown sanity

- TOC links resolve to headings
- Fenced commands copy-paste
- English only
- No committed production secrets
- No hard-coded test counts
- Kubernetes text states manifests only (no implied live cluster)

### 5. Diagrams and tables

Architecture, last-copy, and Outbox diagrams present. Endpoint, env-var, metric, and test matrices are tables.
