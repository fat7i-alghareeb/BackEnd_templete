# 🏛️ Architecture Master Blueprint

## 1. System Overview & Philosophy

Welcome to the **Master Architecture Blueprint**. This document serves as the absolute entry point and ultimate source of truth for the entire software solution.

This enterprise application is meticulously engineered upon the strict principles of **Clean Architecture**. It violently rejects the traditional N-Tier "spaghetti" paradigm in favor of absolute decoupling. The core philosophy is simple: **Dependencies only point inward.** The Domain is the absolute center of the universe, completely oblivious to databases, UI elements, or HTTP protocols.

Furthermore, the Application logic is driven entirely by the **Command Query Responsibility Segregation (CQRS)** pattern utilizing the MediatR pipeline. This ensures that every operation within the system (reading data vs. mutating state) is structurally isolated. Internally, the Application layer organizes these MediatR pipelines following a strict **Vertical Slice Architecture**, grouping features by their domain boundaries rather than scattering logic across massive, monolithic domain services.

If you are a human developer or an AI Agent operating within this codebase, you are bound by the constitution set forth in this document and its child layer blueprints.

---

## 2. The Tech Stack (Forensic Extraction)

A forensic sweep of the environment configurations and `.csproj` dependencies reveals a robust, immensely modernized .NET ecosystem.

### Core & Application Routing

- **.NET SDK**: Modern .NET 10.0 runtime environment.
- **MediatR**: Structural engine for CQRS pipeline routing, decoupling feature execution from HTTP boundaries.
- **FluentValidation**: Native declarative integration (`FluentValidation.DependencyInjectionExtensions`) for fail-fast pipeline input validation.

### Data Access & Persistence

- **Entity Framework Core (PostgreSQL)**: Object-Relational Mapper strictly utilized for Code-First persistence (`Npgsql.EntityFrameworkCore.PostgreSQL`). Enforces a strict **UTC Strategy** for all `DateTimeOffset` values to ensure database consistency. Supports JSONB for bilingual data storage.
- **Microsoft Identity SDK**: Cryptographic token management and User/Role tables (`Microsoft.AspNetCore.Identity.EntityFrameworkCore`).
- **My.Extensions.Localization.Json**: High-performance runtime engine for JSON-based strongly-typed localization.
- **Hybrid Caching**: High-performance multi-tier layer retention (`Microsoft.Extensions.Caching.Hybrid`).

### API Presentation, Documentation & Telemetry

- **API Versioning**: Ensures rigid backwards compatibility protocols (`Asp.Versioning.Mvc`).
- **OpenTelemetry**: Deep instrumentation for distributed Tracing and application Metrics (`OpenTelemetry.Exporter.Prometheus.AspNetCore`, `OpenTelemetry.Exporter.OpenTelemetryProtocol`).
- **Scalar / Swagger**: The dual-engine OpenApi definition display bridging REST logic (`Scalar.AspNetCore`, `Swashbuckle.AspNetCore`).
- **Serilog**: Core structured logging engine, configured via JSON to pipe to Console and Remote Seq instances (`Serilog.AspNetCore`, `Serilog.Sinks.Seq`).

### UI (Browser Sandbox) & Real-Time Client

- **Blazor WebAssembly**: The offline-capable, WASM-rendered SPA client (`Microsoft.AspNetCore.Components.WebAssembly`).
- **SignalR Client**: Real-time push notifications bridging API and UI over bi-directional WebSockets (`Microsoft.AspNetCore.SignalR.Client`).
- **QuestPDF**: Fluent API execution creating meticulously arranged business PDFs (`QuestPDF`).

### Global Configuration & Orchestration

- **`Directory.Packages.props`**: Implements **Centralized Package Management**, ensuring unified versioning for all NuGet dependencies across the entire solution.
- **`Directory.Build.props`**: Enforces global build standards, including Target Framework (`net10.0`), Nullable context, Implicit Usings, and **StyleCop.Analyzers** for strict code quality compliance.
- **`docker-compose.yml`**: Orchestrates the localized infrastructure, provisioning the **PostgreSQL** database and **Seq** log aggregation engine for consistent development environments.

---

## 3. The Architectural Constitution (Layer Guide)

The solution is divided into highly specific boundary rings. You must consult the detailed Layer Blueprints before attempting to modify any layer natively.

### Ring 1: The Core

1. **Domain Layer:** The heart of the business. Contains Aggregates, Entities, Value Objects (including the mandated `LocalizedText` for bilingual fields), Domain Events, and the foundational `Result<T>` pattern. It has zero external dependencies.
   👉 Refer to the core rules here: [`Domain_Layer_Blueprint.md`](src/MechanicShop.Domain/Domain_Layer_Blueprint.md)

### Ring 2: The Vocabulary

