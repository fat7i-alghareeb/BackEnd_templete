# 🏛️ Architectural Constitution: Domain Layer Blueprint

## 1. Executive Summary & Layer Purpose

The Domain Layer is the absolute center of the Clean Architecture concentric circle. It is the heart of the software, responsible for encapsulating the entirety of the enterprise business rules, logic, and state. In a Domain-Driven Design (DDD) approach, this layer models the real-world business as a series of Aggregates, Entities, Value Objects, and Domain Events. It tells the story of the business without being distracted by technical implementation details.

When a new developer joins the team, they should be able to read the source code in the Domain layer and immediately understand the business workflow, the constraints, and the terminology, all without needing to know whether the application is a web API, a console app, or backed by SQL Server or MongoDB.

### What is the exact role of this layer in Clean Architecture?

The primary role of the Domain Layer is to serve as the undisputed source of truth for all business logic. It provides a robust, self-validating, and rich object-oriented model that guarantees data consistency and enforces business invariants. Every time a state change occurs within the system, the Domain Layer is the absolute gatekeeper that determines whether that state change is legally permissible according to the strictly defined business rules.

For example, in an e-commerce domain, rules such as "An order cannot be cancelled if it has already been shipped" or "A customer cannot purchase a product that is out of stock" must be rigidly enforced here. If these rules are pushed up into the Application layer (e.g., inside MediatR command handlers) or the API layer (e.g., inside controllers), the business logic becomes fragmented, leading to massive duplication and fragile codebase integrity.

### What are its primary responsibilities?

1. **Invariant Enforcement:** Ensuring that an entity or aggregate never enters an illegal, invalid, or impossible state. Objects must be valid from the very moment of their creation until their destruction. This heavily relies on factory methods that validate parameters before creating instances.
2. **State Mutation Management:** Controlling how data changes over time through clearly defined, behavior-driven methods (e.g., `ShipOrder()`, `ApplyDiscount()`) rather than simple CRUD-style property setters.
3. **Domain Event Generation:** Emitting historical facts (events) that describe what just happened within the domain. This allows loosely coupled systems (orchestrated via the Application layer) to react to those changes asynchronously, such as sending a confirmation email when an `OrderPlacedEvent` is dispatched.
4. **Error Definition:** Clearly defining all possible business failure conditions through a standardized, exceptionless `Result` pattern. Errors should map to human-readable codes and strict error types.

### What is STRICTLY FORBIDDEN in this layer?

- **External Dependencies:** The Domain Layer must not reference any external packages, frameworks, or libraries (e.g., Entity Framework Core, ASP.NET Core, SendGrid, Stripe). It relies exclusively on the .NET Base Class Libraries (BCL). Even referencing `Microsoft.EntityFrameworkCore` to use `[NotMapped]` is a code smell if it can be avoided (though sometimes pragmatic compromises are made). In strict environments, zero external NuGet packages should appear in this `.csproj` file.
- **Infrastructure Leakage:** It must have zero knowledge of how data is persisted, how data is serialized, or how logging is performed. There are absolutely no SQL queries, `DbContexts`, HTTP clients, or API calls here.
- **Anemic Models:** Classes that are simply "data bags" consisting exclusively of public parameterless constructors and public getters/setters are strictly prohibited. These strip behavior from the domain, leading to scattered, duplicated logic (often referred to as an anti-pattern called "Transaction Script").
- **Exceptions for Flow Control:** Throwing exceptions for predictable, business-rule violations (like "Insufficient Funds", "Item Not In Stock", or "Invalid Email Address") is absolutely banned. Exceptional mechanisms are for unpredictable, unrecoverable system failures only.

---

## 2. Dependency Rules & Boundaries

The Clean Architecture dependency rule states that source code dependencies must point only inward, toward the core business logic.

### Inward Pointing Dependencies

**The Domain Layer points to nothing.** It is at the absolute center of the universe. It does not reference the Application, Infrastructure, API, or Contracts layers. By having zero inward dependencies, the Domain Layer becomes highly testable, extremely stable, and completely immune to changes in technological trends. If the company decides to migrate from SQL Server to PostgreSQL, or to swap the user interface from a web application to a desktop client, the Domain Layer remains completely untouched.

