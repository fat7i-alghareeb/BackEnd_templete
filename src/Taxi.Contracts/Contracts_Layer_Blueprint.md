# Contracts Layer Blueprint — `Taxi.Contracts`

> Start at [AGENTS.md](../../AGENTS.md) — it carries the rules and the add-a-feature
> checklist. This file is the deep reference for the Contracts layer.
> Map of all docs: [NAVIGATION.md](../../docs/NAVIGATION.md).

The shared vocabulary. Inbound request shapes, the localization key registry, the language
registry, and enums that more than one layer needs.

Reference implementation: **`Requests/Cars/CreateCarRequest.cs`** and
**`Common/LocalizationKeys.cs`**.

---

## 1. Purpose

Two jobs, and they are less alike than they look:

1. **Wire contracts** — the exact JSON shape the API accepts, with surface-level
   `DataAnnotations` so malformed payloads are rejected before MediatR ever runs.
2. **The shared registry** — `LocalizationKeys` and `Languages`. These are not DTOs; they are
   constants that Domain, Application, API and Client all need to agree on. Because Contracts is
   the only project every one of them can reference, it hosts them.

Job 2 is why `Taxi.Domain` references this project.

---

## 2. Dependency rules

**References:** nothing — no projects and no packages. `DataAnnotations` comes from the BCL.

**Referenced by:** `Taxi.Domain`, `Taxi.Api`. (`Taxi.Application` gets it
transitively through Domain and uses `LocalizationKeys` and `Languages` freely.)

This project must stay the lightest in the solution — it is the assembly a .NET client (a MAUI or
Blazor front end, a test harness) would share with the API. Adding a project reference or a package
here is a rejection.

**Never add:** a reference to Domain, Application, Infrastructure or Api · EF Core · MediatR ·
FluentValidation · methods on DTOs · anything `async`.

---

## 3. Directory structure

```text
src/Taxi.Contracts/
├── Common/
│   ├── Languages.cs
│   └── LocalizationKeys.cs
├── Requests/
│   ├── Cars/
│   │   ├── CreateCarRequest.cs
│   │   └── UpdateCarRequest.cs
│   └── Identity/
│       ├── GenerateTokenRequest.cs
│       └── RefreshTokenRequest.cs
└── Contracts_Layer_Blueprint.md
```

| Folder | Holds |
|---|---|
| `Common/` | `LocalizationKeys`, `Languages`, shared enums. One type per file. |
| `Requests/<Plural>/` | One class per inbound payload, named `<UseCase>Request`. |

Every file here is live — each request type is bound by a controller action, and every
`LocalizationKeys` constant resolves in both `SharedResource` files. There is no `Responses/`
folder: outbound shapes are Application DTOs. See §6.

---

## 4. Request DTOs

```csharp
using System.ComponentModel.DataAnnotations;
using Taxi.Contracts.Common;

namespace Taxi.Contracts.Requests.Cars;

public class CreateCarRequest
{
    [Required(ErrorMessage = LocalizationKeys.Validation.MakeRequired)]
    [StringLength(100)]
    public string Make { get; set; } = string.Empty;

    [Required(ErrorMessage = LocalizationKeys.Validation.ModelRequired)]
    [StringLength(100)]
    public string Model { get; set; } = string.Empty;

    [Required(ErrorMessage = LocalizationKeys.Validation.YearInvalid)]
    public int Year { get; set; }

    [Required(ErrorMessage = LocalizationKeys.Validation.DescriptionRequired)]
    [StringLength(1000)]
    public string DescriptionEn { get; set; } = string.Empty;

    [Required(ErrorMessage = LocalizationKeys.Validation.DescriptionRequired)]
    [StringLength(1000)]
    public string DescriptionAr { get; set; } = string.Empty;
}
```

Everything that matters here:

- **`class` with `get; set;`**, not `record`. Model binding wants settable properties, and requests
  are mutable by nature.
- **Non-nullable strings default to `string.Empty`** so nullable analysis stays quiet.
- **`ErrorMessage` is a localization key, never English.**
  `InvalidModelStateResponseFactory` (registered in `Taxi.Api/DependencyInjection.cs`
  `AddValidation()`) treats the message as a key and looks it up in `SharedResource.{culture}.json`.
  Writing prose there produces an untranslated response.
- **Bilingual fields arrive flat** (`DescriptionEn` / `DescriptionAr`) and are combined into a
  `LocalizedText` inside the domain factory. The request never carries a nested object for this.
- **Update requests carry no `Id`.** The route (`PUT /api/v1/cars/{id:guid}`) is the only source
  of the resource identity; `CarsController.UpdateCar` passes the route id into the command. There
  is no route/body mismatch to reject, so the controller stays free of `if` statements.

