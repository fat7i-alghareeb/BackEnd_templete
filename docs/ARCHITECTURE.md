# Architecture Master Blueprint

The system-level view of Taxi_Server. Every statement here describes code that exists in this
repository today.

| You want to… | Read |
|---|---|
| add a feature, right now | [AGENTS.md](../AGENTS.md): the copy-paste recipe |
| understand one layer deeply | `src/Taxi.<Layer>/<Layer>_Layer_Blueprint.md` |
| name a route or pick a status code | [RESTful_Naming_Constitution.md](RESTful_Naming_Constitution.md) |
| run it, configure it, deploy it | [README.md](../README.md) |
| find which doc answers a question | [NAVIGATION.md](NAVIGATION.md) |
| use Claude Code on this repo | [USING_CLAUDE.md](USING_CLAUDE.md) |

---

## 1. Philosophy

Clean Architecture with **dependencies pointing inward**, plus **CQRS over MediatR**, organised
internally as **vertical slices** (`Features/<Aggregate>/Commands|Queries/<UseCase>/`) rather than
horizontal technical folders.

Three decisions define the codebase more than anything else:

1. **Failure is a value, not an exception.** Business failures return `Result<T>` carrying an
   `Error`. Exceptions are for genuine system faults only.
2. **Every user-facing string is a key.** Error codes and validation messages are
   `LocalizationKeys` constants, resolved at the API boundary against
   `SharedResource.{en,ar}.json`. There is no English prose in the failure path.
3. **Bilingual data is a first-class modelling concern.** `LocalizedText` is stored as one JSONB
   column and resolved to a single string per request based on `Accept-Language`.

---

## 2. Layers and dependency direction

```text
Taxi.Contracts   ──────────────────────────►  (references nothing)
       ▲
       │
Taxi.Domain      ──────────────────────────►  Taxi.Contracts
       ▲
       │
Taxi.Application ──────────────────────────►  Taxi.Domain
       ▲
       │
Taxi.Infrastructure ───────────────────────►  Taxi.Application
       ▲
       │
Taxi.Api         ──────────────────────────►  Application, Contracts, Infrastructure
```

| Layer | Project | Owns |
|---|---|---|
| Vocabulary | `Taxi.Contracts` | Request DTOs, `LocalizationKeys`, `Languages`, sort whitelists |
| Core | `Taxi.Domain` | Entities, `<Entity>Errors`, `LocalizedText`, `Result<T>` / `Error` / `ErrorKind`, `Entity` / `AuditableEntity` / `ISoftDeletable` / `DomainEvent` |
| Use cases | `Taxi.Application` | Commands, queries, handlers, validators, DTOs, mappers, MediatR behaviours, the interfaces Infrastructure and the API implement |
| Implementations | `Taxi.Infrastructure` | `AppDbContext`, EF configurations, interceptors, migrations, seeding, ASP.NET Identity, JWT, outbox, cache and lock registration, file storage, email, health checks |
| Terminus | `Taxi.Api` | Controllers, middleware, OpenAPI transformers, `Error` → ProblemDetails translation, idempotency, rate limiting, composition root |

**`Taxi.Domain` references `Taxi.Contracts`.** This is a deliberate exception to the usual "Domain
depends on nothing" rule: entity error codes are `LocalizationKeys` constants, and that registry
must be visible to Domain and API alike (and to any future .NET client). **Do not "fix" it**; the
full reasoning and every rejected alternative are in
[Domain blueprint §2](../src/Taxi.Domain/Domain_Layer_Blueprint.md).

**Nothing references `Taxi.Api`** except the test projects. `Taxi.Api` references
`Taxi.Infrastructure` only to call `AddInfrastructure(configuration)` and the startup helpers;
controllers never touch Infrastructure types.

`tests/Taxi.ArchitectureTests` turns these arrows into failing tests at the **type** level: a
project reference can be added in a minute, but the build fails the moment a type uses it.

Three Application interfaces are implemented in the **API** project instead of Infrastructure
(`IUser`, `ILanguageContext`, `ITextLocalizer`) because they need `HttpContext` or the API's
resource files.

---

## 3. Tech stack

### In use

