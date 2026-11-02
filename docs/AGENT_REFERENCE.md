# Agent Reference

The long-form half of [AGENTS.md](../AGENTS.md). `AGENTS.md` is loaded at the start of every agent
chat, so it carries only the rules and the feature checklist. This file holds the detail behind
them and is read **on demand**: open the one section your task needs, not the whole file.

Section numbers are stable and cited from other documents ("AGENT_REFERENCE.md §6").

| § | Read it when |
|---|---|
| 4 | you are deciding where a validation rule goes |
| 5 | you are returning an error or choosing an HTTP status |
| 6 | you are adding or touching a cached query |
| 7 | a field has an English and an Arabic value |
| 8 | you want the real file to copy for something specific |
| 9 | you need the exact order of middleware, behaviours and interceptors |
| 11 | something looks like a bug and might be deliberate |
| 12 | you hit a known gap, or want to know what is knowingly unfinished |

---

## 4. Validation: three tiers, three jobs

| Tier | Where | Enforces | Fails as |
|---|---|---|---|
| 1. Contract | `Contracts/Requests/**`: `DataAnnotations` | Shape of the payload: required, max length, format | 400 from `InvalidModelStateResponseFactory`, **before** MediatR runs |
| 2. Application | `Features/**/<UseCase>Validator.cs`: FluentValidation | Ranges, cross-field rules, anything needing more than an attribute | 400 `ValidationProblemDetails` via `ValidationBehavior`, keyed by property |
| 3. Domain | `Domain/**/<Entity>.cs` guards | Invariants that must hold no matter who calls | `Result` failure carrying an `Error` from `<Entity>Errors` |

Tiers 2 and 3 **deliberately overlap**: `CreateCarCommandValidator` and `Car.Create` both check
make/model/year/description. That is intentional: the validator produces good per-field HTTP
errors, and the domain guard means the entity cannot be constructed invalid even from a seeder, a
background job, or a future caller that skips MediatR. Keep both.

Both tier 1 and tier 2 use localization keys as the message:

```csharp
// Tier 1: Contracts
[Required(ErrorMessage = LocalizationKeys.Validation.MakeRequired)]

// Tier 2: Application
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
Error.ValidationForProperty(propertyName, code)     // 400, keyed by field (ValidationBehavior and Identity mapping only)
Error.NotFound(code, description)                   // 404
Error.Conflict(code, description)                   // 409
Error.Unauthorized(code, description)               // 401
Error.Forbidden(code, description)                  // 403
Error.Failure / Error.Unexpected                    // 500
```

**Which file does an error live in?**

| Failure | Goes in |
|---|---|
| Belongs to one entity: "a car must have a make", "car not found" | `src/Taxi.Domain/<Plural>/<Entity>Errors.cs` |
| Belongs to a use case with no single entity behind it: "refresh token expired", "invalid credentials" | `src/Taxi.Application/Common/Errors/ApplicationErrors.cs` |

**Fixed vs parametrized.** A fixed error is a `static readonly` field. An error that formats
arguments must be a **method**, because it constructs a new value per call:

```csharp
public static readonly Error MakeRequired = Error.Validation(
    code: LocalizationKeys.Car.MakeRequired,
    description: "Car make is required.");

public static Error TooManyCertifications(int max) => Error.Validation(
    code: LocalizationKeys.Driver.TooManyCertifications,
    description: $"A driver can hold at most {max} certifications.",
    args: max);
```

`Code` is always a `LocalizationKeys` constant; the localizer looks it up in
`SharedResource.{culture}.json`. `Description` is the English fallback used only when the key is
missing (and in Development that also logs a warning). `Args` feeds parametrized messages
(`localizer[code, args]`), matching `{0}`-style placeholders in the JSON.

Translation happens in exactly one place: `src/Taxi.Api/Extensions/ProblemExtensions.cs`. If every
error in the list is `ErrorKind.Validation`, it emits a `ValidationProblemDetails`; otherwise it
emits a `ProblemDetails` built from `errors[0]`.

**Statuses that do not come from a `Result<T>`:**

| Status | Source |
|---|---|
| 401 / 403 | Authentication / authorization (fallback policy included); plain English `title` |
| 409 | `DbUpdateConcurrencyException` (`xmin`), or `[Idempotent]` while the first request runs |
| 413 | Request body over the limit (1 MB default, 5 MB for the image upload) |
| 422 | `[Idempotent]` key reused with a different body |
| 429 | Rate limiter (`Retry-After` header) |
| 500 | `GlobalExceptionHandler`; exception detail only in Development |

