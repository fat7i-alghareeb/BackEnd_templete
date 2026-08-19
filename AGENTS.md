# AGENTS.md — Taxi_Server

**Read this file first. It is the pattern.** Every rule below is derived from code that exists in
this repository, not from generic Clean Architecture advice.

The **`Car` feature is the reference implementation.** When adding anything, open the matching
`Car` file and copy its shape. If this document and the code ever disagree, the code wins — and
then fix this document.

| Also | Where |
|---|---|
| Map of every doc | [docs/NAVIGATION.md](docs/NAVIGATION.md) |
| System overview | [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) |
| Route / verb / status rules | [docs/RESTful_Naming_Constitution.md](docs/RESTful_Naming_Constitution.md) |

---

## 0. When this file is not enough

This file covers an ordinary feature end to end. Open a blueprint when you hit one of these:

| If you are… | Also read |
|---|---|
| adding a plain entity with CRUD endpoints | nothing — this file is enough |
| adding a **localized child collection** | [Infrastructure §5 Entity configuration](src/Taxi.Infrastructure/Infrastructure_Layer_Blueprint.md) — `OwnsMany` + `ToJson` is unsupported |
| adding or reordering a **pipeline behaviour** | [Application §7 Pipeline behaviours](src/Taxi.Application/Application_Layer_Blueprint.md) |
| adding a **domain event** | [Domain §11](src/Taxi.Domain/Domain_Layer_Blueprint.md) + [Application §15](src/Taxi.Application/Application_Layer_Blueprint.md) |
| declaring a **new Application interface** | [Application §13 Talking to other layers](src/Taxi.Application/Application_Layer_Blueprint.md) — decides Infrastructure vs Api for the implementation |
| touching **middleware order** or OpenAPI | [Api §6, §8](src/Taxi.Api/Api_Layer_Blueprint.md) |
| touching **tracing, metrics or log sinks** | [Api §10 Observability](src/Taxi.Api/Api_Layer_Blueprint.md) |
| touching **auth, JWT, seeding, migrations** | [Infrastructure §7, §8](src/Taxi.Infrastructure/Infrastructure_Layer_Blueprint.md) |
| adding **pagination** | [Contracts Future Extensions](src/Taxi.Contracts/Contracts_Layer_Blueprint.md) — nothing paginates yet; the envelope shape is specified there |
| touching the **Blazor client** | [Client blueprint](src/Taxi.Client/Client_Layer_Blueprint.md) — it is a scaffold, not a working app |
| seeing behaviour you cannot explain | that layer's blueprint — deliberate design decisions are documented there before you assume a bug |

---

## 1. Layer map

```text
                 ┌──────────────┐
                 │ Taxi.Api     │  controllers, middleware, OpenAPI, composition root
                 └──────┬───────┘
        ┌───────────────┼────────────────┬──────────────┐
        ▼               ▼                ▼              ▼
┌───────────────┐ ┌─────────────┐ ┌──────────────┐ ┌───────────┐
│Taxi.Infra-    │ │Taxi.Applica-│ │Taxi.Contracts│ │Taxi.Client│
│structure      │─▶│tion         │ │              │ │(WASM shell│
│EF, Identity,  │ │CQRS, MediatR│ │requests, keys│ │ unhosted) │
│JWT, cache     │ │behaviours   │ │              │ └─────┬─────┘
└───────────────┘ └──────┬──────┘ └──────▲───────┘       │
                         ▼               │               │
                  ┌─────────────┐        │               │
                  │ Taxi.Domain │────────┘               │
                  │ entities,   │◀───────────────────────┘
                  │ Result<T>   │        (Client → Contracts only)
                  └─────────────┘
```

Actual project references:

| Project | References |
|---|---|
| `Taxi.Contracts` | *nothing* |
| `Taxi.Domain` | `Taxi.Contracts` |
| `Taxi.Application` | `Taxi.Domain` |
| `Taxi.Infrastructure` | `Taxi.Application` |
| `Taxi.Api` | `Application`, `Contracts`, `Infrastructure`, `Client` |
| `Taxi.Client` | `Taxi.Contracts` |

