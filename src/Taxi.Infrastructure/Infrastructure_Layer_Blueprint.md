# 🏛️ Architectural Constitution: Infrastructure Layer Blueprint

## 1. Executive Summary & Layer Purpose

The Infrastructure Layer represents the absolute outer boundary of our application's interaction with the physical world. It is the rugged, industrialized zone where the pristine, abstract business rules of the Domain and Application layers finally collide with databases, file systems, third-party APIs, JWT cryptographic libraries, and background job executing threads.

In a strict Clean Architecture, the Infrastructure Layer is a **plugin**.
The central Application Layer defines what it _needs_ via interfaces (`IAppDbContext`, `IEmailNotifier`, `ITokenProvider`). The Infrastructure Layer's sole purpose is to implement those interfaces using concrete, heavy, third-party libraries (Entity Framework Core, ASP.NET Core Identity, SignalR, Redis, SMTP clients).

If we suddenly decide to replace PostgreSQL with another database, or switch our email provider from SendGrid to Mailgun, the Domain and Application layers must require zero changes. All modifications are strictly contained within the Infrastructure Layer.

### What are its primary responsibilities?

1. **Object-Relational Mapping (ORM):** Translating memory-bound Domain Entities (Aggregates, Value Objects) into SQL tables via EF Core configurations.
2. **Side-Effect execution:** Sending real emails, generating physical PDFs, communicating with websockets.
3. **Cross-Cutting technical concerns:** Managing JSON Web Tokens (JWT), Identity roles, and cryptographic hashing.
4. **Background processing:** Periodically sweeping databases or triggering scheduled tasks outside of the normal HTTP Request pipeline.

---

## 2. Dependency Rules & Boundaries

The Dependency Inversion Principle reaches its absolute peak in the Infrastructure Layer.

### Inward Pointing Dependencies

The Infrastructure Layer references **everything inside**. It MUST reference the Application Layer (to fulfill its interfaces) and the Domain Layer (to map its entities to the database).

### Outward Pointing Dependencies

**Nothing depends on the Infrastructure Layer.**
The Domain, the Contracts, and the Application Layer do NOT reference this project. Even the API Presentation Layer does not reference the inner implementations directly; it merely invokes the `services.AddInfrastructure()` extension method during startup.

This strict rule prevents developers from taking shortcuts, such as directly injecting `SqlConnection` or `AppDbContext` into an API Controller.

---

## 3. Directory Anatomy

Our Infrastructure layer categorizes files by their technical integration strategy.

```text
src/MechanicShop.Infrastructure/
├── BackgroundJobs/
│   └── AbandonedCartCleanupService.cs
├── Data/
│   ├── AppDbContext.cs
│   ├── ApplicationDbContextInitialiser.cs
│   ├── Configurations/
│   │   ├── CustomerConfiguration.cs
│   │   └── OrderConfiguration.cs
│   ├── Interceptors/
│   │   └── AuditableEntityInterceptor.cs
│   └── Migrations/
├── Identity/
│   ├── TokenProvider.cs
│   ├── IdentityService.cs
│   └── Policies/
├── RealTime/
│   ├── OrderHub.cs
│   └── SignalROrderNotifier.cs
├── Services/
│   ├── InvoicePdfGenerator.cs
│   ├── EmailNotificationService.cs
│   └── TaxCalculationPolicy.cs
├── Settings/
│   └── AppSettings.cs
└── DependencyInjection.cs
```

---

## 4. The DbContext & Entity Configurations

If you throw twenty `[Table]` and `[Column]` data annotations onto a Domain Entity, you have ruined the purity of the Domain by marrying it to EF Core. Instead, our Domain remains pure, and the mapping logic occurs strictly within configuration classes in the Infrastructure Layer.

### 4.1. Keeping the DbContext Clean

The `AppDbContext` should largely just consist of `DbSet<T>` properties and global overrides.

```csharp
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Domain.Customers;
using MechanicShop.Domain.Orders;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace MechanicShop.Infrastructure.Data;

// Inherit from IdentityDbContext to seamlessly blend Custom User tables with our Business Tables
public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<AppUser>(options), IAppDbContext
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Magically scans the assembly and applies all IEntityTypeConfiguration classes
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
```