### Outward Pointing Dependencies

All other layers in the architecture look toward the Domain Layer.

- **The Application Layer** depends heavily on the Domain to orchestrate use cases. It fetches Aggregates from database interfaces, calls domain behavioral methods to perform logic, captures emitted Domain Events, and ultimately saves the data back.
- **The Infrastructure Layer** references the Domain in order to map Entities to database tables (via ORM Fluent Interfaces inside `IEntityTypeConfiguration` classes) and to implement concrete repositories.
- **The API Layer** references the Domain (usually transitively via Application) and maps specialized Domain Errors to standard HTTP Problem Details (e.g., mapping `ErrorKind.Conflict` to HTTP 409).
- **The Contracts Layer** may optionally mirror enumerations defined in the Domain Layer (though strictly, Contracts are purely boundary messages), but Contracts never reference the Domain project directly.

### Explanation of the Dependency Inversion Principle (DIP)

Because the Domain Layer cannot depend on the Infrastructure Layer, how does it communicate with a database or an external service? The reality is: It doesn't. Applying the Dependency Inversion Principle, the Domain (or Application) layer dictates the _abstractions_ (interfaces) it needs to perform work, and the outmost rings (Infrastructure) provide the concrete implementations.

For instance, if a domain rule requires checking if a unique product SKU already exists, the domain does not inject an `ISkuRepository` into its entities. Instead, the Application layer performs the lookup using the interface, gathers the boolean result, and passes that pure primitive `bool isUnique` value into the Domain entity's method. The Domain Layer remains pure and agnostic of the technical mechanism.

---

## 3. Directory Structure & Anatomy

To ensure high cohesion, clarity, and ease of navigation for developers, the Domain Layer enforces a disciplined directory anatomy. The directory tree serves as the Table of Contents for your business.

```text
src/MechanicShop.Domain/
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
├── Customers/
│   ├── Customer.cs
│   ├── CustomerErrors.cs
│   └── Events/
│       └── CustomerRegisteredEvent.cs
├── Orders/
│   ├── Billing/
│   │   ├── Invoice.cs
│   │   ├── InvoiceErrors.cs
│   │   └── InvoiceLineItem.cs
│   ├── Enums/
│   │   └── OrderState.cs
│   ├── Events/
│   │   ├── OrderCancelledEvent.cs
│   │   └── OrderPlacedEvent.cs
│   ├── Order.cs
│   ├── OrderErrors.cs
│   ├── OrderLineItem.cs
│   └── ValueObjects/
│       └── Address.cs
└── Products/
    ├── Enums/
    │   └── Category.cs
    ├── Product.cs
    └── ProductErrors.cs
```

### Detailed Breakdown of Directories

- **`Common/`**: Contains the foundational building blocks that all other domain components inherit from. This includes base classes for Entities, Auditable Entities, Domain Events, and the structural implementation of the custom `Result` pattern. Nothing in `Common` should contain business logic specific to any one feature.
- **`[AggregateName]/`**: Root-level folders belong entirely to an aggregate. Everything related to `Orders` goes in the `Orders` directory. This creates distinct technical boundaries drawn along business lines (Bounded Contexts logic inside monoliths).
- **`[AggregateName]/Events/`**: Holds the strongly typed Domain Events containing historical notifications (e.g., `OrderShippedEvent`) that are raised by the aggregate when meaningful business milestones happen. These classes should be lightweight, simple DTOs inheriting from `DomainEvent`.
- **`[AggregateName]/Enums/`**: Holds domain-specific enumerations (e.g., `OrderState`, `PaymentMethod`). These should never be magic strings or magic numbers within the logic.
- **`[AggregateName]/ValueObjects/`**: Holds immutable objects that have no distinct identity but are defined purely by their structural values (e.g., `Address`, `Money`, `EmailAddress`). Two `Money` objects with `$5.00` are perfectly equal, whereas two `Order` entities are distinct even if their data matches, solely due to their Guid Identifiers.
- **`[AggregateName]/[SubordinateEntity]/`**: When an aggregate has child entities (like an `Order` having an `Invoice` or an `OrderLineItem`), they reside in a sub-folder or alongside the root, denoting their clear relationship to the aggregate root.