| Concern | Package / feature |
|---|---|
| Runtime | .NET 10 (`net10.0`, nullable + implicit usings, `Directory.Build.props`) |
| CQRS | MediatR 14: `ISender` in controllers, `IPipelineBehavior` for cross-cutting |
| Validation | FluentValidation 12 + `System.ComponentModel.DataAnnotations` in Contracts |
| Persistence | EF Core 10 + `Npgsql.EntityFrameworkCore.PostgreSQL` (JSONB via `OwnsOne(...).ToJson()`, retry-on-failure ×5, 60 s command timeout) |
| Identity | `Microsoft.AspNetCore.Identity.EntityFrameworkCore` (`AddIdentityCore<AppUser>` + roles) with `Microsoft.AspNetCore.Authentication.JwtBearer` |
| Caching | `Microsoft.Extensions.Caching.Hybrid` driven by `ICachedQuery` + `CachingBehavior`; optional Redis L2 (`Microsoft.Extensions.Caching.StackExchangeRedis`) |
| Email | MailKit (SMTP) behind `IEmailSender`; a logging sender by default |
| Localization | `My.Extensions.Localization.Json` over `Resources/SharedResource.{en,ar}.json` |
| Versioning | `Asp.Versioning.Mvc` + `.ApiExplorer`, URL-segment reader, default `1.0` |
| API docs | `Microsoft.AspNetCore.OpenApi` with four custom transformers, rendered by Swagger UI **and** Scalar (Development only) |
| Logging | Serilog (`Serilog.AspNetCore`, `Serilog.Sinks.Seq`), configured from `appsettings*.json` |
| Telemetry | OpenTelemetry: ASP.NET Core + HttpClient instrumentation, OTLP exporter, Prometheus exporter |
| Quality | StyleCop.Analyzers solution-wide; `Directory.Packages.props` for central package versions |
| Tests | xUnit, NSubstitute, EF Core InMemory, NetArchTest, `Microsoft.AspNetCore.Mvc.Testing`, Testcontainers.PostgreSql, coverlet |

### Observability

| Concern | How |
|---|---|
| Traces | OpenTelemetry ASP.NET Core + HttpClient instrumentation, exported over OTLP to Seq (`OTEL_EXPORTER_OTLP_*` environment variables, set by compose) |
| Metrics | Same instrumentation, exposed at `/metrics` and scraped by Prometheus every 5 s |
| Logs | Serilog → console and a daily rolling file; plus Seq in Development (`appsettings.Development.json`) |
| Dashboards | Grafana over Prometheus (the data source is not provisioned; add `http://prometheus:9090`) |
| Email | `IEmailSender`: logged by default, SMTP when `Email:Provider` is `Smtp`; Mailpit in the development compose |
| Health | `/health/live` (process up) and `/health/ready` (PostgreSQL + Redis when configured, 5 s timeout each) |

### Known slack

`Swashbuckle.AspNetCore` is referenced only for `UseSwaggerUI`; the OpenAPI document itself comes
from `Microsoft.AspNetCore.OpenApi`. A front end is a separate deployment that calls this API; no
client project is in the solution.

### Orchestration

`docker-compose.yml` defines `taxi.api`, `postgres` (16), `redis` (7), `seq`, `prometheus` and
`grafana`. `docker-compose.override.yml` (merged automatically) adds the development ports,
credentials and `mailpit`. `docker-compose.prod.yml` (opt-in) adds a one-shot `taxi.api-init`
volume fixer and reads secrets from `.env` (`.env.example` lists them). `containers/` holds the
Prometheus scrape config and Seq's data directory.

---

## 4. The `Result<T>` pattern

`src/Taxi.Domain/Common/Results/`

```csharp
Result<Car> carResult = Car.Create(...);   // implicit from Car, from Error, from List<Error>
if (carResult.IsError) { return carResult.Errors; }
var car = carResult.Value;
```

- `Result<TValue>`: `IsSuccess` / `IsError`, `Value`, `Errors`, `TopError`, and
  `Match(onValue, onError)`, which the controllers use to fork into an HTTP response.
- Marker structs for void-ish success: `Result.Success`, `Result.Created`, `Result.Updated`,
  `Result.Deleted`. A delete command is `IRequest<Result<Deleted>>`.
- `Error` is a `readonly record struct` with `Code` (always a `LocalizationKeys` constant),
  `Description` (English fallback), `Type` (`ErrorKind`), optional `Args` (localizer format
  arguments) and optional `PropertyName` (set by `Error.ValidationForProperty`, used to key the
  RFC 7807 `errors` dictionary).

`ErrorKind` → HTTP mapping lives in `ProblemExtensions.MapStatus`:

