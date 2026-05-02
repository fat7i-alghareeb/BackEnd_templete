# 🏛️ Architectural Constitution: API Layer Blueprint

## 1. Executive Summary & Layer Purpose

The API / Presentation Layer is the absolute outermost boundary of our Clean Architecture. It is the "bilingual translator" of our system. Its sole, fundamental purpose is to accept requests from the hostile outside world (via HTTP/REST, SignalR WebSockets, or Blazor UI interactions), translate those diverse payloads into rigid Application Commands/Queries, push them into MediatR, and then seamlessly translate the internal Domain results back into appropriate HTTP status codes or visual render states.

It is absolutely devoid of business logic, database querying logic, and authentication cryptography. The API layer is completely "dumb"; it relies entirely on the Application layer to perform the actual orchestration of the system. If business rules change, this layer should theoretically remain completely untouched.

---

## 2. Dependency Rules & Boundaries

### Inward Pointing Dependencies

The API Layer depends on exactly three internal rings, adhering strictly to the Dependency Inversion Principle:

1. **Contracts Layer:** To understand the shape of incoming JSON requests (e.g., `CreateCustomerRequest`) and outgoing responses (`CustomerDto`).
2. **Application Layer:** To instantiate system operations (Commands, Queries) and resolve bridging interfaces like cross-cutting `IUser`.
3. **Infrastructure Layer:** **ONLY** strictly at the composition root (`Program.cs`) to invoke `services.AddInfrastructure(configuration)` and establish the systemic pipeline. Controllers MUST NEVER reference Infrastructure classes or Data Contexts directly.

### Outward Pointing Dependencies

**Nothing.** The API Layer is the absolute terminus of the application. No other layer in the solution references the API Layer. It is purely an execution host.

---

## 3. Directory Structure & Anatomy

Through Forensic Analysis of the codebase, the Presentation layer maintains a complex, highly-segregated directory structure supporting both native UI and dual-HTTP protocols.

```text
src/MechanicShop.Api/
├── Components/         # Blazor Server/WebAssembly UI shell implementations (App.razor)
├── Controllers/        # Domain-driven REST Controllers (Primary Routing)
│   ├── ApiController.cs
│   ├── CustomersController.cs
│   ├── DashboardController.cs
│   └── ...
├── Extensions/         # Extension methods for HTTP Result mapping (ProblemExtensions.cs)
├── Infrastructure/     # Cross-cutting API Middlewares (RequestLogContextMiddleware.cs)
├── OpenApi/
│   └── Transformers/   # OpenAPI configuration transformers
├── Properties/
│   └── launchSettings.json # Multi-environment startup configurations
├── Services/           # Context resolution (CurrentUser.cs)
├── wwwroot/            # Static Web Assets (CSS, JS)
├── appsettings.json    # Application configuration (Serilog, Caching, JWT Params)
├── MechanicShop.Api.http # Native IDE REST testing endpoints
├── DependencyInjection.cs
└── Program.cs          # The Absolute Composition Root
```

---

## 4. Core Architectural Mechanics

Based on microscopic, file-by-file analysis of the ingested codebase, the following advanced, enterprise-grade mechanics govern our API surface.

### 4.1. Hybrid UI & API Hosting (Blazor Integration)

Unlike traditional pure-REST architectures that isolate frontends (e.g., React/Angular/Flutter) into completely disjointed servers, this API project acts as a **Hybrid Host**. It simultaneously exposes JSON endpoints _and_ renders WebAssembly interactable components via Microsoft Blazor natively.

**The Strategy:**
In `Program.cs`, the API registers:

```csharp
builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents();
```

And in the pipeline mapping, it mounts the root application component:

```csharp
app.MapRazorComponents<App>().AllowAnonymous()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(ECommerce.Client._Imports).Assembly);
```

By inspecting `Components/App.razor`, we observe the `Routes` component utilizing the `InteractiveWebAssemblyRenderMode`. This architectural decision allows the application to directly serve a highly interactive frontend from the exact same server that holds the API, maintaining shared Domain logic (via the `Contracts` project) while eliminating cross-origin configuration overhead and connection latency during initial loads.

