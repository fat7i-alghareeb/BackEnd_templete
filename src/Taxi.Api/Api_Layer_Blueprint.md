# API Layer Blueprint — `Taxi.Api`

> Start at [AGENTS.md](../../AGENTS.md): it carries the rules and the add-a-feature checklist.
> This file is the deep reference for the API layer.
> Map of all docs: [NAVIGATION.md](../../docs/NAVIGATION.md).
> Route, verb and status-code rules: [RESTful_Naming_Constitution.md](../../docs/RESTful_Naming_Constitution.md).

The HTTP boundary and the composition root. It translates requests into MediatR messages and
`Result<T>` into RFC 7807 responses.

---

## 1. At a glance

| | |
|---|---|
| Project | `src/Taxi.Api` (`Microsoft.NET.Sdk.Web`) |
| References | projects `Taxi.Application`, `Taxi.Contracts`, `Taxi.Infrastructure` |
| Packages | `Asp.Versioning.Mvc` + `.ApiExplorer`, `Microsoft.AspNetCore.OpenApi`, `Microsoft.OpenApi` (pinned above a vulnerable transitive version), `Swashbuckle.AspNetCore` (only for `UseSwaggerUI`), `Scalar.AspNetCore`, `My.Extensions.Localization.Json`, five `OpenTelemetry.*` packages, EF Core `Design` |
| Referenced by | nothing (the terminus); test projects reference it |
| Reference code | `Controllers/CarsController.cs`, `Controllers/DriversController.cs`, `Extensions/ProblemExtensions.cs` |
| Integration tests | `tests/Taxi.Api.IntegrationTests` (needs Docker) |
| Rules enforced by the build | `tests/Taxi.ArchitectureTests` (see §20) |

---

## 2. Purpose

Protocol translation, and hosting. Nothing else.

A controller action does exactly three things: build the command or query, send it, and match the
`Result<T>` onto an `IActionResult`. If an action contains a loop, a business `if`, a log
statement or a database call, it is wrong.

The project is also the composition root: `Program.cs` is the only place that knows all layers
exist.

---

## 3. Dependency rules

- **Application**: commands, queries, DTOs, and the `IUser` / `ILanguageContext` / `ITextLocalizer`
  interfaces this layer implements.
- **Contracts**: request DTOs and `LocalizationKeys`.
- **Infrastructure**: *only* for `AddInfrastructure(configuration)`,
  `ApplicationDbContextInitialiser` (migrations at startup) and `HealthCheckTags` in the startup
  path. **No controller may reference an Infrastructure type.**

---

## 4. Directory structure

```text
src/Taxi.Api/
├── Controllers/
│   ├── ApiController.cs               base: Problem(...), RequestedApiVersion, default rate limit
│   ├── CarsController.cs              v1 (deprecated) + v2
│   ├── DriversController.cs
│   ├── UsersController.cs
│   ├── IdentityController.cs          api/token, version-neutral
│   └── AccountController.cs           api/account, version-neutral
├── Extensions/ProblemExtensions.cs    Error → ProblemDetails (the one place)
├── Infrastructure/                    ASP.NET plumbing (unrelated to Taxi.Infrastructure)
│   ├── GlobalExceptionHandler.cs
│   ├── RequestLogContextMiddleware.cs
│   ├── SecurityHeadersMiddleware.cs
│   ├── ForwardedHeadersSettings.cs
│   ├── HealthChecks/HealthCheckEndpoints.cs
│   ├── Idempotency/                   IdempotentAttribute, IdempotencyFilter, IdempotencyRecord
│   └── RateLimiting/                  RateLimitPolicies, RateLimitingSettings
├── OpenApi/Transformers/              VersionInfo, BearerSecurityScheme, BearerSecurityOperation,
│                                      AcceptLanguage
├── Resources/                         SharedResource.en.json, SharedResource.ar.json
├── Services/                          CurrentUser, LanguageContext, TextLocalizer
├── Properties/launchSettings.json
├── appsettings.json | appsettings.Development.json | appsettings.Production.json
├── Dockerfile
├── DependencyInjection.cs             AddPresentation(...) and UseCoreMiddlewares(...)
├── IAssemblyMarker.cs
├── Program.cs
├── SharedResource.cs                  marker class for IStringLocalizer<SharedResource>
└── Api_Layer_Blueprint.md
```

| Folder | Holds | Why here |
|---|---|---|
| `Controllers/` | REST endpoints | HTTP is an API concern |
| `Extensions/` | `Error` → `ProblemDetails` | Needs `ControllerBase` and `IStringLocalizer` |
| `Infrastructure/` | Middleware, exception handler, idempotency, rate-limit settings | Named for *ASP.NET plumbing*. These types depend on `HttpContext`, which `Taxi.Infrastructure` must never see |
| `OpenApi/Transformers/` | Document and operation transformers | Keeps `Program.cs` free of OpenAPI lambdas |
| `Resources/` | Translation JSON | Copied to output by explicit `Content Update` entries in the `.csproj`; a new language file needs one too |
| `Services/` | `CurrentUser`, `LanguageContext`, `TextLocalizer` | Implementations of **Application** interfaces that need `HttpContext` or the resource files |