### 4.2. IEntityTypeConfiguration and Value Objects

We use isolated configuration classes to control how objects save to SQL. Particularly, **Value Objects** must be mapped securely so they do not accidentally create separate SQL tables.

```csharp
using MechanicShop.Domain.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MechanicShop.Infrastructure.Data.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.HasKey(o => o.Id);

        // Map Value Object into JSONB column in PostgreSQL
        // All bilingual fields (LocalizedText) must use this pattern
        builder.OwnsOne(o => o.Name, a =>
        {
            a.ToJson();
        });

        // EF Core limitation: OwnsMany -> ToTable() cannot nest OwnsOne -> ToJson()
        // In these cases, map LocalizedText properties as flat columns:
        // items.OwnsOne(i => i.Description, desc => {
        //     desc.Property(d => d.En).HasColumnName("DescriptionEn");
        //     desc.Property(d => d.Ar).HasColumnName("DescriptionAr");
        // });

        // Enforce strong typing for an Enumeration natively backed by a string in SQL
        // WARNING: If an enum represents a value used in calculations (e.g. Duration),
        // do NOT use string conversion as it breaks SQL-side SUM() and AVG().
        builder.Property(o => o.State)
            .HasConversion<string>()
            .HasMaxLength(30);
    }
}
```

---

## 5. Entity Framework Core Interceptors & Flow

Interceptors allow us to intercept Entity Framework exactly at the moment `SaveChanges` is invoked, enabling us to inject systemic rules universally without relying on developers to "remember" to type them manually.

### 5.1. The AuditableEntityInterceptor

When dealing with `CreatedAtUtc`, `CreatedBy`, `LastModifiedUtc`, developers often forget to assign these right before saving. Our `AuditableEntityInterceptor` catches ALL entities inheriting from `AuditableEntity` globally.

```csharp
public class AuditableEntityInterceptor(IUser user, TimeProvider dateTime) : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context == null) return base.SavingChangesAsync(eventData, result, cancellationToken);

        var utcNow = dateTime.GetUtcNow();

        foreach (var entry in eventData.Context.ChangeTracker.Entries<AuditableEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedBy = user.Id;
                entry.Entity.CreatedAtUtc = utcNow;
            }
            if (entry.State == EntityState.Added || entry.State == EntityState.Modified)
            {
                entry.Entity.LastModifiedBy = user.Id;
                entry.Entity.LastModifiedUtc = utcNow;
            }
        }

        // CRITICAL: All timestamps MUST be stored as UTC (TimeOffset.Zero).
        // The interceptor ensures that no local/unspecified date leaks into the DB.
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
```

### 5.2. Dispatching Domain Events

The most critical step in an Aggregate's lifecycle is publishing the Domain Events it accrued in memory right before the database transaction concludes. While sometimes done via interceptors, our architecture specifically handles this directly inside a `SaveChangesAsync` override for maximum transaction proximity.

```csharp
// Inside AppDbContext.cs
public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
{
    // 1. Gather all events from entities currently modified in memory
    var domainEntities = ChangeTracker.Entries()
        .Where(e => e.Entity is Entity baseEntity && baseEntity.DomainEvents.Count != 0)
        .Select(e => (Entity)e.Entity)
        .ToList();

    var domainEvents = domainEntities.SelectMany(e => e.DomainEvents).ToList();

    // 2. Clear the events queue so they aren't double-fired
    foreach (var entity in domainEntities) { entity.ClearDomainEvents(); }

    // 3. Dispatch the events to the Application Layer asynchronously
    foreach (var domainEvent in domainEvents)
    {
        await mediator.Publish(domainEvent, cancellationToken);
    }

    // 4. Finally execute the ORM database mapping
    return await base.SaveChangesAsync(cancellationToken);
}
```

---

## 6. JWT, Authentication, and Identity Management

