# Application Layer Blueprint — `Taxi.Application`

> Start at [AGENTS.md](../../AGENTS.md) — it carries the rules and the add-a-feature
> checklist. This file is the deep reference for the Application layer.
> Map of all docs: [NAVIGATION.md](../../docs/NAVIGATION.md).

Use cases. CQRS over MediatR, organised as vertical slices, with cross-cutting concerns pushed
into pipeline behaviours.

Reference implementation: **`Features/Cars/`** — three commands, two queries, a DTO and a mapper.

---

## 1. Purpose

Orchestration, and nothing else. A handler fetches state, calls a domain method, persists, evicts
cache, and maps to a DTO. The business rule itself lives in the entity; the HTTP concern lives in
the API.

This layer also **owns the interfaces** that Infrastructure implements (`IAppDbContext`,
`IIdentityService`, `ITokenProvider`, `IUser`, `ILanguageContext`). That inversion is what lets
Infrastructure be swapped without touching a use case.

---

## 2. Dependency rules

**References:** `Taxi.Domain` (project); `FluentValidation.DependencyInjectionExtensions` and
`Microsoft.EntityFrameworkCore` (packages). MediatR arrives transitively via Domain.

- EF Core is referenced on purpose: `IAppDbContext` exposes `DbSet<T>`, and handlers use LINQ
  operators like `FirstOrDefaultAsync` / `AsNoTracking`. **Provider-specific packages
  (`Npgsql...`) must never appear here** — only the provider-agnostic core.
- Contracts is reachable transitively (through Domain) and is used freely for
  `LocalizationKeys` and `Languages`.

**Referenced by:** `Taxi.Infrastructure`, `Taxi.Api`.

**Never add:** `HttpContext`, `IActionResult`, `ControllerBase` · `Npgsql` or any provider ·
a reference to Infrastructure or Api.

---

## 3. Directory structure

```text
src/Taxi.Application/
├── Common/
│   ├── Behaviours/
│   │   ├── CachingBehavior.cs
│   │   ├── LoggingBehaviour.cs
│   │   ├── PerformanceBehaviour.cs
│   │   ├── UnhandledExceptionBehaviour.cs
│   │   └── ValidationBehavior.cs
│   ├── Caching/
│   │   └── CacheTags.cs
│   ├── Errors/
│   │   └── ApplicationErrors.cs
│   ├── Interfaces/
│   │   ├── IAppDbContext.cs
│   │   ├── ICachedQuery.cs
│   │   ├── IIdentityService.cs
│   │   ├── ILanguageContext.cs
│   │   ├── ITokenProvider.cs
│   │   └── IUser.cs
│   └── UtilityService.cs            static helpers; not an abstraction, so not in Interfaces/
├── Features/
│   ├── Cars/
│   │   ├── Commands/
│   │   │   ├── CreateCar/{CreateCarCommand,CreateCarCommandHandler,CreateCarCommandValidator}.cs
│   │   │   ├── UpdateCar/{UpdateCarCommand,UpdateCarCommandHandler,UpdateCarCommandValidator}.cs
│   │   │   └── RemoveCar/{RemoveCarCommand,RemoveCarCommandHandler}.cs      ← no validator
│   │   ├── Dtos/CarDto.cs
│   │   ├── Mappers/CarMapper.cs
│   │   └── Queries/
│   │       ├── GetCarById/{GetCarByIdQuery,GetCarByIdQueryHandler,GetCarByIdQueryValidator}.cs
│   │       └── GetCars/{GetCarsQuery,GetCarsQueryHandler}.cs
│   └── Identity/
│       ├── Commands/
│       │   ├── GenerateToken/{GenerateTokenCommand,…Handler,…Validator}.cs
│       │   └── RefreshToken/{RefreshTokenCommand,…Handler,…Validator}.cs
│       ├── Dtos/{AppUserDto,TokenResponse}.cs
│       └── Queries/
│           └── GetUserInfo/{GetUserByIdQuery,GetUserByIdQueryHandler}.cs
├── DependencyInjection.cs
└── Application_Layer_Blueprint.md
```

Token issuance and refresh are **Commands**, not Queries, because both rotate the stored refresh
token. `GetUserInfo` is a genuine read and stays a Query.