`Domain → Contracts` is real and intentional: `CarErrors` uses `LocalizationKeys` for its error
codes, and Contracts is the only project the Domain, the API and the Client can all see.
**Do not "fix" it** — the reasoning and every rejected alternative are in
[Domain blueprint §2](src/Taxi.Domain/Domain_Layer_Blueprint.md).

**Never** add a reference that reverses one of these arrows.

Tests live in `tests/Taxi.Domain.UnitTests` and `tests/Taxi.Application.UnitTests`.

---

## 2. Hard rules

### Domain

- **MUST** make every entity constructor `private`. Creation goes through
  `public static Result<T> Create(...)`; mutation goes through named behaviour methods returning
  `Result<Updated>`.
- **MUST** keep a private parameterless constructor for EF Core materialisation.
- **MUST** use `private set` (or no setter) on entity properties.
- **NEVER** throw for a business-rule failure. Return an `Error` — `Result<T>` has an implicit
  conversion from `Error` and from `List<Error>`, so `return CarErrors.NotFound;` compiles.
- **MUST** put every entity's errors in a sibling `<Entity>Errors.cs` as
  `public static readonly Error` fields.
- **MUST** back every `Error.Code` with a `LocalizationKeys` constant. Never a raw string.
- **MUST** use `LocalizedText` for any translatable field. Never `NameEn` / `NameAr` columns.
- **NEVER** reference EF Core, ASP.NET, MediatR handlers, or `HttpContext` from Domain.
  (`DomainEvent : INotification` is the single sanctioned MediatR touchpoint.)

### Contracts

- **MUST** be plain classes/records with `DataAnnotations` and no methods.
- **MUST** write `[Required(ErrorMessage = LocalizationKeys.Validation.X)]` — the `ErrorMessage`
  **is** the localization key, never English prose.
- **NEVER** reference Domain or Application types from Contracts.

### Application

- **MUST** give each use case its own folder: `Features/<Plural>/Commands/<UseCase>/` or
  `Features/<Plural>/Queries/<UseCase>/`, one type per file.
- **MUST** return `Result<T>` from every handler. Commands that produce nothing return
  `Result<Updated>` or `Result<Deleted>`.
- **MUST** name anything that writes a `Command`, even when it is reached by a `GET`-shaped idea.
  Token issuance is `GenerateTokenCommand` because it rotates the refresh token.
- **MUST** access the database only through `IAppDbContext`.
- **MUST** use `.AsNoTracking()` in query handlers.
- **MUST** map to a DTO before returning. Never return an entity.
- **NEVER** reference `HttpContext`, `IActionResult`, or Infrastructure types.

### Infrastructure

- **MUST** implement Application interfaces; never define the business abstraction there.
- **MUST** map entities via `IEntityTypeConfiguration<T>` in `Data/Configurations/` —
  `ApplyConfigurationsFromAssembly` picks them up automatically.
- **MUST** map `LocalizedText` with `builder.OwnsOne(x => x.Field, b => b.ToJson())`.

### API

- **MUST** keep controller actions to: build the command/query, `await sender.Send(...)`,
  `return result.Match(success, this.Problem);`. Nothing else. No loops, no `if` on business
  state, no logging, no data access.
- **MUST** bind a **Contracts** request type and map it to the command by hand. Never bind an
  Application command straight from `[FromBody]`.
- **MUST** inherit from `ApiController`, which supplies `Problem(List<Error>)`.
- **NEVER** inject `AppDbContext`, `UserManager<AppUser>`, or any Infrastructure type into a
  controller.
- **NEVER** write a `try/catch` in a controller.

### Everywhere

- **MUST** prefix instance members with `this.` — StyleCop is on solution-wide.
- **MUST** assign primary-constructor parameters to `private readonly` fields (the house style).
- **MUST** take the current user id from `IUser`, never from a request payload, and never from
  `HttpContext` outside `Taxi.Api`.
- **MUST** add new NuGet packages to `Directory.Packages.props` (with `Version`) and reference
  them in the `.csproj` **without** a `Version` attribute — central package management is on.
- **MUST** add every new localization key to **both** `SharedResource.en.json` **and**
  `SharedResource.ar.json`. A missing key silently degrades to the English `Description`.