The identity system (usually `Microsoft.AspNetCore.Identity`) is notoriously heavy. If we leaked its `UserManager<T>` into our Application Layer, we would be eternally locked into Microsoft framework specifics.

Instead, the Application Layer uses a lightweight wrapper: `IIdentityService`. The Infrastructure Layer natively resolves the complex JWT claims, password hashing, and token issuance.

```csharp
// The implementation of the Application Layer Interface
public class TokenProvider(IConfiguration configuration) : ITokenProvider
{
    public string GenerateToken(AppUser user, IList<string> roles)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email!),
        };

        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var jwtSettings = configuration.GetSection("JwtSettings");
        var secret = jwtSettings["Secret"]!;

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: jwtSettings["Issuer"],
            audience: jwtSettings["Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(120),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
```

---

## 7. Real-Time Communication (SignalR)

Real-time protocols like WebSockets often tempt developers to build controllers directly inside Hubs. This violates CQRS. In Clean Architecture, a SignalR Hub acts purely as a dumb outbound router.

If a user connects to a page, they listen. The actual invocation to send a message comes from an Application Layer MediatR Event Handler, which resolves an `IOrderNotifier` that the Infrastructure implements!

```csharp
// Infrastructure/RealTime/OrderHub.cs
// Notice how absolutely empty this is. It's just a connection pipeline.
public class OrderHub : Hub { }

// Infrastructure/RealTime/SignalROrderNotifier.cs
// The Application Layer depends on IOrderNotifier. This class implements it.
public class SignalROrderNotifier(IHubContext<OrderHub> hubContext) : IOrderNotifier
{
    public async Task NotifyOrderStatusChangedAsync(Guid orderId, string state)
    {
        // Executes the physical web-socket broadcast
        await hubContext.Clients.All.SendAsync("ReceiveOrderUpdate", orderId, state);
    }
}
```

---

## 8. Proactive Discovery: Background Jobs (Hosted Services)

Often, a system requires sweeping automation (e.g., "Cancel all orders that have remained unpaid for 48 hours"). Because this operation operates outside the standard HTTP Request, it belongs in the Infrastructure Layer as a `BackgroundService`.

**Crucial Architecture Note:** Background services execute as Singletons, but `IAppDbContext` is Scoped. You MUST use `IServiceScopeFactory` to spawn a manual scope inside the background loop to safely connect to the database.

```csharp
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace MechanicShop.Infrastructure.BackgroundJobs;

public class AbandonedCartCleanupService(
    IServiceScopeFactory scopeFactory,
    ILogger<AbandonedCartCleanupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Run every 60 minutes perpetually
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(60));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                // CRITICAL: Spawn a local scope to resolve the Application DB Context
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();

                var cutoff = DateTimeOffset.UtcNow.AddHours(-48);
                var abandoned = await db.Orders
                    .Where(O => o.State == OrderState.Pending && o.CreatedAtUtc <= cutoff)
                    .ToListAsync(stoppingToken);

                foreach (var order in abandoned)
                {
                    order.Cancel(); // Domain logic controls the mechanics
                }

                if (abandoned.Count > 0) await db.SaveChangesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Background sweep failed.");
            }
        }
    }
}
```

---

## 9. Proactive Discovery: External Integrations & IOptions Strategy

When communicating with external file writers, blob storage (AWS S3), or physical hard drives, we hide them behind abstractions like `IPdfGenerator`.

When loading heavy settings files (`appsettings.json`), do NOT leak `IConfiguration` into your classes directly. We map configuration blocks into highly-typed POCOs (`AppSettings.cs`) and inject them via the `IOptions<T>` pattern.

```csharp
public class AppSettings
{
    public int CartExpirationMinutes { get; set; }
    public string TaxServiceApiKey { get; set; } = string.Empty;
    public string DefaultLanguage { get; set; } = Languages.Default;
}

// Injected into a Policy/Service using Options Pattern
public class TaxCalculationPolicy(IOptions<AppSettings> options) : ITaxPolicy
{
    private readonly AppSettings _appSettings = options.Value;

    public bool IsTaxRegionEnabled() => _appSettings.TaxServiceApiKey != string.Empty;
}
```

