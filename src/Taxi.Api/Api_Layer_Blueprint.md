# API Layer Blueprint — `Taxi.Api`

> Start at [AGENTS.md](../../AGENTS.md) — it carries the rules and the add-a-feature
> checklist. This file is the deep reference for the API layer.
> Map of all docs: [NAVIGATION.md](../../docs/NAVIGATION.md).

The HTTP boundary and the composition root. Translates requests into MediatR messages and
`Result<T>` into RFC 7807 responses.

Reference implementation: **`Controllers/CarsController.cs`** and
**`Extensions/ProblemExtensions.cs`**.

Route and status-code rules: [RESTful_Naming_Constitution.md](../../docs/RESTful_Naming_Constitution.md).

---

## 1. Purpose

Protocol translation, and hosting. Nothing else.

A controller action does exactly three things: build the command or query, send it, and match the
`Result<T>` onto an `IActionResult`. If an action contains a loop, a business `if`, a log
statement or a database call, it is wrong.

The project is also the composition root — `Program.cs` is the only place that knows all layers
exist.

---

## 2. Dependency rules

**References:** `Taxi.Application`, `Taxi.Contracts`, `Taxi.Infrastructure`, `Taxi.Client`.

- **Application** — commands, queries, DTOs, and the `IUser` / `ILanguageContext` interfaces this
  layer implements.
- **Contracts** — request DTOs and `LocalizationKeys`.
- **Infrastructure** — *only* for `AddInfrastructure(configuration)` and
  `ApplicationDbContextInitialiser` in the startup path. **No controller may reference an
  Infrastructure type.**
- **Client** — referenced but **never hosted**; `Program.cs` has no `MapRazorComponents` call.
  The reference is currently dead weight.

**Referenced by:** nothing. This is the terminus.

Notable packages: `Asp.Versioning.Mvc` + `.ApiExplorer`, `Microsoft.AspNetCore.OpenApi`,
`Swashbuckle.AspNetCore` (used only for `UseSwaggerUI`), `Scalar.AspNetCore`,
`My.Extensions.Localization.Json`, and five OpenTelemetry packages (see §9).

---

## 3. Directory structure

```text
src/Taxi.Api/
├── Controllers/
│   ├── ApiController.cs
│   ├── CarsController.cs
│   └── IdentityController.cs
├── Extensions/
│   └── ProblemExtensions.cs
├── Infrastructure/
│   ├── ForwardedHeadersSettings.cs
│   ├── GlobalExceptionHandler.cs
│   └── RequestLogContextMiddleware.cs
├── OpenApi/
│   └── Transformers/
│       ├── AcceptLanguageOperationTransformer.cs
│       ├── BearerSecurityOperationTransformer.cs
│       ├── BearerSecuritySchemeTransformer.cs
│       └── VersionInfoTransformer.cs
├── Resources/
│   ├── SharedResource.ar.json
│   └── SharedResource.en.json
├── Services/
│   ├── CurrentUser.cs
│   └── LanguageContext.cs
├── Properties/launchSettings.json
├── appsettings.json | appsettings.Development.json | appsettings.Production.json
├── DependencyInjection.cs
├── IAssemblyMarker.cs
├── Program.cs
├── SharedResource.cs
└── Api_Layer_Blueprint.md
```

| Folder | Holds | Why here and not elsewhere |
|---|---|---|
| `Controllers/` | REST endpoints | HTTP is an API concern |
| `Extensions/` | `Error` → `ProblemDetails` | Needs `ControllerBase` and `IStringLocalizer` |
| `Infrastructure/` | Middleware, exception handler, HTTP options | Named for *ASP.NET plumbing*, unrelated to `Taxi.Infrastructure`. These types depend on `HttpContext`, which the Infrastructure project must never see. |
| `OpenApi/Transformers/` | Document/operation transformers | Keeps `Program.cs` free of Swagger lambdas |
| `Resources/` | Translation JSON | Copied to output by an explicit `.csproj` `Content Update` |
| `Services/` | `CurrentUser`, `LanguageContext` | Implementations of **Application** interfaces that need `HttpContext` |

`SharedResource.cs` and `IAssemblyMarker.cs` sit at the project root namespace deliberately, so
`My.Extensions.Localization.Json` resolves `Resources/SharedResource.{culture}.json` without
doubling the folder segment.

HTTP test files live in the repository-root `requests/` folder, not here.

---

## 4. Controllers

### `ApiController`