---

## 3. Add a feature end-to-end

Work outside-in through this list. Each row names the real `Car` file to copy.

| # | Create / edit | Copy from |
|---|---|---|
| 1 | `src/Taxi.Contracts/Common/LocalizationKeys.cs` — add a nested `static class <Entity>` + keys | existing `Car` class |
| 2 | `src/Taxi.Api/Resources/SharedResource.en.json` **and** `.ar.json` — add matching entries | existing `Car.*` entries |
| 3 | `src/Taxi.Contracts/Requests/<Plural>/Create<X>Request.cs` (+ `Update<X>Request.cs`) | `Requests/Cars/CreateCarRequest.cs` |
| 4 | `src/Taxi.Domain/<Plural>/<X>.cs` | `Domain/Cars/Car.cs` |
| 5 | `src/Taxi.Domain/<Plural>/<X>Errors.cs` | `Domain/Cars/CarErrors.cs` |
| 6 | `src/Taxi.Application/Features/<Plural>/Dtos/<X>Dto.cs` | `Features/Cars/Dtos/CarDto.cs` |
| 7 | `src/Taxi.Application/Features/<Plural>/Mappers/<X>Mapper.cs` | `Features/Cars/Mappers/CarMapper.cs` |
| 8 | `.../Commands/Create<X>/{Create<X>Command,Create<X>CommandHandler,Create<X>CommandValidator}.cs` | `Features/Cars/Commands/CreateCar/` |
| 9 | `.../Queries/Get<X>ById/{Get<X>ByIdQuery,Get<X>ByIdQueryHandler,Get<X>ByIdQueryValidator}.cs` | `Features/Cars/Queries/GetCarById/` |
| 10 | `src/Taxi.Application/Common/Caching/CacheTags.cs` — add a tag if you will cache | existing `Cars` constant |
| 11 | `src/Taxi.Application/Common/Interfaces/IAppDbContext.cs` — add `DbSet<X> <Plural> { get; }` | existing `Cars` property |
| 12 | `src/Taxi.Infrastructure/Data/AppDbContext.cs` — add `public DbSet<X> <Plural> => this.Set<X>();` | existing `Cars` property |
| 13 | `src/Taxi.Infrastructure/Data/Configurations/<X>Configuration.cs` | `Data/Configurations/CarConfiguration.cs` |
| 14 | `dotnet ef migrations add Add<X> -p src/Taxi.Infrastructure -s src/Taxi.Api` | — |
| 15 | `src/Taxi.Api/Controllers/<Plural>Controller.cs` | `Controllers/CarsController.cs` |
| 16 | `tests/Taxi.Domain.UnitTests/<Plural>/<X>Tests.cs` — cover every guard | `Cars/CarTests.cs` |
| 17 | `requests/requests.http` — add calls for the new endpoints | existing car calls |

Steps 8 and 9 are per use case — repeat for `Update`, `Remove`, `GetAll`, etc.

Naming is mechanical and non-negotiable:

```text
<UseCase>Command.cs          CreateCarCommand.cs
<UseCase>CommandHandler.cs   CreateCarCommandHandler.cs
<UseCase>CommandValidator.cs CreateCarCommandValidator.cs
<UseCase>Query.cs            GetCarByIdQuery.cs
<UseCase>QueryHandler.cs     GetCarByIdQueryHandler.cs
<UseCase>QueryValidator.cs   GetCarByIdQueryValidator.cs
```

A validator is **optional** — `RemoveCarCommand` has none, and `ValidationBehavior` accepts a null
validator and passes straight through. Add one whenever the request carries anything beyond an id
that a route constraint already guarantees.

---

## 4. Validation — three tiers, three jobs

| Tier | Where | Enforces | Fails as |
|---|---|---|---|
| 1. Contract | `Contracts/Requests/**` — `DataAnnotations` | Shape of the payload: required, max length, format | 400 from `InvalidModelStateResponseFactory`, **before** MediatR runs |
| 2. Application | `Features/**/<UseCase>Validator.cs` — FluentValidation | Ranges, cross-field rules, anything needing more than an attribute | 400 `ValidationProblemDetails` via `ValidationBehavior`, keyed by property |
| 3. Domain | `Domain/**/<Entity>.cs` guards | Invariants that must hold no matter who calls | `Result` failure carrying an `Error` from `<Entity>Errors` |

