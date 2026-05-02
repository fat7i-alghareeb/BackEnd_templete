# 🏛️ Architectural Constitution: Contracts Layer Blueprint

## 1. Executive Summary & Layer Purpose

The Contracts Layer acts as the absolute boundary and communication protocol between the outside world and the inner workings of our software. In a Clean Architecture solution, the Contracts Layer defines the exact shape of data entering the system (Requests) and leaving the system (Responses). It provides a rigidly structured, highly predictable API surface that clients (web apps, mobile apps, other microservices) can depend on without needing to understand the underlying Domain or Application complexities.

When a frontend developer or external consumer integrates with the system, they should only ever look at the Contracts Layer. This layer serves as the "System Schema."

### What is the exact role of this layer in Clean Architecture?

The primary role of the Contracts Layer is to provide strongly-typed Data Transfer Objects (DTOs) that decouple external communication from internal domain modeling. Data entering from an HTTP request or a message queue is immediately bound to a Contract DTO. Only after this DTO is received are the fields mapped to Application Layer commands or Domain entities. Conversely, when the system returns data, internal models are flattened into Response DTOs before serialization to the client.

For example, a `Customer` Domain Entity might have private fields, behavioral methods, and event dispatchers. Passing this raw entity out to a client exposes internal secrets and tightly couples the API to the database schema. Instead, the `Customer` is mapped to a `CustomerResponse` contract, which is safe to serialize.

### What are its primary responsibilities?

1. **Defining Requests:** Establishing exact requirements for inbound data payloads (e.g., `CreateOrderRequest`, `UpdateUserProfileRequest`).
2. **Defining Responses:** Shaping how data is returned to the client in a secure, flattened, and optimized format (e.g., `OrderSummaryResponse`, `PaginatedListResponse`).
3. **Surface-Level Validation:** Applying initial, low-level property validation (e.g., maximum string lengths, email regex formats, required fields) using pure `System.ComponentModel.DataAnnotations`.
4. **Providing Shared Primitives:** Hosting enumerations (e.g., `OrderState`, `InvoiceStatus`) that need to be shared between the API, Domain, and external Clients.
5. **Hosting Localization Keys:** Serving as the central registry for `LocalizationKeys`, ensuring that the API, Domain, and Blazor Client use the exact same strongly-typed keys for all user-facing messages.

### What is STRICTLY FORBIDDEN in this layer?

- **Business Logic:** There must be absolutely no business rule enforcement here. No checking database state, no calculating totals, and no domain invariant checks.
- **Complex Dependencies:** The Contracts layer must be the lightest project in the entire solution. It should have **zero** NuGet package dependencies except for fundamental validation attributes (`Microsoft.AspNetCore.Components.DataAnnotations.Validation` if necessary in newer .NET frameworks).
- **Domain Leakage:** Contracts must never reference Domain Aggregates or Application Command structures. It must be completely ignorant of the system's inner rings.
- **Behavior/Methods:** DTOs are "dumb" records. They should not contain functions, constructors with logic, or behavioral properties. They only hold state.

---

## 2. Dependency Rules & Boundaries

In clean architecture, the Contracts layer sits at a unique intersection. It operates at the outermost edge alongside the API layer, yet it is dependency-free like the Domain layer.

### Inward Pointing Dependencies

**The Contracts Layer points to nothing.** In a strictly enforced project, the Contracts project has no project references. It stands entirely alone. This allows it to be aggressively shared (e.g., packaged as a NuGet or built into a generic library) and consumed by external clients (like a Blazor WebAssembly app or a separate .NET MAUI mobile client) without dragging heavy internal logic or ORM packages over the wire.

### Outward Pointing Dependencies

Because the Contracts Layer defines the communication vocabulary, other layers must reference it:

- **The API / Presentation Layer** references Contracts to bind HTTP JSON bodies to Request objects and to serialize returned data into Response objects.
- **The Application Layer** (Optional but common) often references Contracts to use the primitive Requests when mapping to Commands or returning Queries, avoiding an extra intermediate DTO mapping layer, although strictest Clean Architectures may map Contracts directly in the API controllers.