| Folder | Holds |
|---|---|
| `Common/Behaviours/` | MediatR pipeline behaviours + the request pre-processor. |
| `Common/Caching/` | `CacheTags` — the canonical tag constants. |
| `Common/Errors/` | `ApplicationErrors` — failures that belong to a use case, not an entity. |
| `Common/Interfaces/` | Abstractions Infrastructure implements. |
| `Features/<Plural>/` | One vertical slice per aggregate. |

**A feature folder does not have to map to one entity.** `Features/Identity/` maps to no entity at
all — it orchestrates `IIdentityService` and `ITokenProvider`. Group by *use case cluster*, not by
table.

---

## 4. CQRS shapes

### Command

```csharp
public record CreateCarCommand(
    string Make, string Model, int Year, string DescriptionEn, string DescriptionAr)
    : IRequest<Result<CarDto>>;

public record UpdateCarCommand(Guid Id, ...) : IRequest<Result<Updated>>;

public record RemoveCarCommand(Guid Id) : IRequest<Result<Deleted>>;
```

`record`, positional parameters, always `IRequest<Result<T>>`. When there is nothing meaningful to
return, use the marker structs `Result<Updated>` / `Result<Deleted>` rather than inventing a
response type.

### Query

```csharp
public record GetCarByIdQuery(Guid Id) : ICachedQuery<Result<CarDto>>
{
    public string CacheKey => $"cars-{this.Id}";
    public string[] Tags => [CacheTags.Cars];
    public TimeSpan Expiration => TimeSpan.FromMinutes(10);
}
```

An **uncached** query is `IRequest<Result<T>>` and drops the three members. `ICachedQuery<T>`
already extends `IRequest<T>` — do not write both.

---

## 5. Handlers

### Command handler

```csharp
public class CreateCarCommandHandler(
    IAppDbContext context,
    HybridCache cache,
    ILanguageContext languageContext) : IRequestHandler<CreateCarCommand, Result<CarDto>>
{
    private readonly IAppDbContext context = context;
    private readonly HybridCache cache = cache;
    private readonly ILanguageContext languageContext = languageContext;

    public async Task<Result<CarDto>> Handle(CreateCarCommand request, CancellationToken cancellationToken)
    {
        var carResult = Car.Create(
            Guid.NewGuid(), request.Make, request.Model, request.Year,
            request.DescriptionEn, request.DescriptionAr);

        if (carResult.IsError)
        {
            return carResult.Errors;
        }

        var car = carResult.Value;

        this.context.Cars.Add(car);
        await this.context.SaveChangesAsync(cancellationToken);
        await this.cache.RemoveByTagAsync(CacheTags.Cars, cancellationToken);

        return car.ToDto(this.languageContext.Language);
    }
}
```

The skeleton for a mutating handler, in order: **load or construct → check `IsError` and return
`.Errors` → mutate → `SaveChangesAsync` → `RemoveByTagAsync` → map to DTO.**

Update/Remove load first:

```csharp
var car = await this.context.Cars.FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);

if (car is null)
{
    return CarErrors.NotFound;
}
```

Note the primary-constructor-plus-`private readonly`-field style. It is redundant to the compiler
but it is the house style; match it.

### Query handler

```csharp
public class GetCarByIdQueryHandler(IAppDbContext context, ILanguageContext languageContext)
    : IRequestHandler<GetCarByIdQuery, Result<CarDto>>
{
    public async Task<Result<CarDto>> Handle(GetCarByIdQuery request, CancellationToken cancellationToken)
    {
        var car = await this.context.Cars
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);

        if (car is null)
        {
            return CarErrors.NotFound;
        }

        return car.ToDto(this.languageContext.Language);
    }
}
```

`.AsNoTracking()` on every query. Queries never call `Add` / `Remove` / `SaveChangesAsync`.

---

## 6. Validators

```csharp
public sealed class CreateCarCommandValidator : AbstractValidator<CreateCarCommand>
{
    public CreateCarCommandValidator()
    {
        RuleFor(x => x.Make)
            .NotEmpty().WithMessage(LocalizationKeys.Validation.MakeRequired)
            .MaximumLength(100);

        RuleFor(x => x.Year)
            .InclusiveBetween(1886, DateTime.UtcNow.Year + 2).WithMessage(LocalizationKeys.Validation.YearInvalid);
    }
}
```

- `sealed`, named `<UseCase>{Command|Query}Validator`, in the same folder as the command.
- **`.WithMessage(...)` carries the localization key** — this codebase uses `WithMessage`, not
  `WithErrorCode`, because `ValidationBehavior` reads `e.ErrorMessage` into `Error.Code`.
