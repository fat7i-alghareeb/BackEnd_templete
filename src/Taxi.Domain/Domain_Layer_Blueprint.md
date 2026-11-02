# Domain Layer Blueprint — `Taxi.Domain`

> Start at [AGENTS.md](../../AGENTS.md): it carries the rules and the add-a-feature checklist.
> This file is the deep reference for the Domain layer.
> Map of all docs: [NAVIGATION.md](../../docs/NAVIGATION.md).

The innermost ring: business entities, their invariants, their errors, and the `Result<T>` types
every other layer uses to report failure.

---

## 1. At a glance

| | |
|---|---|
| Project | `src/Taxi.Domain` |
| References | project `Taxi.Contracts`; package `MediatR` (only for `DomainEvent : INotification`) |
| Referenced by | `Taxi.Application` (and, transitively, Infrastructure and Api) |
| Reference code | `Cars/Car.cs` + `Cars/CarErrors.cs` (minimal aggregate), `Drivers/Driver.cs` (child collection, relationship, event) |
| Unit tests | `tests/Taxi.Domain.UnitTests` |
| Rules enforced by the build | `tests/Taxi.ArchitectureTests` (see §13) |

What lives here: entities, their error catalogues, `LocalizedText`, domain events, and the
`Result<T>` / `Error` / `ErrorKind` primitives. Nothing else.

---

## 2. Purpose and dependency rules

The Domain answers one question: *what is a legal state for this business object, and which
operations legally change it?* It knows nothing about HTTP, EF Core, MediatR handlers or JSON. It
also hosts `Result<T>` and `Error`, because domain methods are the main producers of failures and
every outer layer consumes them.

**References:**

- `Taxi.Contracts`: entity error codes are `LocalizationKeys` constants
  (`LocalizationKeys.Car.MakeRequired`). See "Why the Domain references Contracts" below.
- `MediatR`: used for one thing, `DomainEvent : INotification` (`Common/DomainEvent.cs`).

**Never add:** EF Core, ASP.NET Core, `HttpContext`, a logger, a repository interface, a DI
registration class, or any third-party SDK.

### Why the Domain references Contracts

The usual rule is that the Domain points at nothing. This project breaks it on purpose, and every
alternative is worse. **Do not "fix" it.**

`CarErrors`, `DriverErrors` and `RefreshTokenErrors` use `LocalizationKeys` constants as error
codes. That registry must be visible to the Domain (which produces the codes), the API (which
resolves them against `SharedResource.{culture}.json`) and any .NET client that wants to display
them. `Taxi.Contracts` is the one project all of them can reference.

| Alternative | Consequence |
|---|---|
| Move `LocalizationKeys` into `Taxi.Domain` | Forces `Contracts → Domain`. Contracts is the assembly a client shares, so **business rules would ship to the client**. Strictly worse. |
| Duplicate the keys in both projects | Two registries that must agree by hand. They drift silently: a missing key degrades to the English fallback instead of failing. |
| Raw strings for error codes | The defect once removed from `IdentityService`: codes that resolve in no resource file, so responses fall back to English in every language. |

Cost of the current arrangement: low. `Taxi.Contracts` has no project or package references.
**Revisit when** `Taxi.Contracts` gains an HTTP, serialization or persistence dependency. At that
point it stops being a pure vocabulary project and the trade-off changes.

---

## 3. Directory structure

```text
src/Taxi.Domain/
├── Common/
│   ├── AuditableEntity.cs          CreatedAtUtc / CreatedBy / LastModifiedUtc / LastModifiedBy
│   ├── DomainEvent.cs              abstract record : INotification
│   ├── Entity.cs                   Id + domain-event list
│   ├── ISoftDeletable.cs           IsDeleted / DeletedAtUtc / DeletedBy
│   ├── LocalizedText.cs            En / Ar pair
│   └── Results/
│       ├── Abstractions/IResult.cs
│       ├── Error.cs                readonly record struct + factories
│       ├── ErrorKind.cs            seven kinds
│       └── Result.cs               Result<T>, Result.Success/Created/Updated/Deleted markers
├── Cars/
│   ├── Car.cs
│   ├── CarErrors.cs
│   └── Events/CarCreatedDomainEvent.cs
├── Drivers/
│   ├── Driver.cs
│   ├── DriverCertification.cs
│   ├── DriverErrors.cs
│   └── Events/DriverAssignedToCarDomainEvent.cs
├── Identity/
│   ├── RefreshToken.cs
│   └── RefreshTokenErrors.cs
└── Domain_Layer_Blueprint.md
```

