# Domain Layer Blueprint — `Taxi.Domain`

> Start at [AGENTS.md](../../AGENTS.md) — it carries the rules and the add-a-feature
> checklist. This file is the deep reference for the Domain layer.
> Map of all docs: [NAVIGATION.md](../../docs/NAVIGATION.md).

The innermost ring. Business entities, their invariants, their errors, and the `Result<T>`
machinery every other layer uses to report failure.

Reference implementation: **`Cars/Car.cs` + `Cars/CarErrors.cs`**. Copy those.

---

## 1. Purpose

The Domain layer answers one question: *what is a legal state for this business object, and what
operations legally change it?* It knows nothing about HTTP, EF Core, MediatR handlers, or JSON.

It also hosts the `Result<T>` / `Error` types. Those live here rather than in Application because
domain methods are the primary producers of failures, and every outer layer needs to consume them.

---

## 2. Dependency rules

**References:** `Taxi.Contracts` (project) and `MediatR` (package).

Both are deliberate and both are narrow:

- **`Taxi.Contracts`** — entity error codes are `LocalizationKeys` constants
  (`LocalizationKeys.Car.MakeRequired`). See the subsection below before touching this.
- **`MediatR`** — used for exactly one thing: `DomainEvent : INotification`
  (`Common/DomainEvent.cs`), so domain events can be published as `INotification`.

**Referenced by:** `Taxi.Application` (and transitively everything above it).

**Never add:** EF Core, ASP.NET Core, `HttpContext`, a logger, a repository interface, or any
third-party SDK.

### Why the Domain references Contracts — do not "fix" this

The usual rule is that the Domain points at nothing. This project breaks it on purpose, and every
alternative is worse. If you are about to "correct" the layering, read this first.

`CarErrors` and `RefreshTokenErrors` use `LocalizationKeys` constants as their error codes. That
registry must be visible to **three** places at once: the Domain (which produces the codes), the
API (which resolves them against `SharedResource.{culture}.json`), and the Client (which may
display them). `Taxi.Contracts` is the only project all three can reference.

The alternatives and why each is worse:

| Alternative | Consequence |
|---|---|
| Move `LocalizationKeys` into `Taxi.Domain` | Forces `Contracts → Domain`. Contracts is compiled into the WebAssembly client, so **business rules ship to the browser** as a public download. Strictly worse than the current coupling. |
| Duplicate the keys in both projects | Two registries that must agree by hand. They will drift, and the failure is silent — a missing key degrades to the English fallback rather than erroring. |
| Use raw strings for error codes | Exactly the defect that was removed from `IdentityService`: codes that resolve in no resource file, so responses fall back to English in every language. |

**Cost of the current arrangement:** low. `Taxi.Contracts` has no project references and a single
small package, so nothing heavy leaks inward.

**Revisit when** `Taxi.Contracts` gains an HTTP, serialization, or persistence dependency. At that
point it stops being a pure vocabulary project and the trade-off changes.

---

## 3. Directory structure

```text
src/Taxi.Domain/
├── Common/
│   ├── AuditableEntity.cs
│   ├── DomainEvent.cs
│   ├── Entity.cs
│   ├── LocalizedText.cs
│   └── Results/
│       ├── Abstractions/
│       │   └── IResult.cs
│       ├── Error.cs
│       ├── ErrorKind.cs
│       └── Result.cs
├── Cars/
│   ├── Car.cs
│   └── CarErrors.cs
├── Identity/
│   ├── RefreshToken.cs
│   └── RefreshTokenErrors.cs
└── Domain_Layer_Blueprint.md
```

| Folder | Holds |
|---|---|
| `Common/` | Base types every aggregate inherits or returns. Nothing feature-specific. |
| `Common/Results/` | `Result`, `Result<T>`, the `Success`/`Created`/`Updated`/`Deleted` marker structs, `Error`, `ErrorKind`, `IResult`. |
| `<Aggregate>/` | One folder per aggregate, named plural. Contains the entity and its errors file. |

There is **no** `ValueObjects/`, `Events/`, `Enums/` or `Services/` folder today. Create one only
when you have a real member for it, and put it under the owning aggregate
(`Cars/Events/CarRetiredEvent.cs`).

---

## 4. Base types

### `Entity`

