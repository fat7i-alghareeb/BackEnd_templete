# 🏛️ Architectural Constitution: Application Layer Blueprint

## 1. Executive Summary & Layer Purpose

The Application Layer is the central nervous system of our Clean Architecture. While the Domain Layer holds the timeless rules of the business, the Application Layer orchestrates the _use cases_ that execute those rules. It acts as the critical bridge separating the unpredictable external world (HTTP REST APIs, gRPC, user interfaces) from the purity of the Domain and the concrete implementations of the Infrastructure.

If a stakeholder says, "As a user, I want to cancel my order," the Application Layer is completely responsible for fetching the order from the database, calling the Domain's `Cancel()` method, dispatching emails, committing the database transaction, and returning a DTO response to the caller.

### What is the exact role of this layer in Clean Architecture?

The Application Layer dictates the flow of control. It implements the CQRS (Command Query Responsibility Segregation) pattern to heavily isolate data-mutating workflows (Commands) from data-retrieval workflows (Queries). It defines the interfaces (abstractions) that the Infrastructure Layer must implement, adhering to the Dependency Inversion Principle.

### What are its primary responsibilities?

1. **Use Case Execution:** Processing incoming Contracts (Requests), finding the correct business handler, and returning the correct Contracts (Responses).
2. **Cross-Cutting Concerns:** Automatically executing global pipelines for Validation, Exception Handling, Performance Logging, and Caching before the flow ever hits the core business logic.
3. **Abstractions:** Defining `IAppDbContext`, `IEmailService`, `ITokenProvider` and other interfaces that describe what the application _needs_ from external systems, without actually referencing those systems.
4. **Transaction Boundaries:** Acting as the boundary where database `SaveChanges()` occur.

### What is STRICTLY FORBIDDEN in this layer?

- **Infrastructure Leakage:** Using `Microsoft.EntityFrameworkCore.SqlServer` or knowing anything about SQL dialects or Dapper configurations.
- **HTTP / Presentation Leakage:** Returning `IActionResult`, `HttpResponseMessage`, or checking `HttpContext`. The Application Layer does not know it is hosted in a Web API or a Console App.
- **Business Rules:** Containing logic like `if (balance < 0) return Error`. All decisions involving object states belong strictly inside the Domain Entities.
- **Handlers must be kept "slim".** If a mapping takes more than 5 lines, extract it to a dedicated Mapper class.
- **Bilingual Mapping**: Mapper methods (`ToDto()`) should be minimized. Prefer switch-based projections in Handlers to ensure SQL-side JSONB extraction. If a mapper is used, it must resolve correctly based on the current culture from `ILanguageContext`.

---

## 2. Directory Structure & Vertical Slice Architecture

Our Application Layer completely shuns the traditional "layered" folder structure (e.g., throwing all commands into a `Commands` folder and all queries into a `Queries` folder). We utilize **Vertical Slice Architecture** mapped by Domain Aggregates. This ensures high cohesion—when a developer works on "Orders," all related commands, queries, validators, and mappers are physically grouped together.

```text
src/MechanicShop.Application/
├── Common/
│   ├── Behaviours/
│   │   ├── CachingBehavior.cs
│   │   ├── LoggingBehavior.cs
│   │   └── ValidationBehavior.cs
│   ├── Errors/
│   │   └── ApplicationErrors.cs
│   └── Interfaces/    (The Abstractions layer must fulfill)
│       ├── IAppDbContext.cs
│       ├── ICachedQuery.cs
│       └── IEmailNotifier.cs
├── Features/
│   ├── Customers/
│   │   ├── Commands/
│   │   ├── Queries/
│   │   ├── Dtos/
│   │   └── Mappers/
│   └── Orders/
│       ├── Commands/
│       │   ├── CreateOrder/
│       │   │   ├── CreateOrderCommand.cs
│       │   │   ├── CreateOrderCommandHandler.cs
│       │   │   └── CreateOrderCommandValidator.cs
│       │   └── CancelOrder/
│       ├── Queries/
│       │   └── GetOrderById/
│       │       ├── GetOrderByIdQuery.cs
│       │       └── GetOrderByIdQueryHandler.cs
│       ├── Dtos/
│       │   └── OrderSummaryDto.cs
│       └── Mappers/
│           └── OrderMappers.cs
└── DependencyInjection.cs
```