- Registered automatically by `AddValidatorsFromAssembly`; never register one by hand.
- **Optional.** `RemoveCarCommand` has none — `ValidationBehavior` accepts a null validator and
  passes through. Skip the validator when a route constraint (`{id:guid}`) already guarantees the
  input.
- Validators are pure: no database access, no `SaveChanges`.

Overlap with the domain guards in `Car.Create` is deliberate — see
[AGENTS.md §4](../../AGENTS.md).

---

## 7. Pipeline behaviours

Registered in `DependencyInjection.cs`; **registration order is execution order**, outermost first:

```csharp
services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());

    cfg.AddOpenBehavior(typeof(UnhandledExceptionBehaviour<,>));
    cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
    cfg.AddOpenBehavior(typeof(PerformanceBehaviour<,>));
    cfg.AddOpenBehavior(typeof(CachingBehavior<,>));

    cfg.AddOpenRequestPreProcessor(typeof(LoggingBehaviour<>));
});
```

```text
RequestPreProcessorBehavior      ← MediatR's own, prepended; hosts LoggingBehaviour
  └─ UnhandledExceptionBehaviour
       └─ ValidationBehavior
            └─ PerformanceBehaviour
                 └─ CachingBehavior
                      └─ Handler
```

| Behaviour | Applies to | Does |
|---|---|---|
| `UnhandledExceptionBehaviour` | all | `try/catch`, logs with the request payload, rethrows. Registered **first** so it wraps every other behaviour — anything registered before it would escape the catch. Does not convert exceptions to `Result`; `GlobalExceptionHandler` in the API produces the 500. |
| `ValidationBehavior<TRequest,TResponse>` | `where TResponse : IResult` — so only requests returning `Result<T>` | Runs the injected `IValidator<TRequest>` if one exists; on failure maps each `ValidationFailure` to `Error.ValidationForProperty(e.PropertyName, e.ErrorMessage)` and returns `(dynamic)errors`, relying on `Result<T>`'s implicit `List<Error>` conversion. The handler never runs. |
| `PerformanceBehaviour` | all | `Stopwatch`; logs a warning above 500 ms with request name, user id, user name and payload. Resolves the user name through `IIdentityService`, so it costs a database round-trip — but only on requests that are already slow. |
| `CachingBehavior` | `ICachedQuery` only | Registered **last**, so it sits closest to the handler and a cache hit skips as little work as possible. See §8. |
| `LoggingBehaviour<TRequest>` | all | **Not a pipeline behaviour** — an `IRequestPreProcessor<TRequest>`, hosted by MediatR's own `RequestPreProcessorBehavior`, which runs before everything. Logs request name, user id and payload. |

Two ordering rules are load-bearing, and
[`PipelineRegistrationTests`](../../tests/Taxi.Application.UnitTests/Common/PipelineRegistrationTests.cs)
fails the build if either is broken:

1. `UnhandledExceptionBehaviour` is outermost, so no other behaviour's exception goes unlogged.
2. `ValidationBehavior` comes before `CachingBehavior`, so invalid input never reaches the cache.

> ⚠️ **`AddOpenRequestPreProcessor` is required.** MediatR's `RegisterServicesFromAssembly` does
> **not** discover open-generic `IRequestPreProcessor<>` implementations. Before that call was
> added, `LoggingBehaviour` was registered nowhere and silently never ran. Remove the line and
> request logging disappears with no error.

---

## 8. Caching

```csharp
public interface ICachedQuery
{
    string CacheKey { get; }
    string[] Tags { get; }
    TimeSpan Expiration { get; }
    bool IsCultureAware => true;
}

public interface ICachedQuery<TResponse> : IRequest<TResponse>, ICachedQuery;
```

`CachingBehavior` flow:

1. Not an `ICachedQuery`? → `await next(ct)`, done.
2. Build the key: `IsCultureAware ? $"{CacheKey}_{languageContext.Language}" : CacheKey`.
   This is what stops an Arabic response being served to an English caller.
3. Probe with `GetOrCreateAsync` + `HybridCacheEntryFlags.DisableUnderlyingData` — a read-only
   lookup that returns `null` on a miss instead of invoking a factory.
4. On a miss: `await next(ct)`, then `SetAsync` **only if `result is IResult { IsSuccess: true }`**.
   Failures are never cached, so a 404 does not stick for ten minutes.

`HybridCache` itself is registered in `AddInfrastructure` (10-minute default expiration,
30-second L1 window).