---

## 4. Core Design Patterns & Mechanics

The Domain Layer achieves its rigorous consistency through three major patterns: Base Entity Inheritance, Domain Event Dispatching, and Factory Instantiation.

### 4.1. Base `Entity` Mechanics

The `Entity` class provides primary key definitions (typically strong `Guid` values to prevent simple enumeration attacks and provide global uniqueness) and centralized event management. By storing an internal, private list of `DomainEvent` instances and exposing them as a read-only collection, the entity guarantees that external systems cannot arbitrarily push or remove events. Events are added directly by the entity when an internal behavioral method signifies a state change.

It must look somewhat identical to this implementation:

```csharp
public abstract class Entity
{
    public Guid Id { get; }

    private readonly List<DomainEvent> _domainEvents = [];

    // [NotMapped] is the lone acceptable ORM attribute if required, to ensure events aren't mapped to tables
    [NotMapped]
    public IReadOnlyCollection<DomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected Entity() { }

    protected Entity(Guid id)
    {
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
    }

    public void AddDomainEvent(DomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    public void RemoveDomainEvent(DomainEvent domainEvent) => _domainEvents.Remove(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();
}
```

### 4.2. Base `AuditableEntity` Mechanics

Certain business entities demand strict auditing for compliance purposes (knowing exactly _when_ something was created/modified and _who_ did it). The `AuditableEntity` inherits from `Entity` and adds primitive properties like `CreatedAtUtc`, `CreatedBy`, `LastModifiedUtc`, and `LastModifiedBy`.

**Mechanic:** These properties are _not_ manipulated directly by the Domain methods. They act as markers. The framework magic happens entirely within the Infrastructure layer, where an Entity Framework `SaveChangesInterceptor` hooks into the database commit process, detects any tracked `AuditableEntity` being modified or added, and automatically injects the current UTC timestamp and the User ID from the HTTP Context. This relieves the Domain of having to constantly pass `DateTime.UtcNow` into every method.

```csharp
public abstract class AuditableEntity : Entity
{
    protected AuditableEntity() { }
    protected AuditableEntity(Guid id) : base(id) { }

    public DateTimeOffset CreatedAtUtc { get; set; } // Must always be UTC (TimeSpan.Zero)
    public string? CreatedBy { get; set; }
    public DateTimeOffset LastModifiedUtc { get; set; } // Must always be UTC (TimeSpan.Zero)
    public string? LastModifiedBy { get; set; }
}
```

### 4.3. Domain Event Mechanics (`INotification`)

Domain events represent a decoupled notification system. They implement the MediatR `INotification` interface. An aggregate raises an event via `AddDomainEvent(new OrderPlacedEvent { OrderId = Id });`.

When the Application layer's Command Handler triggers `await _dbContext.SaveChangesAsync()`, the Infrastructure interceptor pauses the commit, gathers all events from all tracked entities, and pushes them sequentially through MediatR `IPublisher`. This allows separate handler classes to invoke side-effects (like caching invalidation or pushing to a message bus) entirely decoupled from the entity mutation.

---

## 5. Implementation Guidelines & Code Examples

Developing within the Domain Layer requires strict adherence to encapsulation. Properties must be guarded, constructors must be hidden from developers, and operations must reflect real human behaviors instead of programmatic programmatic assignment operators.

### The "Right Way" Example: Order Aggregate

The following abstract `Order` entity from an E-Commerce domain portrays the gold standard of architectural compliance. Observe how it uses `Result<T>` instead of exceptions, private constructors for the ORM, static factories, encapculated lists, and pure behavior-driven methods.