---

## 10. Anti-Patterns & "Code Smells" (The Rejection Criteria)

The Infrastructure Layer's danger lies in its proximity to the actual underlying technology. Abuse here typically breaks the isolation of the whole application.

### Immediate PR Rejection Checklist for the Infrastructure Layer

1. 🚨 **Business Logic or Throwing Exceptions:**
   - **The Wrong Way:** Executing `if(order.Total < 0) throw new ValidationException()` inside an EF Core Interceptor or `AppDbContext`.
   - **Why it's rejected:** The Infrastructure layer is "dumb". It should assume all entities successfully reaching the `.Add()` phase have already been meticulously validated by the `Result<T>` flow in the Application and Domain layers.
2. 🚨 **Identity Framework Bleed:**
   - **The Wrong Way:** Making the Application Layer MediatR Handlers return `IdentityResult` or injecting `UserManager<AppUser>` directly into a `CreateCustomerCommandHandler`.
   - **The Right Way:** The Infrastructure layer must wrap Identity responses directly into Domain `Result<T>` or `Error` structs via a proxy `IIdentityService`.

3. 🚨 **API Presentation Logic:**
   - **The Wrong Way:** Accessing `HttpContext.Request` directly from within a repository or service class.
   - **Why it's rejected:** If the Application is invoked via a CLI tool, gRPC endpoint, or Background Task, there is no HTTP Context. The Architecture must decouple environment requests from database access. Utilize `IUser` context abstractions.

---

## 11. Dependency Injection Registration

The Infrastructure Layer owns an incredibly heavy configuration file (`DependencyInjection.cs`). This registers JWT defaults, EF Core contexts, Interceptors, Microsoft Identity pipelines, HybridCaching standards, and hooks up the exact real-world classes to the imaginary interfaces the Application Layer promised.

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // 1. Core utilities
        services.AddSingleton(TimeProvider.System);

        // 2. The critical ISaveChangesInterceptor routing
        services.AddScoped<ISaveChangesInterceptor, AuditableEntityInterceptor>();

        var connectionString = configuration.GetConnectionString("DefaultConnection")!;
        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());
            options.UseNpgsql(connectionString);
        });

        // 3. Point the abstract IAppDbContext exactly to the concrete instance
        services.AddScoped<IAppDbContext>(provider => provider.GetRequiredService<AppDbContext>());

        // 4. Configure concrete services mapping to abstractions
        services.AddScoped<ITokenProvider, TokenProvider>();
        services.AddTransient<IIdentityService, IdentityService>();
        services.AddScoped<IPdfGenerator, PdfGenerator>();
        services.AddScoped<IOrderNotifier, SignalROrderNotifier>();

        // 5. Fire off the perpetual Background threads
        services.AddHostedService<AbandonedCartCleanupService>();

        return services;
    }
}
```

---

## 12. Advanced Infrastructure Mechanics

### 12.1. Database Migrations & Seeding

Enterprise applications must reliably apply database schemas and seed foundational data (like Administrator roles or initial configuration values) upon startup. However, placing `dbContext.Database.Migrate()` directly inside the `Program.cs` file of the API dramatically pollutes the Presentation boundary with Infrastructure concerns.

**The Solution:**
The Infrastructure layer provides an `ApplicationDbContextInitialiser` class. It separates schema migration (`InitialiseAsync`) from data insertion (`SeedAsync`). We then expose a clean extension method so the API layer can invoke this pipeline seamlessly.

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using MechanicShop.Domain.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace MechanicShop.Infrastructure.Data;

public class ApplicationDbContextInitialiser(
    AppDbContext context,
    UserManager<AppUser> userManager,
    RoleManager<IdentityRole> roleManager)
{
    public async Task InitialiseAsync()
    {
        // During rapid development/migration phase:
        await context.Database.EnsureDeletedAsync();

        // Applies all pending migrations or creates the database if it doesn't exist
        await context.Database.EnsureCreatedAsync();
    }

    public async Task SeedAsync()
    {
        // Default Roles
        var adminRole = new IdentityRole("Admin");
        if (roleManager.Roles.All(r => r.Name != adminRole.Name))
        {
            await roleManager.CreateAsync(adminRole);
        }

        // Default Users
        var adminUser = new AppUser { Email = "admin@localhost", UserName = "admin@localhost" };
        if (userManager.Users.All(u => u.Email != adminUser.Email))
        {
            await userManager.CreateAsync(adminUser, "Admin123!");
            await userManager.AddToRolesAsync(adminUser, [adminRole.Name]);
        }
    }
}

// 1. The Extension Method hiding the complexity from Program.cs
public static class InitialiserExtensions
{
    public static async Task InitialiseDatabaseAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var initialiser = scope.ServiceProvider.GetRequiredService<ApplicationDbContextInitialiser>();

        await initialiser.InitialiseAsync();
        await initialiser.SeedAsync();
    }
}
```