| `ErrorKind` | Status |
|---|---|
| `Validation` | 400 |
| `Unauthorized` | 401 |
| `Forbidden` | 403 |
| `NotFound` | 404 |
| `Conflict` | 409 |
| `Failure`, `Unexpected` | 500 |

---

## 5. The MediatR pipeline

Registered in `src/Taxi.Application/DependencyInjection.cs`. **Registration order is execution
order**: first registered is outermost.

```text
LoggingBehaviour              IRequestPreProcessor (MediatR's pre-processor behaviour runs first)
UnhandledExceptionBehaviour   logs and rethrows; outermost behaviour so nothing escapes it
  ValidationBehavior          FluentValidation; short-circuits to a failed Result
    PerformanceBehaviour      Stopwatch; warns above 500 ms
      CachingBehavior         ICachedQuery only; returns the cached value on a hit
        Handler
```

- `ValidationBehavior` is constrained `where TResponse : IResult`, so requests that do not return
  a `Result<T>` skip it. Its validator is optional; a use case with no validator passes through.
  Failures become `Error.ValidationForProperty(e.PropertyName, e.ErrorMessage)` and are returned
  through `Result<T>`'s implicit `List<Error>` conversion.
- `CachingBehavior` injects `ILanguageContext` and partitions culture-aware keys as
  `{CacheKey}_{language}`. It caches only successful results.
- `LoggingBehaviour<TRequest>` is an open-generic `IRequestPreProcessor`, not a pipeline behaviour.
  It needs an explicit `AddOpenRequestPreProcessor` registration: MediatR's assembly scan does not
  discover open generics, and without that call it silently never runs.
- Requests implementing `ISensitiveRequest` (passwords, tokens, streams) are logged as
  `[redacted]` by every behaviour that writes a payload.

Two ordering rules are load-bearing and are enforced by `PipelineRegistrationTests`:
`UnhandledExceptionBehaviour` is outermost, and `ValidationBehavior` precedes `CachingBehavior` so
invalid input never reaches the cache.

Details: [Application blueprint §8](../src/Taxi.Application/Application_Layer_Blueprint.md).

---

## 6. Validation, in three tiers

| Tier | Location | Runs | Produces |
|---|---|---|---|
| Contract | `DataAnnotations` on `Contracts/Requests/**` | Model binding, before MediatR | 400 via `InvalidModelStateResponseFactory` (localized) |
| Application | `AbstractValidator<T>` beside the command/query | `ValidationBehavior` | 400 `ValidationProblemDetails` keyed by property |
| Domain | Guards inside `Create` / behaviour methods | Handler calls the entity | `Result` failure with an `<Entity>Errors` value |

Tiers 2 and 3 intentionally overlap. The validator gives clean per-field HTTP errors; the domain
guard guarantees the invariant even for callers that never pass through MediatR (seeder,
background job, future code).

---

## 7. Localization

- `src/Taxi.Contracts/Common/LocalizationKeys.cs`: the single key registry, nested by area (`Car`,
  `Driver`, `RefreshToken`, `Auth`, `Account`, `Concurrency`, `Idempotency`, `Request`,
  `RateLimit`, `Validation`).
- `src/Taxi.Contracts/Common/Languages.cs`: `En`, `Ar`, `Default`, `All`. Never write `"en"` / `"ar"`.
- `Api/Resources/SharedResource.{en,ar}.json`: the translations. Every key must exist in **both**.
- `src/Taxi.Api/SharedResource.cs`: the marker class `IStringLocalizer<SharedResource>` resolves against.
  It lives at the root namespace on purpose, so `My.Extensions.Localization.Json` computes the
  `Resources/` path correctly.
- `UseRequestLocalization` runs first in the pipeline; `LanguageContext` (implementing
  `ILanguageContext`) exposes the resolved language to handlers without touching `HttpContext`.
  Unsupported or absent `Accept-Language` falls back to `Languages.Default`.
- Missing keys: `ProblemExtensions` and `InvalidModelStateResponseFactory` both fall back to the
  English `Description` (or the key itself) and log a warning **in Development only**.
- Email subjects and bodies are resource entries too, composed in Application through
  `ITextLocalizer`, so account emails follow the request's language.
- OpenAPI advertises the header: `AcceptLanguageOperationTransformer` adds an `Accept-Language`
  parameter with an enum of `Languages.All` to every operation.