```csharp
using MechanicShop.Domain.Common;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Orders.Enums;
using MechanicShop.Domain.Orders.Events;

namespace MechanicShop.Domain.Orders;

// 1. Inherit from AuditableEntity for automated metadata tracking
public sealed class Order : AuditableEntity
{
    // 2. Properties have private setters. They cannot be changed casually from the outside.
    public Guid CustomerId { get; }
    public DateTimeOffset PlacedAtUtc { get; private set; }
    public OrderState State { get; private set; }

    // 3. Collections are strictly private and exposed only as read-only.
    // This entirely prevents external code from executing harmful operations: `order.LineItems.Add(illegalItem)`
    private readonly List<OrderLineItem> _lineItems = [];
    public IEnumerable<OrderLineItem> LineItems => _lineItems.AsReadOnly();

    // 4. Calculated properties provide instant, synchronized read models without database queries.
    public decimal TotalAmount => _lineItems.Sum(x => x.LineTotal);

    // 5. Parameterless constructor is absolutely required by Entity Framework Core for hydration via reflection,
    // but MUST be marked private so developers cannot create empty, fundamentally invalid objects in memory.
#pragma warning disable CS8618
    private Order() { }
#pragma warning restore CS8618

    // Private instantiation constructor
    private Order(Guid id, Guid customerId, DateTimeOffset placedAtUtc, List<OrderLineItem> lineItems)
        : base(id)
    {
        CustomerId = customerId;
        PlacedAtUtc = placedAtUtc;
        State = OrderState.Pending;
        _lineItems = lineItems;
    }

    // 6. Static Factory Method ensuring invariants are satisfied BEFORE object creation occurs.
    // This method intercepts bad inputs, guaranteeing invalid state cannot exist.
    public static Result<Order> Create(Guid id, Guid customerId, DateTimeOffset placedAtUtc, List<OrderLineItem> lineItems)
    {
        if (id == Guid.Empty)
        {
            return OrderErrors.OrderIdRequired;
        }

        if (customerId == Guid.Empty)
        {
            return OrderErrors.CustomerIdRequired;
        }

        if (lineItems is null || lineItems.Count == 0)
        {
            return OrderErrors.LineItemsRequired;
        }

        // Return a fully valid object wrapped in a Success Result
        return new Order(id, customerId, placedAtUtc, lineItems);
    }

    // 7. Behavioral Method using the Result pattern. No "public set" for State!
    public Result<Updated> Cancel()
    {
        // Guard checking the business rule.
        if (State is OrderState.Shipped or OrderState.Delivered)
        {
            // Returning a typed failure instead of throwing an Exception
            return OrderErrors.CannotTransition(State, OrderState.Cancelled);
        }

        // State mutation
        State = OrderState.Cancelled;

        // 8. Raising a targeted Domain Event signaling a significant occurrence happened
        AddDomainEvent(new OrderCancelledEvent { OrderId = this.Id });

        return Result.Updated;
    }

    public Result<Updated> AddLineItem(OrderLineItem item)
    {
        if (!IsEditable)
        {
            return OrderErrors.OrderLockedForModifications;
        }

        if (_lineItems.Any(i => i.ProductId == item.ProductId))
        {
            return OrderErrors.DuplicateProduct;
        }

        _lineItems.Add(item);
        return Result.Updated;
    }

    // Abstracting complex validation rules into readable boolean expressions
    public bool IsEditable => State is not (OrderState.Completed or OrderState.Cancelled or OrderState.Shipped);
}
```

---

## 6. The "Result Pattern" & Error Handling (Contextualized)

Exception handling via `try/catch` incurs immense system overhead when the unwinding stack trace logic triggers. Even worse, business rules (like a product being out of stock or a username already being taken) are not "Exceptional"—they are extremely common, completely natural branch conditions of our defined domain. They belong to normal application flow.

### Why the Result Pattern?

The Domain Layer treats failure as a first-class citizen. Rather than interrupting control flow dynamically via unhandled exceptions (which act like hidden `GOTO` statements), Domain entities explicitly return a `Result<T>` struct indicating if an operation succeeded or failed. This forces the caller (the Application Layer) to consciously and actively evaluate the output of the operation before proceeding.

### How Errors Are Handled Specifically in this Layer

Errors are instantiated through a statically typed class combination inside `[Aggregate]Errors.cs` files. These errors provide an unambiguous code and a human-readable description. **Important**: All `code` values must correspond to a key in `LocalizationKeys` (Contracts layer).

By typing the error with a core `ErrorKind` enum, the outer API layer can trivially map a `Validation` error to a 400 Bad Request HTTP status, a `Conflict` to a 409 Conflict status, or a `NotFound` to a 404 Not Found status.

#### Implementing Errors with Parametrized Arguments