`SharedResource.cs` and `IAssemblyMarker.cs` sit at the project root namespace deliberately, so
`My.Extensions.Localization.Json` resolves `Resources/SharedResource.{culture}.json` without doubling
the folder segment.

HTTP sample requests live in the repository-root `requests/requests.http`. `src/Taxi.Api/Taxi.Api.http`
and `src/Taxi.Api/Identity.http` are older, partly stale samples (for example `admin@localhost`);
do not extend them.

---

## 5. Controllers

### `ApiController`

```csharp
[ApiController]
[EnableRateLimiting(RateLimitPolicies.Default)]
public class ApiController : ControllerBase
{
    protected string RequestedApiVersion { get; }      // "1", "2", … for the {version} route segment
    protected IActionResult Problem(List<Error> errors) => errors.ToProblem(this);
}
```

It provides `Problem(List<Error>)`, `RequestedApiVersion` (throws on a non-versioned route), the
`[ApiController]` behaviours (automatic model validation, `[FromBody]` inference) and the default
rate-limit policy for every controller. No localizer plumbing, no per-controller translation.

### A concrete controller

Condensed from `CarsController`:

```csharp
[Route("api/v{version:apiVersion}/cars")]
[ApiVersion("1.0", Deprecated = true)]
[ApiVersion("2.0")]
[Authorize(Policy = Policies.CanReadCars)]
public sealed class CarsController(ISender sender) : ApiController
{
    private readonly ISender sender = sender;

    [HttpPost]
    [ProducesResponseType(typeof(CarDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [EndpointName("CreateCar")]
    [Idempotent]
    [Authorize(Policy = Policies.CanManageCars)]
    [MapToApiVersion("1.0")]
    [MapToApiVersion("2.0")]
    public async Task<IActionResult> CreateCar([FromBody] CreateCarRequest request, CancellationToken ct)
    {
        var result = await this.sender.Send(
            new CreateCarCommand(request.Make, request.Model, request.Year,
                                 request.DescriptionEn, request.DescriptionAr),
            ct);

        return result.Match(
            car => this.CreatedAtAction(
                nameof(this.GetCar), new { version = this.RequestedApiVersion, id = car.Id }, car),
            this.Problem);
    }
}
```

The conventions, all mandatory:

| Convention | Detail |
|---|---|
| Class | `sealed`, primary constructor taking `ISender` assigned to a `private readonly` field, inherits `ApiController` (a convention test enforces `sealed` + the base class) |
| Route | **literal** lowercase plural: `[Route("api/v{version:apiVersion}/cars")]`, never `[controller]` |
| Version | `[ApiVersion("x.y")]` on the class for every version it serves; `[MapToApiVersion("x.y")]` on every action, one per version the action serves |
| Auth | `[Authorize(Policy = Policies.X)]` at class level for the read capability; a stricter policy per write action; `[AllowAnonymous]` per action where needed |
| Naming | Action name = endpoint name = `[EndpointName("GetCar")]` |
| Responses | One `[ProducesResponseType]` per reachable status; `typeof(ProblemDetails)` (or `ValidationProblemDetails` for 400) for failures |
| Cancellation | Every action takes `CancellationToken ct` and forwards it |
| Body | `[FromBody]` / `[FromQuery]` a **Contracts request**, mapped to the command by hand |
| Return | `result.Match(success, this.Problem)`, always |
| Idempotency | `[Idempotent]` on every `POST` that creates something |

Route constraints do real work: `[HttpGet("{id:guid}")]` rejects a malformed id at routing time, so
no validator is needed for it.

`UpdateCar` takes the id from the route only (`UpdateCarRequest` has no `Id`), so there is no
route/body consistency check and no controller logic at all.

> **Legacy exception.** `IdentityController` predates two conventions: it keeps its injected
> `sender` and `currentUser` as captured primary-constructor parameters (no `private readonly`
> fields) and its actions carry no `[MapToApiVersion]` because the controller is
> `[ApiVersionNeutral]`. Do not copy its field style into new code.

### Sub-resource and relationship routes (`DriversController`)

| Shape | Route | Notes |
|---|---|---|
| Child collection | `POST /drivers/{id}/certifications`, `DELETE /drivers/{id}/certifications/{certificationId}` | The certification has no GET of its own, so `CreatedAtAction` points at the **driver** (`nameof(this.GetDriver)`, id = the driver id) |
| To-one relationship | `PUT /drivers/{id}/car` (body `{ carId }`), `DELETE /drivers/{id}/car` | PUT assigns, DELETE unassigns; both 204 |

---

## 6. Endpoint catalogue

Auth column: `read` = the controller's read policy; `write` = the stricter write policy;
`anon` = `[AllowAnonymous]`; `any` = `[Authorize]` with no policy. Rate-limit column: `D` =
`RateLimitPolicies.Default` (inherited), `A` = `RateLimitPolicies.Auth` (action override).

### Cars: `api/v{version:apiVersion}/cars`

Class policy `CanReadCars`; writes `CanManageCars`.