### Invalidation is your job

Tag-based, manual, and mandatory:

```csharp
// GetCarsQuery / GetCarByIdQuery
public string[] Tags => [CacheTags.Cars];

// CreateCar / UpdateCar / RemoveCar handlers
await this.cache.RemoveByTagAsync(CacheTags.Cars, cancellationToken);
```

Always the [`CacheTags`](Common/Caching/CacheTags.cs) constant, never a literal — a typo silently
disables eviction and produces a bug nobody notices for weeks.

**Cache a query only when** it is read-heavy, staleness up to `Expiration` is acceptable, **and**
every mutating command evicts its tag.

`GetUserByIdQuery` is deliberately **not** cached for exactly that reason: it returns roles and
claims, nothing evicts `CacheTags.UserInfo`, and serving a stale role set after a permission change
is the wrong trade for an unmeasured saving. The constant stays defined for whenever a
user-mutating command arrives — add the `RemoveByTagAsync` call at the same time you re-enable the
cache, not after.

### Does `Result<T>` survive the cache?

Yes — verified, not assumed. `CachingBehavior` stores the whole `Result<TResponse>`, and
`Result<TValue>`'s only public constructor is marked `[Obsolete(error: true)]`. That blocks
compile-time use but not System.Text.Json's reflection, so the round trip works;
[`ResultSerializationTests`](../../tests/Taxi.Application.UnitTests/Common/ResultSerializationTests.cs)
pins it. Worth keeping pinned: a serialization break here surfaces as a 500 on the *second*
request to a cached endpoint, which manual testing almost never catches.

---

## 9. DTOs and mappers

```csharp
// Features/Cars/Dtos/CarDto.cs
public record CarDto(Guid Id, string Make, string Model, int Year, string Description);
```

```csharp
// Features/Cars/Mappers/CarMapper.cs
public static class CarMapper
{
    public static CarDto ToDto(this Car car, string language = Languages.Default)
        => new(car.Id, car.Make, car.Model, car.Year,
               language == Languages.Ar ? car.Description.Ar : car.Description.En);

    public static List<CarDto> ToDto(this IEnumerable<Car> cars, string language = Languages.Default)
        => cars.Select(c => c.ToDto(language)).ToList();
}
```

- Positional `record`, one file per DTO, in the feature's `Dtos/` folder.
- A DTO is flat: `LocalizedText Description` becomes a single resolved `string Description`.
- Mapping is **manual extension methods** in `Mappers/`. No AutoMapper, no Mapster. Compile-time
  safe, trivially debuggable, no startup cost.
- Overload for the collection case rather than making callers write `.Select(...)`.
- **Mapping happens in memory, after materialisation.** The handler loads the entity, then calls
  `ToDto(language)`. There is no SQL-side JSONB projection in this codebase, and no
  `[Projection]` logging convention. If you need one later, see §12.3.
- Domain entities never leave this layer. `Result<Car>` is a compile error waiting to happen —
  return `Result<CarDto>`.

---

## 10. Interfaces this layer owns

| Interface | Implemented by | For |
|---|---|---|
| `IAppDbContext` | `AppDbContext` (Infrastructure) | `DbSet<Car>`, `DbSet<RefreshToken>`, `SaveChangesAsync` |
| `IIdentityService` | `IdentityService` | Role checks, policy checks, authenticate, fetch user — wraps `UserManager<AppUser>` so Identity types never reach a handler |
| `ITokenProvider` | `TokenProvider` | Issue a JWT + refresh token, read a principal from an expired token |
| `IUser` | `CurrentUser` (**API layer**) | The current user id from the JWT |
| `ILanguageContext` | `LanguageContext` (**API layer**) | The language resolved from `Accept-Language` |

`IUser` and `ILanguageContext` are implemented in the **API** project, not Infrastructure, because
both are HTTP-context concerns. They are registered in
`Taxi.Api/DependencyInjection.AddIdentityInfrastructure()`. That is intentional: the Application
layer states the need, and whichever host is running supplies it.

`ApplicationErrors` holds failures owned by a use case rather than an entity
(`ExpiredAccessTokenInvalid`, `RefreshTokenExpired`, `UserNotFound`, `TokenGenerationFailed`).
Entity-specific failures belong in `<Entity>Errors` in the Domain.

---

## 11. Belongs / does not belong

**Belongs:** commands, queries, handlers, validators, DTOs, mappers · pipeline behaviours ·
interfaces for external capabilities · `ApplicationErrors` · orchestration across multiple
aggregates.