The `Error` record supports an optional `Args` array for dynamic localization (e.g., "WorkOrder {0} is locked").

```csharp
public static class OrderErrors
{
    // A standard validation error using a key from LocalizationKeys
    public static readonly Error OrderIdRequired = Error.Validation(
        code: LocalizationKeys.Validation.RequiredField,
        description: "Order ID is mandatory.");

    // Dynamic errors passing arguments into the 'Args' array for the localizer
    public static Error CannotTransition(OrderState current, OrderState next) => Error.Conflict(
        code: LocalizationKeys.WorkOrder.InvalidStateTransition,
        description: $"Cannot move from {current} to {next}.",
        args: [current, next]);
}
```

By defining errors purely as simple strings with an associated conceptual `Type` (`Failure`, `Unexpected`, `Validation`, `Conflict`, `NotFound`, `Unauthorized`, `Forbidden`), the domain remains incredibly expressive, powerful, yet entirely decoupled from web mechanics.

#### Field semantics on the `Error` struct

| Field          | Always set?               | Meaning                                                                                                                                                                                                 |
| -------------- | ------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `Code`         | yes                       | The localization key — a `LocalizationKeys.X` constant. Used by the API to look up the translated message.                                                                                              |
| `Description`  | yes                       | English fallback used when the localizer cannot resolve `Code`.                                                                                                                                         |
| `Type`         | yes                       | An `ErrorKind` enum that drives the HTTP status mapping in the API.                                                                                                                                     |
| `Args`         | optional                  | Format arguments for parametrized localized messages (e.g. `WorkOrder.TimingReadonly` uses `{0}` and `{1}`).                                                                                            |
| `PropertyName` | optional, validation only | Set only by `Error.ValidationForProperty(propertyName, code, args)`. Carries the offending request field name (e.g. `"NameEn"`) so the API can build an RFC 7807 `errors` dictionary keyed by property. |

Use `Error.Validation(code, description)` for free-form domain validation errors. Use `Error.ValidationForProperty(propertyName, code)` only from the Application layer's `ValidationBehavior` when the error originates from a specific request property (FluentValidation surfaces the property name; the behavior forwards it). Domain entities should never need to set `PropertyName` directly.

---

## 7. Anti-Patterns & "Code Smells" (The Rejection Criteria)

Any codebase submission attempting to corrupt the sanctity of the Domain Layer will be met with immediate Pull Request rejection. Reviewers will scour branches specifically for these dangerous anti-patterns. Adhere strictly to the avoidance of the following.

### Immediate PR Rejection Checklist for the Domain Layer

1. 🚨 **Anemic Domain Models:**
   - **The Wrong Way:** `public class Order { public Guid Id { get; set; } public string Status { get; set; } }`
   - **Why it's rejected:** Data is blindly exposed. Anyone, anywhere in the app can change the Status, bypassing business events and validation rules.
   - **The Right Way:** Fields utilize `private set`, modifications exist purely through meaningful methods (`public Result<Updated> ShipOrder()`).

2. 🚨 **Throwing Exceptions for Domain Logic:**
   - **The Wrong Way:** `if (balance < 0) throw new InvalidOperationException("Not enough funds");`
   - **Why it's rejected:** Exceptions denote a technical collapse. Exceeding a wallet balance is standard business logic.
   - **The Right Way:** `if (balance < 0) return WalletErrors.InsufficientFunds;`

3. 🚨 **Exposed Collections:**
   - **The Wrong Way:** `public List<OrderLineItem> LineItems { get; set; } = new();`
   - **Why it's rejected:** Any external service can call `.Add()` or `.Clear()`, violating aggregate boundaries.
   - **The Right Way:** `private readonly List<OrderLineItem> _lineItems = []; public IEnumerable<OrderLineItem> LineItems => _lineItems.AsReadOnly();`

4. 🚨 **Bilingual String Pollution (Separate Columns):**
   - **The Wrong Way**: `public string NameEn { get; set; } public string NameAr { get; set; }`
   - **Why it's rejected**: It pollutes the database table with redundant columns and makes the model inflexible.
   - **The Right Way**: Use the `LocalizedText` Value Object: `public LocalizedText Name { get; private set; }`. Ensure the matching `LocalizationKeys` exist for error reporting and `Languages` constants are used in any language-specific logic.