```csharp
public abstract class Entity
{
    public Guid Id { get; }

    private readonly List<DomainEvent> domainEvents = [];

    [NotMapped]
    public IReadOnlyCollection<DomainEvent> DomainEvents => this.domainEvents.AsReadOnly();

    protected Entity()
    {
    }

    protected Entity(Guid id)
    {
        this.Id = id == Guid.Empty ? Guid.NewGuid() : id;
    }

    public void AddDomainEvent(DomainEvent domainEvent) { ... }

    public void RemoveDomainEvent(DomainEvent domainEvent) { ... }

    public void ClearDomainEvents() { ... }
}
```

`Id` is get-only and defends against `Guid.Empty`. The event list is private with a read-only
projection. `[NotMapped]` (from `System.ComponentModel.DataAnnotations.Schema`, part of the BCL —
not an EF Core reference) keeps events out of the schema.

### `AuditableEntity`

Adds `CreatedAtUtc`, `CreatedBy`, `LastModifiedUtc`, `LastModifiedBy` as plain `get; set;`
properties. **Domain code never assigns them** — `AuditableEntityInterceptor` in Infrastructure
stamps them during `SaveChanges`. Inherit from `AuditableEntity`, not `Entity`, unless you have a
reason not to; both current entities do.

### `LocalizedText`

```csharp
public sealed class LocalizedText
{
    public LocalizedText(string en, string ar) { this.En = en; this.Ar = ar; }

    private LocalizedText() { this.En = string.Empty; this.Ar = string.Empty; }  // EF Core

    public string En { get; private set; }

    public string Ar { get; private set; }

    public static LocalizedText Create(string en, string ar) => new(en, ar);
}
```

A plain sealed class — **there is no `ValueObject` base class in this repository** and no
structural-equality implementation. Two `LocalizedText` instances with identical text are *not*
equal. Do not rely on `==`.

Every translatable field on an entity must be a `LocalizedText`. Never `NameEn` / `NameAr`
string pairs on the entity — the flat pair belongs in the Contracts request only.

### `DomainEvent`

```csharp
public abstract class DomainEvent : INotification;
```

The dispatch pipeline is complete (`AppDbContext.SaveChangesAsync` collects events from tracked
entities and publishes each through `IMediator` before saving), but **no concrete event and no
`INotificationHandler` exists yet**. §11 shows how to add the first one correctly.

---

## 5. The entity pattern

```csharp
namespace Taxi.Domain.Cars;

using Taxi.Domain.Common;
using Taxi.Domain.Common.Results;

public sealed class Car : AuditableEntity
{
    private Car()                                    // 1. EF Core materialisation
    {
    }

    private Car(Guid id, string make, string model, int year, LocalizedText description)
        : base(id)                                   // 2. real construction, still private
    {
        this.Make = make;
        this.Model = model;
        this.Year = year;
        this.Description = description;
    }

    public string Make { get; private set; } = null!;    // 3. private set

    public string Model { get; private set; } = null!;

    public int Year { get; private set; }

    public LocalizedText Description { get; private set; } = null!;

    public static Result<Car> Create(                // 4. the only public way in
        Guid id, string make, string model, int year, string descriptionEn, string descriptionAr)
    {
        if (id == Guid.Empty)
        {
            return CarErrors.IdRequired;             // 5. implicit Error → Result<Car>
        }

        if (string.IsNullOrWhiteSpace(make))
        {
            return CarErrors.MakeRequired;
        }

        if (string.IsNullOrWhiteSpace(model))
        {
            return CarErrors.ModelRequired;
        }

        if (year < 1886)
        {
            return CarErrors.InvalidYear;
        }

        if (string.IsNullOrWhiteSpace(descriptionEn) || string.IsNullOrWhiteSpace(descriptionAr))
        {
            return CarErrors.DescriptionRequired;
        }

        return new Car(id, make, model, year, new LocalizedText(descriptionEn.Trim(), descriptionAr.Trim()));
    }

    public Result<Updated> Update(                   // 6. named mutation, re-validates
        string make, string model, int year, string descriptionEn, string descriptionAr)
    {
        // same guards, minus the id check

        this.Make = make;
        this.Model = model;
        this.Year = year;
        this.Description = new LocalizedText(descriptionEn.Trim(), descriptionAr.Trim());

        return Result.Updated;
    }
}
```

Rules the shape encodes:

1. Every constructor is `private`. A parameterless one exists purely for EF Core.
2. `Create` takes primitives and returns `Result<T>` — it can reject input, which a constructor
   cannot do without throwing.
3. **The caller supplies the id.** `CreateCarCommandHandler` passes `Guid.NewGuid()`. This keeps
   the entity deterministic and testable; `Entity` still substitutes a new Guid if you pass
   `Guid.Empty`.
4. Guards run before any state is touched — a failed `Update` leaves the entity untouched.
5. Normalisation (`.Trim()`) happens inside the entity, not in the handler.
6. Mutations return `Result<Updated>`, never `void` and never `bool`.

`Car` has **no** `Delete()` method: `RemoveCarCommandHandler` calls `context.Cars.Remove(car)`
directly, because deletion carries no business rule here. Add a domain method only when it does
(soft delete, "cannot delete an assigned car", etc.).

`RefreshToken` follows the same pattern with get-only properties, since nothing mutates it after
creation.

---

## 6. Errors

One `<Entity>Errors.cs` per entity, in the same folder.

```csharp
using Taxi.Contracts.Common;
using Taxi.Domain.Common.Results;

namespace Taxi.Domain.Cars;

public static class CarErrors
{
    public static readonly Error MakeRequired = Error.Validation(
        code: LocalizationKeys.Car.MakeRequired,
        description: "Car make is required.");

    public static readonly Error NotFound = Error.NotFound(
        code: LocalizationKeys.Car.NotFound,
        description: "Car was not found.");
}
```

- `public static readonly Error` is the canonical form. (`RefreshTokenErrors` uses
  `public static Error X => ...`; that is an inconsistency, not an alternative.)
- `code` is **always** a `LocalizationKeys` constant, and that key must exist in both
  `SharedResource.en.json` and `SharedResource.ar.json`.
- `description` is the English fallback, used only when the localizer cannot resolve the key.
- Pick the factory that implies the right HTTP status: `Validation` (400), `NotFound` (404),
  `Conflict` (409), `Unauthorized` (401), `Forbidden` (403), `Failure` / `Unexpected` (500).
- For a parametrized message, pass `args` and use `{0}`-style placeholders in the JSON resource:

  ```csharp
  public static Error TooOld(int year) => Error.Validation(
      code: LocalizationKeys.Car.InvalidYear,
      description: $"Year {year} is before the first automobile.",
      args: year);
  ```

  A parametrized error must be a method, not a field.

`Error.ValidationForProperty` is reserved for `ValidationBehavior` — domain code never sets
`PropertyName`.

---

## 7. Belongs / does not belong

**Belongs:** entities and their invariants · `<Entity>Errors` · value-type wrappers such as
`LocalizedText` · domain enums · `Result` / `Error` primitives · pure in-memory calculations over
entities.

**Does not belong:** `DbContext`, `DbSet`, LINQ-to-Entities · `IRequest` / `IRequestHandler`
(only `INotification` via `DomainEvent`) · `HttpContext`, `IActionResult` · `ILogger` · DI
registration (there is no `DependencyInjection.cs` in this project and there must not be) ·
mapping to DTOs · `DateTime.UtcNow` inside methods — pass time in, or let the audit interceptor
handle it · anything `async`; the Domain performs no I/O.

---

## 8. Naming

| Thing | Convention | Example |
|---|---|---|
| Aggregate folder | plural noun | `Cars/`, `Identity/` |
| Entity | singular, `sealed` | `Car`, `RefreshToken` |
| Errors class | `<Entity>Errors`, `static` | `CarErrors` |
| Factory | `Create` returning `Result<T>` | `Car.Create(...)` |
| Mutation | verb, returns `Result<Updated>` | `Update`, `Cancel`, `Assign` |
| Error member | the failed condition | `MakeRequired`, `InvalidYear`, `NotFound` |
| UTC timestamps | suffix `Utc` | `CreatedAtUtc`, `ExpiresOnUtc` |

---

## 9. Talking to other layers

The Domain never calls out. Everything is inbound:

- **Application** calls `Car.Create` / `car.Update`, inspects `IsError`, and forwards `.Errors`.
  If a rule needs data the entity cannot see (uniqueness, an external quote), the handler fetches
  it and passes the primitive result *into* the domain method.
- **Infrastructure** maps entities in `IEntityTypeConfiguration<T>` and stamps `AuditableEntity`
  fields in the save interceptor.