```csharp
[ApiController]
public class ApiController : ControllerBase
{
    protected IActionResult Problem(List<Error> errors) => errors.ToProblem(this);
}
```

Two lines. It provides `Problem(List<Error>)` and the `[ApiController]` behaviours (automatic model
validation, `[FromBody]` inference). No localizer plumbing, no per-controller translation.

### A concrete controller

```csharp
[Route("api/v{version:apiVersion}/cars")]
[ApiVersion("1.0")]
[Authorize]
public sealed class CarsController(ISender sender) : ApiController
{
    [HttpGet]
    [ProducesResponseType(typeof(List<CarDto>), StatusCodes.Status200OK)]
    [EndpointName("GetCars")]
    [MapToApiVersion("1.0")]
    public async Task<IActionResult> GetCars(CancellationToken ct)
    {
        var result = await sender.Send(new GetCarsQuery(), ct);
        return result.Match(this.Ok, this.Problem);
    }

    [HttpPost]
    [ProducesResponseType(typeof(CarDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [EndpointName("CreateCar")]
    [MapToApiVersion("1.0")]
    public async Task<IActionResult> CreateCar([FromBody] CreateCarRequest request, CancellationToken ct)
    {
        var result = await sender.Send(
            new CreateCarCommand(request.Make, request.Model, request.Year,
                                 request.DescriptionEn, request.DescriptionAr),
            ct);

        return result.Match(
            car => this.CreatedAtAction(nameof(this.GetCar), new { version = "1.0", id = car.Id }, car),
            this.Problem);
    }
}
```

The conventions, all mandatory:

| Convention | Detail |
|---|---|
| Class | `sealed`, primary constructor taking `ISender`, inherits `ApiController` |
| Route | **literal** lowercase plural — `[Route("api/v{version:apiVersion}/cars")]`, not `[controller]` |
| Version | `[ApiVersion("1.0")]` on the class, `[MapToApiVersion("1.0")]` on every action |
| Auth | `[Authorize]` at class level; `[AllowAnonymous]` per action where needed |
| Naming | Action name = endpoint name = `[EndpointName("GetCar")]` |
| Responses | One `[ProducesResponseType]` per reachable status; `typeof(ProblemDetails)` for failures |
| Cancellation | Every action takes `CancellationToken ct` and forwards it |
| Body | `[FromBody]` a **Contracts request**, mapped to the command by hand |
| Return | `result.Match(success, this.Problem)` — always |

Route constraints do real work: `[HttpGet("{id:guid}")]` rejects a malformed id at routing time, so
no validator is needed for it.

`UpdateCar` contains the one sanctioned piece of controller logic:

```csharp
if (id != request.Id)
{
    return this.BadRequest();
}
```

That is a protocol consistency check (route vs body), not a business rule.

### `IdentityController`

`[Route("api/token")]` with `[ApiVersionNeutral]` — authentication endpoints sit outside the
versioning scheme on purpose, since a client that cannot obtain a token cannot negotiate a version.

Otherwise it follows `CarsController` exactly: `GenerateTokenRequest` / `RefreshTokenRequest` come
from `Taxi.Contracts`, are mapped to `GenerateTokenCommand` / `RefreshTokenCommand` by hand, and
the result is matched. Only `GetUserInfo` carries `[Authorize]`; the other two must be reachable
unauthenticated.

---

## 5. Error translation

`Extensions/ProblemExtensions.cs` is the **single** place an `Error` becomes HTTP.

```csharp
public static IActionResult ToProblem(this List<Error> errors, ControllerBase controller)
{
    if (errors.Count == 0)
    {
        return controller.Problem();
    }

    // ... resolve IStringLocalizer<SharedResource>, ILogger, IHostEnvironment ...

    if (errors.TrueForAll(e => e.Type == ErrorKind.Validation))
    {
        return BuildValidationProblem(errors, controller, localizer, logger, env);
    }

    return BuildProblem(errors[0], controller, localizer, logger, env);
}
```

- **All-validation** → `ValidationProblemDetails` with an `errors` dictionary keyed by
  `Error.PropertyName` (empty string when absent).
- **Anything else** → `ProblemDetails` built from `errors[0]`; the remaining errors are dropped.
- `Translate` resolves `Error.Code` through the localizer, passing `Error.Args` when present. On a
  miss it logs a warning **in Development only** and falls back to `Error.Description`.
- Status mapping: `Validation` 400 · `Unauthorized` 401 · `Forbidden` 403 · `NotFound` 404 ·
  `Conflict` 409 · everything else 500.