1. **Contracts Layer:** The external vocabulary. Contains the "Dumb DTOs" (Data Transfer Objects), Requests, and Responses. It bridges the gap between the public API and the Application layer without leaking internal Domain logic. Hosts the centralized `LocalizationKeys` registry for system-wide translation parity.
   👉 Refer to the rigid boundary rules here: [`Contracts_Layer_Blueprint.md`](src/MechanicShop.Contracts/Contracts_Layer_Blueprint.md)

### Ring 3: The Use Cases

1. **Application Layer:** The CQRS orchestrator. Organizes use cases into Vertical Slices. Contains MediatR Handlers, MediatR Pipeline `IPipelineBehavior` configurations (Validation, Authorization), and cleanly maps domain entities into outgoing Contract DTOs using native Extension methods.
   👉 Refer to the CQRS engineering specs here: [`Application_Layer_Blueprint.md`](Application_Layer_Blueprint.md)

### Ring 4: The Implementations

1. **Infrastructure Layer:** The technical manifestations of inner abstractions. Contains the `AppDbContext`, EF Core Interceptors (Auditable metadata injection), authentication token cryptography, Background Jobs executing `IHostedService`, and optimized Dapper implementations for read-heavy projections.
   👉 Refer to the integration mandates here: [`Infrastructure_Layer_Blueprint.md`](Infrastructure_Layer_Blueprint.md)

### Ring 5: The Terminus

1. **API / Presentation Layer:** The external boundary and Host. Exposes Domain-driven REST Controllers, translates native Domain `Result` objects into strict RFC 7807 `ProblemDetails` via extension mappers (`ProblemExtensions.cs`), and manages OpenTelemetry, Serilog Contexts, CORS, OutputCaching, and Rate Limiting.
   👉 Refer to the HTTP routing standards here: [`Api_Layer_Blueprint.md`](Api_Layer_Blueprint.md) and the [RESTful Naming Constitution](RESTful_Naming_Constitution.md).

### Ring 6: The Consumer Sandbox

1. **Web Client Layer (Blazor):** The detached browser frontend. Consumes APIs via intercepted `HttpClient` wrappers (`BearerTokenHandler`), executes silent Token Refreshes natively upon 401 Unauthorized responses, parses encoded `Claims` inside the `CustomAuthenticationStateProvider`, and actively streams real-time state using the `WorkOrderHubClient`.
   👉 Refer to the SPA state management mandates here: [`Client_Layer_Blueprint.md`](Client_Layer_Blueprint.md)

---

## 4. Execution Philosophies & Advanced Paradigms

### 4.1. The `Result<T>` Pattern (Exceptionless Flow)

Traditional application design relies on throwing C# `Exceptions` to halt business operations (e.g., throwing a `NotFoundException` if a User doesn't exist). This violates architectural boundaries by forcing the Host to run heavy Try/Catch interceptors for completely expected operational failures.

Instead, we utilize the functional `Result<T>` pattern globally. If a business logic constraint fails, it yields `Result.Failure<CustomerDto>(CustomerErrors.NotFound)`. The Application layer receives this explicit failure state and propagates it outward without throwing a catastrophic system thread abort.

### 4.2. MediatR Pipeline Interceptors

Rather than bloating every single `UpdateCustomerCommandHandler` with lines of code checking "Is this user an Admin?" or "Is this Customer name longer than 50 characters?", we decouple validation via Pipeline Behaviors. A `ValidationBehavior<TRequest, TResponse>` intercepts the command _before_ the handler runs, scans the `FluentValidation` registry, perfectly executing the constraints. If it fails, the pipeline aborts immediately returning `Result.Failure`.

### 4.3. Universal Error Mapping & RFC 7807

When the API layer receives a `Result.Failure` from MediatR, it utilizes centralized extension mapping methods (`ProblemExtensions.cs`) to immediately cast Domain-level Error types (NotFound, Conflict, Validation) into standardized `application/problem+json` envelopes. Internal Stack Traces are utterly forbidden from leaking outward.

### 4.4. Security Architecture (Authentication vs. Authorization)

The API leverages JWTs natively generated by the Infrastructure layer. The boundary enforces `Authorization` cleanly via `Authorize` attributes placed directly on controllers. Roles and encoded claims are evaluated strictly by ASP.NET Middleware matrices. Furthermore, OpenAPI documentation (`BearerSecurityOperationTransformer.cs`) dynamically reads these attributes to attach padlock properties contextually, avoiding hardcoded swagger configurations.

### 4.5. Observability & Telemetry Traceability

Because requests cross massive internal layers, traceability is paramount. The system injects a `CorrelationId` via `RequestLogContextMiddleware` explicitly into Serilog. Log aggregation platforms (Seq) can query every executed database query, external HTTP call, and CQRS handler cycle attached universally to the specific HTTP request trace footprint.

**Bilingual Projection Logging**: To verify the strict performance constraint (fetching only the requested language), all query handlers must log their projection using the `[Projection]` prefix, explicitly stating the language being extracted from JSONB (e.g., `[Projection] Fetching Name.Ar from JSONB`).