| Folder | Holds |
|---|---|
| `Common/` | Base types every aggregate inherits or returns. Nothing feature-specific. |
| `Common/Results/` | `Result<T>`, the marker structs, `Error`, `ErrorKind`, `IResult`. |
| `<Aggregate>/` | One folder per aggregate, named plural. The entity, its `<Entity>Errors`, and an `Events/` subfolder when it raises events. |

There is no `ValueObjects/`, `Enums/` or `Services/` folder. Create one only when it has a real
member, and put it under the owning aggregate.

---

## 4. Base types

### `Entity`

```csharp
public abstract class Entity
{
    public Guid Id { get; }                                   // get-only
    [NotMapped]
    public IReadOnlyCollection<DomainEvent> DomainEvents { get; }
    protected Entity() { }                                    // EF Core
    protected Entity(Guid id) { /* Guid.Empty becomes Guid.NewGuid() */ }
    public void AddDomainEvent(DomainEvent domainEvent);
    public void RemoveDomainEvent(DomainEvent domainEvent);
    public void ClearDomainEvents();
}
```

`Id` is get-only and substitutes a new Guid for `Guid.Empty`. EF Core maps it because every
configuration declares `HasKey(x => x.Id)` explicitly (get-only properties are not mapped by
convention). The event list is private with a read-only projection; `[NotMapped]` (a BCL
attribute, not an EF Core reference) keeps it out of the schema. The outline above omits bodies.

### `AuditableEntity`

Adds `CreatedAtUtc`, `CreatedBy`, `LastModifiedUtc`, `LastModifiedBy` as public `get; set;`
properties. **Domain code never assigns them.** `AuditableEntityInterceptor` (Infrastructure)
stamps them during `SaveChanges`. Inherit `AuditableEntity`, not `Entity`, unless you have a
reason; `Car`, `Driver` and `RefreshToken` all do.

### `ISoftDeletable`

`IsDeleted`, `DeletedAtUtc`, `DeletedBy` with public setters, for the same reason: only
`SoftDeleteInterceptor` writes them. Today only `Car` implements it. A soft-deleted row is hidden
from every query by a global filter and is never physically removed.

### `LocalizedText`

```csharp
public sealed class LocalizedText
{
    public LocalizedText(string en, string ar);
    private LocalizedText();                       // EF Core materialisation
    public string En { get; private set; }
    public string Ar { get; private set; }
    public static LocalizedText Create(string en, string ar);
}
```

A plain sealed class. **There is no `ValueObject` base and no structural equality**: two instances
with identical text are not equal, so never compare with `==`. Every translatable entity field is a
`LocalizedText`; never `NameEn` / `NameAr` columns on an entity (the flat pair belongs in Contracts
requests only).

### `DomainEvent`

```csharp
public abstract record DomainEvent : INotification;
```

See §9.

---

## 5. The entity pattern

`Cars/Car.cs` is the template. Shape, with the unremarkable guards elided:

```csharp
public sealed class Car : AuditableEntity, ISoftDeletable
{
    private Car() { }                                           // 1. EF Core materialisation

    private Car(Guid id, string make, string model, int year, LocalizedText description)
        : base(id) { /* assign */ }                             // 2. real construction, private

    public string Make { get; private set; } = null!;           // 3. private set
    public LocalizedText Description { get; private set; } = null!;
    public string? ImageKey { get; private set; }
    public bool IsDeleted { get; set; }                         // ISoftDeletable (interceptor-stamped)

    public static Result<Car> Create(                           // 4. the only public way in
        Guid id, string make, string model, int year, string descriptionEn, string descriptionAr)
    {
        if (id == Guid.Empty) { return CarErrors.IdRequired; }  // 5. implicit Error → Result<Car>
        // ... one guard per rule, all before any state exists ...

        var car = new Car(id, make, model, year,
            new LocalizedText(descriptionEn.Trim(), descriptionAr.Trim()));
        car.AddDomainEvent(new CarCreatedDomainEvent(car.Id, car.Make, car.Model, car.Year));
        return car;
    }

    public Result<Updated> Update(...) { /* same guards, then assign */ return Result.Updated; }
    public Result<Updated> SetImage(string imageKey) { /* guard, assign */ }
    public Result<Updated> RemoveImage() { /* ImageNotFound if none */ }
}
```

Rules the shape encodes:

1. Every constructor is `private`. The parameterless one exists only for EF Core.
2. `Create` takes primitives and returns `Result<T>`. A constructor cannot reject input without
   throwing; a factory can.