### Detailed Breakdown of Directories

- **`Common/Behaviours/`**: Houses the MediatR Pipeline Interceptors. These are the unsung heroes of the architecture.
- **`Common/Interfaces/`**: Defines the rigorous contracts for external services (e.g., sending emails, making API calls, querying the DB) that the Infrastructure Layer must provide.
- **`Features/[AggregateName]/[Operation]/`**: Every single use case (like `CreateOrder`) gets its own isolated folder. This minimizes merge conflicts, follows the Single Responsibility Principle, and makes navigation incredibly predictable.

---

## 3. CQRS Mechanics using MediatR

We strictly enforce Command Query Responsibility Segregation using the MediatR library. This means we have two completely distinct channels of data flow.

### 3.1. Bilingual Language Projection (JSONB)

To maximize database performance and satisfy the strict bilingual retrieval constraint, we utilize PostgreSQL JSONB path accessors directly in LINQ projections.

**Mandatory Patterns:**

1. **Current Language**: Handlers resolve the current culture via `ILanguageContext.Language`.
2. **Ternary Projection**: Projections (`.Select()`) MUST use a ternary operator on the language (e.g., `lang == Languages.Ar ? x.Name.Ar : x.Name.En`). This allows EF Core to generate efficient `->>` JSONB SQL.
3. **Logging Convention**: Handlers must emit a log entry with the `[Projection]` prefix showing exactly which language is being fetched.

**Example Query Handler Projection:**

```csharp
public async Task<Result<List<CustomerDto>>> Handle(GetCustomersQuery request, CancellationToken ct)
{
    var lang = _languageContext.Language;
    _logger.LogInformation("[Projection] Fetching Name.{Lang} from JSONB", lang);

    return await _context.Customers
        .Select(c => new CustomerDto
        {
            Id = c.Id,
            // EF Core translates this ternary into: c.Name->>'Ar' or c.Name->>'En'
            Name = lang == Languages.Ar ? c.Name.Ar : c.Name.En
        })
        .ToListAsync(ct);
}
```

### Commands (Mutating Data)

Commands alter the state of the system. They Create, Update, or Delete.
Every Command maps exclusively to exactly one CommandHandler.

- **The Interface:** `public record CreateOrderCommand(Guid ProductId, int Qty) : IRequest<Result<OrderDto>>;`

### Queries (Retrieving Data)

Queries explicitly return data and have absolutely zero side-effects. They never invoke `.Add()`, `.Update()`, or `.SaveChanges()`.

- **The Interface:** `public record GetOrderByIdQuery(Guid Id) : IRequest<Result<OrderDto>>;`
- **The Rule:** Queries can bypass loading full rich Domain Entities and execute highly optimized projection queries straight into DTOs using `.Select()` or raw SQL via Dapper if required in edge cases.

---

## 4. Pipeline Behaviors (The Secret Sauce)

Pipeline behaviors intercept every single Command and Query executing in our system. Because we utilize the Exceptionless `Result` pattern, our pipelines brilliantly circumvent exceptions entirely.

### 4.1. ValidationBehavior (FluentValidation Integration)

This behavior automatically grabs the incoming Command, locates the corresponding FluentValidation rules, and runs them. If the command violates a rule (e.g., "Email is invalid"), the behavior intercepts the request entirely, refusing to pass it to the handler, and instead converts the FluentValidation errors directly into our Domain `Error` struct, returning a failed `Result<T>`.

**Line-by-Line Mechanics:**