| Verb | Route | Versions | Auth | Success | Notable failures |
|---|---|---|---|---|---|
| GET | `/cars` | v1 (deprecated) | read | 200 `PaginatedList<CarDto>` | 400 |
| GET | `/cars` | v2 | read | 200 `PaginatedList<CarDetailsDto>` | 400 |
| GET | `/cars/{id}` | v1 | read | 200 `CarDto` | 404 |
| GET | `/cars/{id}` | v2 | read | 200 `CarDetailsDto` | 404 |
| POST | `/cars` | v1, v2 | write | 201 `CarDto` + `Location` (`[Idempotent]`) | 400, 403, 409, 422 |
| PUT | `/cars/{id}` | v1, v2 | write | 204 | 400, 403, 404, 409 (concurrency) |
| DELETE | `/cars/{id}` | v1, v2 | write | 204 (soft delete) | 403, 404, 409 (assigned to a driver) |
| PUT | `/cars/{id}/image` | v1, v2 | write | 204 (multipart `file`) | 400, 403, 404, 413 |
| GET | `/cars/{id}/image` | v1, v2 | read | 200 `image/jpeg\|png\|webp` | 404 |
| DELETE | `/cars/{id}/image` | v1, v2 | write | 204 | 403, 404 |

### Drivers: `api/v{version:apiVersion}/drivers` (v1 only)

Class policy `CanReadDrivers`; writes `CanManageDrivers`.

| Verb | Route | Auth | Success | Notable failures |
|---|---|---|---|---|
| GET | `/drivers` | read | 200 `PaginatedList<DriverDto>` | 400 |
| GET | `/drivers/{id}` | read | 200 `DriverDto` | 404 |
| POST | `/drivers` | write | 201 `DriverDto` + `Location` (`[Idempotent]`) | 400, 409 (licence taken) |
| PUT | `/drivers/{id}` | write | 204 | 400, 404, 409 |
| DELETE | `/drivers/{id}` | write | 204 (hard delete) | 404 |
| POST | `/drivers/{id}/certifications` | write | 201 `DriverCertificationDto` | 400, 404, 409 |
| DELETE | `/drivers/{id}/certifications/{certificationId}` | write | 204 | 404 |
| PUT | `/drivers/{id}/car` | write | 204 | 400, 404 (driver or car), 409 (car taken, already assigned) |
| DELETE | `/drivers/{id}/car` | write | 204 | 404, 409 (not assigned) |

### Users: `api/v{version:apiVersion}/users` (v1 only)

| Verb | Route | Auth | Success | Notable failures |
|---|---|---|---|---|
| PUT | `/users/{userId}/roles` | `CanManageUsers` | 204 | 400, 403, 404, 409 (cannot remove own `Manager`) |

Replaces the role set (`{ "roles": ["Manager"] }`). New roles apply from the user's next token.

### Token: `api/token` (version-neutral)

| Verb | Route | Auth | RL | Success | Notable failures |
|---|---|---|---|---|---|
| POST | `/token/generate` | anon | A | 200 `TokenResponse` | 400, 401 (uniform), 409 (email not confirmed), 429 |
| POST | `/token/refresh-token` | anon | A | 200 `TokenResponse` | 400, 409 (invalid token / expired refresh token), 429 |
| GET | `/token/user-info` | any | D | 200 `AppUserDto` (roles, claims) | 401 |

### Account: `api/account` (version-neutral)

Endpoints that take an email answer 202 whether or not the account exists.

| Verb | Route | Auth | RL | Success | Notes |
|---|---|---|---|---|---|
| POST | `/account/register` | anon | A | 202 | Creates a `User`-role account and emails a confirmation link. A weak password is 400 |
| POST | `/account/confirm-email` | anon | A | 204 | Body `{ userId, token }` from the emailed link |
| POST | `/account/resend-confirmation` | anon | A | 202 | No-op for unknown or confirmed accounts |
| POST | `/account/forgot-password` | anon | A | 202 | No-op for unknown or unconfirmed accounts |
| POST | `/account/reset-password` | anon | A | 204 | Body `{ email, token, newPassword }`; revokes refresh tokens |
| POST | `/account/change-password` | any | A | 204 | Wrong current password: 400 keyed `CurrentPassword`; revokes refresh tokens |
| POST | `/account/logout` | any | D | 204 | Revokes the caller's refresh tokens; the access token lives until it expires |

### Infrastructure endpoints (mapped in `Program.cs`, all anonymous)

| Route | Purpose |
|---|---|
| `/health/live` | Process answers; runs no checks |
| `/health/ready` | PostgreSQL (+ Redis if configured), 5 s per check |
| `/metrics` | OpenTelemetry Prometheus scrape endpoint |
| `/openapi/v1.json`, `/openapi/v2.json`, `/swagger`, `/scalar/v1`, `/scalar/v2` | Development only |

---

## 7. Authorization model

| Piece | Where |
|---|---|
| Role names | `Taxi.Application/Common/Security/Roles.cs`: `Manager`, `User`; both seeded on startup |
| Policy names | `Taxi.Application/Common/Security/Policies.cs` |
| Policy → role mapping | `AddAppAuthorization` in `Taxi.Api/DependencyInjection.cs` |
| Fallback | `RequireAuthenticatedUser()`: an endpoint with no auth metadata is **not** public |

| Policy | Roles |
|---|---|
| `CanReadCars` | `Manager`, `User` |
| `CanManageCars` | `Manager` |
| `CanReadDrivers` | `Manager`, `User` |
| `CanManageDrivers` | `Manager` |
| `CanManageUsers` | `Manager` |