Keep requests flat: MVC validates nested objects' annotations, but a nested shape is harder to
evolve and to document than flat properties.

---

## 5. The registries

### `LocalizationKeys`

```csharp
public static class LocalizationKeys
{
    public static class Car
    {
        public const string IdRequired = "Car.Id.Required";
        public const string MakeRequired = "Car.Make.Required";
        // ...
    }

    public static class Auth { ... }

    public static class RefreshToken { ... }

    public static class Validation { ... }
}
```

- Nested `static class` per area. Constant name is PascalCase; the value is dotted
  `Area.Field.Condition`.
- `Car`, `RefreshToken`, `Auth` hold entity- and flow-specific keys. `Validation` holds generic,
  reusable ones (`RequiredField`, `EmailInvalid`, `PageSizeInvalid`).
- **Every key must have an entry in both** `src/Taxi.Api/Resources/SharedResource.en.json` **and**
  `SharedResource.ar.json`. A key present in only one language silently falls back to the English
  `Error.Description` for the other, and logs a warning in Development only.
- The reverse also holds: a resource entry with no `LocalizationKeys` constant is dead weight.
  Both files currently carry exactly 23 keys, and every one has a constant behind it.

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

Used by `UseRequestLocalization`, `LanguageContext`, `CarMapper`, `AppSettings.DefaultLanguage`
and `AcceptLanguageOperationTransformer`. **Never write the literal `"en"` or `"ar"`.**

Adding a language: add the constant, extend `All`, extend `LocalizedText`, add the resource file,
and update every mapper's language switch.

---

## 6. There is no `Responses/` folder

Outbound shapes are **Application DTOs**. The Car endpoints return `CarDto` — a record in
`Features/Cars/Dtos/` — straight out of `result.Match(this.Ok, this.Problem)`.

So the convention is:

> **Inbound** shapes come from `Contracts/Requests`. **Outbound** shapes are Application DTOs.

That is a legitimate choice (one fewer mapping hop, and the DTO is already decoupled from the
entity), but it does mean the API's response schema is owned by the Application layer, and
a .NET client that references only Contracts cannot see it.

If you want responses to flow through Contracts instead, do it as a deliberate, whole-surface
change; see §11.1. Do not do it for one endpoint, leaving the codebase with two conventions.

---

## 7. Belongs / does not belong

**Belongs:** request DTOs with `DataAnnotations` · `LocalizationKeys` · `Languages` · enums shared
across layers · response DTOs and paging envelopes **if** the Future Extensions change is adopted.

**Does not belong:** business logic or calculated properties · methods of any kind, including
`ToCommand()` — that would force a reference to Application and create a cycle · validation that
needs the database · references to Domain entities · anything requiring DI (this project has no
`DependencyInjection.cs` and must not gain one).

---

## 8. Naming

| Thing | Convention | Example |
|---|---|---|
| Request | `<UseCase>Request`, `class` | `CreateCarRequest`, `UpdateCarRequest` |
| Response | `<Thing>Response`, `record` — only if Future Extensions is adopted | `CarResponse` |
| Folder | plural aggregate name | `Requests/Cars/` |
| Key constant | PascalCase condition | `MakeRequired` |
| Key value | `Area.Field.Condition` | `"Car.Make.Required"` |
| Enum | singular, one per file | e.g. `CarCategory` |

---

## 9. Talking to other layers

Contracts references nothing, so every relationship is inbound:

| Layer | Uses Contracts for |
|---|---|
| **Api** | Binds `[FromBody]` request types; resolves `ErrorMessage` keys through `IStringLocalizer<SharedResource>`; feeds `Languages.All` to `UseRequestLocalization` and the OpenAPI `Accept-Language` transformer |
| **Domain** | `LocalizationKeys` for `<Entity>Errors` codes — the reason `Taxi.Domain` references this project at all |
| **Application** | `LocalizationKeys` in validators, `Languages` in mappers. Reaches them transitively through Domain |
| **Infrastructure** | `LocalizationKeys` in `IdentityService`; `Languages.Default` in `AppSettings` |

The one rule that makes this work: **Contracts never references back.** A `ToCommand()` method on
a request would force `Contracts → Application` and create a cycle, which is why request→command
mapping is written by hand in the controller.

A key added here is not finished until it also exists in `src/Taxi.Api/Resources/SharedResource.en.json`
**and** `.ar.json`. Nothing in the build enforces that; a missing key degrades silently to the
English `Error.Description`.

---

## 10. Common mistakes