```csharp
using MediatR;
using FluentValidation;
using ECommerce.Domain.Common.Results;
using ECommerce.Domain.Common.Results.Abstractions;

namespace ECommerce.Application.Common.Behaviours;

// 1. We constrain the pipeline to requests returning our specific IResult abstraction
public class ValidationBehavior<TRequest, TResponse>(IValidator<TRequest>? validator = null)
    : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
        where TResponse : IResult
{
    private readonly IValidator<TRequest>? _validator = validator;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        // 2. If no validator exists for this specific command, proceed normally.
        if (_validator is null)
        {
            return await next(ct);
        }

        // 3. Run complex application logic validation asynchronously.
        var validationResult = await _validator.ValidateAsync(request, ct);

        // 4. If everything is valid, allow the Handler to execute.
        if (validationResult.IsValid)
        {
            return await next(ct);
        }

        // 5. Hard stop. Map FluentValidation errors strictly to Domain Errors.
        // Property name is carried on Error.PropertyName so the API layer can build an
        // RFC 7807 `errors` dictionary keyed by the offending field. Error.Code stays
        // as the LocalizationKeys constant (set via .WithMessage(...) in the validator).
        var errors = validationResult.Errors
            .ConvertAll(e => Error.ValidationForProperty(
                propertyName: e.PropertyName,    // e.g. "NameEn"
                code: e.ErrorMessage));          // e.g. "Validation.EnglishName.Required"

        // 6. DYNAMIC CASTING MAGIC: Because TResponse is constrained to IResult,
        // and Result<T> has an implicit conversion operator from List<Error>,
        // mapping via 'dynamic' safely triggers the struct initialization without throwing.
        return (dynamic)errors;
    }
}
```

### 4.2. CachingBehavior (Modern HybridCache Integration)

We utilize the modern ASP.NET Core `HybridCache` allowing for phenomenal performance utilizing both L1 Memory Caching and L2 Distributed Caching (Redis).

Queries opt-in to caching simply by implementing an interface. No boilerplate required in the handlers!

```csharp
public interface ICachedQuery
{
    string CacheKey { get; }
    TimeSpan? Expiration { get; }
    IEnumerable<string>? Tags { get; }
    bool IsCultureAware { get; } // Mandates cache partitioning by language
}
```

**The Pipeline Execution:**

```csharp
public class CachingBehavior<TRequest, TResponse>(
    HybridCache cache,
    ILogger<CachingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        // 1. Is it a cacheable query? If not, execute immediately.
        if (request is not ICachedQuery cachedRequest)
        {
            return await next(ct);
        }

        // 2. Attempt to resolve from cache first to avoid database IO operations
        var result = await cache.GetOrCreateAsync<TResponse>(
            cachedRequest.CacheKey,
            _ => new ValueTask<TResponse>((TResponse)(object)null!),
            new HybridCacheEntryOptions { Flags = HybridCacheEntryFlags.DisableUnderlyingData },
            cancellationToken: ct);

        // 3. Cache Miss (or expired)? Run the database query.
        if (result is null)
        {
            result = await next(ct); // Executes the actual QueryHandler

            // 4. Only cache authentic successes, do NOT cache NotFound or Validation errors!
            if (result is IResult res && res.IsSuccess)
            {
                await cache.SetAsync(
                    cachedRequest.CacheKey,
                    result,
                    new HybridCacheEntryOptions { Expiration = cachedRequest.Expiration },
                    cachedRequest.Tags,
                    ct);
            }
        }

        return result;
    }
}
```

---

## 5. Command Handlers (Implementation & Rules)

A Command Handler performs the core business choreography. Consider this the absolute "Right Way" template for a mutation use case.

Notice how `Result<T>` dictates the control flow, totally abolishing `try/catch` and exception noise.

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using ECommerce.Application.Common.Interfaces;
using ECommerce.Domain.Orders;
using ECommerce.Domain.Common.Results;

namespace ECommerce.Application.Features.Orders.Commands.CreateOrder;

// 1. Command Definition maps primitive inputs to the Expected Result Dto
public sealed record CreateOrderCommand(
    Guid CustomerId,
    List<CreateOrderLineItemDto> Items) : IRequest<Result<OrderDto>>;