- Not localized: the 401/403 bodies written by the status-code pages, and the `title` of an
  unhandled-exception 500.

---

## 8. Caching

Opt-in per query via `ICachedQuery<TResponse>` (`CacheKey`, `Tags`, `Expiration`,
`IsCultureAware` defaulting to `true`). `HybridCache` is registered in `AddInfrastructure` with a
10-minute default expiration and a 30-second L1 window; when `ConnectionStrings:Redis` is set,
Redis is the shared L2, so cached values and tag invalidations reach every instance (other nodes
can lag by up to the 30-second L1 window: there is no backplane).

Invalidation is tag-based and manual: every command that mutates cached data calls
`RemoveByTagAsync` with the same
[`CacheTags`](../src/Taxi.Application/Common/Caching/CacheTags.cs) constant the query declares.
**A cached query without a matching invalidator is a bug.**

Cached today: the v1 and v2 car reads (tag `Cars`, evicted by all five car commands: create,
update, remove, upload image, remove image) and the driver reads (tag `Drivers`, evicted by all
seven driver commands). `GetUserByIdQuery` is deliberately not cached: it carries roles and
claims, nothing evicts `CacheTags.UserInfo`, and stale authorization data is the wrong trade.

There is no response-level output caching; all caching happens in the MediatR pipeline, keyed per
query and partitioned by language (except the v2 car reads, whose payload holds both languages).

---

## 9. Persistence and Identity

`AppDbContext : IdentityDbContext<AppUser>, IAppDbContext`: Identity tables and business tables
share one context and one transaction. Three interceptors run on every `SaveChanges`, in this
order: `SoftDeleteInterceptor`, `AuditableEntityInterceptor`, `OutboxInterceptor`.

- **Domain events** go through a **transactional outbox**: `OutboxInterceptor` stores them as
  `OutboxMessages` rows in the same transaction as the change, and `OutboxBackgroundService`
  publishes them through MediatR afterwards (one instance at a time via `IDistributedLock`,
  exponential-backoff retries, parked after 5 attempts). Handlers are at-least-once. Reference
  events: `CarCreatedDomainEvent`, `DriverAssignedToCarDomainEvent`.
- **Optimistic concurrency:** `Car` and `Driver` use PostgreSQL's `xmin` as a concurrency token
  (shadow property, no extra column); a lost race surfaces as `DbUpdateConcurrencyException` →
  localized 409.
- **Soft delete:** `SoftDeleteInterceptor` turns a delete of any `ISoftDeletable` (today `Car`)
  into an update of `IsDeleted` / `DeletedAtUtc` / `DeletedBy`; a global query filter hides deleted
  rows. `Driver` is hard-deleted.
- **Audit stamping:** `AuditableEntityInterceptor` writes `CreatedAtUtc` / `CreatedBy` /
  `LastModifiedUtc` / `LastModifiedBy` from `IUser` and `TimeProvider`, including owned entities.
- **Seeding:** `ApplicationDbContextInitialiser` runs `MigrateAsync`, then seeds the roles, the
  `Seed:Admin` and `Seed:User` accounts (only when configured; Development uses `admin@taxi.com` and
  `user@taxi.com`) and, when `Seed:SampleData` is true, one sample car. `ResetDatabaseAsync` is
  destructive and gated to Development behind `Database:ResetOnStartup`.
- **Startup migration** is driven by `ApplyMigrationsWithRetryAsync` in
  `Taxi.Api/DependencyInjection.cs` (5 attempts, 3 s apart), gated by
  `Database:ApplyMigrationsOnStartup`: the code default is on outside Production, and
  `appsettings.Production.json` sets it to `true`, so set it `false` to migrate in a pipeline.
- **No repositories, no Unit of Work wrapper.** `IAppDbContext` exposes `DbSet<T>` directly;
  `DbSet` is the repository and `SaveChangesAsync` is the unit of work.
- **Files:** `IFileStorage` → `LocalFileStorage` under `FileStorage:LocalRootPath`, outside
  `wwwroot`, with server-generated keys; the car image is the reference use.