Endpoints reference policies, never roles, so granting a capability to another role is a one-line
change in `AddAppAuthorization`. A new capability needs a `Policies` constant **and** a mapping
there. Endpoints mapped in `Program.cs` (`/openapi`, `/scalar`, `/metrics`, `/health/*`) call
`.AllowAnonymous()` explicitly because of the fallback.

The JWT is HMAC-SHA256, validated for issuer, audience, lifetime and signature with 30 s clock
skew. A role change takes effect when the user's next token is issued.

---

## 8. Error translation

`Extensions/ProblemExtensions.cs` is the **single** place an `Error` becomes HTTP.

```csharp
public static IActionResult ToProblem(this List<Error> errors, ControllerBase controller)
{
    if (errors.Count == 0) { return controller.Problem(); }

    // … resolve IStringLocalizer<SharedResource>, ILogger, IHostEnvironment …

    if (errors.TrueForAll(e => e.Type == ErrorKind.Validation))
    {
        return BuildValidationProblem(errors, controller, localizer, logger, env);
    }

    return BuildProblem(errors[0], controller, localizer, logger, env);
}
```

- **All-validation** → `ValidationProblemDetails` with an `errors` dictionary keyed by
  `Error.PropertyName` (empty string when absent).
- **Anything else** → `ProblemDetails` built from `errors[0]` (the rest are dropped), with the
  localized message as `title`.
- `Translate` resolves `Error.Code` through the localizer, passing `Error.Args` when present. On a
  miss it logs a warning **in Development only** and falls back to `Error.Description`.
- Status mapping: `Validation` 400 · `Unauthorized` 401 · `Forbidden` 403 · `NotFound` 404 ·
  `Conflict` 409 · everything else (`Failure`, `Unexpected`) 500.

Failures that never go through `Result<T>`:

| Source | Status | Body |
|---|---|---|
| DataAnnotations (`InvalidModelStateResponseFactory`, `AddValidation()`) | 400 (413 when `Content-Length` exceeds the endpoint limit) | `ValidationProblemDetails`, keys resolved from the same resource files |
| `GlobalExceptionHandler`: `BadHttpRequestException` | its own status (413 for an oversized body) | `ProblemDetails` |
| `GlobalExceptionHandler`: `DbUpdateConcurrencyException` | 409 | localized `Concurrency.Conflict` |
| `GlobalExceptionHandler`: anything else | 500 | `title: "Application error"`; `type` and `detail` carry the exception **only in Development**, otherwise a generic message to quote with the `requestId` |
| `[Idempotent]` filter | 400 / 409 / 422 | localized `Idempotency.*` |
| Rate limiter | 429 + `Retry-After` | localized `RateLimit.Exceeded` |
| Authentication / authorization | 401 (with `WWW-Authenticate: Bearer`) / 403 | `application/problem+json` written by `UseStatusCodePages`; `title` is the plain English reason phrase (`Unauthorized`, `Forbidden`), **not** localized |

Because of the fallback policy, an **unmatched route also answers 401 to an unauthenticated
caller**; an authenticated caller gets the usual 404 (`/api/v3/cars` in `VersioningTests`).

Every problem response carries `type`, `title`, `status`, `instance` (`"{METHOD} {path}"`),
`traceId` (the W3C trace id) and `requestId` (the ASP.NET `TraceIdentifier`). `instance` and
`requestId` come from `AddCustomProblemDetails`; either value ties a client report back to the
logs.

---

## 9. `Program.cs` and DI

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddPresentation(builder.Configuration, builder.Environment)
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

builder.WebHost.ConfigureKestrel(...);   // no Server header; MaxRequestBodySize
builder.Host.UseSerilog((context, loggerConfig) => loggerConfig.ReadFrom.Configuration(context.Configuration));

var app = builder.Build();

// Database:ApplyMigrationsOnStartup ?? !IsProduction()
if (applyMigrationsOnStartup) { await app.ApplyMigrationsWithRetryAsync(); }

app.UseForwardedHeaders();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.UseSwaggerUI(...);
    app.MapScalarApiReference(...).AllowAnonymous();
}
else
{
    app.UseHsts();
}

app.UseCoreMiddlewares(builder.Configuration);
app.MapPrometheusScrapingEndpoint().AllowAnonymous();
app.MapAppHealthChecks();
app.MapControllers();
app.Run();