Tiers 2 and 3 **deliberately overlap** — `CreateCarCommandValidator` and `Car.Create` both check
make/model/year/description. That is intentional: the validator produces good per-field HTTP
errors, and the domain guard means the entity cannot be constructed invalid even from a seeder, a
background job, or a future caller that skips MediatR. Keep both.

Both tier 1 and tier 2 use localization keys as the message:

```csharp
// Tier 1 — Contracts
[Required(ErrorMessage = LocalizationKeys.Validation.MakeRequired)]

// Tier 2 — Application
RuleFor(x => x.Make).NotEmpty().WithMessage(LocalizationKeys.Validation.MakeRequired);
```

`ValidationBehavior` forwards `e.ErrorMessage` into `Error.Code` and `e.PropertyName` into
`Error.PropertyName`, and `ProblemExtensions` translates the code and keys the RFC 7807 `errors`
dictionary by the property.

---

## 5. Errors and HTTP status

Failure travels as `Error` (a `readonly record struct`) inside `Result<T>`. Never as an exception.

```csharp
Error.Validation(code, description)                 // 400
Error.ValidationForProperty(propertyName, code)     // 400, keyed by field (ValidationBehavior only)
Error.NotFound(code, description)                   // 404
Error.Conflict(code, description)                   // 409
Error.Unauthorized(code, description)               // 401
Error.Forbidden(code, description)                  // 403
Error.Failure / Error.Unexpected                    // 500
```

**Which file does an error live in?**

| Failure | Goes in |
|---|---|
| Belongs to one entity — "a car must have a make", "car not found" | `src/Taxi.Domain/<Plural>/<Entity>Errors.cs` |
| Belongs to a use case with no single entity behind it — "refresh token expired", "token generation failed" | `src/Taxi.Application/Common/Errors/ApplicationErrors.cs` |

**Fixed vs parametrized.** A fixed error is a `static readonly` field. An error that formats
arguments must be a **method**, because it constructs a new value per call:

```csharp
public static readonly Error MakeRequired = Error.Validation(
    code: LocalizationKeys.Car.MakeRequired,
    description: "Car make is required.");

public static Error TooOld(int year) => Error.Validation(
    code: LocalizationKeys.Car.InvalidYear,
    description: $"Year {year} predates the first automobile.",
    args: year);
```

`Code` is always a `LocalizationKeys` constant — the localizer looks it up in
`SharedResource.{culture}.json`. `Description` is the English fallback used only when the key is
missing (and in Development that also logs a warning). `Args` feeds parametrized messages
(`localizer[code, args]`), matching `{0}`-style placeholders in the JSON.

Translation happens in exactly one place: `src/Taxi.Api/Extensions/ProblemExtensions.cs`. If every
error in the list is `ErrorKind.Validation`, it emits a `ValidationProblemDetails`; otherwise it
emits a `ProblemDetails` built from `errors[0]`.

---

## 6. Caching — opt in, and always pair it with invalidation

A query caches by implementing `ICachedQuery<TResponse>`:

```csharp
public interface ICachedQuery
{
    string CacheKey { get; }
    string[] Tags { get; }
    TimeSpan Expiration { get; }
    bool IsCultureAware => true;   // default: key is suffixed with the request language
}
```

`CachingBehavior` intercepts any request implementing it, builds
`IsCultureAware ? $"{CacheKey}_{language}" : CacheKey`, probes `HybridCache`, and on a miss runs
the handler and stores the result **only when `IResult.IsSuccess`** — failures are never cached.

**Cache only when all three hold:**

1. the data is read far more often than written,
2. staleness up to `Expiration` is acceptable, and
3. **every command that mutates the data calls `RemoveByTagAsync` with the same tag.**

Rule 3 is not optional. Tags come from
[`CacheTags`](src/Taxi.Application/Common/Caching/CacheTags.cs); never write the string inline.

