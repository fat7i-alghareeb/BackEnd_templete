# Application Layer Blueprint — `Taxi.Application`

> Start at [AGENTS.md](../../AGENTS.md): it carries the rules and the add-a-feature checklist.
> This file is the deep reference for the Application layer.
> Map of all docs: [NAVIGATION.md](../../docs/NAVIGATION.md).

Use cases. CQRS over MediatR, organised as vertical slices, with cross-cutting concerns pushed into
pipeline behaviours.

---

## 1. At a glance

| | |
|---|---|
| Project | `src/Taxi.Application` |
| References | project `Taxi.Domain`; packages `FluentValidation.DependencyInjectionExtensions`, `Microsoft.EntityFrameworkCore` |
| Arrives transitively | `MediatR` (via Domain), `HybridCache` abstractions (via EF Core's caching dependencies), Contracts (via Domain) |
| Referenced by | `Taxi.Infrastructure`, `Taxi.Api` |
| Reference code | `Features/Cars/` (minimal slice), `Features/Drivers/` (child collection, relationship, event), `Features/Account/` (anonymous flows, email) |
| Unit tests | `tests/Taxi.Application.UnitTests` |
| Rules enforced by the build | `tests/Taxi.ArchitectureTests` (see §17) |

---

## 2. Purpose

Orchestration, and nothing else. A handler fetches state, calls a domain method, persists, evicts
cache and maps to a DTO. The business rule lives in the entity; the HTTP concern lives in the API.

This layer also **owns the interfaces** that Infrastructure (and, for HTTP-context concerns, the
API) implements (§11). That inversion is what lets a technology be swapped without touching a use
case.

---

## 3. Dependency rules

- EF Core is referenced on purpose: `IAppDbContext` exposes `DbSet<T>`, and handlers use
  `FirstOrDefaultAsync`, `AnyAsync`, `AsNoTracking`, `ExecuteDeleteAsync`. **Provider-specific
  packages (`Npgsql…`) must never appear here**, only the provider-agnostic core.
- `HybridCache` is injected directly (there is no wrapper interface). The abstractions come in
  through EF Core's caching dependencies; the implementation and its registration are
  Infrastructure's.
- Contracts is reachable transitively through Domain and is used freely for `LocalizationKeys`,
  `Languages`, `CarSortFields` and `SortDirections` (the sort whitelists sit in
  `Contracts/Requests/Cars` but are constants, not request DTOs). A `<UseCase>Request` DTO is
  **never** used in Application: the controller maps request → command.

**Never add:** `HttpContext`, `IActionResult`, `ControllerBase`, anything under
`Microsoft.AspNetCore` · `Npgsql` or any provider · a reference to Infrastructure or Api.

---

## 4. Directory structure

```text
src/Taxi.Application/
├── Common/
│   ├── Behaviours/            CachingBehavior, LoggingBehaviour, PerformanceBehaviour,
│   │                          UnhandledExceptionBehaviour, ValidationBehavior, RequestLogPayload
│   ├── Caching/               CacheTags (tag constants), CacheDefaults (L1 lifetime)
│   ├── Errors/                ApplicationErrors (use-case failures, not entity failures)
│   ├── Files/                 ImageFormat (magic-number detection, 5 MB limit)
│   ├── Interfaces/            IAppDbContext, ICachedQuery, IDistributedLock, IEmailSender,
│   │                          IFileStorage, IIdentityService, ILanguageContext, ISensitiveRequest,
│   │                          ITextLocalizer, ITokenProvider, IUser
│   ├── Models/                EmailMessage, FileContent, PaginatedList<T>, Paging
│   ├── Security/              Policies (capability names), Roles (role names)
│   ├── Settings/              ClientAppSettings (front-end URLs the emails link to)
│   └── UtilityService.cs      static helpers (MaskEmail); not an abstraction
├── Features/
│   ├── Account/               Commands/{Register,ConfirmEmail,ResendConfirmation,ForgotPassword,
│   │                          ResetPassword,ChangePassword,Logout}/  +  Emails/AccountEmails.cs
│   ├── Cars/                  Commands/{CreateCar,UpdateCar,RemoveCar,UploadCarImage,RemoveCarImage}/
│   │                          Queries/{GetCars,GetCarById,GetCarDetails,GetCarDetailsById,
│   │                          GetCarImage}/  +  Queries/CarQueryExtensions.cs
│   │                          Dtos/ Mappers/ EventHandlers/
│   ├── Drivers/               Commands/{CreateDriver,UpdateDriver,RemoveDriver,
│   │                          AddDriverCertification,RemoveDriverCertification,
│   │                          AssignDriverCar,UnassignDriverCar}/
│   │                          Queries/{GetDrivers,GetDriverById}/  Dtos/ Mappers/ EventHandlers/
│   ├── Identity/              Commands/{GenerateToken,RefreshToken}/
│   │                          Queries/GetUserInfo/  Dtos/{AppUserDto,IdentityUserSummary,TokenResponse}
│   └── Users/                 Commands/SetUserRoles/
├── DependencyInjection.cs     AddApplication(): validators + MediatR + behaviours
└── Application_Layer_Blueprint.md
```

| Folder | Holds |
|---|---|
| `Common/Behaviours/` | MediatR pipeline behaviours and the request pre-processor |
| `Common/Interfaces/` | Abstractions Infrastructure or the API implements |
| `Features/<Plural>/` | One vertical slice per aggregate or use-case cluster |

**A feature folder does not have to map to one entity.** `Features/Identity/`, `Features/Account/`
and `Features/Users/` map to no entity of their own; they orchestrate `IIdentityService`,
`ITokenProvider` and `IEmailSender`. Group by use-case cluster, not by table.

Token issuance and refresh are **Commands**, not Queries, because both rotate the stored refresh
token. `GetUserInfo` is a genuine read and stays a Query.

---

## 5. CQRS shapes

### Command

```csharp
public record CreateCarCommand(
    string Make, string Model, int Year, string DescriptionEn, string DescriptionAr)
    : IRequest<Result<CarDto>>;

public record UpdateCarCommand(Guid Id, /* … */) : IRequest<Result<Updated>>;

public record RemoveCarCommand(Guid Id) : IRequest<Result<Deleted>>;
```

`record`, positional parameters, always `IRequest<Result<T>>`. When there is nothing meaningful to
return, use the marker structs `Result<Updated>` / `Result<Deleted>` / `Result<Success>` rather
than inventing a response type.

A request that carries a password, token or stream also implements `ISensitiveRequest`:

```csharp
public record GenerateTokenCommand(string Email, string Password)
    : IRequest<Result<TokenResponse>>, ISensitiveRequest;
```

### Query

```csharp
public record GetCarByIdQuery(Guid Id) : ICachedQuery<Result<CarDto>>
{
    public string CacheKey => $"cars-{this.Id}";
    public string[] Tags => [CacheTags.Cars];
    public TimeSpan Expiration => TimeSpan.FromMinutes(10);
}
```

An **uncached** query is `IRequest<Result<T>>` and drops the three members (`GetUserByIdQuery`,
`GetCarImageQuery`). `ICachedQuery<T>` already extends `IRequest<T>`; do not write both.
`IsCultureAware` defaults to `true`; set it to `false` only when the payload does not depend on
`Accept-Language` (the v2 car queries).

---

## 6. Handlers

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

Skeleton of a mutating handler, in order: **load or construct → check `IsError` and return
`.Errors` → mutate → `SaveChangesAsync` → `RemoveByTagAsync` → map to DTO.**

Update and remove handlers load first and return the entity's `NotFound` error:

```csharp
var car = await this.context.Cars.FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);

if (car is null)
{
    return CarErrors.NotFound;
}
```

**A rule that spans aggregates is the handler's job.** Examples to copy:

| Handler | Rule |
|---|---|
| `CreateDriverCommandHandler` | licence number unique (`AnyAsync`), backed by a unique index |
| `AssignDriverCarCommandHandler` | car exists; no other driver holds it; backed by a filtered unique index |
| `RemoveCarCommandHandler` | a car some driver is assigned to cannot be deleted (`CarErrors.AssignedToDriver`, 409) |
| `SetUserRolesCommandHandler` | a Manager cannot remove their own `Manager` role |

**Side effects outside the database** (file storage) follow a compensation order, see
`UploadCarImageCommandHandler`: save the new file, update the row, delete the old file only after
the commit; on a failed save delete the *new* file. `RemoveCarImageCommandHandler` deletes the file
only after the commit.

Note the primary-constructor-plus-`private readonly`-field style. It is redundant to the compiler
but it is the house style; match it.

### Query handler

```csharp
public class GetCarByIdQueryHandler(IAppDbContext context, ILanguageContext languageContext)
    : IRequestHandler<GetCarByIdQuery, Result<CarDto>>
{
    private readonly IAppDbContext context = context;
    private readonly ILanguageContext languageContext = languageContext;

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

`.AsNoTracking()` on every query. Queries never call `Add` / `Remove` / `SaveChangesAsync`. List
queries build an ordered `IQueryable`, then `PaginatedList<T>.CreateAsync(...)`, then
`page.Map(entity => entity.ToDto(language))` (the mapping runs in memory).

---

## 7. Validators

```csharp
public sealed class CreateCarCommandValidator : AbstractValidator<CreateCarCommand>
{
    public CreateCarCommandValidator()
    {
        RuleFor(x => x.Make)
            .NotEmpty().WithMessage(LocalizationKeys.Validation.MakeRequired)
            .MaximumLength(100).WithMessage(LocalizationKeys.Validation.MakeTooLong);
    }
}
```

- `sealed`, named `<UseCase>{Command|Query}Validator`, in the same folder as the command
  (a convention test enforces `sealed` + the `Validator` suffix).
- **`.WithMessage(...)` carries the localization key.** This codebase uses `WithMessage`, not
  `WithErrorCode`, because `ValidationBehavior` reads `e.ErrorMessage` into `Error.Code`.
- Registered automatically by `AddValidatorsFromAssembly`; never register one by hand. A validator
  may take constructor dependencies resolved from DI (`AddDriverCertificationCommandValidator`
  takes `TimeProvider` so tests can pin "today").
- **Optional.** `RemoveCarCommand` has none: `ValidationBehavior` accepts a null validator and
  passes through. Skip it when a route constraint (`{id:guid}`) already guarantees the input.
  Use cases without one today: `RemoveCar`, `RemoveCarImage`, `GetCarDetailsById`, `GetCarImage`,
  `RemoveDriver`, `RemoveDriverCertification`, `UnassignDriverCar`, `GetDriverById`, `Logout`,
  `GetUserInfo`.
- Validators are pure: no database access, no `SaveChanges`.

The overlap with the domain guards in `Car.Create` is deliberate (AGENT_REFERENCE.md §4).

---

## 8. Pipeline behaviours

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

Resulting chain (verified by resolving the registrations):

```text
RequestPreProcessorBehavior      MediatR's own; hosts LoggingBehaviour; runs first
  └─ UnhandledExceptionBehaviour
       └─ ValidationBehavior
            └─ PerformanceBehaviour
                 └─ CachingBehavior
                      └─ Handler
```

| Behaviour | Applies to | Does |
|---|---|---|
| `LoggingBehaviour<TRequest>` | all | **Not a pipeline behaviour**: an `IRequestPreProcessor<TRequest>`. Logs request name, user id and payload (`[redacted]` for an `ISensitiveRequest`). |
| `UnhandledExceptionBehaviour` | all | `try/catch`, logs with the request payload, rethrows. Registered first so it wraps every other behaviour. It does not convert exceptions to `Result`; `GlobalExceptionHandler` in the API produces the 500. |
| `ValidationBehavior<TRequest,TResponse>` | `where TResponse : IResult`, so only requests returning `Result<T>` | Runs the injected `IValidator<TRequest>` if one exists. On failure it maps each failure to `Error.ValidationForProperty(e.PropertyName, e.ErrorMessage)` and returns `(dynamic)errors`, relying on `Result<T>`'s implicit `List<Error>` conversion. The handler never runs. |
| `PerformanceBehaviour` | all | `Stopwatch`; logs a warning above 500 ms with request name, user id, user name and payload. It resolves the user name through `IIdentityService`, a database round trip, but only for requests that are already slow. |
| `CachingBehavior` | `ICachedQuery` only | Innermost, so a cache hit skips as little work as possible. See §9. |

Two ordering rules are load-bearing, and
[`PipelineRegistrationTests`](../../tests/Taxi.Application.UnitTests/Common/PipelineRegistrationTests.cs)
fails the build if either breaks:

1. `UnhandledExceptionBehaviour` is outermost, so no other behaviour's exception goes unlogged.
2. `ValidationBehavior` precedes `CachingBehavior`, so invalid input never reaches the cache.

> ⚠️ **`AddOpenRequestPreProcessor` is required.** MediatR's `RegisterServicesFromAssembly` does
> **not** discover open-generic `IRequestPreProcessor<>` implementations. Before that call was
> added, `LoggingBehaviour` was registered nowhere and silently never ran. Remove the line and
> request logging disappears with no error.
>
> 🔒 **Payloads are logged with `{@Request}`** by `LoggingBehaviour`, `PerformanceBehaviour` and
> `UnhandledExceptionBehaviour`, through `RequestLogPayload.For(request)`. A request that carries a
> password, a token or a stream implements `ISensitiveRequest` and is logged as `[redacted]`.
> `SensitiveRequestTests` fails when a request with such a property is not marked: plaintext
> passwords once reached the logs this way.

---

## 9. Caching

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

1. Not an `ICachedQuery`? `await next(ct)`, done.
2. Build the key: `IsCultureAware ? $"{CacheKey}_{languageContext.Language}" : CacheKey`. This is
   what stops an Arabic response being served to an English caller.
3. Probe with `GetOrCreateAsync` + `HybridCacheEntryFlags.DisableUnderlyingData`: a read-only
   lookup that returns `null` on a miss instead of invoking a factory. **The probe passes the
   query's `Tags`**: on an L2 (Redis) hit HybridCache copies the value into L1 with the tags of
   that call, and an L1 entry without tags is never evicted by `RemoveByTagAsync`.
4. On a miss: `await next(ct)`, then `SetAsync` **only if `result is IResult { IsSuccess: true }`**.
   Failures are never cached, so a 404 does not stick for ten minutes. `SetAsync` sets
   `LocalCacheExpiration` explicitly (`CacheDefaults.LocalExpirationFor`): per-call options do not
   inherit the registered defaults, and L1 would otherwise live as long as `Expiration`.

`HybridCache` itself is registered in `AddInfrastructure` (10-minute default expiration, 30-second
L1 window from `CacheDefaults.LocalExpiration`), with Redis as L2 when `ConnectionStrings:Redis`
is set.

### What is cached today

| Query | Key | Tag | Culture-aware |
|---|---|---|---|
| `GetCarsQuery` (v1 list) | `cars-p{page}-s{size}-q{search}-m{make}-y{year}-o{sort}-d{dir}` | `CacheTags.Cars` (`"car"`) | yes |
| `GetCarByIdQuery` (v1 one) | `cars-{id}` | `Cars` | yes |
| `GetCarDetailsQuery` (v2 list) | `car-details-p…` | `Cars` | **no** (both languages in the payload) |
| `GetCarDetailsByIdQuery` (v2 one) | `car-details-{id}` | `Cars` | **no** |
| `GetDriversQuery` | `drivers-p{page}-s{size}-q{search}` | `CacheTags.Drivers` (`"driver"`) | yes |
| `GetDriverByIdQuery` | `drivers-{id}` | `Drivers` | yes |

Not cached: `GetUserByIdQuery`, `GetCarImageQuery` (a stream).

Eviction (`RemoveByTagAsync`): the five car commands (`CreateCar`, `UpdateCar`, `RemoveCar`,
`UploadCarImage`, `RemoveCarImage`) evict `Cars`; the seven driver commands (`CreateDriver`,
`UpdateDriver`, `RemoveDriver`, `AddDriverCertification`, `RemoveDriverCertification`,
`AssignDriverCar`, `UnassignDriverCar`) evict `Drivers`.

### Multiple instances

With Redis configured, every instance shares L2 entries and tag invalidations. HybridCache has no
backplane, so after a write on node A, node B may serve its own L1 copy for up to
`CacheDefaults.LocalExpiration` (30 s) before it re-reads L2 and sees the invalidation. That is the
staleness budget for any cached query: shorten `LocalExpiration` if a query cannot tolerate it, or
do not cache it. Without Redis the cache is per process and only correct for a single instance.

### Invalidation is your job

Tag-based, manual, and mandatory:

```csharp
// a cached query
public string[] Tags => [CacheTags.Cars];

// every command that touches the data
await this.cache.RemoveByTagAsync(CacheTags.Cars, cancellationToken);
```

Always the [`CacheTags`](Common/Caching/CacheTags.cs) constant, never a literal: a typo silently
disables eviction and produces a bug nobody notices for weeks.

**Cache a query only when** it is read-heavy, staleness up to `Expiration` is acceptable, **and**
every mutating command evicts its tag.

`GetUserByIdQuery` is deliberately **not** cached for exactly that reason: it returns roles and
claims, nothing evicts `CacheTags.UserInfo`, and serving a stale role set after a permission change
is the wrong trade. The constant stays defined for whenever a user-mutating command arrives: add
the `RemoveByTagAsync` call at the same time you add caching, not after.

### Does `Result<T>` survive the cache?

Yes. `CachingBehavior` stores the whole `Result<TResponse>`, and `Result<TValue>`'s public
constructor is marked `[Obsolete(error: true)]` + `[JsonConstructor]`. That blocks compile-time use
but not System.Text.Json's reflection, so the round trip works;
[`ResultSerializationTests`](../../tests/Taxi.Application.UnitTests/Common/ResultSerializationTests.cs)
pins it, including `PaginatedList<T>`. Keep it pinned: a serialization break surfaces as a 500 on
the *second* request to a cached endpoint, which manual testing almost never catches.

---

## 10. DTOs and mappers

```csharp
// Features/Cars/Dtos/CarDto.cs
public record CarDto(Guid Id, string Make, string Model, int Year, string Description, bool HasImage = false);

// Features/Cars/Mappers/CarMapper.cs
public static CarDto ToDto(this Car car, string language = Languages.Default) =>
    new(car.Id, car.Make, car.Model, car.Year,
        language == Languages.Ar ? car.Description.Ar : car.Description.En,
        car.ImageKey is not null);
```

- Positional `record`, one file per DTO, in the feature's `Dtos/` folder.
- A DTO is flat: `LocalizedText Description` becomes one resolved `string Description` (v1), or a
  `LocalizedTextDto(En, Ar)` when the client edits both languages (`CarDetailsDto`, v2).
- Mapping is **manual extension methods** in `Mappers/`. No AutoMapper, no Mapster.
- Mapping happens **in memory, after materialisation**: the handler loads the entity, then calls
  `ToDto(language)`. There is no SQL-side JSONB projection (see §20.2).
- A DTO may expose state derived from the entity (`HasImage`) but never the storage key.
- Domain entities never leave this layer. `Result<Car>` is a compile error waiting to happen:
  return `Result<CarDto>`.
- Existing shapes: `CarDto` (v1), `CarDetailsDto` (v2: both languages, `CreatedAtUtc`,
  `LastModifiedUtc`), `DriverDto` (with `DriverCertificationDto`), `AppUserDto`, `TokenResponse`,
  `IdentityUserSummary`.

---

## 11. Interfaces this layer owns

| Interface | Implemented by | For |
|---|---|---|
| `IAppDbContext` | `AppDbContext` (Infrastructure) | `DbSet<Car>`, `DbSet<Driver>`, `DbSet<RefreshToken>`, `SaveChangesAsync` |
| `IIdentityService` | `IdentityService` (Infrastructure) | Role and policy checks, authenticate, find/create user, email-confirmation and password-reset tokens, change password, set roles. Wraps `UserManager<AppUser>` so Identity types never reach a handler. Account tokens cross the boundary Base64Url-encoded; `IdentityResult` errors come back as localized per-field `Error`s |
| `ITokenProvider` | `TokenProvider` (Infrastructure) | Issue a JWT + refresh token; read a principal from an expired token |
| `IFileStorage` | `LocalFileStorage` (Infrastructure) | Save / open / delete binary content by server-generated key; swap for an object store behind the same interface |
| `IDistributedLock` | `RedisDistributedLock` / `InMemoryDistributedLock` (Infrastructure) | Atomic, expiring, try-once lock (Redis when configured); used by `[Idempotent]` POSTs and the outbox processor |
| `IEmailSender` | `LoggingEmailSender` / `SmtpEmailSender` (Infrastructure) | Send an `EmailMessage`; chosen by `Email:Provider` |
| `IUser` | `CurrentUser` (**API**) | The current user id from the JWT (`null` outside a request) |
| `ILanguageContext` | `LanguageContext` (**API**) | The language resolved from `Accept-Language` |
| `ITextLocalizer` | `TextLocalizer` (**API**) | Resource-file text (email subjects and bodies) in the request's language |
| `ICachedQuery` / `ISensitiveRequest` | n/a (markers implemented by requests) | Opt-in caching; payload redaction in logs |

`IUser`, `ILanguageContext` and `ITextLocalizer` are implemented in the **API** project, not
Infrastructure, because each depends on `HttpContext` or on the API's resource files. They are
registered in `Taxi.Api/DependencyInjection.AddIdentityInfrastructure()`. The Application layer
states the need; whichever host runs supplies it.

### `ApplicationErrors`

Failures owned by a use case rather than an entity (`Common/Errors/ApplicationErrors.cs`). They are
properties (`=>`), each building a new `Error`.

| Member | Kind | Used by |
|---|---|---|
| `InvalidCredentials` | Unauthorized | `IdentityService.AuthenticateAsync` (unknown email, wrong password and lockout all return it) |
| `ExpiredAccessTokenInvalid`, `UserIdClaimInvalid`, `RefreshTokenExpired` | Conflict | `RefreshTokenCommandHandler`, `LogoutCommandHandler`, `ChangePasswordCommandHandler` |
| `UserNotFound` | NotFound | `IdentityService` (user lookups by id) |
| `AccountTokenInvalid` | Validation (property `Token`) | `IdentityService.ConfirmEmailAsync` / `ResetPasswordAsync` |
| `CannotRemoveOwnManagerRole` | Conflict | `SetUserRolesCommandHandler` |
| `CarNotFound`, `InvalidRefreshToken`, `TokenGenerationFailed` | NotFound / Validation / Failure | **unreferenced today**; prefer `CarErrors.NotFound` and `RefreshTokenErrors.ExpiryInvalid` |

Entity-specific failures belong in `<Entity>Errors` in the Domain.

---

## 12. Domain event handlers

A domain event (Domain blueprint §9) is handled by an `INotificationHandler<TEvent>` in
`Features/<Plural>/EventHandlers/<Event>Handler.cs`. MediatR's assembly scan registers it. The
references are `Features/Cars/EventHandlers/CarCreatedDomainEventHandler.cs` and
`Features/Drivers/EventHandlers/DriverAssignedToCarDomainEventHandler.cs`; both only log.

Handlers are invoked by Infrastructure's `OutboxProcessor`, **not** by the request:

- after the transaction committed, a few seconds later (poll interval 5 s), in their own DI scope
  with no `HttpContext` (`IUser.Id` is null, `ILanguageContext` falls back to the default);
- **at least once**: make them idempotent;
- a handler that throws is retried with exponential backoff, parked after 5 attempts, and never
  fails the request that raised the event.

Use them for side effects (email, derived-data refresh, integrations). Anything the caller must see
in the response belongs in the command handler instead. The naming convention is enforced:
handlers end in `Handler`.

---

## 13. Feature catalogue

Every use case that exists today. Policy and route live in the API blueprint §6.

| Feature | Use case | Kind | Returns | Notes |
|---|---|---|---|---|
| Account | `Register` | command, sensitive | `Result<Success>` | Always success for a valid request, even when the email exists (the owner is emailed instead); a weak password is a 400 |
| | `ConfirmEmail` | command, sensitive | `Result<Success>` | Unknown user and bad token look identical |
| | `ResendConfirmation` | command | `Result<Success>` | Silent no-op for unknown or already-confirmed accounts |
| | `ForgotPassword` | command | `Result<Success>` | Silent no-op for unknown or unconfirmed accounts |
| | `ResetPassword` | command, sensitive | `Result<Success>` | Revokes refresh tokens, clears lockout |
| | `ChangePassword` | command, sensitive | `Result<Success>` | Revokes refresh tokens; the current access token lives until it expires |
| | `Logout` | command | `Result<Success>` | Deletes the caller's refresh tokens |
| Users | `SetUserRoles` | command | `Result<Updated>` | Canonicalises role casing; a Manager cannot drop their own `Manager` role |
| Identity | `GenerateToken` | command, sensitive | `Result<TokenResponse>` | Rotates the refresh token |
| | `RefreshToken` | command, sensitive | `Result<TokenResponse>` | Failures are 409 (`Conflict`) |
| | `GetUserInfo` (`GetUserByIdQuery`) | query, uncached | `Result<AppUserDto>` | Roles and claims |
| Cars | `CreateCar`, `UpdateCar`, `RemoveCar` | command | `CarDto` / `Updated` / `Deleted` | Evict `Cars`; remove is soft and blocked while a driver holds the car |
| | `UploadCarImage` (sensitive), `RemoveCarImage` | command | `Updated` / `Deleted` | Evict `Cars`; format by magic bytes |
| | `GetCars`, `GetCarById` | query, cached | `PaginatedList<CarDto>` / `CarDto` | v1 |
| | `GetCarDetails`, `GetCarDetailsById` | query, cached | `PaginatedList<CarDetailsDto>` / `CarDetailsDto` | v2, culture-neutral cache |
| | `GetCarImage` | query, uncached | `FileContent` | Stream plus content type |
| Drivers | `CreateDriver`, `UpdateDriver`, `RemoveDriver` | command | `DriverDto` / `Updated` / `Deleted` | Unique licence number; hard delete |
| | `AddDriverCertification`, `RemoveDriverCertification` | command | `DriverCertificationDto` / `Deleted` | Child collection |
| | `AssignDriverCar`, `UnassignDriverCar` | command | `Updated` | Cross-aggregate rule; assignment raises `DriverAssignedToCarDomainEvent` |
| | `GetDrivers`, `GetDriverById` | query, cached | `PaginatedList<DriverDto>` / `DriverDto` | Tag `Drivers` |

---

## 14. Naming

| Thing | Convention |
|---|---|
| Slice | `Features/<Plural>/` (or the use-case cluster: `Account`, `Identity`, `Users`) |
| Use case folder | `Commands/<Verb><Entity>/` or `Queries/<Verb><Entity>/` |
| Files | `<UseCase>Command.cs`, `<UseCase>CommandHandler.cs`, `<UseCase>CommandValidator.cs` |
| Query files | `<UseCase>Query.cs`, `<UseCase>QueryHandler.cs`, `<UseCase>QueryValidator.cs` |
| DTO | `<Entity>Dto`, positional `record` |
| Mapper | `<Entity>Mapper`, `static class`, `ToDto` extensions |
| Event handler | `<Event>Handler` in `EventHandlers/` |

The Command/Query split is about **side effects, not HTTP verb**. Anything that writes is a
`Command`, even if it is reached by `POST /api/token/generate` and feels like a read: issuing a
token rotates the stored refresh token, so it lives in `Commands/GenerateToken/`.

---

## 15. Talking to other layers

| Direction | How |
|---|---|
| **← API** | The controller builds a command/query and sends it through `ISender`. The Application layer never sees `HttpContext`. |
| **→ Domain** | Handlers call `Entity.Create(...)` / `entity.Method(...)`, check `IsError`, and forward `.Errors` unchanged. Rules the entity cannot see are resolved in the handler. |
| **→ Infrastructure** | Only through interfaces this layer declares. Application defines `IAppDbContext`; Infrastructure implements it. Never the reverse. |
| **→ Contracts** | Reads `LocalizationKeys`, `Languages`, `CarSortFields`, `SortDirections`. Never uses a `<UseCase>Request` DTO. |
| **→ API** | Returns `Result<T>`. `ProblemExtensions` in the API turns the `Error` into an HTTP status. |

**Where does the implementation of a new interface go?** Infrastructure by default. The exception
is anything that needs `HttpContext` or the API's resource files: those live in
**`Taxi.Api/Services/`** and are registered in `Taxi.Api/DependencyInjection.AddIdentityInfrastructure()`.

---

## 16. Belongs / does not belong

**Belongs:** commands, queries, handlers, validators, DTOs, mappers · pipeline behaviours ·
interfaces for external capabilities · `ApplicationErrors` · event handlers · orchestration across
multiple aggregates · account-email composition (`AccountEmails`).

**Does not belong:** invariant enforcement (Domain) · `HttpContext` / `IActionResult` / status
codes (API) · SQL, provider packages, migrations (Infrastructure) · returning entities ·
`DependencyInjection` registrations for Infrastructure services.

---

## 17. Enforced by tests

| Rule | Test |
|---|---|
| Behaviour order (`Unhandled` outermost, `Validation` before `Caching`, `Caching` innermost) | `PipelineRegistrationTests` |
| `LoggingBehaviour` registered as a pre-processor; validators registered from the assembly | `PipelineRegistrationTests` |
| Secret-carrying requests are `ISensitiveRequest`; the log never contains the payload | `SensitiveRequestTests` |
| Cache probe passes tags; failures never cached; key suffixed with language; L1 ≤ expiration | `CachingBehaviorTests` |
| `Result<T>` and `PaginatedList<T>` survive a JSON round trip | `ResultSerializationTests` |
| Handlers end in `Handler` and live under `Features`; validators are sealed and end in `Validator` | `ConventionTests` |
| No dependency on Infrastructure, Api or `Microsoft.AspNetCore` | `LayerDependencyTests` |
| Handler behaviour (EF InMemory + NSubstitute) | `Features/{Account,Cars,Drivers,Users}/*Tests` |
| Validators and mappers | `Validators/ValidatorTests`, `Mappers/CarMapperTests` |

---

## 18. Deliberate decisions (not bugs)

| Behaviour | Why |
|---|---|
| `GetUserByIdQuery` is uncached | Stale roles after a permission change are the wrong trade (§9). |
| Refresh-token failures are `Conflict` (409), not `Unauthorized` | Existing contract; the client reads the localized title. |
| `Register` / `ForgotPassword` / `ResendConfirmation` succeed for unknown or existing emails | No account enumeration. |
| `Logout` and password change do not invalidate the current access token | JWTs are stateless; the token lives until it expires. |
| `PerformanceBehaviour` resolves the user name from the database | Only for requests already over 500 ms. |
| `ValidationBehavior` returns `(dynamic)errors` | The generic response type is only known to implement `IResult`; the cast resolves the implicit `List<Error>` conversion at run time. |

---

## 19. Common mistakes

| ❌ | ✅ |
|---|---|
| `IRequest<CarDto>` | `IRequest<Result<CarDto>>` |
| `throw new NotFoundException()` | `return CarErrors.NotFound;` |
| Query without `.AsNoTracking()` | Always `.AsNoTracking()` in query handlers |
| Business rule inside the handler | Push it into the entity method (unless it needs another aggregate) |
| `RemoveByTagAsync("car", ct)` | `RemoveByTagAsync(CacheTags.Cars, ct)` |
| Cached query with no evicting command | Add the `RemoveByTagAsync` call to every mutating command |
| `await context.Cars.FindAsync(id)` inside a `foreach` | One `Where(x => ids.Contains(x.Id))` |
| Injecting another handler | Send through `ISender`, or raise a domain event |
| Returning `Result<Car>` | Return `Result<CarDto>` |
| Naming a state-mutating request `...Query` | Name it `...Command` |
| Registering a validator manually | `AddValidatorsFromAssembly` already did |
| A password, token or stream request without `ISensitiveRequest` | Implement it; `SensitiveRequestTests` will fail otherwise |
| Adding an open-generic pre-processor and expecting the scan to find it | Call `AddOpenRequestPreProcessor` explicitly |
| Implementing an interface in Infrastructure that needs `HttpContext` | Put it in `Taxi.Api/Services/` |
| Mapping a Contracts request inside a handler | Map in the controller |
| Deleting a stored file before the row commits | Commit first, then delete (§6) |

---

## 20. Future Extensions — NOT IMPLEMENTED

> ⚠️ **None of the following exists in this repository.** Sketches only.

### 20.1 Authorization behaviour

Today authorization is named policies (`Common/Security/Policies.cs`) applied with
`[Authorize(Policy = ...)]` on controllers. If a rule ever needs to live with the use case rather
than the endpoint (for example "only the owning driver may cancel this ride"), add a behaviour
rather than scattering checks through handlers.

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

Notes: `next` takes the `CancellationToken` in MediatR 14; the user id comes from `IUser`;
`IsInRoleAsync` / `AuthorizeAsync` are the real `IIdentityService` signatures; the `(dynamic)` cast
needs a `List<Error>`. You would also add `Unauthorized` and `Forbidden` to `ApplicationErrors`
with matching `LocalizationKeys`, and register the behaviour **before** `ValidationBehavior`
(fail authorization before spending effort on validation), then update
`PipelineRegistrationTests`.

### 20.2 SQL-side bilingual projection

Current mapping materialises the entity then picks a language in memory. For a very large list
endpoint that becomes wasteful, since both JSONB values cross the wire. EF Core can push the choice
into SQL:

```csharp
var language = this.languageContext.Language;

var cars = await this.context.Cars
    .AsNoTracking()
    .Select(c => new CarDto(
        c.Id,
        c.Make,
        c.Model,
        c.Year,
        language == Languages.Ar ? c.Description.Ar : c.Description.En,
        c.ImageKey != null))
    .ToListAsync(cancellationToken);
```

Npgsql translates the owned-JSON member access into a `->>` extraction. Verify the generated SQL
before adopting it (Serilog logs EF commands at Information level): if it evaluates client-side
you have gained nothing. If you switch, do it for every list endpoint at once so there is one
convention.

### 20.3 Transactions across several `SaveChanges`

Every handler performs exactly one `SaveChangesAsync`, which EF Core already wraps in a
transaction. If a use case ever needs two saves atomically, do **not** add a transaction behaviour
to the whole pipeline: take the transaction explicitly in that one handler, which would require
widening `IAppDbContext` (for example with a `Database` accessor). Prefer restructuring so one save
suffices.