---

## 6. Caching: opt in, and always pair it with invalidation

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
the handler and stores the result **only when `IResult.IsSuccess`**: failures are never cached.

**Cache only when all three hold:**

1. the data is read far more often than written,
2. staleness up to `Expiration` is acceptable, and
3. **every command that mutates the data calls `RemoveByTagAsync` with the same tag.**

Rule 3 is not optional. Tags come from
[`CacheTags`](../src/Taxi.Application/Common/Caching/CacheTags.cs); never write the string inline.

```csharp
// query
public string[] Tags => [CacheTags.Cars];

// every command that touches cars
await this.cache.RemoveByTagAsync(CacheTags.Cars, cancellationToken);
```

Cached today:

| Queries | Tag | Evicted by |
|---|---|---|
| `GetCarsQuery`, `GetCarByIdQuery` (v1), `GetCarDetailsQuery`, `GetCarDetailsByIdQuery` (v2; `IsCultureAware = false`) | `Cars` | `CreateCar`, `UpdateCar`, `RemoveCar`, `UploadCarImage`, `RemoveCarImage` |
| `GetDriversQuery`, `GetDriverByIdQuery` | `Drivers` | all seven driver commands |

**`GetUserByIdQuery` is deliberately not cached.** It returns roles and claims, nothing evicts
`CacheTags.UserInfo`, and a stale role set after a permission change is the wrong trade. If you add
a user-mutating command, add the eviction and the caching together. Token issuance and refresh are
never cached: they mutate state and their results are single-use.

With Redis configured, other instances can serve their in-process copy for up to 30 seconds after
an eviction (no backplane). Details: [Application §9](../src/Taxi.Application/Application_Layer_Blueprint.md).

---

## 7. Bilingual data

Storage and retrieval are two separate mechanisms; do not confuse them.

- **Entity field:** `public LocalizedText Description { get; private set; }`, constructed inside
  the factory: `new LocalizedText(descriptionEn.Trim(), descriptionAr.Trim())`.
- **Persistence:** one JSONB column, via
  `builder.OwnsOne(c => c.Description, d => { d.ToJson(); ... })`.
- **Request:** two flat properties, `DescriptionEn` / `DescriptionAr`.
- **Response:** normally one resolved string. The mapper picks the language:

  ```csharp
  public static CarDto ToDto(this Car car, string language = Languages.Default)
      => new(car.Id, car.Make, car.Model, car.Year,
             language == Languages.Ar ? car.Description.Ar : car.Description.En,
             car.ImageKey is not null);
  ```

  A DTO for a client that edits both languages returns a `LocalizedTextDto(En, Ar)` instead
  (`CarDetailsDto`, API v2).
- **Language source:** inject `ILanguageContext` into the handler and pass
  `this.languageContext.Language` into the mapper. `LanguageContext` reads the culture that
  `UseRequestLocalization` resolved from `Accept-Language`, falling back to `Languages.Default`.
- **Never** hard-code `"en"` / `"ar"`. Use `Languages.En`, `Languages.Ar`, `Languages.Default`,
  `Languages.All`.

Mapping is done **in memory after materialisation**: the handler loads the entity, then calls
`ToDto(language)`. There is no SQL-side JSONB projection in this codebase today.

> ⚠️ **EF Core cannot nest `OwnsOne(...).ToJson()` inside an `OwnsMany(...).ToTable(...)`.** If a
> `LocalizedText` lives on a *child collection* item, map the two languages as flat columns
> instead; the entity still exposes a `LocalizedText`:
>
> ```csharp
> certification.OwnsOne(c => c.Name, name =>
> {
>     name.Property(n => n.En).HasColumnName("NameEn").HasMaxLength(100).IsRequired();
>     name.Property(n => n.Ar).HasColumnName("NameAr").HasMaxLength(100).IsRequired();
> });
> ```
>
> Details in the [Infrastructure blueprint §6](../src/Taxi.Infrastructure/Infrastructure_Layer_Blueprint.md).

---

## 8. Templates: open the real file and copy its shape

These files **are** the templates. Read the current source rather than a copy that can drift.