### 4.2. Routing Standard: REST Controllers

This system adopts a strictly **Controller-based** routing standard for all business logic interactions.

- **MVC Controllers** (`[ApiController]`) are utilized for all endpoints. All of these inherit from a proprietary `ApiController.cs` base class which overrides validation handling natively.

Every controller enforces rigid API versioning natively via `Asp.Versioning`.

### 4.2.1. RESTful Naming Constitution (Key Takeaways)

To ensure a professional, predictable, and scalable API surface, all developers and AI Agents must adhere to the following naming standards:

1. **Nouns over Verbs**: Endpoints represent "things" (`/products`), not actions. Let HTTP methods (`GET`, `POST`, `PUT`, `DELETE`) define the action.
2. **Always Pluralize**: Use plural nouns for all collections to maintain consistency, whether fetching a list (`/users`) or a single item (`/users/5`).
3. **Keep Nesting Shallow**: Nest URLs at most one level deep to show relationships (e.g., `/products/5/reviews`). Avoid deep chains; if it goes deeper than two levels, link directly to the sub-resource.
4. **Format Consistently**: All URIs must be **lowercase** and use **kebab-case** to separate words (e.g., `/customer-orders`).
5. **Version Everything**: Always include a version indicator in your base route (e.g., `/api/v1/resources`). This protects clients from breaking during system overhauls.

**The Rules for Controllers:**

1. **Dumb Envelopes**: Controllers MUST NOT contain business logic. They should simply unwrap HTTP requests, dispatch a MediatR command/query, and wrap the `Result` into an `ActionResult`.
2. **Versioned Routes**: Every controller must be decorated with `[ApiVersion("1.0")]` and `[Route("api/v{version:apiVersion}/[controller]")]`.
3. **Action Specificity**: Use precise HTTP Verbs (`[HttpGet]`, `[HttpPost]`, etc.) and provide `Produces` attributes for Swagger clarity.

### 4.3. Deep OpenAPI Customization (Transformers)

Configuring Swagger dynamically via nested lambdas inside `Program.cs` pollutes the composition root terribly. Instead, this architecture heavily leverages native `IOpenApiDocumentTransformer` and `IOpenApiOperationTransformer` implementations inside the `OpenApi/Transformers` directory to manipulate the generated JSON definitions securely.

1. **`VersionInfoTransformer.cs`**: Injected natively to rewrite the document title and version parameters gracefully.
2. **`BearerSecuritySchemeTransformer.cs`**: Programmatically adds the "Bearer / JWT" HTTP specification globally into the OpenAPI Component scheme without relying on third-party Swashbuckle options.
3. **`BearerSecurityOperationTransformer.cs`** (CRITICAL MECHANIC):
   This transformer utilizes advanced Reflection to inspect endpoint metadata natively:

```csharp
var hasAuthorize = metadata.OfType<AuthorizeAttribute>().Any();
var hasAllowAnonymous = metadata.OfType<AllowAnonymousAttribute>().Any();

if (!hasAuthorize || hasAllowAnonymous) return Task.CompletedTask;

operation.Security.Add(new OpenApiSecurityRequirement { ... });
```

This forces the Swagger UI to render the "Lock" padlock icon ONLY on endpoints that actively require JWTs!

### 4.4. Identity Resolution (The `CurrentUser` Service)

The inner Application Layer defines an `IUser` interface to retrieve the currently authenticated actor's Identity, but the Application Layer has absolute zero knowledge of HTTP Contexts, Headers, or JSON Web Tokens.

**The Mechanics:**
The API layer fulfills this crucial contract by implementing a proxy `CurrentUser` service inside the `Services/` directory. It statically injects `IHttpContextAccessor` to bridge the gap, parsing the decoded JWT natively attached to the Context by the ASP.NET Authentication middleware.

```csharp
using System.Security.Claims;
using ECommerce.Application.Common.Interfaces;

namespace ECommerce.Api.Services;

// This service is registered as Scoped in DependencyInjection.cs
public class CurrentUser(IHttpContextAccessor httpContextAccessor) : IUser
{
    // Extracts the user ID securely from the JWT payload
    // eliminating the need to pass 'UserId' redundantly in every JSON request payload
    public string? Id => httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
}
```