DataAnnotation failures take a parallel path — `InvalidModelStateResponseFactory`, registered in
`DependencyInjection.AddValidation()` — but resolve keys against the same `SharedResource`
dictionary, so a client sees one consistent error format regardless of which tier rejected the
request.

`AddCustomProblemDetails` adds `requestId` (the `TraceIdentifier`) and an `instance` of
`"{METHOD} {path}"` to every problem response, which ties a client report back to the logs.

---

## 6. `Program.cs` and DI

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddPresentation(builder.Configuration, builder.Environment)
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

builder.Host.UseSerilog((context, loggerConfig) => loggerConfig.ReadFrom.Configuration(context.Configuration));

var app = builder.Build();

// Database:ApplyMigrationsOnStartup ?? !IsProduction()
if (applyMigrationsOnStartup) { await app.ApplyMigrationsWithRetryAsync(); }

app.UseForwardedHeaders();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(...);
    app.MapScalarApiReference();
}
else
{
    app.UseHsts();
}

app.UseCoreMiddlewares(builder.Configuration);
app.MapControllers();
app.Run();

public partial class Program;
```

`public partial class Program;` exists so `WebApplicationFactory<Program>` can target it in
integration tests (none exist yet).

`AddPresentation` composes twelve registrations: ProblemDetails · API versioning · exception
handling · controllers + JSON (`JsonIgnoreCondition.WhenWritingNull`) · validation ·
`IUser`/`ILanguageContext`/`IHttpContextAccessor` · localization · CORS ·
rate limiting · forwarded headers · OpenAPI documents.

### Middleware order (`UseCoreMiddlewares`)

| # | Middleware | Why here |
|---|---|---|
| 1 | `UseRequestLocalization` | Must run first so everything downstream — including the exception handler — sees the right culture |
| 2 | `RequestLogContextMiddleware` | Pushes `CorrelationId` before anything can log |
| 3 | `UseExceptionHandler` | Catches everything below it |
| 4 | `UseStatusCodePages` | Bodies for bare status results |
| 5 | `UseHttpsRedirection` | Bounce insecure requests early |
| 6 | `UseStaticFiles` | |
| 7 | `UseSerilogRequestLogging` | One summary line per request |
| 8 | `UseCors` | Before authentication, so preflight `OPTIONS` is not challenged |
| 9 | `UseRateLimiter` | Before authentication, so brute force is throttled |
| 10 | `UseAuthentication` | Decode the JWT |
| 11 | `UseAuthorization` | Evaluate `[Authorize]` |

`UseForwardedHeaders` runs earlier, in `Program.cs`, before anything reads the scheme or client IP.

**There is no response-level output caching.** `AddOutputCache`/`UseOutputCache` were registered
but no endpoint ever carried `[OutputCache]`, so they were removed. All caching happens in the
MediatR `CachingBehavior`, keyed per query and partitioned by language — see the
[Application blueprint](../Taxi.Application/Application_Layer_Blueprint.md).

Rate limiting: one sliding window — 100 requests/minute, 6 segments, queue of 10, 429 on reject.
It is global, not per-user or per-endpoint.

---

## 7. HTTP-context services

Both implement **Application** interfaces and live here because they need `HttpContext`:

```csharp
public class CurrentUser(IHttpContextAccessor httpContextAccessor) : IUser
{
    public string? Id => this.httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
}
```

```csharp
public sealed class LanguageContext(IHttpContextAccessor accessor) : ILanguageContext
{
    public string Language =>
        accessor.HttpContext?.Features.Get<IRequestCultureFeature>()
            ?.RequestCulture.UICulture.TwoLetterISOLanguageName
        ?? Languages.Default;
}
```

`LanguageContext` is strictly read-only — it never mutates `CultureInfo.CurrentUICulture`, so it
is safe across async boundaries. Both are registered scoped in `AddIdentityInfrastructure()`.

`CurrentUser.Id` is null outside a request (background work, seeding), which is why
`AuditableEntity.CreatedBy` is nullable.

---

## 8. OpenAPI

Four transformers, registered per document in `AddApiDocumentation` (currently `["v1"]`):

| Transformer | Kind | Does |
|---|---|---|
| `VersionInfoTransformer` | document | Sets `Info.Version` and `Info.Title` from the document name |
| `BearerSecuritySchemeTransformer` | document | Registers the `Bearer` HTTP/JWT scheme in `Components` |
| `BearerSecurityOperationTransformer` | operation | Reads `ActionDescriptor.EndpointMetadata`; adds the security requirement **only** when `[Authorize]` is present without `[AllowAnonymous]` — so the padlock appears exactly on protected endpoints |
| `AcceptLanguageOperationTransformer` | operation | Adds an `Accept-Language` header parameter with an enum of `Languages.All`, defaulting to `Languages.Default` |

Development only: OpenAPI JSON at `/openapi/v1.json`, Swagger UI, and Scalar. Production gets
`UseHsts()` instead.

Adding a version means adding it to the `versions` array **and** to the controllers'
`[ApiVersion]` / `[MapToApiVersion]` attributes.

---

## 9. Configuration

| Section | Consumed by | Note |
|---|---|---|
| `ConnectionStrings:DefaultConnection` | `AddInfrastructure` | **Blank in `appsettings.json`** — user secrets / env / `.env` |
| `JwtSettings` | `AddInfrastructure`, `TokenProvider` | `Secret` blank on purpose |
| `AppSettings` | CORS, `DefaultLanguage` | `CorsPolicyName`, `AllowedOrigins` |
| `ForwardedHeaders` | `AddAppForwardedHeaders` | `KnownProxies` / `KnownIPNetworks` parsed with clear failure messages; `AllowAllInDevelopment` relaxes them locally only |
| `Database:ApplyMigrationsOnStartup` | `Program.cs` | Defaults to on outside Production |
| `Database:ResetOnStartup` | `ApplyMigrationsWithRetryAsync` | **Destructive**; ignored (with a warning) outside Development |
| `Serilog` | `UseSerilog` | Console + daily rolling file under `logs/` |

---

## 10. Observability

Wired in `AddObservability()` and consumed by services `docker-compose` already provisions.

| Signal | How | Where it lands |
|---|---|---|
| Traces | `AddAspNetCoreInstrumentation` + `AddHttpClientInstrumentation`, OTLP exporter | Seq, via `OTEL_EXPORTER_OTLP_ENDPOINT` |
| Metrics | Same instrumentation, Prometheus exporter | `/metrics`, scraped by Prometheus, graphed in Grafana |
| Logs | Serilog | Console, rolling file, and Seq |

- The OTLP exporter is configured **entirely by environment variables**
  (`OTEL_EXPORTER_OTLP_ENDPOINT`, `OTEL_EXPORTER_OTLP_PROTOCOL`), which `docker-compose.override.yml`
  sets. Running outside compose without them simply produces no exported traces — it does not fail.
- `MapPrometheusScrapingEndpoint()` in `Program.cs` serves `/metrics`, matching the path in
  `containers/prometheus/prometheus.yml`. **It is unauthenticated**: it exposes request counts and
  durations, never payloads, and is only reachable on the published port. Put it behind the proxy
  before exposing the API publicly.
- The Seq sink is declared in `appsettings.Development.json` pointing at `localhost:5341`. Inside
  compose, `Serilog__WriteTo__2__Args__serverUrl` overrides it to the `seq` service name. That index
  matters — .NET configuration merges arrays positionally, so the sink list is restated in full
  rather than appended to.

---

## 11. Belongs / does not belong

**Belongs:** controllers · middleware and exception handling · `Error` → `ProblemDetails` ·
OpenAPI configuration · CORS, rate limiting, forwarded headers, localization setup ·
`HttpContext`-backed implementations of Application interfaces · the composition root · resource
JSON.

**Does not belong:** business rules · database access · `AppDbContext`, `UserManager<AppUser>` or
any Infrastructure type in a controller · `try/catch` in an action · returning domain entities ·
manual localization inside a controller.

---

## 12. Naming

Full rules and the verb/status matrix: [RESTful Naming Constitution](../../docs/RESTful_Naming_Constitution.md).
The essentials:

| Thing | Convention | Example |
|---|---|---|
| Controller | `<Plural>Controller`, `sealed` | `CarsController` |
| Route | literal, lowercase, plural, versioned | `api/v{version:apiVersion}/cars` |
| Auth route | version-neutral | `api/token` |
| Action | verb + singular/plural noun | `GetCar`, `GetCars`, `CreateCar`, `RemoveCar` |
| Endpoint name | identical to the action | `[EndpointName("GetCar")]` |
| Route parameter | constrained | `{id:guid}` |
| Multi-word segment | kebab-case | `/maintenance-records`, `/refresh-token` |

The action verb follows the Application layer, not HTTP: `DELETE /cars/{id}` maps to `RemoveCar`
because the command is `RemoveCarCommand`.

---

## 13. Talking to other layers

| Direction | How |
|---|---|
| **← client** | JSON binds to a `Taxi.Contracts` request type; DataAnnotations run first, and a failure becomes a localized 400 through `InvalidModelStateResponseFactory` before MediatR is touched. |
| **→ Application** | The action maps the request to a command/query **by hand** and sends it via `ISender`. No mapping library. |
| **← Application** | A `Result<T>` comes back. `result.Match(success, this.Problem)` forks it; `ProblemExtensions` converts `Error` to RFC 7807. |
| **→ Infrastructure** | Startup only — `AddInfrastructure(configuration)` and `ApplyMigrationsWithRetryAsync`. Never from a controller. |
| **→ Contracts** | Request types and `LocalizationKeys` for the resource lookup. |
| **implements** | `IUser` → `CurrentUser` and `ILanguageContext` → `LanguageContext`, both Application interfaces that need `HttpContext`, registered in `AddIdentityInfrastructure()`. |

Responses currently return Application DTOs (`CarDto`) rather than `Contracts/Responses` types.
See the [Contracts blueprint](../Taxi.Contracts/Contracts_Layer_Blueprint.md) for why, and what
changing it would cost.

---

## 14. Common mistakes

| ❌ | ✅ |
|---|---|
| `[Route("api/v{version:apiVersion}/[controller]")]` | `[Route("api/v{version:apiVersion}/cars")]` |
| `if (request.Year < 1886) return BadRequest();` | Let the validator / domain guard produce it |
| `try { ... } catch { return StatusCode(500); }` | `GlobalExceptionHandler` handles it |
| Injecting `AppDbContext` | Inject `ISender`, send a query |
| `return Ok(car)` where `car` is a `Car` | Return the DTO the handler produced |
| Omitting `[EndpointName]` | Every action has one |
| Omitting `CancellationToken` | Every action takes and forwards `ct` |
| `CreatedAtAction(..., new { id = dto }, dto)` | `new { version = "1.0", id = dto.Id }` — the fixed bug |
| Building `ProblemDetails` by hand | `return result.Match(success, this.Problem);` |
| Registering middleware directly in `Program.cs` | Add it to `UseCoreMiddlewares`, in the right position |
| Binding an Application command straight from `[FromBody]` | Bind a `Taxi.Contracts` request and map it |
| Returning `exception.Message` to the caller | `GlobalExceptionHandler` exposes detail in Development only |

---

## 15. Future Extensions — NOT IMPLEMENTED

> ⚠️ **None of the following exists in this repository.** Corrected sketches only. In particular,
> the previous version of this document described Blazor hosting and a SignalR hub as if they were
> already wired — they are not.

### 15.1 Hosting the Blazor client

`Taxi.Api` already references `Taxi.Client` and `Microsoft.AspNetCore.Components.WebAssembly.Server`,
so only the wiring is missing. The client would first need an `App.razor`, a `Routes.razor` and an
`_Imports.razor` — today it has none.

```csharp
// registration
builder.Services.AddRazorComponents().AddInteractiveWebAssemblyComponents();