| Writing | Copy from |
|---|---|
| entity | `src/Taxi.Domain/Cars/Car.cs` |
| entity errors | `src/Taxi.Domain/Cars/CarErrors.cs` |
| request contract | `src/Taxi.Contracts/Requests/Cars/CreateCarRequest.cs` |
| query-string request + sort whitelist | `Requests/Cars/GetCarsRequest.cs` + `CarSortFields.cs` |
| command + handler + validator | `src/Taxi.Application/Features/Cars/Commands/CreateCar/` |
| update command (loads, mutates, evicts) | `src/Taxi.Application/Features/Cars/Commands/UpdateCar/` |
| delete command (no validator) | `src/Taxi.Application/Features/Cars/Commands/RemoveCar/` |
| cached query + handler | `src/Taxi.Application/Features/Cars/Queries/GetCarById/` |
| paged, filtered, sorted query | `Features/Cars/Queries/GetCars/` + `CarQueryExtensions.cs` |
| uncached query | `src/Taxi.Application/Features/Identity/Queries/GetUserInfo/` |
| second API version of a read | `Features/Cars/Queries/GetCarDetails/`, `CarDetailsDto`, `CarsController.GetCarsV2` |
| DTO + mapper | `src/Taxi.Application/Features/Cars/Dtos/`, `.../Mappers/` |
| EF configuration | `src/Taxi.Infrastructure/Data/Configurations/CarConfiguration.cs` |
| controller | `src/Taxi.Api/Controllers/CarsController.cs` |
| domain unit tests | `tests/Taxi.Domain.UnitTests/Cars/CarTests.cs` |
| handler unit tests with fakes | `tests/Taxi.Application.UnitTests/Features/Cars/CarCommandHandlerTests.cs`, `TestDoubles/` |
| integration test | `tests/Taxi.Api.IntegrationTests/Features/CarsTests.cs` |
| aggregate with a child collection | `src/Taxi.Domain/Drivers/Driver.cs` + `DriverCertification.cs` |
| owned collection mapping (flat `LocalizedText` columns) | `src/Taxi.Infrastructure/Data/Configurations/DriverConfiguration.cs` |
| relationship by id + cross-aggregate rule | `src/Taxi.Application/Features/Drivers/Commands/AssignDriverCar/` |
| domain event + handler | `src/Taxi.Domain/Drivers/Events/` + `src/Taxi.Application/Features/Drivers/EventHandlers/` |
| sub-resource routes | `src/Taxi.Api/Controllers/DriversController.cs` (`/{id}/certifications`, `/{id}/car`) |
| file upload with compensation | `Features/Cars/Commands/UploadCarImage/` |
| idempotent `POST` | `CarsController.CreateCar` (`[Idempotent]`) |
| anonymous flow that sends email | `Features/Account/Commands/Register/` + `Emails/AccountEmails.cs` |

**Not visible in those files: read before you copy.**

- `Car` has **no** `Delete()`. Removal is `context.Cars.Remove(car)` in the handler. Add a domain
  method only when deletion carries a rule the entity can evaluate.
- `Car` is **soft-deleted**: it implements `ISoftDeletable`, `SoftDeleteInterceptor` turns `Remove`
  into an update stamping `IsDeleted` / `DeletedAtUtc` / `DeletedBy`, and a global query filter
  hides the row everywhere. Implement the interface on a new entity to get the same. `Driver` is
  hard-deleted.
- `RemoveCarCommand` has **no validator**, and that is legal: `ValidationBehavior` accepts a null
  validator. Skip it when a route constraint (`{id:guid}`) already guarantees the input.
- `CreatedAtAction` **must** pass `version = this.RequestedApiVersion` in the route values, or URL
  generation fails against the `api/v{version:apiVersion}/…` template. Never a literal like
  `"1.0"`; it breaks once v2 exists. Pass the scalar id, never the DTO.
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
  → UseForwardedHeaders               Program.cs, before everything
  → UseRequestLocalization            resolves Accept-Language → CultureInfo
  → RequestLogContextMiddleware       pushes CorrelationId into Serilog
  → SecurityHeadersMiddleware         nosniff, frame-deny, CSP, referrer policy …
  → UseExceptionHandler               GlobalExceptionHandler → problem+json
  → UseStatusCodePages, UseHttpsRedirection, UseSerilogRequestLogging
  → UseCors → UseAuthentication → UseRateLimiter → UseAuthorization
  → controller action                 [Idempotent] filter on creating POSTs
  → ISender.Send(command|query)
      → LoggingBehaviour              IRequestPreProcessor; runs first; logs name + user id + payload
      → UnhandledExceptionBehaviour   logs and rethrows; outermost behaviour so nothing escapes it
      → ValidationBehavior            FluentValidation; short-circuits to a Result failure
      → PerformanceBehaviour          warns > 500 ms
      → CachingBehavior               ICachedQuery only; returns cached value on hit
      → Handler                       domain call + IAppDbContext + SaveChangesAsync
          → AppDbContext.SaveChangesAsync, interceptors in registration order:
              → SoftDeleteInterceptor turns deletes of ISoftDeletable into IsDeleted updates
              → AuditableEntityInterceptor stamps Created*/LastModified*
              → OutboxInterceptor writes domain events as OutboxMessages (same transaction)
              → PostgreSQL
  → Result<T>
  → result.Match(Ok/Created/NoContent, Problem)
  → ProblemExtensions                 Error → localized RFC 7807