---

## 3. Directory Structure & Anatomy

The Contracts directory structure directly aligns with the features or conceptual Aggregates of the system.

```text
src/MechanicShop.Contracts/
├── Common/
│   ├── LocalizationKeys.cs  <-- Strongly-Typed Error/UI Keys
│   ├── Languages.cs         <-- Canonical Language Codes (en, ar)
│   ├── OrderState.cs
│   ├── PaymentMethod.cs
│   └── CountryCode.cs
├── Requests/
│   ├── Customers/
│   │   ├── CreateCustomerRequest.cs
│   │   └── UpdateCustomerRequest.cs
│   └── Orders/
│       ├── CreateOrderRequest.cs
│       ├── CreateOrderLineItemRequest.cs
│       └── CancelOrderRequest.cs
└── Responses/
    ├── Common/
    │   └── PagedResponse.cs
    ├── Customers/
    │   └── CustomerSummaryResponse.cs
    └── Orders/
        ├── OrderDetailsResponse.cs
        └── OrderLineItemResponse.cs
```

### Detailed Breakdown of Directories

- **`Common/`**: Contains shared data structures and Enumerations that dictate system states (e.g., `OrderState`). Storing Enums here ensures that a strongly-typed web client can use the exact same Enums as the backend Domain.
- **`Requests/`**: A rigidly categorized folder structure holding all inbound payload definitions. Every controller action that expects a body should have a corresponding `[Action]Request` class here. Grouped by aggregate/feature.
- **`Responses/`**: Holds all outbound payload definitions. These are optimized for the client’s screen or data needs, fully decoupled from the internal database schema.

---

## 4. Core Design Patterns & Mechanics

### 4.1. The Dumb DTO Pattern

A Contract object is a genuine Data Transfer Object. It requires no complex instantiation, no immutable builders, and no behavior. In modern C#, these are typically modeled as `class` with `get; set;` properties, or `record` types if immutability after binding is desired.

### 4.2. Surface Validation Strategy (DataAnnotations)

While complex business validation (e.g., "Is this invoice strictly paid before shipping?") belongs in the Application and Domain layers, **surface-level validation** (e.g., "Is the email field actually formatted like an email?", "Is the name missing?", "Is the string over 500 characters?") belongs perfectly in the Contracts layer.

We use `System.ComponentModel.DataAnnotations` (like `[Required]`, `[EmailAddress]`, `[MaxLength]`) directly on Request properties. This allows the API framework (ASP.NET Core) to instantly reject malformed payloads with a 400 Bad Request before the request even hits the Application layer, saving compute cycles.

### 4.3. The Composition Approach (Avoiding Inheritance)

Contracts should avoid deep inheritance hierarchies. An API payload should be readable top-to-bottom. If a `CreateOrderRequest` needs shipping details, it should compose a `ShippingDetailsRequest` property rather than inheriting from a complex base class. Flat, composed structures serialize and deserialize reliably and predictably.

---

## 5. Localization & Strongly-Typed Keys

The Contracts layer hosts the `LocalizationKeys` class. This is the **Supreme Dictionary** of the system.

- **No Magic Strings**: Every error message, validation rule, and UI label that requires translation MUST have a constant in `LocalizationKeys`.
- **Languages Registry**: The `Languages` class defines the supported culture codes (`En`, `Ar`). All layers must use `Languages.Ar` / `Languages.En` instead of raw "ar" / "en" strings.
- **Cross-Layer Parity**: Because the Blazor Client and the API both reference the Contracts layer, they use the exact same keys and language constants.
- **Shared Resources**: Every key in `LocalizationKeys` MUST have a matching entry in `SharedResource.en.json` and `SharedResource.ar.json`.
- **DataAnnotation Integration**: Validation attributes such as `[Required(ErrorMessage = LocalizationKeys.Validation.EnglishNameRequired)]` are picked up at runtime by the API's `InvalidModelStateResponseFactory` (registered in `MechanicShop.Api/DependencyInjection.cs` `AddValidation()`) and translated through the same `IStringLocalizer<SharedResource>` used by FluentValidation and domain errors. The `ErrorMessage` value IS the localization key — never a raw English string.

