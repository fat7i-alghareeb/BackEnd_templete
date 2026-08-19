# Infrastructure Layer Blueprint — `Taxi.Infrastructure`

> Start at [AGENTS.md](../../AGENTS.md) — it carries the rules and the add-a-feature
> checklist. This file is the deep reference for the Infrastructure layer.
> Map of all docs: [NAVIGATION.md](../../docs/NAVIGATION.md).

The plugin ring. Concrete implementations of the interfaces `Taxi.Application` declares:
PostgreSQL via EF Core, ASP.NET Identity, JWT issuance, and cache registration.

Reference implementations: **`Data/AppDbContext.cs`**, **`Data/Configurations/CarConfiguration.cs`**,
**`Identity/TokenProvider.cs`**.

---

## 1. Purpose

Application says *what* it needs (`IAppDbContext`, `IIdentityService`, `ITokenProvider`);
Infrastructure says *how*. Everything technology-specific lives here so that swapping PostgreSQL,
or the token format, or the cache backend, touches no use case.

It is also the composition point for all data-related DI: `AddInfrastructure(configuration)` is a
single call from `Program.cs`.

---

## 2. Dependency rules

**References:** `Taxi.Application` (project). Packages: `Npgsql.EntityFrameworkCore.PostgreSQL`,
`Microsoft.AspNetCore.Identity.EntityFrameworkCore`, `Microsoft.AspNetCore.Authentication.JwtBearer`,
`Microsoft.AspNetCore.Authorization`, `Microsoft.Extensions.Caching.Hybrid`,
`Serilog.AspNetCore`, `Serilog.Sinks.Seq`, EF Core `Tools` + `Design` (both `PrivateAssets=all`).

Domain and Contracts are reachable transitively and used directly (entities, `Result<T>`,
`LocalizationKeys`).

**Referenced by:** `Taxi.Api` — **only** so `Program.cs` can call `AddInfrastructure`. No
controller may reference a type from this project.

**Never add:** a reference to `Taxi.Api` · business rules · anything that decides *what* should
happen rather than *how* it is stored or transmitted.

`Serilog.Sinks.Seq` backs the Seq sink configured in `appsettings.Development.json`.

---

## 3. Directory structure

```text
src/Taxi.Infrastructure/
├── Data/
│   ├── AppDbContext.cs
│   ├── ApplicationDbContextInitialiser.cs
│   ├── Configurations/
│   │   ├── CarConfiguration.cs
│   │   └── RefreshTokenConfiguration.cs
│   ├── Interceptors/
│   │   ├── AuditableEntityExtensions.cs
│   │   ├── AuditableEntityInterceptor.cs
│   │   └── DispatchDomainEventsInterceptor.cs
│   └── Migrations/
│       ├── 20260502062106_Initial_Cars.cs
│       ├── 20260502070738_AddLocalizedDescriptionToCar.cs
│       └── AppDbContextModelSnapshot.cs
├── Identity/
│   ├── AppUser.cs
│   ├── IdentityService.cs
│   └── TokenProvider.cs
├── Settings/
│   └── AppSettings.cs
├── DependencyInjection.cs
└── Infrastructure_Layer_Blueprint.md
```

| Folder | Holds |
|---|---|
| `Data/` | `DbContext`, initialiser, EF configurations, interceptors, migrations |
| `Data/Configurations/` | One `IEntityTypeConfiguration<T>` per entity — auto-discovered |
| `Data/Interceptors/` | `ISaveChangesInterceptor` implementations |
| `Identity/` | The Identity user type and the two services wrapping ASP.NET Identity + JWT |
| `Settings/` | Strongly-typed options bound from configuration |

There is **no** `BackgroundJobs/`, `RealTime/`, `Services/` or `Identity/Policies/` folder. Add one
only when you have a real member for it.

---

## 4. `AppDbContext`

```csharp
public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<AppUser>(options), IAppDbContext
{
    public DbSet<Car> Cars => this.Set<Car>();

    public DbSet<RefreshToken> RefreshTokens => this.Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
```

Deliberately minimal: `DbSet` properties and one override. Cross-cutting save behaviour —
auditing, domain events — lives in interceptors, so the context never grows a pipeline.

- Inherits `IdentityDbContext<AppUser>` — Identity tables and business tables share one context
  and therefore one transaction.
- Implements `IAppDbContext`, the Application-facing surface. Adding an entity means adding the
  `DbSet` in **both** places.