```csharp
// query
public string[] Tags => [CacheTags.Cars];

// every command that touches cars
await this.cache.RemoveByTagAsync(CacheTags.Cars, cancellationToken);
```

Cached today: `GetCarsQuery` and `GetCarByIdQuery`, both invalidated by all three Car commands.

**`GetUserByIdQuery` is deliberately not cached** — it returns roles and claims, nothing evicts
`CacheTags.UserInfo`, and a stale role set after a permission change is the wrong trade. If you add
a user-mutating command, add the eviction and the caching together.

Token issuance and refresh are never cached: they mutate state and their results are single-use.

---

## 7. Bilingual data

Storage and retrieval are two separate mechanisms; do not confuse them.

- **Entity field:** `public LocalizedText Description { get; private set; }`, constructed inside
  the factory: `new LocalizedText(descriptionEn.Trim(), descriptionAr.Trim())`.
- **Persistence:** one JSONB column, via
  `builder.OwnsOne(c => c.Description, d => { d.ToJson(); ... })`.
- **Request:** two flat properties, `DescriptionEn` / `DescriptionAr`.
- **Response:** one resolved string. The mapper picks the language:

  ```csharp
  public static CarDto ToDto(this Car car, string language = Languages.Default)
      => new(car.Id, car.Make, car.Model, car.Year,
             language == Languages.Ar ? car.Description.Ar : car.Description.En);
  ```

- **Language source:** inject `ILanguageContext` into the handler and pass
  `this.languageContext.Language` into the mapper. `LanguageContext` reads the culture that
  `UseRequestLocalization` resolved from `Accept-Language`, falling back to `Languages.Default`.
- **Never** hard-code `"en"` / `"ar"`. Use `Languages.En`, `Languages.Ar`, `Languages.Default`,
  `Languages.All`.

Mapping is done **in memory after materialisation** — the handler loads the entity, then calls
`ToDto(language)`. There is no SQL-side JSONB projection in this codebase today.

> ⚠️ **EF Core cannot nest `OwnsOne(...).ToJson()` inside an `OwnsMany(...).ToTable(...)`.** If a
> `LocalizedText` lives on a *child collection* item, map the two languages as flat columns
> instead — the entity still exposes a `LocalizedText`:
>
> ```csharp
> items.OwnsOne(i => i.Label, label =>
> {
>     label.Property(l => l.En).HasColumnName("LabelEn");
>     label.Property(l => l.Ar).HasColumnName("LabelAr");
> });
> ```
>
> Details in the [Infrastructure blueprint](src/Taxi.Infrastructure/Infrastructure_Layer_Blueprint.md).

---

## 8. Templates — open the real file and copy its shape

These files **are** the templates. Read the current source rather than a copy that can drift.

| Writing | Copy from |
|---|---|
| entity | `src/Taxi.Domain/Cars/Car.cs` |
| entity errors | `src/Taxi.Domain/Cars/CarErrors.cs` |
| request contract | `src/Taxi.Contracts/Requests/Cars/CreateCarRequest.cs` |
| command + handler + validator | `src/Taxi.Application/Features/Cars/Commands/CreateCar/` |
| update command (loads, mutates, evicts) | `src/Taxi.Application/Features/Cars/Commands/UpdateCar/` |
| delete command (no validator) | `src/Taxi.Application/Features/Cars/Commands/RemoveCar/` |
| cached query + handler | `src/Taxi.Application/Features/Cars/Queries/GetCarById/` |
| uncached query | `src/Taxi.Application/Features/Identity/Queries/GetUserInfo/` |
| DTO + mapper | `src/Taxi.Application/Features/Cars/Dtos/`, `.../Mappers/` |
| EF configuration | `src/Taxi.Infrastructure/Data/Configurations/CarConfiguration.cs` |
| controller | `src/Taxi.Api/Controllers/CarsController.cs` |
| domain unit tests | `tests/Taxi.Domain.UnitTests/Cars/CarTests.cs` |

**Not visible in those files — read before you copy:**

- `Car` has **no** `Delete()`. Removal is `context.Cars.Remove(car)` in the handler. Add a domain
  method only when deletion carries a rule (soft delete, "cannot delete an assigned car").