**Does not belong:** invariant enforcement (Domain) · `HttpContext` / `IActionResult` /
status codes (API) · SQL, provider packages, migrations (Infrastructure) · returning entities ·
`DependencyInjection` registrations for Infrastructure services.

---

## 12. Naming

| Thing | Convention |
|---|---|
| Slice | `Features/<Plural>/` |
| Use case folder | `Commands/<Verb><Entity>/` or `Queries/<Verb><Entity>/` |
| Files | `<UseCase>Command.cs`, `<UseCase>CommandHandler.cs`, `<UseCase>CommandValidator.cs` |
| Query files | `<UseCase>Query.cs`, `<UseCase>QueryHandler.cs`, `<UseCase>QueryValidator.cs` |
| DTO | `<Entity>Dto`, positional `record` |
| Mapper | `<Entity>Mapper`, `static class`, `ToDto` extensions |

The Command/Query split is about **side effects, not HTTP verb**. Anything that writes is a
`Command`, even if it is reached by `POST /api/token/generate` and feels like a read — issuing a
token rotates the stored refresh token, so it lives in `Commands/GenerateToken/`.

---

## 13. Talking to other layers

| Direction | How |
|---|---|
| **← API** | The controller builds a command/query and sends it through `ISender`. The Application layer never sees `HttpContext`. |
| **→ Domain** | Handlers call `Entity.Create(...)` / `entity.Method(...)`, check `IsError`, and forward `.Errors` unchanged. Rules the entity cannot see (uniqueness, an external quote) are resolved in the handler and passed in as primitives. |
| **→ Infrastructure** | Only through interfaces this layer declares. Application defines `IAppDbContext`; Infrastructure implements it. Never the reverse. |
| **→ Contracts** | Reads `LocalizationKeys` and `Languages` (reachable transitively through Domain). Never references `Contracts/Requests` — mapping a request to a command happens in the controller. |
| **→ API** | Returns `Result<T>`. `ProblemExtensions` in the API turns the `Error` into an HTTP status. |

**Where does the implementation of a new interface go?** Infrastructure by default. The exception
is anything that needs `HttpContext` — those live in **`Taxi.Api/Services/`**, which is why
`IUser` → `CurrentUser` and `ILanguageContext` → `LanguageContext` are registered in
`Taxi.Api/DependencyInjection.AddIdentityInfrastructure()` rather than `AddInfrastructure`. The
Application layer states the need; whichever host is running supplies it.

---

## 14. Common mistakes

| ❌ | ✅ |
|---|---|
| `IRequest<CarDto>` | `IRequest<Result<CarDto>>` |
| `throw new NotFoundException()` | `return CarErrors.NotFound;` |
| Query without `.AsNoTracking()` | Always `.AsNoTracking()` in query handlers |
| Business rule inside the handler | Push it into the entity method |
| `RemoveByTagAsync("car", ct)` | `RemoveByTagAsync(CacheTags.Cars, ct)` |
| Cached query with no evicting command | Add the `RemoveByTagAsync` call |
| `await context.Cars.FindAsync(id)` inside a `foreach` | Batch with a single `Where(x => ids.Contains(x.Id))` |
| Injecting another handler | Send through `ISender`, or publish a domain event |
| Returning `Result<Car>` | Return `Result<CarDto>` |
| Naming a state-mutating request `...Query` | Name it `...Command` |
| Registering a validator manually | `AddValidatorsFromAssembly` already did |
| Adding an open-generic pre-processor and expecting the assembly scan to find it | Call `AddOpenRequestPreProcessor` explicitly |
| Implementing an Application interface in Infrastructure when it needs `HttpContext` | Put it in `Taxi.Api/Services/` |

---

## 15. Future Extensions — NOT IMPLEMENTED

> ⚠️ **None of the following exists in this repository.** Corrected sketches only.

### 12.1 Authorization behaviour

Today authorization is `[Authorize]` on controllers. If a rule ever needs to live with the use
case rather than the endpoint (for example "only the owning driver may cancel this ride"), add a
behaviour rather than scattering checks through handlers.

```csharp
// src/Taxi.Application/Common/Security/AuthorizeAttribute.cs
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class AuthorizeAttribute : Attribute
{
    public string Roles { get; set; } = string.Empty;

    public string Policy { get; set; } = string.Empty;
}
```