- `base.OnModelCreating` must be called first, or the Identity model is not configured.
- `ApplyConfigurationsFromAssembly` picks up every `IEntityTypeConfiguration<T>` automatically —
  never register one by hand.

### Domain event dispatch

`DispatchDomainEventsInterceptor` publishes domain events **after the transaction commits**.

The split is forced by EF Core's lifecycle: events must be *collected* in `SavingChanges`, because
the ChangeTracker is reset afterwards, but *published* in `SavedChanges`, once the data is durable.
The interceptor is scoped, so it holds the pending events on itself between the two callbacks and
clears each entity's list at collection time — a second `SaveChanges` in the same scope cannot
re-publish them.

**Why after the commit:** an event asserts that something *has happened*. Publishing before the
write is durable means a handler can act on a change that then fails to persist.

**The trade-off:** a handler that throws no longer rolls the transaction back, so the side effect
is lost while the write survives. If a side effect must never be lost, write an outbox row inside
the same transaction and process it separately — see Future Extensions.

No concrete `DomainEvent` exists yet, so nothing exercises this today.

---

## 5. Entity configuration

```csharp
public class CarConfiguration : IEntityTypeConfiguration<Car>
{
    public void Configure(EntityTypeBuilder<Car> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Make).IsRequired().HasMaxLength(100);
        builder.Property(c => c.Model).IsRequired().HasMaxLength(100);
        builder.Property(c => c.Year).IsRequired();

        builder.OwnsOne(c => c.Description, description =>
        {
            description.ToJson();
            description.Property(d => d.En).IsRequired();
            description.Property(d => d.Ar).IsRequired();
        });

        builder.HasIndex(c => new { c.Make, c.Model });
    }
}
```

- **All mapping lives here.** The Domain carries no EF attributes.
- **`LocalizedText` is always `OwnsOne(...).ToJson()`** — one JSONB column holding
  `{"En": "...", "Ar": "..."}`. This is why `LocalizedText` needs its private parameterless
  constructor and settable properties.
- Constrain lengths here as well as in the Contracts DataAnnotations — the annotation protects the
  API, this protects the database.
- EF Core cannot nest `OwnsOne(...).ToJson()` inside an `OwnsMany(...).ToTable(...)`. In that
  situation map the two languages as flat columns instead:

  ```csharp
  items.OwnsOne(i => i.Label, label =>
  {
      label.Property(l => l.En).HasColumnName("LabelEn");
      label.Property(l => l.Ar).HasColumnName("LabelAr");
  });
  ```

  This is a persistence workaround only — the entity still exposes a `LocalizedText`.

### Migrations

```powershell
dotnet ef migrations add <Name> -p src/Taxi.Infrastructure -s src/Taxi.Api
```

