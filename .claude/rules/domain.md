---
paths:
  - "src/Taxi.Domain/**"
  - "tests/Taxi.Domain.UnitTests/**"
---

# Domain rules (loaded when you touch `src/Taxi.Domain/`)

Copy `src/Taxi.Domain/Cars/Car.cs` + `CarErrors.cs`; `Drivers/Driver.cs` for a child collection, a
relationship by id or an event.

- Constructors are `private`; a private parameterless one is for EF Core. Create through
  `public static Result<T> Create(Guid id, ...)`, mutate through named methods returning
  `Result<Updated>`. The caller supplies the id; `Guid.Empty` is rejected.
- Properties: `private set` or get-only. Only `AuditableEntity` / `ISoftDeletable` members are
  public (interceptors stamp them).
- All guards run before any assignment, so a rejected `Update` leaves the entity untouched.
  Normalise (`.Trim()`) inside the entity.
- Never throw for a business rule: `return CarErrors.MakeRequired;`. Errors live in
  `<Entity>Errors` as `public static readonly Error`; a parametrized one is a method. Every
  `Error.Code` is a `LocalizationKeys` constant that exists in both `SharedResource` files.
- Translatable text is `LocalizedText`, never `NameEn`/`NameAr`. `LocalizedText` has no equality.
- A relationship to another aggregate is a `Guid?` id, never a navigation.
- A collection is a private `List<T>` exposed as `IReadOnlyCollection<T>`, changed only by methods
  on the aggregate.
- A domain event is a `sealed record ... : DomainEvent` of serializable values (ids, strings,
  numbers), raised after the guards pass, in `<Plural>/Events/`.
- Never reference EF Core, ASP.NET, `HttpContext`, a logger, DI or async code (a test fails).

A rule that needs a query (uniqueness, another aggregate) is not an entity rule: it lives in the
command handler. Details only if needed: `src/Taxi.Domain/Domain_Layer_Blueprint.md` §5–§9.