later, in the background (not in the request):
  OutboxBackgroundService → OutboxProcessor → INotificationHandler<TEvent>   (at least once)
```

Behaviour order comes from the registration order in
`src/Taxi.Application/DependencyInjection.cs`, and
[`PipelineRegistrationTests`](../tests/Taxi.Application.UnitTests/Common/PipelineRegistrationTests.cs)
fails the build if it changes. Two rules are load-bearing: `UnhandledExceptionBehaviour` is
outermost, so no other behaviour's exception goes unlogged, and `ValidationBehavior` precedes
`CachingBehavior`, so invalid input never reaches the cache.

`LoggingBehaviour` is an open-generic `IRequestPreProcessor` and needs an explicit
`AddOpenRequestPreProcessor` call; MediatR's assembly scan does not find it.

---

## 11. Deliberate decisions: do not "fix" these

Each is documented where it is implemented. An agent that "fixes" one undoes a decision made on
purpose.

| Looks like a bug | Why it is not | Where |
|---|---|---|
| `Taxi.Domain` references `Taxi.Contracts` | Error codes are `LocalizationKeys`; the alternatives are worse | Domain §2 |
| Public setters on `CreatedAtUtc`, `IsDeleted`, … | Interceptors stamp them; the convention test allows exactly these | Domain §4 |
| `GetUserByIdQuery` is not cached | Stale roles are the wrong trade | §6 |
| Refresh-token failures are 409, not 401 | Existing client contract | Application §18 |
| Register / forgot-password / resend answer 202 for unknown accounts | No account enumeration | Api §6 |
| Login answers one 401 for unknown email, wrong password and lockout | No account enumeration | Infrastructure §10 |
| The development JWT secret and seed passwords are committed | Public local placeholders | README |
| `/metrics` is anonymous | Scrape target on the compose network; protect at the proxy | Api §21 |
| Exception detail is hidden outside Development | Messages leak paths and connection strings | Api §8 |
| Domain events are not handled inside the request | Transactional outbox: after commit, in the background, at least once | Infrastructure §8 |
| `ApplyMigrationsOnStartup` is `true` in `appsettings.Production.json` | Self-contained single-container deployment; set it `false` to migrate in a pipeline | Infrastructure §9 |
| Mapping to DTOs happens in memory | No SQL-side JSONB projection yet | Application §10 |
| `IdentityController` / `AccountController` are version-neutral | A client that cannot sign in cannot negotiate a version | Api §21 |
| `Result<T>` has an obsolete public constructor | The serializer needs it so cached results round-trip | Domain §8 |
| `PerformanceBehaviour` hits the database for the user name | Only for requests already over 500 ms | Application §8 |

---

## 12. Known gaps (true today; do not rely on them, do not silently "fix" them)

| Gap | Detail |
|---|---|
| Year message | `Validation.Year.Invalid` reads "between 1886 and 2026" (static text) while the validator allows up to the current year + 2; the domain guard only checks `year >= 1886` |
| Unused errors | `ApplicationErrors.CarNotFound`, `InvalidRefreshToken` and `TokenGenerationFailed` are unreferenced; prefer `CarErrors.NotFound` and `RefreshTokenErrors.ExpiryInvalid` |
| Unused setting | `AppSettings:DefaultLanguage` is bound but never read; the default is `Languages.Default` |
| Data Protection | Key ring is not persisted or shared, so email/reset tokens may not survive a restart or a second instance |
| Stale samples | `src/Taxi.Api/Taxi.Api.http` and `Identity.http` are older than `requests/requests.http` (for example `admin@localhost`); extend only the latter |
| Grafana | Starts with no data source or dashboard provisioned |
| Production logs | The Seq sink exists only in `appsettings.Development.json` |
| Password policy | Deliberately relaxed (6 characters, nothing else required); tighten before production |