5. 🚨 **Domain Instantiation Bypassing Validation:**
   - **The Wrong Way:** Utilizing a `public Order(...)` constructor. Constructors cannot easily perform asynchronous work and usually cannot gracefully return a `Result<T>` struct (they return the object itself).
   - **Why it's rejected:** It leads to exceptions being thrown during `new` operations.
   - **The Right Way:** Enforcing a `private Order(...)` constructor combined with a `public static Result<Order> Create(...)` factory.

6. 🚨 **Infrastructure / Third-Party Imports:**
   - **The Wrong Way:** `using Microsoft.EntityFrameworkCore;`, `using AutoMapper;`, `using MediatR;` (except for `INotification` abstractions) appearing _anywhere_ in the Domain project files.
   - **Why it's rejected:** The Domain must survive if the technology stack is replaced.
   - **The Right Way:** Data access configuration, mapping, and annotations must occur in the Infrastructure layer exclusively, utilizing separate `IEntityTypeConfiguration<T>` implementations for EF Core.

7. 🚨 **Primitive Obsession & Magic Strings / Numbers:**
   - **The Wrong Way:** `if (orderStatus == "Complete")` or `if (type == 1)`
   - **Why it's rejected:** String comparisons are brittle, prone to typos, and impossible to safely refactor. Integer types lack descriptive context.
   - **The Right Way:** `if (State == OrderState.Completed)`. Always use typed `enums` or strongly-typed Value Objects.

8. 🚨 **Fetching Data Inside Aggregates:**
   - **The Wrong Way:** Injecting a repository directly into a Domain Entity to check a database state.
   - **Why it's rejected:** Entities are meant to exist in memory freely. They shouldn't be orchestrating asynchronous IO operations.
   - **The Right Way:** Perform the data lookup in the Application Layer (Command Handler) and pass the retrieved primitive value into the entity's behavior method.

---

## 8. Registration & Dependency Injection

The fundamental question typically asked by developers new to Clean Architecture is: _"How do we configure our Domain Dependency Injection inside our standard `DependencyInjection.cs` setup file?"_

The answer is profound: **We don't.**

The Domain Layer does not have an inversion of control (IoC) container. It does not configure runtime services, transient lifetimes, scoped lifetimes, or singletons. It simply provides raw object blueprints, invariant rules, and data structures. A standard `DependencyInjection.cs` extension file should literally not exist within the root of the Domain project because the Domain has zero services orchestrating asynchronous remote calls, logging, or database fetching patterns.

### Where Do Dependencies Belong?

If your Domain Layer ever requires fetching an external state to make a decision (e.g., calling an external shipping calculator before authorizing a shipment), a Domain Interface (e.g., `IShippingCalculator`) should be established inside the Application layer (or optionally the Domain layer if deeply coupled to modeling terms). The concrete implementation of that calculator must dwell in the Infrastructure Layer where it is registered in IoC.

The Application Layer's Command Handler will inject the `IShippingCalculator`, perform the asynchronous call over HTTP, receive the parsed flat cost value, and then inject that raw `decimal` cost _into_ the pure Domain entity's behavioral method.
If your Domain Layer ever requires fetching an external state to make a decision (e.g., calling an external shipping calculator before authorizing a shipment), a Domain Interface (e.g., `IShippingCalculator`) should be established inside the Application layer (or optionally the Domain layer if deeply coupled to modeling terms). The concrete implementation of that calculator must dwell in the Infrastructure Layer where it is registered in IoC.

The Application Layer's Command Handler will inject the `IShippingCalculator`, perform the asynchronous call over HTTP, receive the parsed flat cost value, and then inject that raw `decimal` cost _into_ the pure Domain entity's behavioral method.

By avoiding DI containers, bypassing external assemblies, and strictly restricting dependencies, the Domain Layer becomes effortlessly tested explicitly via unit tests. Because there are no HTTP clients or databases referenced, you require absolutely zero mock setups (like Moq or NSubstitute) and zero scaffolding grids to prove your business logic works. A simple instantiation and method call in xUnit or NUnit is all that is ever required. This results in blisteringly fast test suites that run in milliseconds, ensuring unparalleled developer productivity and supreme system integrity.