### 4.5. Universal Error Mapping (The Domain-to-HTTP Translator)

The Application Layer never throws exceptions for business logic; it returns a generic `Result<T>` containing a strong `Error` record. The API Layer uses `IStringLocalizer<SharedResource>` to translate these errors into localized, RFC 7807 compliant `ProblemDetails`. There is exactly **one canonical translator** — [`Extensions/ProblemExtensions.cs`](Extensions/ProblemExtensions.cs). The base `ApiController.Problem(List<Error>)` is a one-line delegator that hands every controller's failure result to it.

**Field semantics on `Error`:**

1. **`Code`** is **always** the localization key (a `LocalizationKeys.X` constant). Never a property name.
2. **`Description`** is the English fallback used when the localizer cannot resolve the key.
3. **`PropertyName`** is set only on validation errors that are bound to a specific request property (produced by `ValidationBehavior` via `Error.ValidationForProperty(...)`). When present, it becomes the dictionary key in the RFC 7807 `errors` payload.
4. **`Args`** are passed to the localizer for parametrized messages (e.g., `WorkOrder.TimingReadonly` uses `{0}` and `{1}`).
5. **DataAnnotations** errors are localized through the same `SharedResource` dictionary by an `InvalidModelStateResponseFactory` registered in [`DependencyInjection.AddValidation()`](DependencyInjection.cs).

**Status code mapping** (`ErrorKind` → HTTP):

| Kind           | Status                      |
| -------------- | --------------------------- |
| `Validation`   | `400 Bad Request`           |
| `NotFound`     | `404 Not Found`             |
| `Conflict`     | `409 Conflict`              |
| `Unauthorized` | `401 Unauthorized`          |
| `Forbidden`    | `403 Forbidden`             |
| anything else  | `500 Internal Server Error` |

**Missing-key dev warning:** when `localizer[key].ResourceNotFound` is `true` and the host is `IsDevelopment()`, a Serilog warning is emitted (`"Missing localization key '{Key}' for culture '{Culture}'"`) — surfaces JSON/key drift immediately without spamming production logs. The fallback to `Error.Description` is preferred over leaking the raw key string to the client.

```csharp
public static IActionResult ToProblem(this List<Error> errors, ControllerBase controller)
{
    if (errors.Count == 0)
    {
        return controller.Problem();
    }

    var sp = controller.HttpContext.RequestServices;
    var localizer = sp.GetRequiredService<IStringLocalizer<SharedResource>>();
    var logger = sp.GetRequiredService<ILogger<SharedResource>>();
    var env = sp.GetRequiredService<IHostEnvironment>();

    if (errors.TrueForAll(e => e.Type == ErrorKind.Validation))
    {
        var modelState = new ModelStateDictionary();
        foreach (var e in errors)
        {
            var message = Translate(e.Code, e.Args, e.Description, localizer, logger, env);
            modelState.AddModelError(e.PropertyName ?? string.Empty, message);
        }
        return controller.ValidationProblem(modelState);
    }

    var firstError = errors[0];
    var title = Translate(firstError.Code, firstError.Args, firstError.Description, localizer, logger, env);
    return controller.Problem(statusCode: MapStatus(firstError.Type), title: title);
}
```

`ApiController` itself stays microscopic:

```csharp
[ApiController]
public class ApiController : ControllerBase
{
    protected IActionResult Problem(List<Error> errors) => errors.ToProblem(this);
}
```

No localizer plumbing in the base class, no per-controller translation logic. Every controller just calls `Problem(result.Errors)` from the inherited method.

### 4.6. Exception Handling (`GlobalExceptionHandler`) & Context Middlewares

For strictly unhandled system panics (out of memory, database connection dropped, null reference exceptions), we implement ASP.NET Core 8's native `IExceptionHandler` via `Infrastructure/GlobalExceptionHandler.cs`. This ensures fatal crashes return sterile `application/problem+json` formatting rather than HTML stack traces, shielding server intervals from exposure.