```csharp
// src/Taxi.Application/Common/Behaviours/AuthorizationBehavior.cs
public class AuthorizationBehavior<TRequest, TResponse>(
    IUser user,
    IIdentityService identityService)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : IResult
{
    private readonly IUser user = user;
    private readonly IIdentityService identityService = identityService;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        var attributes = request.GetType()
            .GetCustomAttributes<AuthorizeAttribute>(inherit: true)
            .ToList();

        if (attributes.Count == 0)
        {
            return await next(ct);
        }

        var userId = this.user.Id;

        if (string.IsNullOrEmpty(userId))
        {
            return (dynamic)new List<Error> { ApplicationErrors.Unauthorized };
        }

        foreach (var attribute in attributes)
        {
            if (!string.IsNullOrEmpty(attribute.Roles)
                && !await this.identityService.IsInRoleAsync(userId, attribute.Roles))
            {
                return (dynamic)new List<Error> { ApplicationErrors.Forbidden };
            }

            if (!string.IsNullOrEmpty(attribute.Policy)
                && !await this.identityService.AuthorizeAsync(userId, attribute.Policy))
            {
                return (dynamic)new List<Error> { ApplicationErrors.Forbidden };
            }
        }

        return await next(ct);
    }
}
```

Points the old version of this document got wrong and this one fixes: `next` takes the
`CancellationToken` in MediatR 14; the user id comes from `IUser` (`IIdentityService` has no
`GetCurrentUserId`); `IsInRoleAsync`/`AuthorizeAsync` are the real signatures; and the
`(dynamic)` cast needs a `List<Error>`, since `Result<T>`'s implicit operator is defined for the
list, not for a bare `Error`, in the generic context.

You would also need to add `Unauthorized` and `Forbidden` to `ApplicationErrors` with matching
`LocalizationKeys`, and register the behaviour **before** `ValidationBehavior` (fail auth before
spending effort on validation).

### 12.2 Domain event handlers

The dispatch side already works (`AppDbContext.SaveChangesAsync` publishes through `IMediator`).
Once a concrete `DomainEvent` exists (see the Domain blueprint), the handler goes here:

```csharp
// src/Taxi.Application/Features/Cars/EventHandlers/CarRetiredEventHandler.cs
public class CarRetiredEventHandler(
    HybridCache cache,
    ILogger<CarRetiredEventHandler> logger)
    : INotificationHandler<CarRetiredEvent>
{
    private readonly HybridCache cache = cache;
    private readonly ILogger<CarRetiredEventHandler> logger = logger;

    public async Task Handle(CarRetiredEvent notification, CancellationToken ct)
    {
        this.logger.LogInformation("Car {CarId} retired.", notification.CarId);
        await this.cache.RemoveByTagAsync(CacheTags.Cars, ct);
    }
}
```

MediatR's assembly scan registers it. Convention: `Features/<Plural>/EventHandlers/<Event>Handler.cs`.

Know the semantics before relying on it: dispatch happens **before** the commit, so a throwing
handler aborts the save and a handler cannot assume the data is durable. For side effects that
must only run after a successful commit (emails, webhooks), either move dispatch to
`SavedChangesAsync` in an interceptor or queue an outbox row.

### 12.3 SQL-side bilingual projection

Current mapping materialises the entity then picks a language in memory. For a large list endpoint
that becomes wasteful — both JSONB values cross the wire. EF Core can push the choice into SQL:

```csharp
var language = this.languageContext.Language;

var cars = await this.context.Cars
    .AsNoTracking()
    .Select(c => new CarDto(
        c.Id,
        c.Make,
        c.Model,
        c.Year,
        language == Languages.Ar ? c.Description.Ar : c.Description.En))
    .ToListAsync(cancellationToken);
```

Npgsql translates the owned-JSON member access into a `->>` extraction. Verify the generated SQL
before adopting it (Serilog logs EF commands at Information level) — if it evaluates client-side
you have gained nothing. If you switch, do it for every list endpoint at once so there is one
convention, and keep `CarMapper` for the single-entity case where the difference is irrelevant.

### 12.4 Transactions across several `SaveChanges`

Every handler today performs exactly one `SaveChangesAsync`, which EF Core already wraps in a
transaction. If a use case ever needs two saves atomically, do **not** add a transaction behaviour
to the whole pipeline — take the transaction explicitly in that one handler via
`IAppDbContext`'s underlying `DbContext.Database.BeginTransactionAsync()`, which would require
widening the interface. Prefer restructuring so one save suffices.