// after app.MapControllers()
app.MapRazorComponents<App>()
   .AddInteractiveWebAssemblyRenderMode()
   .AddAdditionalAssemblies(typeof(Taxi.Client._Imports).Assembly);
```

You would also need `app.UseWebAssemblyDebugging()` in Development and
`app.MapFallbackToFile("index.html")` for client-side routing. Note `UseStaticFiles` is already in
the pipeline. If the client stays a separate deployment instead, **remove the project reference** —
right now it is dead weight.

### 15.2 Correlating logs with traces

`RequestLogContextMiddleware` pushes ASP.NET's `TraceIdentifier` as `CorrelationId`. Now that
OpenTelemetry is active, the W3C trace id is the more useful correlator — switching to
`Activity.Current?.TraceId` would let a Seq log line and its trace be joined directly.

### 15.3 Per-endpoint rate limiting

The current limiter is a single global window. For a stricter policy on `/api/token/generate`
(the brute-force target), add a named policy in `AddAppRateLimiting` and apply
`[EnableRateLimiting("TokenPolicy")]` to the action. Partition by IP with
`RateLimitPartition.GetFixedWindowLimiter(httpContext.Connection.RemoteIpAddress)` — but only
after `UseForwardedHeaders` is correctly configured with `KnownProxies`, or every request behind a
proxy shares one bucket.