public partial class Program;
```

`public partial class Program;` lets `WebApplicationFactory<Program>` target the host in the
integration tests.

`AddPresentation` composes fourteen registrations: ProblemDetails · API versioning · exception
handling · controllers + JSON (`JsonIgnoreCondition.WhenWritingNull`) · validation ·
`IUser` / `ILanguageContext` / `ITextLocalizer` / `IHttpContextAccessor` · authorization ·
localization · CORS · rate limiting · forwarded headers · security hardening (HSTS options) ·
observability · OpenAPI documents.

### Middleware order (`UseCoreMiddlewares`)

| # | Middleware | Why here |
|---|---|---|
| 1 | `UseRequestLocalization` | First, so everything downstream (including the exception handler) sees the right culture |
| 2 | `RequestLogContextMiddleware` | Pushes `CorrelationId` before anything can log |
| 3 | `SecurityHeadersMiddleware` | Sets `nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer`, `Permissions-Policy`, `Cross-Origin-Opener-Policy` and a deny-all CSP (relaxed for `/swagger` and `/scalar`) before anything can write a response |
| 4 | `UseExceptionHandler` | Catches everything below it |
| 5 | `UseStatusCodePages` | Bodies for bare status results |
| 6 | `UseHttpsRedirection` | Bounce insecure requests early |
| 7 | `UseSerilogRequestLogging` | One summary line per request |
| 8 | `UseCors` | Before authentication, so a preflight `OPTIONS` is not challenged |
| 9 | `UseAuthentication` | Decode the JWT |
| 10 | `UseRateLimiter` | After authentication so `RateLimitPolicies.Default` can partition by user id; before authorization and the endpoint so anonymous credential endpoints are still throttled |
| 11 | `UseAuthorization` | Evaluate `[Authorize]` and policies |

`UseForwardedHeaders` runs earlier, in `Program.cs`, before anything reads the scheme or client IP.
There is no static-files middleware.

### Transport hardening

- `UseHsts()` outside Development: max-age 365 days with `includeSubDomains`.
- No `Server` response header (`KestrelServerOptions.AddServerHeader = false`).
- Request bodies are capped at `Kestrel:Limits:MaxRequestBodySize` (default **1 MB**), read
  explicitly in `ConfigureKestrel` because Kestrel does not bind its limits from configuration by
  itself. An endpoint that accepts files raises it locally with `[RequestSizeLimit]`.
- CORS: one named policy from `AppSettings:CorsPolicyName` / `AllowedOrigins`, any header, any
  method, credentials allowed.
- Forwarded headers (`ForwardedHeaders:*`): `KnownProxies` / `KnownIPNetworks` are parsed with clear
  failure messages; `AllowAllInDevelopment` relaxes them locally only. Behind a proxy that is not
  listed, every caller shares one rate-limit bucket.

There is **no response-level output caching**. All caching happens in the MediatR
`CachingBehavior`, keyed per query and partitioned by language.

---

## 10. HTTP-context services

All implement **Application** interfaces and live here because they need `HttpContext` or the
resource files:

```csharp
public class CurrentUser(IHttpContextAccessor httpContextAccessor) : IUser
{
    public string? Id =>
        this.httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
}