## 9. Advanced DDD Concepts

### 9.1. Value Objects: Conquering Primitive Obsession

A **Value Object** is an immutable type whose identity is defined entirely by its structural values rather than a unique identifier (like a `Guid`). In the physical world, two fifty-dollar bills are effectively identical and interchangeable; you only care about their value, not their serial numbers.

**Why use them?**
Relying on standard primitives (`string`, `decimal`, `int`) to represent complex business concepts is an anti-pattern known as _Primitive Obsession_. For instance, representing an address as four disconnected `string` properties on an `Order` entity scatters the validation logic and makes the entity bloated. Instead, we encapsulate them into a `ValueObject`.

**Key Constraints:**

- **Immutability:** Once created, a Value Object cannot be altered. To change it, you must replace it entirely.
- **Structural Equality:** Two Value Objects are equal if, and only if, all their internal properties match exactly.

#### The "Right Way" Example: LocalizedText (Bilingual Strings)

The `LocalizedText` Value Object is the mandated carrier for all translatable fields (Names, Descriptions). It ensures English and Arabic versions are always coupled and stored atomically in JSONB.

```csharp
namespace MechanicShop.Domain.Common;

public sealed class LocalizedText : ValueObject
{
    public LocalizedText(string en, string ar)
    {
        En = en;
        Ar = ar;
    }

    // Parameterless constructor required by EF Core for JSONB materialization
    private LocalizedText()
    {
        En = string.Empty;
        Ar = string.Empty;
    }

    public string En { get; private set; }
    public string Ar { get; private set; }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return En;
        yield return Ar;
    }
}
```

**Bilingual Rule**: All translatable string fields on entities MUST use `LocalizedText`. Raw `string` properties for bilingual text (e.g., `NameEn`) are a PR rejection criterion.

---

#### The "Right Way" Example: Address Value Object

```csharp
using MechanicShop.Domain.Common;
using MechanicShop.Domain.Common.Results;

namespace MechanicShop.Domain.Orders.ValueObjects;

// Inherits from a foundational base class that overrides Equality Operators (==, !=, Equals, GetHashCode)
public sealed class Address : ValueObject
{
    public string Street { get; }
    public string City { get; }
    public string ZipCode { get; }
    public string Country { get; }

    private Address(string street, string city, string zipCode, string country)
    {
        Street = street;
        City = city;
        ZipCode = zipCode;
        Country = country;
    }

    public static Result<Address> Create(string street, string city, string zipCode, string country)
    {
        if (string.IsNullOrWhiteSpace(street) || string.IsNullOrWhiteSpace(city))
        {
            return Error.Validation("Address.Invalid", "Street and City must be provided.");
        }

        // Additional domain validation (e.g., ZipCode formatting) happens here...

        return new Address(street.Trim(), city.Trim(), zipCode.Trim(), country.Trim());
    }

    // Required by the abstract ValueObject base class to perform structural equality
    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Street;
        yield return City;
        yield return ZipCode;
        yield return Country;
    }
}
```

---

### 9.2. Domain Services: Dealing with Cross-Aggregate Rules

There are times when a piece of business logic does not naturally reside within a single Aggregate or Entity. Forcing a cross-cutting calculation into an entity often creates an awkward, mathematically disjointed model. This is where **Domain Services** step in.

**What are they?**
Domain Services are stateless classes containing pure business algorithms that orchestrate logic across multiple domain objects.

**How do they differ from Application Services?**
An _Application Service_ (or MediatR Command Handler) is responsible for infrastructure orchestration: fetching from databases, calling APIs, sending emails, and wrapping transactions. A _Domain Service_ performs zero I/O. It simply takes Domain Entities as inputs, performs hardcore business calculations natively in memory, and returns a `Result`.

#### The "Right Way" Example: Cross-Entity Discount Calculation