---

## 6. Implementation Guidelines & Code Examples

Adhere to absolute simplicity when crafting Contracts.

### The "Right Way" Example: Inbound Request Structure

```csharp
using System.ComponentModel.DataAnnotations;
using ECommerce.Contracts.Common;

namespace ECommerce.Contracts.Requests.Orders;

// Note: A simple class with primitive properties and embedded objects.
public class CreateOrderRequest
{
    [Required(ErrorMessage = "The CustomerId parameter is mandatory.")]
    public Guid CustomerId { get; set; }

    [Required(ErrorMessage = "At least one item must be submitted for the order.")]
    [MinLength(1, ErrorMessage = "At least one item must be submitted for the order.")]
    [ValidateComplexType] // Ensures nested objects run their own DataAnnotations
    public List<CreateOrderLineItemRequest> LineItems { get; set; } = [];

    // Enums are native to the Contracts project, allowing strict strongly-typed payloads
    public PaymentMethod PreferredPaymentMethod { get; set; }
}

public class CreateOrderLineItemRequest
{
    [Required]
    public Guid ProductId { get; set; }

    [Range(1, 999, ErrorMessage = "Quantity must be between 1 and 999.")]
    public int Quantity { get; set; }
}
```

### The "Right Way" Example: Outbound Response Structure

Responses never use DataAnnotations, as the internal system emits them, and we trust our internal data to be valid. Responses often flatten complex domain graphs into simple structures needed by the View.

```csharp
using ECommerce.Contracts.Common;

namespace ECommerce.Contracts.Responses.Orders;

// Using 'record' types for Responses is highly encouraged in C# due to
// value equality reporting and the intent of being a read-only projection
public record OrderDetailsResponse(
    Guid Id,
    Guid CustomerId,
    string CustomerFullName,      // Flattened from Customer aggregate
    string CheckoutEmail,         // Flattened from ValueObject wrapper
    DateTimeOffset PlacedAtUtc,
    OrderState State,
    decimal TotalAmount,          // Computed value exposed as primitive
    List<OrderLineItemResponse> Items
);

public record OrderLineItemResponse(
    Guid ProductId,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal
);
```

---

## 7. Anti-Patterns & "Code Smells" (The Rejection Criteria)

PR submissions altering the Contracts Layer will be rigorously scrutinized. The following practices mandate an instant rejection.

### Immediate PR Rejection Checklist for the Contracts Layer

1. 🚨 **Domain Imports (The Ultimate Sin):**
   - **The Wrong Way:** `using ECommerce.Domain.Orders;` inside a Contract file.
   - **Why it's rejected:** The Contracts layer must be distributed to external web/mobile clients. If a Contract references a Domain class, the client must pull down the entire Domain project containing all proprietary business logic.
   - **The Right Way:** Contracts rely purely on .NET Base Class Library primitives (`string`, `int`, `Guid`, `DateTime`).

2. 🚨 **Business Logic / Calculations in DTOs:**
   - **The Wrong Way:** Adding a method `public decimal CalculateTax() { return TotalAmount * 0.05m; }` to a `Response`.
   - **Why it's rejected:** The client shouldn't rely on logic executed during serialization. This logic strictly belongs in the Domain.
   - **The Right Way:** Compute tax inside the Domain aggregate, store it mathematically, and just map it directly to a flat `decimal TaxAmount` property in the Response.

3. 🚨 **Complex Validation (FluentValidation) in Contracts:**
   - **The Wrong Way:** Implementing a system checking if "ProductId exists in database" inside the Contracts project.
   - **Why it's rejected:** The Contracts project cannot connect to a database or use MediatR.
   - **The Right Way:** Use basic `[Required]` or `[MaxLength]` DataAnnotations in Contracts. Perform complex database/business validation using FluentValidation behaviors in the **Application Layer**.