### 12.2. Refresh Token Mechanics

While short-lived JWTs define stateless authorization, Enterprise architectures require Refresh Tokens to maintain persistent sessions without constantly prompting the user to log in.

**The Mechanics:**
Unlike JWTs, Refresh Tokens are completely opaque, mathematically random strings generated purely for database binding. The Infrastructure layer uses cryptographic random number generators to create them, stores them in the `RefreshToken` DbSet mapped to the user (Token Binding), and validates them later to reissue new Access Tokens.

```csharp
using System.Security.Cryptography;
using MechanicShop.Domain.Identity;

namespace MechanicShop.Infrastructure.Identity;

public class TokenProvider : ITokenProvider
{
    public string GenerateRefreshToken()
    {
        // Generates an absolutely random, cryptographically secure 64-byte string
        var randomNumber = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }
}

// Inside IdentityService.cs during Login:
// var refreshToken = tokenProvider.GenerateRefreshToken();
// context.RefreshTokens.Add(new RefreshToken { UserId = user.Id, Token = refreshToken, ExpiresAt = DateTime.UtcNow.AddDays(7) });
// await context.SaveChangesAsync();
```

### 12.3. The Repository Pattern Strategy

You may have noticed the complete absence of `ICustomerRepository` or `IOrderRepository` directories. Clean Architecture does not strictly dictate _how_ an application reads or writes data, merely that it must be abstracted.

**The Stance (DB Context as Unit of Work):**
In this architecture, we strongly reject the "Generic Repository Pattern" (`Repository<T>`) when using Entity Framework Core. EF Core's `DbSet<T>` is _already_ an implementation of the Repository Pattern, and `AppDbContext` is _already_ an implementation of the Unit of Work pattern.

Wrapping them merely creates useless boilerplate. Instead, the Application layer depends on the `IAppDbContext` interface.

**The Strict Rule:**
The Application Layer uses `IAppDbContext` to query and track entities natively. However, it must **never** leak database-specific extensions (like checking SQL Server error codes) into the handlers.

### 12.4. High-Performance Read Queries (Micro-ORMs)

When executing massive Query Handlers (e.g., generating end-of-year analytical reports), EF Core, even with `.AsNoTracking()`, can introduce unwanted memory allocation overhead during object projection.

**The Strategy:**
The Infrastructure layer is perfectly positioned to utilize parallel Micro-ORMs (like **Dapper**) purely for Queries, while keeping EF Core for Commands.

By injecting an `ISqlConnectionFactory` interface, the Application layer can write raw, hyper-optimized SQL that Dapper maps directly into the read-only Contract DTOs, completely bypassing the Entity mapping layer.

```csharp
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using MechanicShop.Application.Common.Interfaces;

namespace MechanicShop.Infrastructure.Data;

// Fulfills the Application Layer's need to execute raw, high-speed queries
public class SqlConnectionFactory(IConfiguration configuration) : ISqlConnectionFactory
{
    public async Task<IEnumerable<T>> QueryAsync<T>(string sql, object? parameters = null)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        await using var connection = new SqlConnection(connectionString);

        return await connection.QueryAsync<T>(sql, parameters);
    }
}
```