```csharp
using MechanicShop.Domain.Orders;
using MechanicShop.Domain.Customers;
using MechanicShop.Domain.Common.Results;

namespace MechanicShop.Domain.Services;

// Notice there is no interface injection (no IRepository) in the constructor.
// This is pure, in-memory domain mathematics.
public sealed class OrderDiscountDomainService
{
    public Result<decimal> CalculateLoyaltyDiscount(Customer customer, Order incomingOrder)
    {
        if (customer.TotalHistoricalSpend > 10000m && incomingOrder.TotalAmount > 500m)
        {
            // Give a 15% discount
            return incomingOrder.TotalAmount * 0.15m;
        }

        if (customer.IsVIP)
        {
            // Give a flat 10% discount
            return incomingOrder.TotalAmount * 0.10m;
        }

        // No discount
        return 0m;
    }
}
```

---

### 9.3. Aggregate Roots & Persistence Boundaries

One of the most critical laws of Domain-Driven Design is the concept of the **Aggregate Root bounding box**. An Aggregate is a cluster of domain objects (Entities and Value Objects) that are treated as a single transactional unit of work.

**The Strict Repository Rule:**
Only the _Aggregate Root_ is permitted to have a Repository. You will have an `IOrderRepository`, but you are strictly forbidden from creating an `IOrderLineItemRepository` or `IInvoiceRepository`.

**Why? Transactional Consistency.**
If a developer could directly fetch an `OrderLineItem` from the database, update its pricing, and save it directly via an `IOrderLineItemRepository`, they would entirely bypass the business rules enforced by the parent `Order` aggregate (such as recalculating total discounts, checking shipping limits, or verifying the order state is `Pending`).

By enforcing that all actions must flow through the Aggregate Root, we guarantee that the entity’s invariants are mathematically proven before the database transaction commits.

#### The Rejection Criteria: Persistence Boundaries

- 🚨 **The Wrong Way:** Injecting `IOrderLineItemRepository` to insert a new row into the `OrderLineItems` SQL table directly.
- ✅ **The Right Way:** Fetching the `Order` aggregate cleanly via `IOrderRepository.GetByIdAsync()`, calling `order.AddLineItem(...)`, and then calling `IOrderRepository.Update(order)`. The ORM (Entity Framework Core) seamlessly tracks the graph and inserts the child records automatically upon `SaveChangesAsync()`.

---

### 9.4. Unit Testing Strategy: The Reward of Purity

Because the Domain Layer explicitly bans all forms of external I/O, database contexts, third-party network libraries, and complex Dependency Injection, it unlocks the highest tier of software testing: **Blazing Fast, Mock-Free Unit Tests.**

Since Domain Entities are just pure C# objects, you simply instantiate them using `new` (or via their `Create` factory), call their methods, and assert the output. A suite of 10,000 domain unit tests will execute in mere milliseconds.

#### The "Right Way" Example: Testing the Result Pattern

```csharp
using Xunit;
using FluentAssertions;
using MechanicShop.Domain.Orders;
using MechanicShop.Domain.Orders.Enums;

namespace MechanicShop.Domain.UnitTests.Orders;

public class OrderTests
{
    [Fact]
    public void Cancel_ShouldReturnSuccess_WhenOrderIsPending()
    {
        // Arrange: Create a valid aggregate in memory
        var orderResult = Order.Create(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, GetValidLineItems());
        var order = orderResult.Value;

        // Act: Execute the business behavior
        var cancelResult = order.Cancel();

        // Assert: Verify the Result pattern output
        cancelResult.IsSuccess.Should().BeTrue();
        order.State.Should().Be(OrderState.Cancelled);
        order.DomainEvents.Should().ContainSingle(e => e is OrderCancelledEvent);
    }

    [Fact]
    public void Cancel_ShouldReturnError_WhenOrderIsAlreadyShipped()
    {
        // Arrange
        var orderResult = Order.Create(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, GetValidLineItems());
        var order = orderResult.Value;

        // Simulating the internal transition to Shipped to test the invariant guard
        order.Ship();
        // Simulating the internal transition to Shipped to test the invariant guard
        order.Ship();

        // Act
        var cancelResult = order.Cancel();

        // Assert
        cancelResult.IsSuccess.Should().BeFalse();
        cancelResult.TopError.Code.Should().Be(OrderErrors.CannotTransition(OrderState.Shipped, OrderState.Cancelled).Code);
        order.State.Should().Be(OrderState.Shipped); // Ensure state didn't change
    }
}
```