public sealed class LanguageContext(IHttpContextAccessor accessor) : ILanguageContext
{
    public string Language =>
        accessor.HttpContext?.Features.Get<IRequestCultureFeature>()
            ?.RequestCulture.UICulture.TwoLetterISOLanguageName
        ?? Languages.Default;
}
```

`TextLocalizer` wraps `IStringLocalizer<SharedResource>` for the email subjects and bodies
Application composes. `LanguageContext` is strictly read-only: it never mutates
`CultureInfo.CurrentUICulture`, so it is safe across async boundaries. All three are registered
scoped in `AddIdentityInfrastructure()`.

`CurrentUser.Id` is null outside a request (outbox handlers, seeding), which is why
`AuditableEntity.CreatedBy` is nullable.

---

## 11. OpenAPI and versioning

Four transformers, registered per document in `AddApiDocumentation` (`["v1", "v2"]`):

| Transformer | Kind | Does |
|---|---|---|
| `VersionInfoTransformer` | document | Sets `Info.Version` and `Info.Title` (`Taxi API v1`) from the document name |
| `BearerSecuritySchemeTransformer` | document | Registers the `Bearer` HTTP/JWT scheme in `Components` |
| `BearerSecurityOperationTransformer` | operation | Reads `ActionDescriptor.EndpointMetadata`; adds the security requirement **only** when `[Authorize]` is present without `[AllowAnonymous]`, so the padlock appears exactly on protected endpoints |
| `AcceptLanguageOperationTransformer` | operation | Adds an `Accept-Language` header parameter with an enum of `Languages.All`, defaulting to `Languages.Default` |

Development only: OpenAPI JSON at `/openapi/v1.json` and `/openapi/v2.json`, Swagger UI (both
documents, v2 first) at `/swagger`, Scalar at `/scalar/v2` and `/scalar/v1`. Outside Development
none of them is mapped and `UseHsts()` is used instead.

### Versioning: the v1 → v2 example

`CarsController` serves both versions ("interleaving": one controller, versions per action):

| Action | v1 | v2 | Why |
|---|---|---|---|
| `GetCars`, `GetCar` | ✅ `CarDto` | none | description resolved to one language |
| `GetCarsV2`, `GetCarV2` | none | ✅ `CarDetailsDto` | **breaking** shape change: description in both languages + timestamps |
| create, update, delete, image | ✅ | ✅ | identical in both: two `[MapToApiVersion]` |

- Reader: `UrlSegmentApiVersionReader`; default version 1.0; `AssumeDefaultVersionWhenUnspecified`;
  `ReportApiVersions` adds `api-supported-versions` and `api-deprecated-versions`.
- v1 is `[ApiVersion("1.0", Deprecated = true)]`: a v1 response carries
  `api-deprecated-versions: 1.0` and `api-supported-versions: 2.0`, and
  `options.Policies.Sunset(1.0)` adds `Sunset` and `Link` headers with the retirement date
  (**2027-12-31** in `AddCustomApiVersioning`, an example: set your own).
- Both lists share `CarQueryExtensions.ApplyFilters` / `ApplySort`, so versions cannot drift
  (`VersioningTests.V1AndV2_FilterTheSameWay`).
- `CreatedAtAction(nameof(this.GetCar), new { version = this.RequestedApiVersion, … })` works for
  both: link generation only fills the `{version}` segment, so a v2 POST answers
  `Location: /api/v2/cars/{id}`, which routes to `GetCarV2`.
- An unknown version (`/api/v3/...`) is a 404.

To add a version: declare it on the controller, add `[MapToApiVersion]` to every action that is
unchanged, write a new action only where the contract breaks, and add the document name to the
`versions` array in `AddApiDocumentation` and to the Swagger UI endpoints in `Program.cs`. To
retire one: mark it `Deprecated = true`, add a sunset policy, remove it after the date.

---

## 12. File uploads (car image)

`PUT /api/v{version}/cars/{id}/image` binds `IFormFile file` (multipart). It is the one action that
binds a framework type instead of a Contracts request (Contracts cannot reference ASP.NET), and it
still only opens the stream, sends `UploadCarImageCommand`, and matches.

- `[RequestSizeLimit]` / `[RequestFormLimits]` raise the 1 MB global cap to 5 MB + 64 KB of
  multipart overhead for this action only (`ImageFormat.MaxBytes`). An oversized upload is a
  localized **413**: the form reader reports it as a model error, so
  `InvalidModelStateResponseFactory` compares `Content-Length` with the endpoint's limit before
  building a 400.
- The handler identifies the format from the **leading bytes** (`Common/Files/ImageFormat`:
  JPEG, PNG, WebP), never from the client's `Content-Type` or file name.
- `GET …/image` streams the file with the detected content type (`nosniff` is already set);
  `DELETE …/image` removes it. `CarDto.HasImage` / `CarDetailsDto.HasImage` tell clients whether to
  ask.

---

## 13. Idempotent POSTs

`[Idempotent]` (`Infrastructure/Idempotency/`) on an action makes it safe to retry. It is on
`CreateCar` and `CreateDriver`; put it on any `POST` that creates something.

| Request | Response |
|---|---|
| no `Idempotency-Key` header | runs normally |
| new key | runs once under an `IDistributedLock` on the key; a 2xx response is stored 24 h |
| same key + same body, finished | stored status, body and `Location` replayed, plus `Idempotency-Replayed: true` |
| same key + different body | **422** |
| same key while the first is still running | **409**: retry shortly |
| key empty or longer than 128 characters | **400** |

The cache key is scoped to user + method + path + key. Non-2xx responses are not stored, so a
failed request can be retried with the same key. With Redis configured, both the stored responses
(HybridCache L2) and the lock span instances; without it they are per process.

---

## 14. Rate limiting

Two named policies in `Infrastructure/RateLimiting/RateLimitPolicies.cs`, with limits bound from the
`RateLimiting` section (`RateLimitingSettings`, validated on start):

| Policy | Applied by | Algorithm | Default | Partition |
|---|---|---|---|---|
| `RateLimitPolicies.Default` (`"default"`) | `[EnableRateLimiting]` on `ApiController`, so every controller inherits it | sliding window, 6 segments | 100 / 60 s, queue 10 | user id, else client IP |
| `RateLimitPolicies.Auth` (`"auth"`) | `[EnableRateLimiting]` on each credential or email action | fixed window | 10 / 60 s, no queue | client IP |

An action-level attribute wins over the controller-level one, so a credential endpoint uses only
`Auth`. A rejection is a localized 429 `ProblemDetails` (`LocalizationKeys.RateLimit.Exceeded`)
with a `Retry-After` header. Health, metrics and documentation endpoints are mapped outside
controllers and are not limited.

IP partitioning is only as good as `UseForwardedHeaders`: behind a proxy that is not listed in
`ForwardedHeaders:KnownProxies` / `KnownIPNetworks`, every caller shares one bucket.

---

## 15. Configuration

| Section / key | Consumed by | Note |
|---|---|---|
| `ConnectionStrings:DefaultConnection` | `AddInfrastructure` | **Blank in `appsettings.json`**: user secrets / env / `.env` |
| `ConnectionStrings:Redis` | `AddAppCaching` | Blank = in-process cache and lock |
| `JwtSettings:*` | `JwtSettings` options (validated on start), JwtBearer, `TokenProvider` | `Secret` blank on purpose; boot fails unless ≥ 32 characters |
| `AppSettings:CorsPolicyName`, `AllowedOrigins` | `AddConfiguredCors`, `UseCoreMiddlewares` | `DefaultLanguage` is bound but unused |
| `ForwardedHeaders:*` | `AddAppForwardedHeaders` | `KnownProxies`, `KnownIPNetworks`, `ForwardLimit`, `AllowAllInDevelopment` |
| `RateLimiting:Default`, `RateLimiting:Auth` | `AddAppRateLimiting` | §14 |
| `Kestrel:Limits:MaxRequestBodySize` | `ConfigureKestrel` | Bytes; default 1048576 |
| `Database:ApplyMigrationsOnStartup` | `Program.cs` | Code default: on outside Production; `appsettings.Production.json` sets `true` |
| `Database:ResetOnStartup` | `ApplyMigrationsWithRetryAsync` | **Destructive**; ignored (with a warning) outside Development |
| `Email`, `ClientApp`, `FileStorage`, `Outbox`, `Seed` | Infrastructure / Application | Infrastructure blueprint §13, §12, §8, §9 |
| `Serilog` | `UseSerilog` | Console + daily rolling file `logs/log-.txt`; the Seq sink only in `appsettings.Development.json` |
| `OTEL_EXPORTER_OTLP_ENDPOINT`, `OTEL_EXPORTER_OTLP_PROTOCOL` | OpenTelemetry OTLP exporter | Environment variables, set by compose |

Environment variables use `__` for nesting (`Seed__Admin__Email`).

---

## 16. Observability

Wired in `AddObservability()` and consumed by services `docker-compose` provisions.

| Signal | How | Where it lands |
|---|---|---|
| Traces | `AddAspNetCoreInstrumentation` + `AddHttpClientInstrumentation`, OTLP exporter | Seq, via `OTEL_EXPORTER_OTLP_ENDPOINT` (compose sets `http://seq:5341/ingest/otlp/v1/traces`, `http/protobuf`) |
| Metrics | Same instrumentation, Prometheus exporter | `/metrics`, scraped by Prometheus every 5 s (`containers/prometheus/prometheus.yml`) |
| Logs | Serilog (`UseSerilog` + `UseSerilogRequestLogging`) | Console and rolling file always; Seq in Development (compose overrides the sink URL to `http://seq:5341`) |
| Health | `MapAppHealthChecks()` | `/health/live`, `/health/ready` |

