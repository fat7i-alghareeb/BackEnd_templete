# Contracts Layer Blueprint — `Taxi.Contracts`

> Start at [AGENTS.md](../../AGENTS.md): it carries the rules and the add-a-feature checklist.
> This file is the deep reference for the Contracts layer.
> Map of all docs: [NAVIGATION.md](../../docs/NAVIGATION.md).

The shared vocabulary: inbound request shapes, the localization key registry, the language
registry, and the sort whitelists that more than one layer needs.

---

## 1. At a glance

| | |
|---|---|
| Project | `src/Taxi.Contracts` |
| References | **nothing** (no projects, no packages) |
| Referenced by | `Taxi.Domain`, `Taxi.Api` directly; Application and Infrastructure transitively |
| Reference code | `Requests/Cars/CreateCarRequest.cs`, `Common/LocalizationKeys.cs` |
| Rules enforced by the build | `tests/Taxi.ArchitectureTests` (see §12) |

---

## 2. Purpose

Two jobs that are less alike than they look:

1. **Wire contracts**: the exact JSON (or query string) shape the API accepts, with surface-level
   `DataAnnotations` so malformed payloads are rejected before MediatR runs.
2. **The shared registry**: `LocalizationKeys`, `Languages`, `SortDirections`, `CarSortFields`.
   These are not DTOs; they are constants Domain, Application and API must agree on. Contracts is
   the only project all of them can reference, so it hosts them.

Job 2 is why `Taxi.Domain` references this project (Domain blueprint §2).

---

## 3. Dependency rules

**References:** nothing. `DataAnnotations` comes from the BCL.

This project must stay the lightest in the solution: it is the assembly a .NET client (a MAUI or
Blazor front end, a test harness) would share with the API. Adding a project reference or a package
here is a rejection.

**Never add:** a reference to Domain, Application, Infrastructure or Api · EF Core · MediatR ·
FluentValidation · ASP.NET Core types · methods on request classes · anything `async`.

---

## 4. Directory structure

```text
src/Taxi.Contracts/
├── Common/
│   ├── Languages.cs               En / Ar / Default / All
│   ├── LocalizationKeys.cs        the key registry (10 nested classes)
│   └── SortDirections.cs          asc / desc
├── Requests/
│   ├── Account/                   Register, ConfirmEmail, ResendConfirmation, ForgotPassword,
│   │                              ResetPassword, ChangePassword
│   ├── Cars/                      CreateCar, UpdateCar, GetCars (query string), CarSortFields
│   ├── Drivers/                   CreateDriver, UpdateDriver, GetDrivers (query string),
│   │                              AddDriverCertification, AssignDriverCar
│   ├── Identity/                  GenerateToken, RefreshToken
│   └── Users/                     SetUserRoles
└── Contracts_Layer_Blueprint.md
```

| Folder | Holds |
|---|---|
| `Common/` | `LocalizationKeys`, `Languages`, `SortDirections`. One type per file. |
| `Requests/<Plural>/` | One class per inbound payload, named `<UseCase>Request`, plus that feature's sort whitelist. |

Every file is live: each request type is bound by a controller action, and every
`LocalizationKeys` constant resolves in both `SharedResource` files. There is no `Responses/`
folder (§8).

---

## 5. Request DTOs

```csharp
using System.ComponentModel.DataAnnotations;
using Taxi.Contracts.Common;

namespace Taxi.Contracts.Requests.Cars;

public class CreateCarRequest
{
    [Required(ErrorMessage = LocalizationKeys.Validation.MakeRequired)]
    [StringLength(100, ErrorMessage = LocalizationKeys.Validation.MakeTooLong)]
    public string Make { get; set; } = string.Empty;

    [Required(ErrorMessage = LocalizationKeys.Validation.YearInvalid)]
    public int Year { get; set; }

    [Required(ErrorMessage = LocalizationKeys.Validation.DescriptionRequired)]
    [StringLength(1000, ErrorMessage = LocalizationKeys.Validation.DescriptionTooLong)]
    public string DescriptionEn { get; set; } = string.Empty;

    // Model, DescriptionAr: same shape
}
```

Everything that matters:

- **`class` with `get; set;`**, not `record`. Model binding wants settable properties.
- **Non-nullable strings default to `string.Empty`** so nullable analysis stays quiet.
- **Every attribute carries `ErrorMessage = LocalizationKeys.…`, never English.**
  `InvalidModelStateResponseFactory` (`AddValidation()` in `Taxi.Api/DependencyInjection.cs`)
  treats the message as a key and looks it up in `SharedResource.{culture}.json`. A prose message
  produces an untranslated response. `[StringLength]` takes its key the same way (`MakeTooLong`,
  `FullNameTooLong`, …).
- **Bilingual fields arrive flat** (`DescriptionEn` / `DescriptionAr`, `NameEn` / `NameAr`) and are
  combined into a `LocalizedText` inside the domain factory. A request never carries a nested
  object for this.
- **Update requests carry no `Id`.** The route (`PUT /api/v1/cars/{id:guid}`) is the only source of
  identity; the controller passes the route id into the command, so there is no route/body
  mismatch to reject.
- **Query-string requests** (`GetCarsRequest`, `GetDriversRequest`) are bound with `[FromQuery]`
  and use `[Range]` / `[StringLength]` with keys. Their defaults (`Page = 1`, `PageSize = 20`) are
  the same numbers as `Paging.DefaultPage` / `DefaultPageSize` in Application.
- **No methods.** A `ToCommand()` would force `Contracts → Application`. Request-to-command
  mapping is written by hand in the controller. A convention test enforces "no methods".

Keep requests flat: MVC validates nested objects' annotations, but a nested shape is harder to
evolve and to document.

---

## 6. The registries

### `LocalizationKeys`

```csharp
public static class LocalizationKeys
{
    public static class Car { public const string NotFound = "Car.NotFound"; /* … */ }
    // Driver, RefreshToken, Auth, Account, Concurrency, Idempotency, Request, RateLimit, Validation
}
```

| Nested class | Holds |
|---|---|
| `Car`, `Driver`, `RefreshToken` | Entity error codes (`CarErrors`, `DriverErrors`, `RefreshTokenErrors`) |
| `Auth` | Sign-in and token-refresh failures |
| `Account` | Self-service failures and the email subjects/bodies |
| `Concurrency`, `Idempotency`, `Request`, `RateLimit` | Platform responses produced by middleware and filters |
| `Validation` | Request-shape messages used by `DataAnnotations`, FluentValidation and the Identity password mapper |

- Constant names are PascalCase; values are dotted `Area.Field.Condition`
  (`"Car.Make.Required"`).
- **Every key must exist in both** `src/Taxi.Api/Resources/SharedResource.en.json` **and**
  `SharedResource.ar.json`. Nothing in the build enforces that. A key missing from one language
  silently falls back to the English `Error.Description` (and logs a warning in Development only).
- The reverse also holds: a resource entry with no constant is dead weight. Today both files carry
  exactly the 87 keys the class defines.
- Some keys take arguments (`Request.TooLarge` takes the byte limit,
  `Driver.Certifications.TooMany` the maximum, `Validation.Password.TooShort` the length): the
  JSON uses `{0}` placeholders.

### `Languages`

```csharp
public static class Languages
{
    public const string En = "en";
    public const string Ar = "ar";
    public const string Default = En;
    public static readonly string[] All = [En, Ar];
}
```

Used by `UseRequestLocalization`, `LanguageContext`, the mappers, `AppSettings.DefaultLanguage` and
`AcceptLanguageOperationTransformer`. **Never write the literals `"en"` or `"ar"`.**

Adding a language: add the constant, extend `All`, extend `LocalizedText` and every mapper's
language switch, add the `SharedResource.<lang>.json` file, and add the file to the Api `.csproj`
`Content Update` list.

### `SortDirections` and `CarSortFields`

`SortDirections` (`asc`, `desc`) is shared by every list endpoint. `CarSortFields` (`make`, `model`,
`year`, `createdAt`; default `createdAt`) is the whitelist for `GET /cars`. A new sortable list gets
its own `<Entity>SortFields` beside its request.

---

## 7. Paging, filtering and sorting

`GET /api/v1/cars` and `GET /api/v2/cars` are paged, filtered and sorted. `GET /api/v1/drivers` is
paged and searchable. The pieces:

| Piece | Where |
|---|---|
| Query-string contract | `Requests/Cars/GetCarsRequest.cs`, `Requests/Drivers/GetDriversRequest.cs`, bound with `[FromQuery]` |
| Sort whitelist | `Requests/Cars/CarSortFields.cs` and `Common/SortDirections.cs` |
| Limits | `Taxi.Application/Common/Models/Paging.cs`: default page size 20, **max 100 (clamped, not rejected)** |
| Envelope | `Taxi.Application/Common/Models/PaginatedList<T>`, returned as the response body |
| Validation keys | `Validation.PageInvalid` / `PageSizeInvalid` (below 1 → 400), `SearchTooLong`, `SortByInvalid`, `SortDirectionInvalid` |

| Parameter | Meaning | Default |
|---|---|---|
| `page`, `pageSize` | 1-based page; size clamped to 100 | `1`, `20` |
| `search` | cars: make **or** model contains the text; drivers: name **or** licence number contains it. Case-insensitive | none |
| `make` | cars only: exact make, case-insensitive | none |
| `year` | cars only: exact year | none |
| `sortBy` | cars only: one of `CarSortFields.All`, case-insensitive; anything else → 400 | `createdAt` |
| `sortDirection` | cars only: `asc` or `desc`, case-insensitive | `desc` |

Sorting is a **whitelist mapped to key selectors** in `CarQueryExtensions.ApplySort`, never a
column name passed to SQL, and `Id` is always the final tie-breaker so page boundaries are
deterministic. The driver list is fixed at newest-first.

The response shape:

```json
{
  "items": [],
  "page": 2,
  "pageSize": 20,
  "totalCount": 57,
  "totalPages": 3,
  "hasPreviousPage": true,
  "hasNextPage": true
}
```

The envelope is an Application type (§8). Never return a bare JSON array from a collection
endpoint: adding metadata later breaks every deserializer.

Every paging, filter and sort input is part of the query's `CacheKey` (trimmed and lower-cased, so
`?make=Tesla` and `?make=tesla` share an entry). Each combination is cached separately, and the
shared `CacheTags.Cars` tag lets one `RemoveByTagAsync` evict them all.

---

## 8. There is no `Responses/` folder

Outbound shapes are **Application DTOs**. The car endpoints return `CarDto` (v1) and
`CarDetailsDto` (v2), records in `Taxi.Application/Features/Cars/Dtos/`, straight out of
`result.Match(this.Ok, this.Problem)`.

> **Inbound** shapes come from `Contracts/Requests`. **Outbound** shapes are Application DTOs.

That is a legitimate choice (one fewer mapping hop; the DTO is already decoupled from the entity),
but it means the response schema is owned by the Application layer, and a .NET client that
references only Contracts cannot see it. To flow responses through Contracts instead, make it a
deliberate, whole-surface change (§15.1). Do not do it for one endpoint.

---

## 9. Belongs / does not belong

**Belongs:** request DTOs with `DataAnnotations` · `LocalizationKeys` · `Languages` · sort
whitelists and direction constants · enums shared across layers.

**Does not belong:** business logic or calculated properties · methods of any kind, including
`ToCommand()` · validation that needs the database · references to Domain entities · anything
requiring DI (no `DependencyInjection.cs` here, and there must not be) · FluentValidation
validators (they live beside the command in Application).

---

## 10. Naming

| Thing | Convention | Example |
|---|---|---|
| Request | `<UseCase>Request`, `class` | `CreateCarRequest`, `AssignDriverCarRequest` |
| Query-string request | `Get<Plural>Request` | `GetCarsRequest` |
| Sort whitelist | `<Entity>SortFields`, static class of `const string` + `All` | `CarSortFields` |
| Folder | plural aggregate name, or the feature area | `Requests/Cars/`, `Requests/Account/` |
| Key constant | PascalCase condition | `MakeRequired` |
| Key value | `Area.Field.Condition` | `"Car.Make.Required"` |
| Enum | singular, one per file | none exists yet |

---

## 11. Talking to other layers

Contracts references nothing, so every relationship is inbound.

| Layer | Uses Contracts for |
|---|---|
| **Api** | Binds `[FromBody]` / `[FromQuery]` request types; resolves `ErrorMessage` keys through `IStringLocalizer<SharedResource>`; feeds `Languages.All` to `UseRequestLocalization` and to the OpenAPI `Accept-Language` transformer |
| **Domain** | `LocalizationKeys` for `<Entity>Errors` codes, the reason `Taxi.Domain` references this project at all |
| **Application** | `LocalizationKeys` in validators and `ApplicationErrors`; `Languages` in mappers; `CarSortFields` / `SortDirections` in query validators and `CarQueryExtensions`. Reached transitively through Domain |
| **Infrastructure** | `LocalizationKeys` in `IdentityService` and `IdentityErrorMapper`; `Languages.Default` in `AppSettings` |