4. 🚨 **Massive, Monolithic Enumerations:**
   - **The Wrong Way:** Storing fifty different, unrelated enumerations inside a single `Constants.cs` contract file.
   - **The Right Way:** Isolate enums contextually (e.g., `OrderState.cs`, `PaymentMethod.cs` in the `Contracts/Common` block).

---

## 8. Registration & Dependency Injection

Because the Contracts Layer simply defines vocabulary (classes, records, and enums) using system primitives, it executes no underlying functionality.

**There is no Dependency Injection configuration for this Layer.**

You will never register a service, interface, or lifetime scoped context in the Contracts project because it performs zero actions. It is a dictionary defining the language of the application boundary.

---

## 9. Advanced Contract Mechanics

### 8.1. Strict Mapping Rules (The Circular Dependency Trap)

A common mistake made by developers transitioning to Clean Architecture is placing mapping methods (e.g., `.ToDomain()` or `.FromCommand()`) directly inside the Contract DTOs.

**Why is this strictly forbidden?**
If a `CreateOrderRequest` object contains a method `public CreateOrderCommand ToCommand()`, then the `Contracts` project must hold a physical assembly reference to the `Application` project (where `CreateOrderCommand` lives). Conversely, the `Application` project needs to reference the `Contracts` project if it intends to return a `Response` DTO directly. This creates a fatal **Circular Dependency** and tightly couples the external schema to the internal behavior.

**The Right Way:**
Contract DTOs must remain purely anemic. The conversion of a `CreateOrderRequest` into a `CreateOrderCommand` (or a `Customer` into a `CustomerResponse`) must happen exactly at the boundary—usually within the **API / Presentation Layer** (in the controller) or within the **Application Layer** (using a tool like Mapster, AutoMapper, or manual mapping functions).

### 8.2. Standardized Wrappers & Pagination

Returning raw JSON arrays (e.g., `[ { "id": 1 }, { "id": 2 } ]`) from list endpoints is a massive architectural anti-pattern. It abruptly halts future extensibility. If a mobile app expects an array, and you later need to return total page counts or status metadata, you will break the mobile app's deserializer by changing the root JSON structure to an object.

**The Right Way:**
Always wrap collection responses in a standardized paging envelope from day one.

```csharp
namespace ECommerce.Contracts.Responses.Common;

public record PagedResponse<T>
{
    // The actual array of data payload
    public IEnumerable<T> Items { get; init; } = Enumerable.Empty<T>();

    // Pagination metadata
    public int PageNumber { get; init; }
    public int PageSize { get; init; }
    public int TotalPages { get; init; }
    public int TotalRecords { get; init; }

    // Extensibility markers
    public bool HasNextPage => PageNumber < TotalPages;
    public bool HasPreviousPage => PageNumber > 1;
}

// Usage Example:
// return new PagedResponse<OrderSummaryResponse> { Items = orders, TotalRecords = 1500, ... };
```

### 8.3. Contract Versioning Strategy

APIs inevitably evolve, and external clients (especially iOS/Android apps installed on user devices) cannot be updated instantaneously. To prevent breaking existing clients, the schema must be versioned.

**How does this affect the Contracts layer?**
Versioning dictates the directory structure. Instead of infinitely bolting optional properties onto `CreateCustomerRequest`, you physically separate the contracts into designated namespace folders representing the API version.

```text
src/ECommerceApp.Contracts/
├── v1/
│   ├── Requests/
│   │   └── Customers/CreateCustomerRequest.cs  (Requires 'FirstName' and 'LastName')
│   └── Responses/
└── v2/
    ├── Requests/
    │   └── Customers/CreateCustomerRequest.cs  (Requires single 'FullName' field)
    └── Responses/
```

By isolating structures by version, a `v1` endpoint in the API layer continues to bind identically to the `v1.CreateCustomerRequest` and maps to legacy handlers, while the `v2` endpoint binds the new shape. The Contracts layer remains the exact historical record of the system's external schema changes over time.
