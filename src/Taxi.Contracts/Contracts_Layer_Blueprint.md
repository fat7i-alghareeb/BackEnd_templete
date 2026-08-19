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

**References:** no projects. One package,
`Microsoft.AspNetCore.Components.DataAnnotations.Validation` (for `[ValidateComplexType]` on
nested request objects).

**Referenced by:** `Taxi.Domain`, `Taxi.Api`, `Taxi.Client`. (`Taxi.Application` gets it
transitively through Domain and uses `LocalizationKeys` and `Languages` freely.)

This project must stay the lightest in the solution — it is compiled into the WebAssembly client's
download. Adding a project reference or a heavy package here is a rejection.

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
- `UpdateCarRequest` includes its own `Id`. `CarsController.UpdateCar` compares it to the route id
  and returns 400 if they disagree.

Nested objects need `[ValidateComplexType]` on the parent property, or their annotations are not
evaluated. That is the only reason the DataAnnotations.Validation package is referenced.

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
`Taxi.Client` cannot see it — the client references Contracts only.

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
| **Client** | Its only project reference — request shapes and both registries, so browser and server cannot drift on a property name or a key |

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

## 11. Future Extensions — NOT IMPLEMENTED

> ⚠️ **None of the following is wired up today.** Sketches only, corrected for this stack.

### 11.1 Making `Responses/` real

If the response schema should belong to Contracts (so `Taxi.Client` can deserialize into shared
types), the change is:

1. Define `Contracts/Responses/Cars/CarResponse.cs` mirroring `CarDto`.
2. Map at the **controller**, not the handler — the handler keeps returning `CarDto`, the
   controller projects it. This keeps `Taxi.Application` free of any Contracts response reference
   and avoids a second mapping concern inside handlers.
3. Update `[ProducesResponseType(typeof(CarResponse), ...)]`.
4. Do it for every endpoint at once.

Weigh it honestly: it buys a client-visible schema and costs a mapping layer. With the client
currently an empty shell, returning `CarDto` directly is the reasonable status quo.

### 11.2 Paging envelope

Nothing paginates today. When the first paged endpoint arrives you will need three new pieces:
a `PaginatedList<T>` in `Taxi.Application/Common/Models/`, `PageInvalid` / `PageSizeInvalid`
constants in `LocalizationKeys.Validation` (with entries in both `SharedResource` files), and the
envelope below:

```csharp
// src/Taxi.Contracts/Responses/Common/PagedResponse.cs
namespace Taxi.Contracts.Responses.Common;

public sealed record PagedResponse<T>(
    IReadOnlyCollection<T> Items,
    int PageNumber,
    int PageSize,
    int TotalPages,
    int TotalCount)
{
    public bool HasNextPage => this.PageNumber < this.TotalPages;

    public bool HasPreviousPage => this.PageNumber > 1;
}
```

Never return a bare JSON array from a collection endpoint — adding metadata later is a breaking
change for every deserializer.

The query side carries the paging inputs:

```csharp
public record GetCarsQuery(int Page = 1, int PageSize = 20) : ICachedQuery<Result<PaginatedList<CarDto>>>
{
    public string CacheKey => $"cars-p{this.Page}-s{this.PageSize}";
    public string[] Tags => [CacheTags.Cars];
    public TimeSpan Expiration => TimeSpan.FromMinutes(10);
}
```

with a validator using `LocalizationKeys.Validation.PageInvalid` / `PageSizeInvalid`. Note that
paging multiplies cache keys — tag-based invalidation still clears them all, which is why
`Tags` matters more than `CacheKey` here.

### 11.3 Contract versioning

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