- The OpenTelemetry service name is `Taxi.Api`. Outside compose, without the `OTEL_*` variables,
  the exporter has no collector and simply produces no exported traces; it does not fail.
- **Health probes.** `/health/live` runs no checks, so a database outage never gets a healthy
  container restarted (the image's `HEALTHCHECK` probes it). `/health/ready` runs every check
  tagged `HealthCheckTags.Ready` (`postgres`, plus `redis` when configured), each capped at 5 s.
  Both are anonymous, outside rate limiting, answer 200 or 503 and return a small JSON report
  (`{ status, totalDurationMs, checks: [{ name, status, durationMs, description }] }`) without
  exception details.
- `MapPrometheusScrapingEndpoint()` serves `/metrics`. **It is unauthenticated**: it exposes
  request counts and durations, never payloads. Prometheus scrapes `taxi.api:8080` on the compose
  network, but the endpoint is also reachable on the published API port; put it behind the proxy
  before exposing the API publicly.
- The Seq sink is declared in `appsettings.Development.json`. Inside compose,
  `Serilog__WriteTo__2__Args__serverUrl` overrides its URL to the `seq` service name; the index
  matters because .NET configuration merges arrays positionally. `appsettings.Production.json`
  declares no Seq sink, so production logs go to the console and the rolling file unless you add
  one.
- `RequestLogContextMiddleware` pushes the ASP.NET `TraceIdentifier` as `CorrelationId` into the
  Serilog `LogContext`; `requestId` in problem responses carries the same value.
- `PerformanceBehaviour` logs a warning for any request over 500 ms.
- Grafana starts with **no data source or dashboard provisioned**: add Prometheus
  (`http://prometheus:9090`) yourself.

---

## 17. Belongs / does not belong

**Belongs:** controllers · middleware and exception handling · `Error` → `ProblemDetails` ·
OpenAPI configuration · CORS, rate limiting, forwarded headers, localization setup · idempotency ·
`HttpContext`-backed implementations of Application interfaces · the composition root · resource
JSON.

**Does not belong:** business rules · database access · `AppDbContext`, `UserManager<AppUser>` or
any Infrastructure type in a controller · `try/catch` in an action · returning domain entities ·
manual localization inside a controller.

---

## 18. Naming

Full rules and the verb/status matrix: [RESTful Naming Constitution](../../docs/RESTful_Naming_Constitution.md).
The essentials:

| Thing | Convention | Example |
|---|---|---|
| Controller | `<Plural>Controller`, `sealed` | `CarsController` |
| Route | literal, lowercase, plural, versioned | `api/v{version:apiVersion}/cars` |
| Self-service route | version-neutral | `api/token`, `api/account` |
| Action | verb + singular/plural noun | `GetCar`, `GetCars`, `CreateCar`, `RemoveCar` |
| Endpoint name | identical to the action | `[EndpointName("GetCar")]` |
| Route parameter | constrained | `{id:guid}` |
| Multi-word segment | kebab-case | `/refresh-token`, `/confirm-email` |

The action verb follows the Application layer, not HTTP: `DELETE /cars/{id}` maps to `RemoveCar`
because the command is `RemoveCarCommand`.

---

## 19. Talking to other layers

| Direction | How |
|---|---|
| **← client** | JSON binds to a `Taxi.Contracts` request type; DataAnnotations run first, and a failure becomes a localized 400 through `InvalidModelStateResponseFactory` before MediatR is touched. |
| **→ Application** | The action maps the request to a command/query **by hand** and sends it via `ISender`. No mapping library. |
| **← Application** | A `Result<T>` comes back. `result.Match(success, this.Problem)` forks it; `ProblemExtensions` converts `Error` to RFC 7807. |
| **→ Infrastructure** | Startup only: `AddInfrastructure(configuration)`, `ApplyMigrationsWithRetryAsync`, `HealthCheckTags`. Never from a controller. |
| **→ Contracts** | Request types and `LocalizationKeys` for the resource lookup. |
| **implements** | `IUser` → `CurrentUser`, `ILanguageContext` → `LanguageContext`, `ITextLocalizer` → `TextLocalizer`, registered in `AddIdentityInfrastructure()`. |

Responses return Application DTOs (`CarDto`, `DriverDto`) rather than `Contracts/Responses` types.
See the [Contracts blueprint](../Taxi.Contracts/Contracts_Layer_Blueprint.md) §8 for why, and what
changing it would cost.

---

## 20. Enforced by tests

| Rule | Test |
|---|---|
| Controllers are `sealed`, inherit `ApiController` | `ConventionTests.Controllers_InheritApiController_AndAreSealed` |
| Controllers use no Infrastructure, EF Core or Identity type | `ConventionTests.Controllers_DoNotTouchInfrastructureOrEfCore` |
| CRUD + cache invalidation, localization, RBAC, paging | `CarsTests`, `DriversTests` |
| Login/refresh/logout contract | `AuthTests` |
| Account lifecycle, enumeration resistance | `AccountTests` |
| Health probes anonymous; security headers; rate limit 429 | `PlatformTests` |
| v1 deprecated + `Sunset`; v2 shape; unknown version 404 | `VersioningTests` |
| Idempotency replay, soft delete, `xmin` 409, outbox, image upload | `CarsTests` |

The integration suite uses `WebApplicationFactory<Program>` and a Testcontainers PostgreSQL
instance, so it needs Docker.

---

## 21. Deliberate decisions (not bugs)

| Behaviour | Why |
|---|---|
| `Taxi.Api` references `Taxi.Infrastructure` | Startup wiring only; controllers never touch it. |
| `/metrics` is unauthenticated | Scrape target on the compose network; protect it at the proxy before going public. |
| `GlobalExceptionHandler` hides the exception outside Development | Messages can leak connection strings and paths; the `requestId` correlates with the log. |
| The committed development JWT secret and seeded passwords | Public placeholders for local use; replace them before deploying anywhere else. |
| `IdentityController` / `AccountController` are version-neutral | A client that cannot sign in cannot negotiate a version. |
| v1 cars are deprecated but still served | Demonstrates deprecation and a sunset policy; remove v1 after the date. |
| `AddOutputCache` was removed | No endpoint carried `[OutputCache]`; all caching is in MediatR. |
| Refresh failures return 409 | See Application blueprint §18. |
| Health endpoints are not rate limited | Orchestrators probe them continuously. |

---

## 22. Common mistakes

| ❌ | ✅ |
|---|---|
| `[Route("api/v{version:apiVersion}/[controller]")]` | `[Route("api/v{version:apiVersion}/cars")]` |
| `if (request.Year < 1886) return BadRequest();` | Let the validator / domain guard produce it |
| `try { ... } catch { return StatusCode(500); }` | `GlobalExceptionHandler` handles it |
| Injecting `AppDbContext` | Inject `ISender`, send a query |
| `return Ok(car)` where `car` is a `Car` | Return the DTO the handler produced |
| Omitting `[EndpointName]` | Every action has one |
| Omitting `CancellationToken` | Every action takes and forwards `ct` |
| `CreatedAtAction(..., new { id = dto }, dto)` | `new { version = this.RequestedApiVersion, id = dto.Id }` |
| `new { version = "1.0", ... }` | `this.RequestedApiVersion`; a literal breaks `Location` once v2 exists and renders `v1.0` instead of `v1` |
| Building `ProblemDetails` by hand | `return result.Match(success, this.Problem);` |
| Registering middleware directly in `Program.cs` | Add it to `UseCoreMiddlewares`, in the right position |
| Binding an Application command straight from `[FromBody]` | Bind a `Taxi.Contracts` request and map it |
| Returning `exception.Message` to the caller | `GlobalExceptionHandler` exposes detail in Development only |
| `[Authorize(Roles = "Manager")]` | `[Authorize(Policy = Policies.CanManageCars)]` |
| A new `POST` that creates, without `[Idempotent]` | Add it |
| An anonymous endpoint without `[AllowAnonymous]` | The fallback policy would answer 401 |
| Copying `IdentityController`'s captured-parameter style | Assign to a `private readonly` field (see §5) |

---

## 23. Future Extensions — NOT IMPLEMENTED

> ⚠️ **None of the following exists in this repository.** Sketches only.

### 23.1 Correlating logs with traces

`RequestLogContextMiddleware` pushes ASP.NET's `TraceIdentifier` as `CorrelationId`. Now that
OpenTelemetry is active, the W3C trace id is the more useful correlator: switching to
`Activity.Current?.TraceId` would let a Seq log line and its trace be joined directly.

### 23.2 Persisting Data Protection keys

Email-confirmation and password-reset tokens depend on ASP.NET Core Data Protection, whose key ring
is neither persisted nor shared today. Before running several instances, call
`services.AddDataProtection().PersistKeysTo…(…)` (a shared database or Redis) in the composition
root.

### 23.3 Metrics endpoint protection

Move `/metrics` behind the reverse proxy, or require a scrape token, before the API is reachable
publicly.