- **API** never touches the Domain except through `Result<T>` and `Error`, which
  `ProblemExtensions` translates.

---

## 10. Common mistakes

| ❌ | ✅ |
|---|---|
| `public Car(...)` | `private Car(...)` + `public static Result<Car> Create(...)` |
| `throw new ArgumentException("make required")` | `return CarErrors.MakeRequired;` |
| `public string Make { get; set; }` | `public string Make { get; private set; }` |
| `Error.Validation("Car_Make_Required", ...)` | `Error.Validation(LocalizationKeys.Car.MakeRequired, ...)` |
| `public string NameEn`, `public string NameAr` | `public LocalizedText Name { get; private set; }` |
| Adding a key to `LocalizationKeys` only | Adding it to `LocalizationKeys` **and both** `SharedResource` files |
| `public List<X> Items { get; set; }` | `private readonly List<X> items = []; public IReadOnlyCollection<X> Items => this.items.AsReadOnly();` |
| Mutating state before the guards | All guards first, then assign |
| Injecting anything into an entity | Fetch in the handler, pass the primitive in |
| `if (status == "Active")` | a domain `enum` |

---

## 11. Future Extensions — NOT IMPLEMENTED

> ⚠️ **None of the following exists in this repository.** These are corrected sketches for how to
> add each capability *in this stack* if it is ever needed. Do not treat them as existing pattern,
> and do not add them speculatively.

### 11.1 A concrete domain event

The infrastructure is already in place — `DomainEvent`, `Entity.AddDomainEvent`, and dispatch in
`AppDbContext.SaveChangesAsync`. To use it:

```csharp
// src/Taxi.Domain/Cars/Events/CarRetiredEvent.cs
namespace Taxi.Domain.Cars.Events;

using Taxi.Domain.Common;

public sealed class CarRetiredEvent(Guid carId) : DomainEvent
{
    public Guid CarId { get; } = carId;
}
```

```csharp
// raised from inside the entity, never from the handler
public Result<Updated> Retire()
{
    if (this.IsRetired)
    {
        return CarErrors.AlreadyRetired;
    }

    this.IsRetired = true;
    this.AddDomainEvent(new CarRetiredEvent(this.Id));
    return Result.Updated;
}
```

The handler side goes in Application as an `INotificationHandler<CarRetiredEvent>` — see the
Application blueprint's future-extensions section.

**Caveat worth knowing before you rely on it:** the current dispatch happens *before*
`base.SaveChangesAsync`, so a handler observes the change before it is committed, and a handler
throwing will abort the save. If you need after-commit semantics, move dispatch into an
`ISaveChangesInterceptor.SavedChangesAsync` implementation instead.

### 11.2 A real value object base

If structural equality is ever needed (money, coordinates, an address), add the base type rather
than overriding equality per class:

```csharp
// src/Taxi.Domain/Common/ValueObject.cs
namespace Taxi.Domain.Common;

public abstract class ValueObject
{
    protected abstract IEnumerable<object?> GetEqualityComponents();

    public override bool Equals(object? obj)
        => obj is ValueObject other
           && this.GetType() == other.GetType()
           && this.GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());

    public override int GetHashCode()
    {
        var hash = default(HashCode);
        foreach (var component in this.GetEqualityComponents())
        {
            hash.Add(component);
        }

        return hash.ToHashCode();
    }

    public static bool operator ==(ValueObject? left, ValueObject? right) => Equals(left, right);

    public static bool operator !=(ValueObject? left, ValueObject? right) => !Equals(left, right);
}
```

Retrofitting `LocalizedText` onto it is safe (EF Core's `OwnsOne(...).ToJson()` mapping is
unaffected by equality overrides), but do it as its own change so the migration story stays clear.

Value objects that can be invalid get the same `private ctor` + `static Result<T> Create` shape as
entities.

### 11.3 A domain service

Only when a rule spans two aggregates and belongs to neither. Stateless, no I/O, no interfaces
injected, returns `Result<T>`:

```csharp
// src/Taxi.Domain/Services/FareCalculator.cs — hypothetical
public sealed class FareCalculator
{
    public Result<decimal> Calculate(Ride ride, Driver driver) { ... }
}
```

Register it in `AddApplication()` (the Domain has no DI container of its own and must not gain
one). If the rule fits inside one aggregate, put it on the entity instead — a domain service is
the last resort, not the first.