- **Auth** is JWT bearer, HMAC-SHA256, configured from the typed, start-validated `JwtSettings`
  options. `TokenProvider` issues the access token and rotates the refresh token (deletes the
  user's existing rows, inserts a new one living `RefreshTokenExpirationInDays`, default 7).
  Authorization is named policies (`Policies.*`, mapped to `Roles.*` in `AddAppAuthorization`)
  behind a fallback that requires an authenticated user; `IIdentityService` wraps `UserManager`
  so the Application layer never sees ASP.NET Identity types. Account self-service (register,
  confirm, reset, change password, logout) is `Features/Account`.

---

## 10. API surface

- Business routes are literal, lowercase, plural and versioned:
  `[Route("api/v{version:apiVersion}/cars")]`. The token and account controllers are
  `[ApiVersionNeutral]` at `api/token` and `api/account`. Endpoint-by-endpoint catalogue:
  [Api blueprint §6](../src/Taxi.Api/Api_Layer_Blueprint.md).
- Every action carries `[EndpointName]` and one `[ProducesResponseType]` per reachable status;
  versioned controllers add `[MapToApiVersion]` to every action.
- `ApiController` is a small base class supplying `Problem(List<Error>)` (which delegates to
  `ProblemExtensions.ToProblem`, the single place `Error` becomes HTTP), `RequestedApiVersion`, and
  the default rate-limit policy.
- **Versioning:** `CarsController` serves a deprecated v1 and a v2 with a different read shape;
  v1 responses carry `api-deprecated-versions` and `Sunset` headers.
- `GlobalExceptionHandler` (an `IExceptionHandler`) turns unhandled exceptions into
  `application/problem+json`. The exception type and message are included **only in Development**;
  outside it the response carries a generic message plus the `requestId` that correlates it with
  the logs. It also maps `BadHttpRequestException` to its own status (413) and
  `DbUpdateConcurrencyException` to 409.
- **Middleware order** (`UseCoreMiddlewares`): RequestLocalization → RequestLogContextMiddleware →
  SecurityHeadersMiddleware → ExceptionHandler → StatusCodePages → HttpsRedirection →
  SerilogRequestLogging → Cors → Authentication → RateLimiter → Authorization.
  `UseForwardedHeaders` runs earlier, directly in `Program.cs`.
- **Rate limiting:** `RateLimitPolicies.Default` (sliding window, 100 requests/minute per user or
  IP, inherited from `ApiController`) and `RateLimitPolicies.Auth` (fixed window, 10
  requests/minute per IP, on the credential and account actions). Limits come from the
  `RateLimiting` section; rejections are a localized 429 with `Retry-After`.
- **Idempotency:** `[Idempotent]` on a creating `POST` makes an `Idempotency-Key` retry replay the
  stored response (24 h) instead of creating a second row.
- **OpenAPI transformers:** `VersionInfoTransformer` (title/version), `BearerSecuritySchemeTransformer`
  (the `Bearer` scheme), `BearerSecurityOperationTransformer` (padlock only on endpoints that
  actually carry `[Authorize]` without `[AllowAnonymous]`), `AcceptLanguageOperationTransformer`.

---

## 11. Observability

- Serilog replaces the default logger, configured from `appsettings*.json` (console + daily rolling
  file under `logs/`, enriched with machine name and thread id; Seq added in Development).
- `RequestLogContextMiddleware` pushes `CorrelationId` (the ASP.NET `TraceIdentifier`) into the
  Serilog `LogContext`, so every log line emitted during a request carries it.
- `AddCustomProblemDetails` attaches `requestId` and an `instance` of `"{METHOD} {path}"` to every
  problem response, so a client-reported error maps back to log lines.
- OpenTelemetry (`AddObservability()`) exports traces over OTLP and metrics to Prometheus at
  `/metrics`. Without the `OTEL_*` variables there is no collector and nothing is exported; the app
  still runs.
- `PerformanceBehaviour` logs a warning for any request over 500 ms, with the user and payload.
- Health: `/health/live` runs no checks; `/health/ready` runs the PostgreSQL (and Redis) checks.

---

## 12. Security posture

| Concern | Mechanism |
|---|---|
| Authentication | JWT bearer (HMAC-SHA256, ≥ 32-character secret validated at startup, 30 s clock skew); refresh tokens rotate and are single-live per user |
| Brute force | Identity lockout (5 failures → 15 min) + `Auth` rate limit per IP + one uniform 401 for unknown email, wrong password and lockout |
| Account enumeration | Register, resend, forgot-password answer 202 regardless; confirm/reset treat unknown user and bad token identically |
| Authorization | Named policies over roles; fallback policy requires authentication everywhere |
| Transport | HSTS outside Development; no `Server` header; forwarded headers with explicit trusted proxies; TLS terminates at your proxy |
| Response headers | `nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy`, `Permissions-Policy`, COOP, deny-all CSP (relaxed only for `/swagger`, `/scalar`) |
| Input | 1 MB body cap (5 MB for the image upload), DataAnnotations + FluentValidation + domain guards, sort whitelists, uploaded images identified by magic bytes, server-generated storage keys |
| Secrets | Blank in `appsettings.json`; user secrets / environment / `.env`; request payloads with secrets logged as `[redacted]`; emails masked in logs |
| Errors | Exception detail only in Development; `requestId` correlates |
| Known gaps | `/metrics` anonymous; Data Protection keys not persisted; relaxed password policy; development secrets committed (see [AGENT_REFERENCE.md §12](AGENT_REFERENCE.md)) |

---

## 13. Runtime topology

Development compose (`docker compose up -d`):

```text
client ──► taxi.api  (container 8080, host 5001)
              ├──► postgres   (container 5432, host 5433)
              ├──► redis      (6379)   HybridCache L2 + distributed locks
              ├──► seq        (ingest 5341, UI host 8081)   OTLP traces + Serilog sink
              ├──► mailpit    (SMTP 1025, UI 8025)          development only
              └─ /metrics ◄── prometheus (9090) ◄── grafana (3000)
```

Running `dotnet run --project src/Taxi.Api` instead starts the API on the host
(`http://localhost:5000`) against the compose PostgreSQL on host port 5433, with an in-process cache
because `ConnectionStrings:Redis` is blank.

Production compose (`-f docker-compose.yml -f docker-compose.prod.yml`) publishes **only** the API
(host 5001); PostgreSQL, Redis, Seq, Prometheus and Grafana stay on the internal network. Put a
reverse proxy in front for TLS, and configure `ForwardedHeaders:KnownProxies` /
`KnownIPNetworks` so client IPs (and rate-limit partitions) are real.

---

## 14. Testing and CI

| Suite | Style |
|---|---|
| `Taxi.Domain.UnitTests` | Pure unit tests of factories, guards and `Result<T>` |
| `Taxi.Application.UnitTests` | Handlers against EF InMemory with NSubstitute fakes; validators; mappers; `CachingBehavior`; pipeline order; JSON round trip of cached results |
| `Taxi.ArchitectureTests` | NetArchTest: layer arrows and conventions; failing types are listed in the message |
| `Taxi.Api.IntegrationTests` | `WebApplicationFactory<Program>` + a Testcontainers PostgreSQL (`postgres:17-alpine`); real middleware, auth, localization, caching, outbox, uploads, rate limiting |

`.github/workflows/build-and-test.yml` runs three jobs on every push to `main` and every pull
request: **build-and-test** (restore, Release build with `-warnaserror`, a vulnerable-package gate,
all suites with coverage, a coverage report in the job summary and as an artifact),
**docker-image** (the API image must keep building) and **markdown-lint** (the docs are part of the
template). Dependabot opens grouped weekly updates for NuGet, GitHub Actions and the Docker base
images.

---

## 15. Architectural rules

1. Dependencies point inward. The table in §2 is the whole permitted graph.
2. Business failures are `Result<T>` + `Error`. Exceptions mean the system broke.
3. Entities are created by `static Result<T> Create(...)` and mutated by named methods. No public
   constructors, no public setters (except interceptor-stamped fields), no anemic models.
4. Every error code and validation message is a `LocalizationKeys` constant present in both
   resource files.
5. Translatable text is `LocalizedText`, persisted as JSONB (or flat columns inside an owned
   collection), resolved per request.
6. Controllers dispatch and match. No business logic, no data access, no `try/catch`.
7. Handlers return DTOs. Domain entities never cross the API boundary.
8. One folder per use case, one type per file, `<UseCase>{Command,CommandHandler,CommandValidator}`.
9. Queries use `.AsNoTracking()`. Cached queries declare a tag; every mutating command evicts it.
10. Application defines the interface, Infrastructure implements it, never the other way round
    (HTTP-context implementations live in `Taxi.Api/Services/`).
11. Package versions go in `Directory.Packages.props`; `.csproj` entries carry no `Version`.
12. Instance members are accessed with `this.` (a house convention; the StyleCop rule is off).
13. A command that creates something is `[Idempotent]`; a request carrying a secret is
    `ISensitiveRequest`.
14. Domain events are handled after commit, in the background, at least once.