- `RemoveCarCommand` has **no validator**, and that is legal — `ValidationBehavior` accepts a null
  validator. Skip it when a route constraint (`{id:guid}`) already guarantees the input.
- `CreatedAtAction` **must** pass `version` in the route values, or URL generation fails against
  the `api/v{version:apiVersion}/…` template. Pass the scalar id, never the DTO.
- The handler supplies the id: `Car.Create(Guid.NewGuid(), ...)`. The entity does not generate it.
- Guards run **before** any assignment, so a rejected `Update` leaves the entity untouched. Keep
  that order.
- Normalisation (`.Trim()`) happens inside the entity, not the handler.
- The primary-constructor-plus-`private readonly`-field style is redundant to the compiler and
  mandatory here.

---

## 9. Request pipeline

```text
HTTP request
  → UseRequestLocalization            resolves Accept-Language → CultureInfo
  → RequestLogContextMiddleware       pushes CorrelationId into Serilog
  → UseExceptionHandler               GlobalExceptionHandler → problem+json
  → routing, CORS, rate limiter, authentication, authorization
  → CarsController action
  → ISender.Send(command|query)
      → LoggingBehaviour              IRequestPreProcessor; logs name + user id
      → UnhandledExceptionBehaviour   logs and rethrows; outermost so nothing escapes it
      → ValidationBehavior            FluentValidation; short-circuits to Result failure
      → PerformanceBehaviour          warns > 500 ms
      → CachingBehavior               ICachedQuery only; returns cached value on hit
      → Handler                       domain call + IAppDbContext + SaveChangesAsync
          → AppDbContext.SaveChangesAsync
              → dispatch domain events via IMediator.Publish
              → AuditableEntityInterceptor stamps Created*/LastModified*
              → PostgreSQL
  → Result<T>
  → result.Match(Ok/Created/NoContent, Problem)
  → ProblemExtensions                 Error → localized RFC 7807
```

Behaviour order comes from the registration order in
`src/Taxi.Application/DependencyInjection.cs`, and
[`PipelineRegistrationTests`](tests/Taxi.Application.UnitTests/Common/PipelineRegistrationTests.cs)
fails the build if it changes. Two rules are load-bearing: `UnhandledExceptionBehaviour` is
outermost so no other behaviour's exception goes unlogged, and `ValidationBehavior` precedes
`CachingBehavior` so invalid input never reaches the cache.

`LoggingBehaviour` is an open-generic `IRequestPreProcessor` and needs an explicit
`AddOpenRequestPreProcessor` call — MediatR's assembly scan does not find it.

---

## 10. Verify your change

```powershell
dotnet build Taxi_Server.slnx -c Debug      # must be clean; StyleCop runs here
dotnet test Taxi_Server.slnx                # 45 tests today
dotnet list package --vulnerable --include-transitive
npx markdownlint-cli2                       # if you touched any .md
docker compose up -d postgres
dotnet run --project src/Taxi.Api           # migrates + seeds on startup outside Production
```

Then exercise the endpoints from `requests/requests.http`, or:

1. `POST /api/token/generate` — `admin@taxi.com` / `Admin123!` → 200 + `TokenResponse`.
2. `POST /api/v1/cars` with the bearer token → 201, `Location: /api/v1/cars/{guid}`.
3. `GET /api/v1/cars/{id}` twice → second call logs `Checking cache for GetCarByIdQuery`.
4. `GET /api/v1/cars` with `Accept-Language: ar`, then `en` → different `description`.
5. `PUT /api/v1/cars/{id}` then `GET /api/v1/cars` → the new value is visible (tag invalidation).
6. Send an invalid payload → 400 `ValidationProblemDetails` keyed by the offending property, with
   the message translated into the requested language.

OpenAPI in Development: `/openapi/v1.json`, Swagger UI at `/swagger`, Scalar at `/scalar/v1`.

`appsettings.json` ships with `ConnectionStrings:DefaultConnection` and `JwtSettings:Secret` blank;
`appsettings.Development.json` carries working local values. For anything else, use user secrets,
environment variables, or `.env` (see `.env.example`).
