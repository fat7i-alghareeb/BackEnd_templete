# Architecture Master Blueprint

The system-level view of Taxi_Server. Every statement here describes code that exists in this
repository today.

| You want to… | Read |
|---|---|
| add a feature, right now | [AGENTS.md](../AGENTS.md) — the copy-paste recipe |
| understand one layer deeply | `src/Taxi.<Layer>/<Layer>_Layer_Blueprint.md` |
| name a route or pick a status code | [RESTful_Naming_Constitution.md](RESTful_Naming_Constitution.md) |
| find which doc answers a question | [NAVIGATION.md](NAVIGATION.md) |

---

## 1. Philosophy

Clean Architecture with **dependencies pointing inward**, plus **CQRS over MediatR**, organised
internally as **vertical slices** (`Features/<Aggregate>/<UseCase>/`) rather than horizontal
technical folders.

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
Taxi.Api         ──────────────────────────►  Application, Contracts, Infrastructure, Client

Taxi.Client      ──────────────────────────►  Taxi.Contracts
```

| Layer | Project | Owns |
|---|---|---|
| Vocabulary | `Taxi.Contracts` | Request DTOs, `LocalizationKeys`, `Languages`, shared enums |
| Core | `Taxi.Domain` | Entities, `<Entity>Errors`, `LocalizedText`, `Result<T>` / `Error` / `ErrorKind`, `Entity` / `AuditableEntity` / `DomainEvent` |
| Use cases | `Taxi.Application` | Commands, queries, handlers, validators, DTOs, mappers, MediatR behaviours, the interfaces Infrastructure must satisfy |
| Implementations | `Taxi.Infrastructure` | `AppDbContext`, EF configurations, interceptor, migrations, seeding, ASP.NET Identity, JWT, HybridCache registration |
| Terminus | `Taxi.Api` | Controllers, middleware, OpenAPI transformers, `Error`→ProblemDetails translation, composition root |
| Consumer | `Taxi.Client` | Blazor WebAssembly shell (see §9) |

**`Taxi.Domain` references `Taxi.Contracts`.** This is a deliberate exception to the usual "Domain
depends on nothing" rule: entity error codes are `LocalizationKeys` constants, and that registry
must be visible to Domain, API and Client alike. **Do not "fix" it** — the full reasoning and every
rejected alternative are in
[Domain blueprint §2](../src/Taxi.Domain/Domain_Layer_Blueprint.md).

**Nothing references `Taxi.Api`.** `Taxi.Api` references `Taxi.Infrastructure` only to call
`AddInfrastructure(configuration)` at startup; controllers never touch Infrastructure types.

---

## 3. Tech stack

### In use

| Concern | Package / feature |
|---|---|
| Runtime | .NET 10 (`net10.0`, nullable + implicit usings, `Directory.Build.props`) |
| CQRS | MediatR 14 — `ISender` in controllers, `IPipelineBehavior` for cross-cutting |
| Validation | FluentValidation 12 (`FluentValidation.DependencyInjectionExtensions`) + `System.ComponentModel.DataAnnotations` in Contracts |
| Persistence | EF Core 10 + `Npgsql.EntityFrameworkCore.PostgreSQL` (JSONB via `OwnsOne(...).ToJson()`, retry-on-failure ×5, 60 s command timeout) |
| Identity | `Microsoft.AspNetCore.Identity.EntityFrameworkCore` (`AddIdentityCore<AppUser>` + roles) with `Microsoft.AspNetCore.Authentication.JwtBearer` |
| Caching | `Microsoft.Extensions.Caching.Hybrid` driven by `ICachedQuery` + `CachingBehavior` |
| Localization | `My.Extensions.Localization.Json` over `Resources/SharedResource.{en,ar}.json` |
| Versioning | `Asp.Versioning.Mvc` + `.ApiExplorer`, URL-segment reader, default `1.0` |
| Docs | `Microsoft.AspNetCore.OpenApi` with four custom transformers, rendered by Swagger UI **and** Scalar (Development only) |
| Logging | Serilog (`Serilog.AspNetCore`), configured from `appsettings.json` → Console + rolling file |
| Quality | StyleCop.Analyzers solution-wide; `Directory.Packages.props` for central package versions |

### Observability

| Concern | How |
|---|---|
| Traces | OpenTelemetry, ASP.NET Core + HttpClient instrumentation, exported over OTLP to Seq |
| Metrics | Same instrumentation, exposed at `/metrics` and scraped by Prometheus |
| Logs | Serilog → Console, rolling file, and Seq |
| Dashboards | Grafana over Prometheus |

All four are provisioned by `docker-compose` and wired in `AddObservability()`.

### Known slack

`Swashbuckle.AspNetCore` is referenced only for `UseSwaggerUI`; the OpenAPI document itself comes
from `Microsoft.AspNetCore.OpenApi`. `Taxi.Client` is referenced by the API but never hosted —
there is no `MapRazorComponents` call.

Every other `PackageVersion` pin has a matching `PackageReference`.

### Tests

`tests/Taxi.Domain.UnitTests` covers the entity factories and `Result<T>`;
`tests/Taxi.Application.UnitTests` pins the MediatR pipeline registration order and the
`Result<T>` cache round trip. `dotnet test Taxi_Server.slnx` runs both.

### Orchestration

`docker-compose.yml` (+ `.override.yml`, `.prod.yml`) provisions PostgreSQL, Seq, Prometheus
and Grafana; `containers/` holds the Prometheus scrape config and Seq's data volume.
`.env.example` documents the
expected environment variables.

---

## 4. The `Result<T>` pattern

`src/Taxi.Domain/Common/Results/`

```csharp
Result<Car> carResult = Car.Create(...);   // implicit from Car, from Error, from List<Error>
if (carResult.IsError) { return carResult.Errors; }
var car = carResult.Value;
```

- `Result<TValue>` — `IsSuccess` / `IsError`, `Value`, `Errors`, `TopError`, and
  `Match(onValue, onError)` which the controllers use to fork into an HTTP response.
- Marker structs for void-ish success: `Result.Success`, `Result.Created`, `Result.Updated`,
  `Result.Deleted`. A delete command is `IRequest<Result<Deleted>>`.
- `Error` is a `readonly record struct` with `Code` (always a `LocalizationKeys` constant),
  `Description` (English fallback), `Type` (`ErrorKind`), optional `Args` (localizer format
  arguments) and optional `PropertyName` (set only by `Error.ValidationForProperty`, used to key
  the RFC 7807 `errors` dictionary).

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
order** — first registered is outermost:

```text
LoggingBehaviour              → IRequestPreProcessor; logs request name + user id
UnhandledExceptionBehaviour   → logs and rethrows; outermost so nothing escapes it
  ValidationBehavior          → FluentValidation; short-circuits to a failed Result
    PerformanceBehaviour      → Stopwatch; warns above 500 ms
      CachingBehavior         → ICachedQuery only; returns the cached value on a hit
        Handler