Furthermore, we utilize `RequestLogContextMiddleware` to inject the unique HTTP request `TraceIdentifier` directly into the Serilog `LogContext`, ensuring every log emitted during a request lifecycle is centrally traceable.

```csharp
public class RequestLogContextMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext httpContext)
    {
        // Pushes TraceIdentifier into context so Serilog attaches it to all inner logs
        using (LogContext.PushProperty("CorrelationId", httpContext.TraceIdentifier))
        {
            return next(httpContext);
        }
    }
}
```

### 4.7. Language Resolution & Localization Engine

The API utilizes `My.Extensions.Localization.Json` as the runtime engine, with resources anchored to the `SharedResource` marker class.

**Mechanics:**

- **LanguageContext**: A scoped service (`ILanguageContext`) implemented in [`Services/LanguageContext.cs`](Services/LanguageContext.cs). It is a **read-only** accessor that returns `IRequestCultureFeature.RequestCulture.UICulture.TwoLetterISOLanguageName`. It NEVER mutates `Thread.CurrentUICulture` — culture flow is entirely owned by the framework's async-execution context. It uses `Languages.Default` as its internal fallback.
- **Culture Synchronization**: `UseRequestLocalization` is registered as the very first middleware (see §4.11) so every later component — including `UseExceptionHandler` — sees the correct `CultureInfo.CurrentUICulture`. Supported cultures are defined by `Languages.All`.
- **SharedResource marker class**: Lives at the project's root namespace `MechanicShop.Api` (file: [`SharedResource.cs`](SharedResource.cs)), **not** inside a `Resources` sub-namespace. This keeps the path computation in `My.Extensions.Localization.Json` clean.
- **JSON Resources**: Stored at [`Resources/SharedResource.en.json`](Resources/SharedResource.en.json) and [`Resources/SharedResource.ar.json`](Resources/SharedResource.ar.json). Explicitly deployed to `bin/.../Resources/` by an entry in [`MechanicShop.Api.csproj`](MechanicShop.Api.csproj).
- **Swagger Integration**: All API endpoints in Swagger include an `Accept-Language` header parameter with a dropdown for `en` and `ar` via [`AcceptLanguageOperationTransformer`](OpenApi/Transformers/AcceptLanguageOperationTransformer.cs).
- **ModelState Localization**: `InvalidModelStateResponseFactory` (registered in [`DependencyInjection.AddValidation()`](DependencyInjection.cs)) translates DataAnnotation errors through the same `SharedResource` dictionary used by FluentValidation and domain errors. Contracts request DTOs declare `[Required(ErrorMessage = LocalizationKeys.Validation.X)]` and the factory looks the key up at runtime.

### 4.9. AppSettings & Multi-Environment Configuration

Forensic analysis of `appsettings.json` and `launchSettings.json` reveals deep configuration hooks:

- **Serilog Definitions**: Logging overrides natively writing to both `Console` and external remote telemetry servers (e.g., `Seq` at `http://ops.seq:5341`) utilizing `WithMachineName` and `WithThreadId` enrichers.
- **Launch Profiles**: The IDE relies on complex profiles handling `https`, `http`, `IIS Express`, and fully detached `Docker` orchestrations, ensuring immediate cross-platform developer readiness.
- **Cache Directives**: `LocalCacheExpirationInMins`, `DistributedCacheExpirationMins` strictly controlled via remote configuration without hardcoding constants.

### 4.10. HTTP (.http) Native Testing Files

The repository strictly rejects total reliance on heavy, out-of-band testing environments like Postman. Instead, it relies natively on `.http` files (e.g., `Identity.http`, `ECommerce.Api.http`) located precisely next to the API host tree.

**Architectural Rule:**
All new endpoints MUST be verifiable via the native IDE `.http` clients. This ensures testing suites are version-controlled strictly alongside the codebase branch state. Developers can mock headers (`POST {{baseUrl}}/token/generate`), inject payloads natively, and debug seamlessly.

### 4.11. Advanced Middleware Pipeline Ordering (`DependencyInjection.cs`)

Security, Telemetry, and Stability dictate the exact chronological execution of middlewares. The API layer implements an incredibly explicit, unchangeable sequence via the `UseCoreMiddlewares()` extension block:

1. `UseRequestLocalization()` - Resolves the `Accept-Language` header into a `CultureInfo` and attaches `IRequestCultureFeature` to the HttpContext. Must run first so every later component — including `UseExceptionHandler` — sees the correct culture and produces translated problem responses. Configured with `SetDefaultCulture(Languages.Default)` and `AddSupportedUICultures(Languages.All)`.
2. `UseExceptionHandler()` - Must catch unhandled panics before rendering responses.
3. `UseStatusCodePages()` - Traps standard status code fallbacks.
4. `UseHttpsRedirection()` - Bounces insecure connections before processing logic.
5. `UseSerilogRequestLogging()` - Early trap to track total request cycle duration exactly.
6. `UseCors()` - Must execute before Authentication to process preflight OPTIONS requests natively.
7. `UseRateLimiter()` - Pre-Authentication. Protects against DDOS attacks and brute-force Auth looping using the `SlidingWindowLimiter` (max 100 reqs/min).
8. `UseAuthentication()` - Decodes JWT payload.
9. `UseAuthorization()` - Examines Role allocations.
10. `UseOutputCache()` - Post-Authorization caching. The base policy includes `.SetVaryByHeader("Accept-Language")` so cached responses are partitioned by culture and never leak across `en` / `ar` requesters.

---

## 5. Anti-Patterns & Rejection Criteria

### Immediate PR Rejection Checklist for the API Layer

1. 🚨 **Business Logic / Conditions:**
   - **The Wrong Way:** Checking `if(dto.Price < 0)` or iterating lists inside the Controller.
   - **Why it's rejected:** The API handles protocol translation exclusively. Universal model verification strictly belongs in the Application layer `ValidationBehavior<T>`.
2. 🚨 **Direct Infrastructure Interactions:**
   - **The Wrong Way:** Injecting `AppDbContext` or `SignInManager<AppUser>` directly into a Controller.
   - **Why it's rejected:** Bypasses Application Layer CQRS Pipelines, Interceptors, Telemetry, and invalidates architectural decoupling.
3. 🚨 **Returning Raw Domain Entities:**
   - **The Wrong Way:** Returning a Domain `Customer` entity directly via `Results.Ok(Customer)`.
   - **Why it's rejected:** Openly exposes total internal database schemas and relational navigation properties. You must ALWAYS map to and return a `Contracts` layer DTO to shelter internal refactoring from external clients.
4. 🚨 **Swallowing Exceptions:**
   - **The Wrong Way:** using `try { } catch { return StatusCode(500); }` arbitrarily inside Controllers.
   - **Why it's rejected:** The Global `IExceptionHandler` catches infrastructural failures automatically. Do not write localized catch blocks. Ensure the Domain returns proper `Error` structs natively.

---

## 6. The Composition Root & DI Registration (Program.cs)

The `Program.cs` file is the sole Composition Root of the entire distributed architecture. It is entirely stripped of proprietary logic, simply connecting the sprawling Dependency Injection registrations from the three inner rings recursively.

```csharp
var builder = WebApplication.CreateBuilder(args);

// The Full Clean Architecture Registration Stack
builder.Services
    .AddPresentation(builder.Configuration)
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

// Overrides the .NET default logger with Serilog entirely.
builder.Host.UseSerilog((context, loggerConfig) =>
    loggerConfig.ReadFrom.Configuration(context.Configuration));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    // Registers the manipulated OpenApi payload
    app.MapOpenApi();
    app.UseSwaggerUI(...);

    // An alternative API interface
    app.MapScalarApiReference();

    // Secure Migrations execution, abstracting Entity Framework hooks
    await app.InitialiseDatabaseAsync();
}

// Executes exact chronological security & routing pipe mapped previously
app.UseCoreMiddlewares(builder.Configuration);

app.MapControllers(); // Wires up MVC

// Mounts Blazor Components natively into the DOM
app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(MechanicShop.Client._Imports).Assembly);

// Maps native SignalR Websocket pipelines
app.MapHub<WorkOrderHub>("/hubs/workorders");

app.Run();
```
