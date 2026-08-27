# Configuration catalog

Names taken from `appsettings.json`, `AuthenticationConfiguration`, `DatabaseStartup`, `OutboxProcessor`, Compose, and Kubernetes. Re-verify at implement. **Do not invent keys.**

## API process

| Variable | Purpose | Required | Secret | Local example / source |
|----------|---------|----------|--------|-------------------------|
| `ASPNETCORE_ENVIRONMENT` | Host environment; Swagger in Development | Typical | No | Compose: `Development`; K8s: `Production` |
| `ASPNETCORE_URLS` | Bind URL | Typical | No | `http://+:8080` |
| `ConnectionStrings__Postgres` | EF Core PostgreSQL | Yes (runtime) | Yes | Compose: `Host=postgres;...Password=postgres` (local only) |
| `ConnectionStrings__Redis` | Redis cache | Yes (runtime) | Context-dependent | Compose: `redis:6379` |
| `Authentication__Authority` | JWT issuer / OIDC authority | Yes unless TestAuth | No | Compose: `http://localhost:8081/realms/library-manager` |
| `Authentication__Audience` | JWT audience | Yes unless TestAuth | No | `library-manager-api` |
| `Authentication__MetadataAddress` | Alternate OIDC metadata URL (container DNS) | No | No | Compose: `http://keycloak:8080/realms/library-manager/.well-known/openid-configuration` |
| `Authentication__ValidIssuers__0` | Extra valid `iss` | No | No | Compose: `http://localhost:8081/realms/library-manager` |
| `Authentication__ValidIssuers__1` | Extra valid `iss` | No | No | Compose: `http://keycloak:8080/realms/library-manager` |
| `Testing__UseTestAuth` | Test JWT scheme; forbidden in Production | No (default false) | No | Compose/K8s: `false` |
| `Database__ApplyMigrations` | Apply EF migrations on startup | No (also true in Development via code) | No | Compose `true`; K8s `false` |
| `Outbox__ProcessorEnabled` | Background processor | No (enabled unless `"false"`) | No | `appsettings.json`: `true` |
| `Outbox__BatchSize` | Claim batch | No | No | `10` |
| `Outbox__LeaseSeconds` | Claim lease | No | No | `30` |
| `Outbox__PollIntervalMilliseconds` | Poll delay | No | No | `2000` |
| `Outbox__MaxBackoffSeconds` | Retry backoff cap | No | No | `60` |
| `OpenTelemetry__OtlpEndpoint` | OTLP export | No | No | `appsettings.json`: empty (disabled) |

`Authentication:Authority` and `Authentication:Audience` must be set when TestAuth is off (`AuthenticationConfiguration`).

`Database:ApplyMigrations` is also treated as true when the host environment is Development (`DatabaseStartup`).

If `ConnectionStrings:Postgres` or `ConnectionStrings:Redis` is omitted, code fallbacks exist (`localhost` postgres/redis in `DependencyInjection` / `HealthEndpoints` / `RedisAvailabilityCache`). README should prefer Compose/K8s values, not invent extra key names. There is **no** `Jwt:` or `OTEL_*` section; OTLP is only `OpenTelemetry:OtlpEndpoint`.

Optional logging keys in appsettings (`Logging:LogLevel:Default`, `Logging:LogLevel:Microsoft.AspNetCore`, `AllowedHosts`) need not dominate the operational table.

## Kubernetes-only notes

- ConfigMap `library-manager-api`: `Authentication__Authority`, `Authentication__Audience`
- Secret `library-manager-api`: `ConnectionStrings__Postgres`, `ConnectionStrings__Redis` with `REPLACE_WITH_*` placeholders — do not replace with real production passwords in git

## Not API settings (document in Docker/Keycloak, not as invented API env)

Compose Keycloak: `KC_BOOTSTRAP_ADMIN_USERNAME`, `KC_BOOTSTRAP_ADMIN_PASSWORD`, `KC_HTTP_ENABLED`, `KC_HOSTNAME`, `KC_HOSTNAME_STRICT`, `KC_HOSTNAME_BACKCHANNEL_DYNAMIC`, `KC_HEALTH_ENABLED`. Postgres: `POSTGRES_USER`, `POSTGRES_PASSWORD`, `POSTGRES_DB`.