```

- `ValidationBehavior` is constrained `where TResponse : IResult`, so requests that do not return
  a `Result<T>` skip it. Its validator is optional; a use case with no validator passes through.
  Failures become `Error.ValidationForProperty(e.PropertyName, e.ErrorMessage)` and are returned
  through `Result<T>`'s implicit `List<Error>` conversion.
- `CachingBehavior` injects `ILanguageContext` and partitions culture-aware keys as
  `{CacheKey}_{language}`. It caches only successful results.
- `LoggingBehaviour<TRequest>` is an open-generic `IRequestPreProcessor`, not a pipeline behaviour.
  It needs an explicit `AddOpenRequestPreProcessor` registration — MediatR's assembly scan does not
  discover open generics, and without that call it silently never runs.

Two ordering rules are load-bearing and are enforced by
`tests/Taxi.Application.UnitTests`: `UnhandledExceptionBehaviour` is outermost, and
`ValidationBehavior` precedes `CachingBehavior` so invalid input never reaches the cache.

Details: [Application blueprint](../src/Taxi.Application/Application_Layer_Blueprint.md).

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

- `Contracts/Common/LocalizationKeys.cs` — the single key registry, nested by area
  (`Car`, `RefreshToken`, `Auth`, `Validation`).
- `Contracts/Common/Languages.cs` — `En`, `Ar`, `Default`, `All`. Never write `"en"` / `"ar"`.
- `Api/Resources/SharedResource.{en,ar}.json` — the translations. Every key must exist in **both**.
- `Api/SharedResource.cs` — the marker class `IStringLocalizer<SharedResource>` resolves against.
  It lives at the root namespace on purpose, so `My.Extensions.Localization.Json` computes the
  `Resources/` path correctly.
- `UseRequestLocalization` runs first in the pipeline; `LanguageContext` (implementing
  `ILanguageContext`) exposes the resolved language to handlers without touching `HttpContext`.
- Missing keys: `ProblemExtensions` and `InvalidModelStateResponseFactory` both fall back to the
  English `Description` and log a warning **in Development only**.
- OpenAPI advertises the header: `AcceptLanguageOperationTransformer` adds an `Accept-Language`
  parameter with an enum of `Languages.All` to every operation.

---

## 8. Caching

Opt-in per query via `ICachedQuery<TResponse>` (`CacheKey`, `Tags`, `Expiration`,
`IsCultureAware` defaulting to `true`). `HybridCache` is registered in
`AddInfrastructure` with a 10-minute default expiration and a 30-second L1 window.

Invalidation is tag-based and manual: every command that mutates cached data calls
`RemoveByTagAsync` with the same
[`CacheTags`](../src/Taxi.Application/Common/Caching/CacheTags.cs) constant the query declares.
**A cached query without a matching invalidator is a bug.**

Cached today: `GetCarsQuery` and `GetCarByIdQuery`, both invalidated by all three Car commands.
`GetUserByIdQuery` is deliberately not cached — it carries roles and claims, nothing evicts
`CacheTags.UserInfo`, and stale authorization data is the wrong trade.

There is no response-level output caching; all caching happens in the MediatR pipeline, keyed per
query and partitioned by language.

---

## 9. Persistence and Identity

`AppDbContext : IdentityDbContext<AppUser>, IAppDbContext` — Identity tables and business tables
share one context and one transaction.

- `SaveChangesAsync` is overridden to **dispatch domain events via `IMediator.Publish` before**
  calling `base.SaveChangesAsync`. The mechanism is complete; no concrete `DomainEvent` subclass
  exists yet.
- `AuditableEntityInterceptor` (an `ISaveChangesInterceptor`) stamps `CreatedAtUtc` / `CreatedBy` /
  `LastModifiedUtc` / `LastModifiedBy` from `IUser` and `TimeProvider`, including owned entities.
- `ApplicationDbContextInitialiser` runs `MigrateAsync`, then seeds the `Manager` role, the
  `admin@taxi.com` user and one sample car. `ResetDatabaseAsync` is destructive and gated to
  Development behind `Database:ResetOnStartup`.
- Startup migration is driven by `ApplyMigrationsWithRetryAsync` in `Taxi.Api/DependencyInjection.cs`
  (5 attempts, 3 s apart), gated by `Database:ApplyMigrationsOnStartup` — defaulting to on outside
  Production.
- **No repositories, no Unit of Work wrapper.** `IAppDbContext` exposes `DbSet<T>` directly;
  `DbSet` is the repository and `SaveChangesAsync` is the unit of work.
- Auth is JWT bearer, HMAC-SHA256, configured from the `JwtSettings` section. `TokenProvider`
  issues the access token and rotates the refresh token (deletes the user's existing rows, inserts
  a new 7-day one). Authorization is `[Authorize]` on controllers/actions; `IIdentityService`
  wraps `UserManager` so the Application layer never sees ASP.NET Identity types.

---

## 10. API surface

- Routes are literal, lowercase, plural: `[Route("api/v{version:apiVersion}/cars")]`. The auth
  controller is `[Route("api/token")]` + `[ApiVersionNeutral]`.
- Every action carries `[EndpointName]`, `[MapToApiVersion("1.0")]` and one
  `[ProducesResponseType]` per reachable status.
- `ApiController` is a two-line base class supplying `Problem(List<Error>)`, which delegates to
  `ProblemExtensions.ToProblem` — the single place `Error` becomes HTTP.
- `GlobalExceptionHandler` (an `IExceptionHandler`) turns unhandled exceptions into
  `application/problem+json`. The exception type and message are included **only in Development**;
  outside it the response carries a generic message plus the `requestId` that correlates it with
  the logs.
- Middleware order (`UseCoreMiddlewares`): RequestLocalization → RequestLogContextMiddleware →
  ExceptionHandler → StatusCodePages → HttpsRedirection → StaticFiles → SerilogRequestLogging →
  Cors → RateLimiter → Authentication → Authorization.
  `UseForwardedHeaders` runs earlier, directly in `Program.cs`.
- Rate limiting: one sliding window, 100 requests/minute, 6 segments, queue of 10, 429 on reject.
- OpenAPI transformers: `VersionInfoTransformer` (title/version), `BearerSecuritySchemeTransformer`
  (the `Bearer` scheme), `BearerSecurityOperationTransformer` (padlock only on endpoints that
  actually carry `[Authorize]` without `[AllowAnonymous]`), `AcceptLanguageOperationTransformer`.

---

## 11. Observability

- Serilog replaces the default logger, configured entirely from `appsettings.json`
  (Console + daily rolling file under `logs/`, enriched with machine name and thread id).
- `RequestLogContextMiddleware` pushes `CorrelationId` (the ASP.NET `TraceIdentifier`) into the
  Serilog `LogContext`, so every log line emitted during a request carries it.
- `AddCustomProblemDetails` attaches `requestId` and an `instance` of `"{METHOD} {path}"` to every
  problem response, so a client-reported error maps back to log lines.
- `PerformanceBehaviour` logs a warning for any request over 500 ms, with the user and payload.

There is no distributed tracing or metrics export today despite the OpenTelemetry packages being
referenced, and no Seq sink is configured.

---

## 12. `Taxi.Client`

A Blazor WebAssembly project containing `Program.cs` (registering a named `HttpClient` called
`TaxiServerClient` pointed at the host base address) and `wwwroot` static assets. No components,
no auth handler, no hub client. `Taxi.Api` references the project but does not host it — there is
no `MapRazorComponents` call. Treat it as a placeholder.

---

## 13. Architectural rules

1. Dependencies point inward. The table in §2 is the whole permitted graph.
2. Business failures are `Result<T>` + `Error`. Exceptions mean the system broke.
3. Entities are created by `static Result<T> Create(...)` and mutated by named methods. No public
   constructors, no public setters, no anemic models.
4. Every error code and validation message is a `LocalizationKeys` constant present in both
   resource files.
5. Translatable text is `LocalizedText`, persisted as JSONB, resolved per request.
6. Controllers dispatch and match. No business logic, no data access, no `try/catch`.
7. Handlers return DTOs. Domain entities never cross the API boundary.
8. One folder per use case, one type per file, `<UseCase>{Command,CommandHandler,CommandValidator}`.
9. Queries use `.AsNoTracking()`. Cached queries declare a tag; their mutating commands evict it.
10. Application defines the interface, Infrastructure implements it — never the other way round.
11. Package versions go in `Directory.Packages.props`; `.csproj` entries carry no `Version`.
12. Instance members are accessed with `this.` (StyleCop).