The one rule that makes this work: **Contracts never references back.**

A key added here is not finished until it also exists in `SharedResource.en.json` **and**
`SharedResource.ar.json`.

---

## 12. Enforced by tests

| Rule | Test |
|---|---|
| Contracts depends on no other layer | `LayerDependencyTests.Contracts_DependsOnNoOtherLayer` |
| Request types are data only (no methods) | `ConventionTests.ContractRequests_AreDataOnly` |
| A rejected payload answers a localized 400 keyed by field | `CarsTests.InvalidInput_IsALocalizedValidationProblem_KeyedByField`, `AccountTests.Register_WithAWeakPassword_IsALocalizedFieldError` |

Not enforced by any test: resource-file parity. Check it by hand when you add a key (§6).

---

## 13. Deliberate decisions (not bugs)

| Behaviour | Why |
|---|---|
| Requests are mutable `class`es, not records | Model binding wants settable properties; positional records bind poorly. |
| No `Responses/` folder | Outbound shapes are Application DTOs (§8). |
| `pageSize` above 100 is clamped, not rejected | A friendlier contract for clients that ask for "everything"; only values below 1 are errors. |
| The sort whitelist sits in `Requests/Cars/` | It belongs to the request that accepts it. |
| `Taxi.Domain` references this project | `LocalizationKeys` must be visible to the Domain (Domain blueprint §2). |
| Page defaults appear here and in Application's `Paging` | Contracts cannot reference Application; keep the two numbers equal (`Page = 1`, `PageSize = 20`). |

---

## 14. Common mistakes

| ❌ | ✅ |
|---|---|
| `[Required(ErrorMessage = "Make is required")]` | `[Required(ErrorMessage = LocalizationKeys.Validation.MakeRequired)]` |
| `[StringLength(100)]` with no `ErrorMessage` | `[StringLength(100, ErrorMessage = LocalizationKeys.Validation.MakeTooLong)]` |
| Adding a key without touching the resource files | `LocalizationKeys` + `SharedResource.en.json` + `SharedResource.ar.json` |
| `public CreateCarCommand ToCommand()` on a request | Map in the controller by hand |
| `using Taxi.Domain.Cars;` in a contract | Contracts see BCL primitives and their own types only |
| `if (culture == "ar")` | `if (culture == Languages.Ar)` |
| A FluentValidation validator here | Validators live beside the command in Application |
| `public record CreateCarRequest(...)` | `class` with `get; set;`; positional records bind poorly |
| A request type with no controller binding it | Build the endpoint in the same change, or do not add the contract |
| An `Id` in an update request | Take it from the route only |
| A sort field passed straight to `OrderBy(string)` | Add it to the whitelist and map it to a key selector |

---

## 15. Future Extensions — NOT IMPLEMENTED

> ⚠️ **None of the following is wired up today.** Sketches only.

### 15.1 Making `Responses/` real

If the response schema should belong to Contracts (so a .NET client can deserialize into shared
types):

1. Define `Contracts/Responses/Cars/CarResponse.cs` mirroring `CarDto`.
2. Map at the **controller**, not the handler: the handler keeps returning `CarDto`, the controller
   projects it. That keeps `Taxi.Application` free of any Contracts response reference.
3. Update `[ProducesResponseType(typeof(CarResponse), ...)]`.
4. Do it for every endpoint at once.

Weigh it honestly: it buys a client-visible schema and costs a mapping layer. With no .NET client
in the solution, returning Application DTOs is the reasonable status quo.

### 15.2 Contract versioning

The API versions by URL segment (`api/v{version:apiVersion}/...`). `CarsController` serves a
deprecated v1 and a v2 (Api blueprint §11), and that v2 changed only a **response** shape, which
lives in Application DTOs, so Contracts did not change. If a **request** ever needs a breaking
change, split by namespace rather than bolting optional properties on:

```text
src/Taxi.Contracts/
├── V1/Requests/Cars/CreateCarRequest.cs
└── V2/Requests/Cars/CreateCarRequest.cs
```

Do this only for a genuinely breaking change; additive optional fields do not need a new version.