// 2. Handler isolates the execution. Dependencies injected via Primary Constructors.
public class CreateOrderCommandHandler(
    ILogger<CreateOrderCommandHandler> logger,
    IAppDbContext context,
    HybridCache cache
    ) : IRequestHandler<CreateOrderCommand, Result<OrderDto>>
{
    public async Task<Result<OrderDto>> Handle(CreateOrderCommand command, CancellationToken ct)
    {
        // 3. Pre-flight check via Infrastructure (Database).
        // The Application Layer queries the database state; it does not assume it.
        var customerExists = await context.Customers.AnyAsync(c => c.Id == command.CustomerId, ct);

        if (!customerExists)
        {
            logger.LogWarning("Customer {Id} not found.", command.CustomerId);
            // 4. Early out using a standard Error, preventing Exceptions.
            return ApplicationErrors.NotFound("Customer", command.CustomerId);
        }

        // 5. Construct necessary Domain dependencies (in memory mapping)
        List<OrderLineItem> lineItems = [];
        foreach (var itemDto in command.Items)
        {
            var itemResult = OrderLineItem.Create(Guid.NewGuid(), itemDto.ProductId, itemDto.Quantity);

            // 6. Bubble up core Domain validation failures instantly
            if (itemResult.IsError) return itemResult.Errors;

            lineItems.Add(itemResult.Value);
        }

        // 7. Core execution delegating entirely to the rich Aggregate Root Factory
        var createResult = Order.Create(
            Guid.NewGuid(),
            command.CustomerId,
            DateTimeOffset.UtcNow,
            lineItems);

        if (createResult.IsError) return createResult.Errors;

        var order = createResult.Value;

        // 8. Add to the ORM DbSet (in memory tracking).
        context.Orders.Add(order);

        // 9. Flush to Database via Transaction.
        // THIS is where the magic happens. Infrastructure intercepts this SaveChanges()
        // call, hunts down any Domain Events inside the 'order' Aggregate, and dispatches them!
        await context.SaveChangesAsync(ct);

        // 10. Cache Invalidation. Tag-based eviction kills the whole associated list cache.
        await cache.RemoveByTagAsync("orders-list", ct);

        logger.LogInformation("Order successfully processed. Order ID: {OrderId}", order.Id);

        // 11. Final output converted to Boundary DTO.
        return order.ToDto();
    }
}
```

---

## 6. Query Handlers (Implementation & Rules)

Queries are brutally fast. They do not load state for the sake of making logic choices; they selectively retrieve data columns for read-only projection into user interfaces.

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using ECommerce.Application.Common.Interfaces;
using ECommerce.Domain.Common.Results;

namespace ECommerce.Application.Features.Orders.Queries.GetOrderById;

// 1. Opt-in to caching via the ICachedQuery interface trivially
public sealed record GetOrderByIdQuery(Guid Id) : IRequest<Result<OrderDto>>, ICachedQuery
{
    public string CacheKey => $"query-order-{Id}";
    public TimeSpan? Expiration => TimeSpan.FromMinutes(10);
    public IEnumerable<string>? Tags => ["order-single"];
}

public class GetOrderByIdQueryHandler(IAppDbContext context)
    : IRequestHandler<GetOrderByIdQuery, Result<OrderDto>>
{
    public async Task<Result<OrderDto>> Handle(GetOrderByIdQuery request, CancellationToken ct)
    {
        // 2. CRITICAL RULE: Queries must use .AsNoTracking() immediately.
        // We do not want Entity Framework memory bloat for read-only outputs.
        var orderDto = await context.Orders
            .AsNoTracking()
            .Where(o => o.Id == request.Id)
            .Select(o => new OrderDto(o.Id, o.CustomerId, o.TotalAmount, o.State.ToString()))
            .FirstOrDefaultAsync(ct);

        if (orderDto is null)
        {
            return ApplicationErrors.NotFound("Order", request.Id);
        }

        return orderDto;
    }
}
```

---

## 7. Validation Strategies (FluentValidation Rules)

While `System.ComponentModel.DataAnnotations` handles basic regex and empty fields inside the Contracts Layer, **FluentValidation** dominates the Application Layer. Why? Because FluentValidation allows complex, multi-property checks, asynchronous logic, and strict separation of rules from the DTO properties.