3. **The caller supplies the id.** `CreateCarCommandHandler` passes `Guid.NewGuid()`, which keeps the
   entity deterministic and testable. `Guid.Empty` is rejected by `Car.Create` (`IdRequired`).
4. Guards run **before** any assignment, so a rejected `Update` leaves the entity untouched.
5. Normalisation (`.Trim()`, `ToUpperInvariant()` for `Driver.LicenseNumber`) happens inside the
   entity, not in the handler.
6. Mutations are named methods returning `Result<Updated>`, never `void` and never `bool`.
7. Raise a domain event only after every guard has passed.

`Car` has **no `Delete()`**. `RemoveCarCommandHandler` calls `context.Cars.Remove(car)` because
deletion carries no rule *on the car itself*. The cross-aggregate rule ("an assigned car cannot be
deleted") is checked in the handler. Add a domain method only when deletion carries a rule the
entity can evaluate.

`RefreshToken` follows the same pattern with get-only properties, since nothing mutates it after
creation. Get-only properties are **not mapped by EF Core by convention**, so each one must be
listed in `RefreshTokenConfiguration` (see Infrastructure §6).

---

## 6. The second reference aggregate: `Driver`

`Drivers/Driver.cs` shows what `Car` cannot.

| Pattern | Where | How |
|---|---|---|
| **Child collection** | `Driver.Certifications` | Private `List<DriverCertification>` exposed as `IReadOnlyCollection`. Changes only through `AddCertification` / `RemoveCertification`, which own the rules: both names required, no duplicate English name (case-insensitive), at most `Driver.MaxCertifications` (10), parametrized error `DriverErrors.TooManyCertifications(max)`. |
| **Child entity** | `DriverCertification` | No public constructor, no setters; its `internal static Create` is reachable only from `Driver`. Its `Name` is a `LocalizedText`. |
| **Relationship by id** | `Driver.AssignedCarId` | A `Guid?`, never a `Car` navigation: aggregates reference each other by identity. |
| **Rule placement** | `AssignCar` vs. handler | "Not already on this car" is the driver's own state, so it lives in `Driver.AssignCar`. "The car exists" and "nobody else drives it" need queries, so they live in `AssignDriverCarCommandHandler`, backed by a filtered unique index. "An assigned car cannot be deleted" lives in `RemoveCarCommandHandler`. |
| **Domain event with prior state** | `DriverAssignedToCarDomainEvent` | Carries `PreviousCarId`, captured before the assignment. |
| **Normalisation** | `Driver.Create` / `Update` | `LicenseNumber` is trimmed and upper-cased inside the entity. |

`UnassignCar` raises no event; only `AssignCar` does.

---

## 7. Errors

One `<Entity>Errors.cs` per entity, in the same folder.

```csharp
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

- `public static readonly Error` is the form for a fixed error.
- A **parametrized** error is a method, because it builds a new value per call:

  ```csharp
  public static Error TooManyCertifications(int max) => Error.Validation(
      code: LocalizationKeys.Driver.TooManyCertifications,
      description: $"A driver can hold at most {max} certifications.",
      args: max);
  ```

  The JSON resource uses `{0}` placeholders; `args` feeds them.
- `code` is **always** a `LocalizationKeys` constant, and that key must exist in both
  `SharedResource.en.json` and `SharedResource.ar.json`.
- `description` is the English fallback, used only when the localizer cannot resolve the key.
- Choose the factory that implies the HTTP status: `Validation` 400, `NotFound` 404, `Conflict`
  409, `Unauthorized` 401, `Forbidden` 403, `Failure` / `Unexpected` 500.
- `Error.ValidationForProperty` is reserved for `ValidationBehavior` (and for Infrastructure code
  that maps Identity failures to request fields); entity code never sets `PropertyName`.

Existing catalogues: `CarErrors` (9), `DriverErrors` (12), `RefreshTokenErrors` (4). Failures that
belong to a use case rather than an entity live in `Taxi.Application/Common/Errors/`.

---

## 8. `Result<T>` and `Error`

Files: `Common/Results/`.

```csharp
Result<Car> created = Car.Create(...);        // implicit from Car, Error, List<Error>
if (created.IsError) { return created.Errors; }
var car = created.Value;
```

| Member | Meaning |
|---|---|
| `IsSuccess` / `IsError` | Mutually exclusive. |
| `Value` | The success value; `default` when failed. |
| `Errors` | `List<Error>`; empty on success. |
| `TopError` | First error, or `default`. |
| `Match(onValue, onError)` | Folds to one type; controllers use it to build the response. |
| `Result.Success` / `Created` / `Updated` / `Deleted` | Marker `readonly record struct`s for "succeeded, nothing to return". |

`Error` is a `readonly record struct` with a private constructor and these factories:
`Failure`, `Unexpected`, `Validation`, `ValidationForProperty`, `Conflict`, `NotFound`,
`Unauthorized`, `Forbidden`. Members: `Code`, `Description`, `Type` (`ErrorKind`), optional `Args`,
optional `PropertyName`.

`ErrorKind` has seven values: `Failure`, `Unexpected`, `Validation`, `Conflict`, `NotFound`,
`Unauthorized`, `Forbidden`. The HTTP mapping lives in the API (`ProblemExtensions.MapStatus`),
never here.

`Result<TValue>` has a public constructor marked `[Obsolete(error: true)]` plus `[JsonConstructor]`.
That blocks compile-time use but lets System.Text.Json rebuild a cached result, which
`CachingBehavior` relies on. Do not remove it (`ResultSerializationTests` pins the round trip).

---

## 9. Domain events

`DomainEvent` is an `abstract record : INotification`, the single sanctioned MediatR touchpoint.
Two exist:

| Event | Raised by | Handler |
|---|---|---|
| `Cars/Events/CarCreatedDomainEvent(CarId, Make, Model, Year)` | `Car.Create` | `Application/Features/Cars/EventHandlers/CarCreatedDomainEventHandler` |
| `Drivers/Events/DriverAssignedToCarDomainEvent(DriverId, CarId, PreviousCarId)` | `Driver.AssignCar` | `Application/Features/Drivers/EventHandlers/DriverAssignedToCarDomainEventHandler` |

```csharp
public sealed record CarCreatedDomainEvent(Guid CarId, string Make, string Model, int Year)
    : DomainEvent;

// inside a factory or behaviour method, never from a handler
car.AddDomainEvent(new CarCreatedDomainEvent(car.Id, car.Make, car.Model, car.Year));
```

Rules:

- A **record of serializable values** (ids, strings, numbers, dates). It is stored as JSON in the
  outbox and rehydrated later, possibly on another instance. Never put an entity or a service in it.
- Name it `<Entity><PastTenseVerb>DomainEvent`, one file per event under `<Plural>/Events/`.
- It must be a **concrete, non-abstract class in the Domain assembly**: `OutboxProcessor` only
  deserializes types it finds there.
- Raise it only after the guards pass, so a rejected operation raises nothing.
- Events are **not** dispatched during the request. `OutboxInterceptor` writes them to the
  `OutboxMessages` table in the same transaction; `OutboxBackgroundService` publishes them a few
  seconds later, at least once. Handlers: Application blueprint §12; mechanism: Infrastructure §8.

---

## 10. Belongs / does not belong

**Belongs:** entities and their invariants · `<Entity>Errors` · value-type wrappers such as
`LocalizedText` · domain events · domain enums · `Result` / `Error` primitives · pure in-memory
calculations over entities.

**Does not belong:** `DbContext`, `DbSet`, LINQ-to-Entities · `IRequest` / `IRequestHandler` (only
`INotification` via `DomainEvent`) · `HttpContext`, `IActionResult` · `ILogger` · DI registration
(there is no `DependencyInjection.cs` here and there must not be) · mapping to DTOs · reading the
clock inside methods (pass time in; the audit interceptor handles timestamps) · anything `async`:
the Domain performs no I/O.

---

## 11. Naming

| Thing | Convention | Example |
|---|---|---|
| Aggregate folder | plural noun | `Cars/`, `Drivers/`, `Identity/` |
| Entity | singular, `sealed` | `Car`, `Driver`, `RefreshToken` |
| Child entity | singular, `sealed`, `internal` factory | `DriverCertification` |
| Errors class | `<Entity>Errors`, `static` | `CarErrors` |
| Factory | `Create` returning `Result<T>` | `Car.Create(...)` |
| Mutation | verb, returns `Result<Updated>` (or `Result<Deleted>` / the created child) | `Update`, `AssignCar`, `RemoveCertification` |
| Error member | the failed condition | `MakeRequired`, `InvalidYear`, `NotFound` |
| Event | `<Entity><PastTense>DomainEvent` | `CarCreatedDomainEvent` |
| UTC timestamps | suffix `Utc` | `CreatedAtUtc`, `ExpiresOnUtc` |

---

## 12. Talking to other layers

The Domain never calls out; everything is inbound.

- **Application** calls `Car.Create` / `car.Update`, inspects `IsError`, and forwards `.Errors`
  unchanged. If a rule needs data the entity cannot see (uniqueness, another aggregate), the
  handler fetches it and passes the primitive result *into* the domain method, or enforces the
  rule itself (see §6).
- **Infrastructure** maps entities in `IEntityTypeConfiguration<T>` and stamps `AuditableEntity` /
  `ISoftDeletable` fields in interceptors. It also serializes domain events into the outbox.
- **API** touches the Domain only through `Result<T>` and `Error`, which `ProblemExtensions`
  translates.

---

## 13. Enforced by tests

| Rule | Test |
|---|---|
| Entities have no public constructors | `ConventionTests.Entities_HaveNoPublicConstructors` |
| Entities have no public setters, except the audit and soft-delete fields | `ConventionTests.Entities_HaveNoPublicSetters_ExceptInterceptorStampedFields` |
| Domain depends only on Contracts | `LayerDependencyTests.Domain_DependsOnlyOnContracts` |
| Domain uses no EF Core or ASP.NET Core | `LayerDependencyTests.Domain_DoesNotUseEfCoreOrAspNetCore` |
| Every guard returns the right error | `tests/Taxi.Domain.UnitTests/{Cars,Drivers,Identity,Common}` |
| Every public entity property is mapped by EF Core | `ModelMappingTests` (integration suite) |

---

## 14. Deliberate decisions (not bugs)

| Behaviour | Why |
|---|---|
| `Taxi.Domain → Taxi.Contracts` | §2. |
| Public setters on `AuditableEntity` and `ISoftDeletable` members | Interceptors must write them; domain code never does. The convention test allows exactly these names. |
| `RefreshToken.Create` compares the expiry with `DateTimeOffset.UtcNow` | The one place the Domain reads the clock. A test that needs a token must pass an expiry in the real future. |
| `LocalizedText` has no equality | It is a persistence-shaped value, not a `ValueObject`. |
| `Car.Update` does not re-check the id | The id never changes after `Create`. |
| `Result<T>` has an obsolete public constructor | Required by the serializer; see §8. |

---

## 15. Common mistakes

| ❌ | ✅ |
|---|---|
| `public Car(...)` | `private Car(...)` + `public static Result<Car> Create(...)` |
| `throw new ArgumentException("make required")` | `return CarErrors.MakeRequired;` |
| `public string Make { get; set; }` | `public string Make { get; private set; }` |
| `Error.Validation("Car_Make_Required", ...)` | `Error.Validation(LocalizationKeys.Car.MakeRequired, ...)` |
| `public string NameEn`, `public string NameAr` | `public LocalizedText Name { get; private set; }` |
| Adding a key to `LocalizationKeys` only | Add it to `LocalizationKeys` **and both** `SharedResource` files |
| `public List<X> Items { get; set; }` | `private readonly List<X> items = []; public IReadOnlyCollection<X> Items => this.items.AsReadOnly();` |
| Mutating state before the guards | All guards first, then assign |
| Injecting a service into an entity | Fetch in the handler, pass the primitive in |
| `public Car Car { get; }` navigation on `Driver` | `public Guid? AssignedCarId { get; private set; }` |
| `if (status == "Active")` | A domain `enum` |
| Raising an event before the guards pass | Raise it last, just before returning success |
| Putting an entity inside a `DomainEvent` | Ids and primitives only |

---

## 16. Future Extensions — NOT IMPLEMENTED

> ⚠️ **None of the following exists in this repository.** These are sketches for how to add each
> capability in this stack if it is ever needed. Do not treat them as existing pattern and do not
> add them speculatively.

### 16.1 A real value object base

If structural equality is ever needed (money, coordinates, an address), add a base type rather than
overriding equality per class:

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

Retrofitting `LocalizedText` onto it is safe (EF Core's `OwnsOne(...).ToJson()` mapping ignores
equality overrides), but do it as its own change so the migration story stays clear. Value objects
that can be invalid get the same `private` constructor + `static Result<T> Create` shape as
entities.

### 16.2 A domain service

Only when a rule spans two aggregates and belongs to neither. Stateless, no I/O, returns
`Result<T>`:

```csharp
// src/Taxi.Domain/Services/FareCalculator.cs — hypothetical
public sealed class FareCalculator
{
    public Result<decimal> Calculate(Ride ride, Driver driver) { ... }
}
```

Register it in `AddApplication()`: the Domain has no DI container of its own and must not gain
one. If the rule fits inside one aggregate, put it on the entity instead. A domain service is the
last resort, not the first.