Migrations always target Infrastructure with the API as startup project (the connection string
lives in the API's configuration). Review the generated file — an unintended `DropColumn` on a
JSONB owned type is easy to produce and expensive to discover.

---

## 6. Interceptors

Two are registered as `ISaveChangesInterceptor` (both scoped) and attached to the context in
`AddInfrastructure` via `options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>())`.
Adding a third means registering it there and nothing else.

| Interceptor | Runs | Does |
|---|---|---|
| `AuditableEntityInterceptor` | `SavingChanges` | Stamps audit columns |
| `DispatchDomainEventsInterceptor` | `SavingChanges` + `SavedChanges` | Collects events before the write, publishes after the commit (§4) |

### `AuditableEntityInterceptor`

Stamps every tracked `AuditableEntity`:

- `EntityState.Added` → `CreatedBy` + `CreatedAtUtc`, then `LastModifiedBy` + `LastModifiedUtc`.
- `EntityState.Modified` **or** `entry.HasChangedOwnedEntities()` → `LastModifiedBy` +
  `LastModifiedUtc`. The owned-entity check matters: editing only a `LocalizedText` leaves the
  owner `Unchanged`, and without it the timestamp would not move.
- Owned references that are themselves `AuditableEntity` get stamped too.

Values come from `IUser` (implemented in the **API** layer as `CurrentUser`) and `TimeProvider`
(registered as `TimeProvider.System`, so tests can substitute a fake clock).

`AuditableEntityExtensions.HasChangedOwnedEntities` is the supporting extension.

---

## 7. Initialisation and seeding

`ApplicationDbContextInitialiser`:

| Method | Does |
|---|---|
| `InitialiseAsync` | `Database.MigrateAsync()` when the provider is Npgsql. Logs and rethrows on failure. |
| `SeedAsync` | Delegates to `TrySeedAsync` with logging. |
| `TrySeedAsync` | Creates the `Manager` role, the `admin@taxi.com` / `Admin123!` user (`EmailConfirmed = true`), and one sample `Car` — each guarded by an existence check, so it is idempotent. |
| `ResetDatabaseAsync` | **Destructive.** `EnsureDeletedAsync` then `MigrateAsync`. |

**Who calls it:** `Program.cs` → `app.ApplyMigrationsWithRetryAsync()`, which lives in
`Taxi.Api/DependencyInjection.cs`. It honours `Database:ResetOnStartup` (ignored outside
Development) and retries `InitialiseAsync` + `SeedAsync` up to 5 times, 3 seconds apart, so the
API survives starting before PostgreSQL is ready. Migration on startup is gated by
`Database:ApplyMigrationsOnStartup`, defaulting to on outside Production.

`ApplyMigrationsWithRetryAsync` is the single entry point. An older retry-free
`InitialiserExtensions.InitialiseDatabaseAsync` used to sit here unused and has been removed —
do not reintroduce a second path.

Seeding uses the real `Car.Create` factory and checks `IsSuccess` — seed data goes through the same
invariants as user input.

---

## 8. Identity and tokens

### `IdentityService : IIdentityService`

Wraps `UserManager<AppUser>`, `IUserClaimsPrincipalFactory<AppUser>` and `IAuthorizationService`
so no ASP.NET Identity type reaches the Application layer. Returns `Result<AppUserDto>`, never
`IdentityResult`.

Failure codes are `LocalizationKeys.Auth.*` constants — `UserNotFound`, `EmailNotConfirmed`,
`InvalidLoginAttempt`. Emails in the `Description` fallback are masked through
`UtilityService.MaskEmail`. **Any new failure here needs a `LocalizationKeys` constant and entries
in both resource files.**

### `TokenProvider : ITokenProvider`

- `GenerateJwtTokenAsync` — HMAC-SHA256 access token from the `JwtSettings` section
  (`Secret`, `Issuer`, `Audience`, `TokenExpirationInMinutes`), with `sub`, `email`, and one
  `ClaimTypes.Role` per role.
- Refresh token rotation, in one unit of work: `ExecuteDeleteAsync` all existing rows for the
  user, then `RefreshToken.Create(...)` (checked for `IsError`) with a 7-day expiry, then
  `SaveChangesAsync`. One live refresh token per user.
- `GenerateRefreshToken` — `RandomNumberGenerator.GetBytes(32)`, base64. Opaque and random, not a
  JWT.
- `GetPrincipalFromExpiredToken` — validates issuer, audience and signature with
  `ValidateLifetime = false`, so an expired access token can still identify its owner during
  refresh. Rejects any algorithm other than HMAC-SHA256.

`AppUser : IdentityUser` is currently empty. Extra profile fields go here (and produce a migration).

---

## 9. Settings

```csharp
public class AppSettings
{
    public string CorsPolicyName { get; set; } = default!;
    public string[] AllowedOrigins { get; set; } = default!;
    public string DefaultLanguage { get; set; } = Languages.Default;
}
```

Bound with `services.Configure<AppSettings>(configuration.GetSection("AppSettings"))` and consumed
through `IOptions<AppSettings>` — or, in `AddConfiguredCors`, read directly at startup via
`configuration.GetSection("AppSettings").Get<AppSettings>()`, which is acceptable in the
composition root only.

`JwtSettings` and `ConnectionStrings:DefaultConnection` are **not** bound to a settings class —
`TokenProvider` and `AddInfrastructure` read `IConfiguration` directly. Both are blank in
`appsettings.json` on purpose; supply them via user secrets, environment variables or `.env`.

`ForwardedHeadersSettings` lives in the **API** project, since forwarded headers are an HTTP
concern.

---

## 10. `AddInfrastructure(configuration)`

In order:

1. `Configure<AppSettings>` from the `AppSettings` section.
2. `AddSingleton(TimeProvider.System)`.
3. Read `ConnectionStrings:DefaultConnection`; `ArgumentNullException.ThrowIfNull` — fail fast at
   startup rather than on first request.
4. `AddScoped<ISaveChangesInterceptor, AuditableEntityInterceptor>()`.
5. `AddDbContext<AppDbContext>` — attaches all registered interceptors, `UseNpgsql` with
   `EnableRetryOnFailure(5, 10s)` and a 60-second command timeout.
6. `AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>())` — the same scoped
   instance behind both types, so one transaction covers a request.
7. JWT bearer authentication: validates issuer, audience, lifetime and signing key, 30-second
   clock skew.
8. `AddIdentityCore<AppUser>().AddRoles<IdentityRole>().AddEntityFrameworkStores<AppDbContext>()
   .AddDefaultTokenProviders()` — **`IdentityCore`, not `AddIdentity`**: no cookie scheme is
   registered, which is correct for a token API. Password rules are deliberately relaxed
   (6 chars, no digit/upper/symbol requirement) — tighten before production.
9. `AddTransient<IIdentityService, IdentityService>()`, `AddTransient<ITokenProvider, TokenProvider>()`.
10. `AddHybridCache` — 10-minute default expiration, 30-second L1 window.

Note the lifetimes: the interceptor and `DbContext` are **scoped**; the identity services are
**transient** but depend on scoped `UserManager` / `IAppDbContext`, which is safe (transient
resolving scoped inside a scoped request).

---

## 11. Belongs / does not belong

**Belongs:** `DbContext` and EF configurations · migrations and seeding · interceptors ·
implementations of Application interfaces · JWT and Identity plumbing · options classes ·
DI registration for all of the above.

**Does not belong:** business rules or `throw` on a business condition — by the time an entity
reaches `Add()`, Domain and Application have already validated it · `HttpContext` access (use
`IUser`) · defining a new abstraction (declare it in Application, implement it here) ·
returning `IdentityResult` or any provider type to an outer layer.

---

## 12. Naming

| Thing | Convention |
|---|---|
| Configuration | `<Entity>Configuration : IEntityTypeConfiguration<Entity>` in `Data/Configurations/` |
| Interceptor | `<Concern>Interceptor : SaveChangesInterceptor` in `Data/Interceptors/` |
| Service | named after the interface without the `I` — `IIdentityService` → `IdentityService` |
| Settings | `<Area>Settings`, matching the configuration section name |
| Migration | `dotnet ef migrations add <VerbNoun>` — `AddLocalizedDescriptionToCar` |

Members use the `this.` prefix like the rest of the solution; primary-constructor parameters are
assigned to `private readonly` fields without an underscore.

---

## 13. Talking to other layers

| Direction | How |
|---|---|
| **← Application** | Implements the interfaces Application declares — `IAppDbContext`, `IIdentityService`, `ITokenProvider`. It never declares its own business abstraction. |
| **→ Application** | Returns `Result<T>` and Application DTOs (`AppUserDto`, `TokenResponse`). Never `IdentityResult`, never an EF or Npgsql type. |
| **→ Domain** | Maps entities in `IEntityTypeConfiguration<T>`; seeds through the real `Entity.Create` factories; stamps `AuditableEntity` fields in the interceptor. Reads `Result<T>` when a factory can fail. |
| **→ Contracts** | `LocalizationKeys` for error codes, `Languages.Default` for `AppSettings`. |
| **← Api** | One call only: `AddInfrastructure(configuration)` from `Program.cs`, plus `ApplicationDbContextInitialiser` in the startup path. No controller may name a type from this project. |
| **← Api (inbound dependency)** | `IUser` is consumed by `AuditableEntityInterceptor` but **implemented in `Taxi.Api`** (`CurrentUser`), because it needs `HttpContext`. Outside a request `IUser.Id` is null — which is why `CreatedBy` is nullable. |

Adding a capability follows one direction every time: **declare the interface in Application,
implement it here, register it in `AddInfrastructure`.** Never the reverse.

---

## 14. Common mistakes

| ❌ | ✅ |
|---|---|
| `[Table("Cars")]` on the entity | `builder.ToTable("Cars")` in the configuration |
| `builder.Property(c => c.Description)` for a `LocalizedText` | `builder.OwnsOne(c => c.Description, d => d.ToJson())` |
| Registering a configuration manually | `ApplyConfigurationsFromAssembly` already found it |
| `throw` on a business condition in a service | Return `Error` / `Result<T>` |
| `Error.NotFound("User_Not_Found", ...)` | `Error.NotFound(LocalizationKeys.Auth.UserNotFound, ...)` |
| `IHttpContextAccessor` in a service here | Inject `IUser` |
| Adding an entity to `AppDbContext` only | Add the `DbSet` to `IAppDbContext` too |
| `dotnet ef migrations add X` from the repo root | `-p src/Taxi.Infrastructure -s src/Taxi.Api` |
| `EnsureCreated()` | `MigrateAsync()` — `EnsureCreated` skips the migration history |

---

## 15. Future Extensions — NOT IMPLEMENTED

> ⚠️ **None of the following exists in this repository.** Corrected sketches only — in particular,
> the previous version of this document showed a Dapper sample using `Microsoft.Data.SqlClient`
> against a PostgreSQL database, which would not work.

### 15.1 Background jobs

```csharp
// src/Taxi.Infrastructure/BackgroundJobs/ExpiredRefreshTokenSweeper.cs
public sealed class ExpiredRefreshTokenSweeper(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<ExpiredRefreshTokenSweeper> logger) : BackgroundService
{
    private readonly IServiceScopeFactory scopeFactory = scopeFactory;
    private readonly TimeProvider timeProvider = timeProvider;
    private readonly ILogger<ExpiredRefreshTokenSweeper> logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1), this.timeProvider);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                // CRITICAL: BackgroundService is a singleton; IAppDbContext is scoped.
                using var scope = this.scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<IAppDbContext>();

                var cutoff = this.timeProvider.GetUtcNow();

                await context.RefreshTokens
                    .Where(t => t.ExpiresOnUtc < cutoff)
                    .ExecuteDeleteAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Refresh token sweep failed.");
            }
        }
    }
}
```

Register with `services.AddHostedService<ExpiredRefreshTokenSweeper>();` in `AddInfrastructure`.
Two rules: never resolve a scoped service in the constructor (create a scope per tick), and never
let the loop escape without a `catch`, or one failure kills the service for the process lifetime.
`AuditableEntityInterceptor` will fail to attribute changes here — `IUser.Id` is null outside a
request; supply a system-user constant of your own if attribution matters.

### 15.2 Real-time push (SignalR)

`Microsoft.AspNetCore.SignalR.Client` is already referenced by `Taxi.Client`; the server side would
need `Microsoft.AspNetCore.SignalR` on the API. The hub is a dumb transport; the decision to notify
belongs to Application.

```csharp
// Application declares the need
// src/Taxi.Application/Common/Interfaces/IRideNotifier.cs
public interface IRideNotifier
{
    Task RideStateChangedAsync(Guid rideId, string state, CancellationToken ct);
}
```

```csharp
// Infrastructure implements it
// src/Taxi.Infrastructure/RealTime/RideHub.cs
public sealed class RideHub : Hub;

// src/Taxi.Infrastructure/RealTime/SignalRRideNotifier.cs
public sealed class SignalRRideNotifier(IHubContext<RideHub> hubContext) : IRideNotifier
{
    private readonly IHubContext<RideHub> hubContext = hubContext;

    public Task RideStateChangedAsync(Guid rideId, string state, CancellationToken ct)
        => this.hubContext.Clients.All.SendAsync("RideStateChanged", rideId, state, ct);
}
```

`Program.cs` maps the endpoint (`app.MapHub<RideHub>("/hubs/rides")`); the caller is an
`INotificationHandler<TDomainEvent>` in Application, not a command handler.

### 15.3 Dapper for heavy reads

Only if an EF Core projection is measurably too slow — measure first. Use **`NpgsqlConnection`**,
never `SqlConnection`:

```csharp
// src/Taxi.Application/Common/Interfaces/ISqlConnectionFactory.cs
public interface ISqlConnectionFactory
{
    Task<IEnumerable<T>> QueryAsync<T>(string sql, object? parameters = null, CancellationToken ct = default);
}
```

```csharp
// src/Taxi.Infrastructure/Data/NpgsqlConnectionFactory.cs
public sealed class NpgsqlConnectionFactory(IConfiguration configuration) : ISqlConnectionFactory
{
    private readonly string connectionString =
        configuration.GetConnectionString("DefaultConnection")!;

    public async Task<IEnumerable<T>> QueryAsync<T>(string sql, object? parameters = null, CancellationToken ct = default)
    {
        await using var connection = new NpgsqlConnection(this.connectionString);
        return await connection.QueryAsync<T>(new CommandDefinition(sql, parameters, cancellationToken: ct));
    }
}
```

Caveats: raw SQL bypasses the audit interceptor and domain events, so it is read-only territory;
JSONB columns need `->>'En'` extraction by hand; and you now have two schema truths to keep in
sync with migrations. Always parameterize — never interpolate into `sql`.

### 15.4 Outbox for reliable side effects

If domain events must survive a crash, in-process `IMediator.Publish` after commit is not
enough — the process can die between the commit and the publish. The standard fix is an
`OutboxMessage` entity written in the **same transaction** as the
business change, plus a `BackgroundService` (see Background jobs, above) that reads unprocessed
rows and dispatches them. Worth it only when an external system must not miss an event.