```csharp
using FluentValidation;
using ECommerce.Application.Features.Orders.Commands.CreateOrder;

namespace ECommerce.Application.Features.Orders.Commands.CreateOrder;

// Placed directly alongside the Command file in the same folder.
public class CreateOrderCommandValidator : AbstractValidator<CreateOrderCommand>
{
    public CreateOrderCommandValidator()
    {
        RuleFor(x => x.CustomerId)
            .NotEmpty()
            .WithErrorCode(LocalizationKeys.Validation.RequiredField)
            .WithMessage("Customer identification is strictly required.");

        RuleFor(x => x.Items)
            .NotEmpty()
            .WithErrorCode(LocalizationKeys.Validation.RequiredField)
            .WithMessage("An order must contain at least one line item.");

        RuleForEach(x => x.Items).ChildRules(items =>
        {
            items.RuleFor(i => i.ProductId)
                .NotEmpty()
                .WithErrorCode(LocalizationKeys.Validation.RequiredField);

            items.RuleFor(i => i.Quantity)
                .GreaterThan(0)
                .WithErrorCode(LocalizationKeys.Validation.InvalidFormat)
                .WithMessage("Quantity must be positive.");
        });
    }
}
```

---

## 8. Mapping & DTO Transformations

To maintain an unpolluted Domain, mapping logic belongs directly in the Application feature slices. We prefer simple Extension Methods (`ToDto()`) ensuring compile-time safety and lightning-fast execution over reflection-heavy libraries like AutoMapper, unless the mappings become unsustainably large.

```csharp
namespace ECommerce.Application.Features.Orders.Mappers;

public static class OrderMappers
{
    // A pure extension mapping separating internal entities from outward boundaries
    public static OrderDto ToDto(this Order order)
    {
        return new OrderDto(
            order.Id,
            order.CustomerId,
            order.TotalAmount,
            order.State.ToString(),
            order.LineItems.Select(x => new OrderLineItemDto(x.ProductId, x.Quantity)).ToList()
        );
    }
}
```

---

## 9. Anti-Patterns & "Code Smells" (The Rejection Criteria)

If you attempt to leak responsibilities inside the Application Layer, your code will fail rigorous architecture reviews. Monitor carefully for these "Code Smells."

### Immediate PR Rejection Checklist for the Application Layer

1. 🚨 **Database Calls inside Loops:**
   - **The Wrong Way:** `foreach (var item in command.Items) { var product = await _context.Products.FindAsync(item.ProductId); }`
   - **Why it's rejected:** The N+1 Query problem decimates performance.
   - **The Right Way:** Query the products cleanly via an `IN` clause prior to the loop: `var products = await _context.Products.Where(p => prodIds.Contains(p.Id)).ToListAsync();`

2. 🚨 **Direct SaveChanges inside Validation rules:**
   - **The Wrong Way:** Executing database transactions or saves inside an `IValidator<T>` definition.
   - **Why it's rejected:** Validation pipelines are not meant for asynchronous data mutation. Validators observe state, they do not change it.

3. 🚨 **Calling other Command Handlers directly:**
   - **The Wrong Way:** Injecting `CreateUserCommandHandler` into `CreateOrderCommandHandler` to execute logic.
   - **Why it's rejected:** Bypasses MediatR pipelines entirely and tightly couples features.
   - **The Right Way:** Publish a Domain Event (e.g., `OrderProcessedEvent`) during `SaveChanges()` to organically trigger separate logic asynchronously.

4. 🚨 **Failing to use `.AsNoTracking()` in Queries:**
   - **The Wrong Way:** `await _context.Orders.ToListAsync();` inside a `GetOrdersQueryHandler`.
   - **Why it's rejected:** Entity Framework builds heavy change-tracking proxy graphs. Queries never mutate data, thereby making tracking memory-bloat useless.

5. 🚨 **Returning Raw Entities from Commands/Queries:**
   - **The Wrong Way:** `public record CreateOrderCommand() : IRequest<Result<Order>>;`
   - **Why it's rejected:** The Application Layer's job is mapping out to DTOs. Returning an `Order` Domain Entity passes your highly complex internal state model outward, forcing the caller (the API) to become tightly coupled to your Database ORM schema.

---

## 10. Registration & Dependency Injection Setup

The `DependencyInjection.cs` file at the root of the Application project configures its massive capabilities gracefully within a single extension method. It registers MediatR, configures the executing assembly for automatic Handler discovery, injects the behaviors systematically, and attaches FluentValidation.