**Culture-Aware Caching**: The `CachingBehavior` automatically partitions the cache by culture code (e.g., `query-key_ar`) using `ILanguageContext.Language` for queries that implement `ICachedQuery` with `IsCultureAware = true`.

**The Languages Registry**: To eliminate magic strings, all language-based logic must utilize the `MechanicShop.Contracts.Common.Languages` static class, which defines canonical constants for `En`, `Ar`, and the `Default` language.

---

## 5. The Golden Workflow (Strict Directives for AI Agents & Devs)

When introducing a new feature to the system, you must obey the **"Inside-Out"** development principle. Begin at the absolute core (Contracts/Domain) and meticulously build outwards toward the API.

Do not deviate from the following 5-Step process:

### Step 1: Define Contracts (Requests/Responses)

Navigate to the **Contracts Layer**. Define the exact shape of the JSON data entering the system (`CreateFeatureRequest`) and the shape of the data leaving the system (`FeatureDto`). This solidifies the external "vocabulary" of the feature upfront before any logic is written. Do NOT put methods on Contract objects.

### Step 2: Model the Domain (Entities, Errors, Events)

Navigate to the **Domain Layer**. If new business logic is required, define the Aggregate Root or Entities within their own folder. Enforce strict invariants inside the entity constructors or `public Result UpdateStatus()` methods. Define static `FeatureErrors` within the Domain namespace.

**Bilingual Rule**: If an entity has a translatable string field (name, description, etc.), it MUST use the `LocalizedText` Value Object. Raw `string` properties for bilingual text are forbidden. All error messages must be defined in `LocalizationKeys` (Contracts layer).

If side-effects are required (like sending an email), raise a Domain Event (`FeatureCreatedDomainEvent`) inside the Aggregate—do not execute infrastructure logic directly.

### Step 3: Build the Application Use Case

Navigate to the **Application Layer**. Create a new folder mimicking Vertical Slice Organization (e.g., `Features/NewFeature/Commands`). Implement the following:

- **The MediatR Command/Query**: Encapsulates the explicit input parameters routing through the CQRS engine.
- **The Validator**: Implement `AbstractValidator<Command>` to parse string lengths, formats, and basic logic loops instantly. Use `.WithErrorCode(LocalizationKeys...)` to map validation failures to strongly-typed localization keys.
- **The Handler**: Write the `IRequestHandler<Command, Result<FeatureDto>>`. Inject the specific persistence abstraction (`IApplicationDbContext`), execute the domain operations seamlessly, and await `SaveChangesAsync()`.
- **The Mapper**: Provide an extension method cleanly mapping the resulting domain models back into the Contract DTO defined in Step 1.

### Step 4: Wire the Infrastructure (Only If Necessary)

Navigate to the **Infrastructure Layer**. Does the feature require mapping a brand new Entity framework table? Add the `IEntityTypeConfiguration<TEntity>` class mapping the database relationships.

**JSONB Mapping**: Use `.OwnsOne(x => x.FieldName).ToJson()` to store bilingual data as a single JSONB column. Note: EF Core does not support `.ToJson()` for child entities in `OwnsMany` relationships; use flat columns (`FieldNameEn`, `FieldNameAr`) in those specific cases.

Append a `DbSet` to the `ApplicationDbContext`. Run `dotnet ef migrations add` exclusively against the Infrastructure persistence layer if the schema changed.

### Step 5: Expose the API (Controllers)

Navigate to the **API Layer**. Within the specific `Controllers/` directory, explicitly map the HTTP method to the MediatR ISender dispatch executing the appropriate Command using the exact versioned route structure.
The action should be microscopically minimal, absolutely devoid of loops, logging logic, or SQL calls, simply matching the unified Application `Result<T>`:

```csharp
var result = await sender.Send(command, cancellationToken);
return result.Match(
    response => Ok(response),
    Problem); // inherited from ApiController; delegates to ProblemExtensions.ToProblem(this)
```

**The feature is now fully implemented following flawless Enterprise Clean Architecture execution.**

---

## 6. RESTful API Naming Constitution (Key Takeaways)

To ensure a professional, predictable, and scalable API surface, all developers and AI Agents must adhere to the following naming standards:

1. **Nouns over Verbs**: Endpoints represent "things" (`/products`), not actions. Let HTTP methods (`GET`, `POST`, `PUT`, `DELETE`) define the action.
2. **Always Pluralize**: Use plural nouns for all collections to maintain consistency, whether fetching a list (`/users`) or a single item (`/users/5`).
3. **Keep Nesting Shallow**: Nest URLs at most one level deep to show relationships (e.g., `/products/5/reviews`). Avoid deep chains; if it goes deeper than two levels, link directly to the sub-resource.
4. **Format Consistently**: All URIs must be **lowercase** and use **kebab-case** to separate words (e.g., `/customer-orders`).
5. **Version Everything**: Always include a version indicator in your base route (e.g., `/api/v1/resources`). This protects clients from breaking during system overhauls.

---