| ❌ | ✅ |
|---|---|
| `[Required(ErrorMessage = "Make is required")]` | `[Required(ErrorMessage = LocalizationKeys.Validation.MakeRequired)]` |
| Adding a key without touching the resource files | Add to `LocalizationKeys` + `SharedResource.en.json` + `SharedResource.ar.json` |
| `public CreateCarCommand ToCommand()` on a request | Map in the controller by hand |
| `using Taxi.Domain.Cars;` in a contract | Contracts see BCL primitives and their own enums only |
| `if (culture == "ar")` | `if (culture == Languages.Ar)` |
| Putting a FluentValidation validator here | Validators live beside the command in Application |
| `public record CreateCarRequest(...)` | `class` with `get; set;` — positional records bind poorly |
| Adding a request with no controller binding it | Build the endpoint in the same change, or do not add the contract |

---

## 10a. Paging, filtering and sorting

`GET /api/v1/cars` is paged, filtered and sorted. The pieces, and where each lives:

| Piece | Where |
|---|---|
| Query-string contract | `Requests/Cars/GetCarsRequest.cs`, bound with `[FromQuery]` |
| Sort whitelist | `Requests/Cars/CarSortFields.cs` (`make`, `model`, `year`, `createdAt`) and `Common/SortDirections.cs` (`asc`, `desc`) |
| Limits | `Taxi.Application/Common/Models/Paging.cs` — default 20, **max 100 (clamped, not rejected)** |
| Envelope | `Taxi.Application/Common/Models/PaginatedList<T>` — returned as the response body |
| Validation keys | `LocalizationKeys.Validation.PageInvalid` / `PageSizeInvalid` (below 1 → 400), `SearchTooLong`, `SortByInvalid`, `SortDirectionInvalid` |

| Parameter | Meaning | Default |
|---|---|---|
| `page`, `pageSize` | 1-based page; size clamped to 100 | `1`, `20` |
| `search` | make **or** model contains the text, case-insensitive | — |
| `make` | exact make, case-insensitive | — |
| `year` | exact year | — |
| `sortBy` | one of `CarSortFields.All`, case-insensitive; anything else → 400 | `createdAt` |
| `sortDirection` | `asc` or `desc`, case-insensitive | `desc` |

Sorting is a **whitelist mapped to key selectors** in the handler, never a column name passed to
SQL, and `Id` is always the final tie-breaker so page boundaries are deterministic.

The response shape:

```json
{
  "items": [ ... ],
  "page": 2,
  "pageSize": 20,
  "totalCount": 57,
  "totalPages": 3,
  "hasPreviousPage": true,
  "hasNextPage": true
}
```

The envelope is an Application type, consistent with §6: outbound shapes are Application DTOs.
Never return a bare JSON array from a collection endpoint — adding metadata later is a breaking
change for every deserializer.

Every paging, filter and sort input is part of the query's `CacheKey` (trimmed and lower-cased,
so `?make=Tesla` and `?make=tesla` share an entry), so each combination is cached separately; the
shared `CacheTags.Cars` tag is what lets one `RemoveByTagAsync` evict them all.

---

## 11. Future Extensions — NOT IMPLEMENTED

> ⚠️ **None of the following is wired up today.** Sketches only, corrected for this stack.

### 11.1 Making `Responses/` real

If the response schema should belong to Contracts (so a .NET client can deserialize into shared
types), the change is:

1. Define `Contracts/Responses/Cars/CarResponse.cs` mirroring `CarDto`.
2. Map at the **controller**, not the handler — the handler keeps returning `CarDto`, the
   controller projects it. This keeps `Taxi.Application` free of any Contracts response reference
   and avoids a second mapping concern inside handlers.
3. Update `[ProducesResponseType(typeof(CarResponse), ...)]`.
4. Do it for every endpoint at once.

Weigh it honestly: it buys a client-visible schema and costs a mapping layer. With no .NET client
in the solution, returning `CarDto` directly is the reasonable status quo.

### 11.2 Contract versioning

The API already versions by URL segment (`api/v{version:apiVersion}/...`) with
`[MapToApiVersion("1.0")]` per action. If a **breaking** payload change is ever needed, split by
namespace rather than bolting optional properties on:

```text
src/Taxi.Contracts/
├── V1/Requests/Cars/CreateCarRequest.cs
└── V2/Requests/Cars/CreateCarRequest.cs
```

and register the second version in `AddApiDocumentation`'s `versions` array in
`Taxi.Api/DependencyInjection.cs`, which is currently hard-coded to `["v1"]`. Only do this for a
genuinely breaking change — additive optional fields do not need a new version.