```csharp
using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using ECommerce.Application.Common.Behaviours;

namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        // Automatically scans the assembly and registers every single IValidator<T>
        services.AddValidatorsFromAssembly(assembly);

        // Configures MediatR routing and applies pipeline filters
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);

            // ORDER MATTERS!
            // 1. Log errors immediately.
            cfg.AddOpenBehavior(typeof(UnhandledExceptionBehaviour<,>));
            // 2. Perform Validation. Reject before processing.
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
            // 3. If Valid, grab from Cache if available.
            cfg.AddOpenBehavior(typeof(CachingBehavior<,>));
            // 4. Log stopwatch time taken to execute the eventual Handler.
            cfg.AddOpenBehavior(typeof(PerformanceBehaviour<,>));
        });

        return services;
    }
}
```

The Application Layer remains completely pure of ASP.NET constructs. The outer web API host will simply invoke `builder.Services.AddApplication();` to consume this entire, fully armed system.

---

## 11. Advanced Application Mechanics

### 11.1. Domain Event Handlers (The Side-Effects Engine)

A strict rule of CQRS is that a Command Handler should execute exactly one primary database mutation. If you find your `CreateOrderCommandHandler` also attempting to send a confirmation email, ping a third-party CRM, and write to a message bus, you are violating the Single Responsibility Principle and drastically slowing down the database transaction.

**The Solution:**
The Domain entity raises an `INotification` (e.g., `OrderCancelledEvent`). Once `_context.SaveChangesAsync()` successfully commits, MediatR automatically publishes these events. The Application Layer implements standalone Handlers to react to these events asynchronously.

```csharp
using MediatR;
using ECommerce.Domain.Orders.Events;
using ECommerce.Application.Common.Interfaces;

namespace ECommerce.Application.Features.Orders.EventHandlers;

// 1. Listens for the INotification emitted by the Domain Layer.
// Multiple handlers can safely listen to the exact same event.
public class OrderCancelledEventHandler(
    IEmailNotifier emailNotifier,
    ILogger<OrderCancelledEventHandler> logger)
    : INotificationHandler<OrderCancelledEvent>
{
    public async Task Handle(OrderCancelledEvent notification, CancellationToken ct)
    {
        logger.LogInformation("Domain Event Received: Order {OrderId} was cancelled.", notification.OrderId);

        // 2. Execute the side-effect via an Infrastructure Abstraction.
        // This keeps our core Command Handler extremely fast and pure!
        await emailNotifier.SendOrderCancellationEmailAsync(notification.OrderId, ct);
    }
}
```

### 11.2. Authorization Pipeline Behavior

Do not scatter `if (!user.IsAdmin) return Error.Unauthorized();` checks inside every single Command Handler. Keep your Handlers focused purely on business orchestration by intercepting authorization via a MediatR Pipeline Behavior.

**The Strategy:**

1. Create a custom `[Authorize(Roles = "Admin")]` attribute and place it natively on your `IRequest` (Command).
2. Create an `AuthorizationBehavior` that uses reflection to find these attributes on the incoming request.
3. If an attribute exists, utilize the `IIdentityService` (an abstraction resolved via Infrastructure) to verify claims.

```csharp
using MediatR;
using System.Reflection;
using ECommerce.Domain.Common.Results;
using ECommerce.Application.Common.Interfaces;

namespace ECommerce.Application.Common.Behaviours;

public class AuthorizationBehavior<TRequest, TResponse>(
    IIdentityService identityService)quest, TResponse>(
    IIdentityService identityService)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : IResult
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var authAttributes = request.GetType().GetCustomAttributes<AuthorizeAttribute>(true);

        if (authAttributes.Any())
        {
            // Abstraction representing the decoded JWT / Claims from HttpContext
            var currentUserId = identityService.GetCurrentUserId();

            if (currentUserId == null)
            {
                // Returns an immediate Domain Error, aborting the pipeline
                return (dynamic)ApplicationErrors.Unauthorized();
            }

            foreach (var attribute in authAttributes)
            {
                if (!string.IsNullOrEmpty(attribute.Roles))
                {
                    bool inRole = await identityService.IsInRoleAsync(currentUserId.Value, attribute.Roles);
                    if (!inRole)
                    {
                        return (dynamic)ApplicationErrors.Forbidden();
                    }
                }
            }
        }

        // Authorization passed (or wasn't required), proceed to Validation or Caching.
        return await next(ct);
    }
}
```
