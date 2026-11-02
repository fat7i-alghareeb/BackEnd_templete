# Infrastructure Layer Blueprint — `Taxi.Infrastructure`

> Start at [AGENTS.md](../../AGENTS.md): it carries the rules and the add-a-feature checklist.
> This file is the deep reference for the Infrastructure layer.
> Map of all docs: [NAVIGATION.md](../../docs/NAVIGATION.md).

The plugin ring: concrete implementations of the interfaces `Taxi.Application` declares.
PostgreSQL through EF Core, ASP.NET Identity, JWT issuance, the transactional outbox, caching and
locking (Redis optional), file storage, email and health checks.

---

## 1. At a glance

| | |
|---|---|
| Project | `src/Taxi.Infrastructure` |
| References | project `Taxi.Application` |
| Packages | `Npgsql.EntityFrameworkCore.PostgreSQL`, `Microsoft.AspNetCore.Identity.EntityFrameworkCore`, `Microsoft.AspNetCore.Authentication.JwtBearer`, `Microsoft.AspNetCore.Authorization`, `Microsoft.Extensions.Caching.Hybrid`, `Microsoft.Extensions.Caching.StackExchangeRedis`, `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore`, `MailKit`, `Serilog.AspNetCore`, `Serilog.Sinks.Seq`; EF Core `Tools` + `Design` (`PrivateAssets=all`) |
| Referenced by | `Taxi.Api`, only so `Program.cs` can call `AddInfrastructure` and the startup path can use `ApplicationDbContextInitialiser` |
| Reference code | `Data/AppDbContext.cs`, `Data/Configurations/CarConfiguration.cs` and `DriverConfiguration.cs`, `Identity/TokenProvider.cs`, `Outbox/` |
| Rules enforced by the build | `tests/Taxi.ArchitectureTests` (see §20) |

Domain and Contracts are reachable transitively and used directly (entities, `Result<T>`,
`LocalizationKeys`).

---

## 2. Purpose

Application says *what* it needs (`IAppDbContext`, `IIdentityService`, `ITokenProvider`,
`IFileStorage`, `IEmailSender`, `IDistributedLock`); Infrastructure says *how*. Everything
technology-specific lives here so that swapping PostgreSQL, the token format, the cache backend or
the mail provider touches no use case.

It is also the composition point for all data-related DI: `AddInfrastructure(configuration)` is a
single call from `Program.cs`.

---

## 3. Dependency rules

**Never add:** a reference to `Taxi.Api` · business rules · anything that decides *what* should
happen rather than *how* it is stored or transmitted.

No controller may reference a type from this project (`ConventionTests` enforces it).

---

## 4. Directory structure

```text
src/Taxi.Infrastructure/
├── Data/
│   ├── AppDbContext.cs
│   ├── ApplicationDbContextInitialiser.cs    migrate + seed (+ destructive reset)
│   ├── Configurations/                       CarConfiguration, DriverConfiguration,
│   │                                         RefreshTokenConfiguration, OutboxMessageConfiguration,
│   │                                         ConcurrencyTokenExtensions (xmin)
│   ├── Interceptors/                         AuditableEntityInterceptor, AuditableEntityExtensions,
│   │                                         SoftDeleteInterceptor
│   └── Migrations/                           eight migrations + AppDbContextModelSnapshot
├── Email/                                    EmailSettings, LoggingEmailSender, SmtpEmailSender
├── Files/                                    FileStorageSettings, LocalFileStorage
├── HealthChecks/                             DistributedCacheHealthCheck, HealthCheckTags
├── Identity/                                 AppUser, IdentityService, IdentityErrorMapper,
│                                             TokenProvider
├── Locking/                                  RedisDistributedLock, InMemoryDistributedLock
├── Outbox/                                   OutboxMessage, OutboxInterceptor, OutboxProcessor,
│                                             OutboxBackgroundService, OutboxSettings
├── Settings/                                 AppSettings, JwtSettings, SeedSettings
├── DependencyInjection.cs
└── Infrastructure_Layer_Blueprint.md
```

| Folder | Holds |
|---|---|
| `Data/` | `DbContext`, initialiser, EF configurations, interceptors, migrations |
| `Data/Configurations/` | One `IEntityTypeConfiguration<T>` per mapped type, auto-discovered |
| `Identity/` | The Identity user type and the services wrapping ASP.NET Identity and JWT |
| `Outbox/` | The transactional outbox (its interceptor lives here, not in `Data/Interceptors/`) |
| `Settings/` | Strongly typed options bound from configuration; feature-specific settings sit with their feature (`Email/`, `Files/`, `Outbox/`) |

There is no `BackgroundJobs/`, `RealTime/` or `Identity/Policies/` folder. Add one only when it has
a real member.

---

## 5. `AppDbContext`

```csharp
public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<AppUser>(options), IAppDbContext
{
    public DbSet<Car> Cars => this.Set<Car>();
    public DbSet<Driver> Drivers => this.Set<Driver>();
    public DbSet<RefreshToken> RefreshTokens => this.Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        ApplySoftDeleteQueryFilters(builder);
    }
}
```

Deliberately minimal: `DbSet` properties and one override. Cross-cutting save behaviour (auditing,
soft delete, domain events) lives in interceptors, so the context never grows a pipeline.

- Inherits `IdentityDbContext<AppUser>`: Identity tables and business tables share one context and
  therefore one transaction.
- Implements `IAppDbContext`, the Application-facing surface. **Adding an entity means adding the
  `DbSet` in both places** (`AppDbContext` and `IAppDbContext`).
- `base.OnModelCreating` must be called first, or the Identity model is not configured.
- `ApplyConfigurationsFromAssembly` picks up every `IEntityTypeConfiguration<T>`: never register one
  by hand.
- `ApplySoftDeleteQueryFilters` adds `WHERE NOT "IsDeleted"` to every root `ISoftDeletable` entity
  type (today `Car`). Use `IgnoreQueryFilters()` only for a deliberate "show deleted" or "restore"
  use case.
- `OutboxMessage` is **not** a `DbSet` and not on `IAppDbContext`; the outbox reaches it through
  `context.Set<OutboxMessage>()`. It is an Infrastructure persistence type, not a domain entity.

---

## 6. Entity configuration and migrations

Reference configurations:

| File | Shows |
|---|---|
| `CarConfiguration` | Scalar columns with lengths; a `LocalizedText` JSON column; a composite index; soft-delete columns; `ImageKey`; `xmin` concurrency |
| `DriverConfiguration` | A unique index; an owned **collection** in its own table whose `LocalizedText` is flat columns; a relationship by id; a filtered unique index; `xmin` concurrency |
| `RefreshTokenConfiguration` | Explicit mapping of **every get-only property** |
| `OutboxMessageConfiguration` | A `jsonb` column and a partial index |

```csharp
// CarConfiguration (excerpt)
builder.OwnsOne(c => c.Description, description =>
{
    description.ToJson();
    description.Property(d => d.En).IsRequired();
    description.Property(d => d.Ar).IsRequired();
});
builder.HasIndex(c => new { c.Make, c.Model });
builder.UseXminConcurrencyToken();
```

- **All mapping lives here.** The Domain carries no EF attributes.
- **`LocalizedText` on a root entity is always `OwnsOne(...).ToJson()`**: one JSONB column holding
  `{"En": "...", "Ar": "..."}`. That is why `LocalizedText` has a private parameterless constructor
  and settable properties.
- **Get-only entity properties are not mapped by convention.** List every one. `ExpiresOnUtc` was
  once missing from `RefreshTokenConfiguration`, so it was never stored and every refresh token
  read back as expired. `ModelMappingTests` (integration suite) now fails if any public property of
  any domain entity is unmapped.
- Constrain lengths here as well as in the Contracts `DataAnnotations`: the annotation protects
  the API, this protects the database.
- **EF Core cannot nest `OwnsOne(...).ToJson()` inside `OwnsMany(...).ToTable(...)`.** For a
  `LocalizedText` on a child-collection item, map the two languages as flat columns (the entity
  still exposes a `LocalizedText`):

  ```csharp
  certification.OwnsOne(c => c.Name, name =>
  {
      name.Property(n => n.En).HasColumnName("NameEn").HasMaxLength(100).IsRequired();
      name.Property(n => n.Ar).HasColumnName("NameAr").HasMaxLength(100).IsRequired();
  });
  ```

- A relationship to another aggregate is by id only, with no navigation on either side:
  `builder.HasOne<Car>().WithMany().HasForeignKey(d => d.AssignedCarId).OnDelete(Restrict)`. A
  one-to-one-in-practice rule is a **filtered unique index**
  (`HasFilter("\"AssignedCarId\" IS NOT NULL")`) so unassigned drivers do not collide.
- A private backing-field collection needs `builder.Navigation(d => d.Certifications)
  .UsePropertyAccessMode(PropertyAccessMode.Field)`.

### Migrations

```powershell
dotnet ef migrations add <Name> -p src/Taxi.Infrastructure -s src/Taxi.Api
```

Migrations always target Infrastructure with the API as startup project (the connection string
lives in the API's configuration; `Taxi.Api` also references `Microsoft.EntityFrameworkCore.Design`).
Review the generated file: an unintended `DropColumn` on a JSONB owned type is easy to produce and
expensive to discover. Current migrations: `Initial_Cars`, `AddLocalizedDescriptionToCar`,
`AddCarSoftDelete`, `AddCarXminConcurrencyToken`, `AddOutboxMessages`, `AddCarImage`,
`MapRefreshTokenExpiry`, `AddDrivers`. Never edit an applied migration or its `.Designer.cs` by
hand; add a new one.

---

## 7. Interceptors

Three `ISaveChangesInterceptor`s are registered **scoped**, in this order, and attached to the
context in `AddInfrastructure` via `options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>())`.
**Registration order is execution order.** Adding another means registering it there, in the right
position.

| # | Interceptor | Runs | Does |
|---|---|---|---|
| 1 | `SoftDeleteInterceptor` | `SavingChanges` | Turns a delete of an `ISoftDeletable` into an update stamping `IsDeleted`, `DeletedAtUtc`, `DeletedBy` |
| 2 | `AuditableEntityInterceptor` | `SavingChanges` | Stamps `Created*` / `LastModified*` on every tracked `AuditableEntity` |
| 3 | `OutboxInterceptor` | `SavingChanges` | Turns pending domain events into `OutboxMessage` rows in the same transaction (§8) |

Soft delete runs **before** the audit interceptor so the entry it turns into `Modified` also gets
`LastModified*`.

### `AuditableEntityInterceptor`

- `EntityState.Added`: `CreatedBy` + `CreatedAtUtc`, then `LastModifiedBy` + `LastModifiedUtc`.
- `EntityState.Modified` **or** `entry.HasChangedOwnedEntities()`: `LastModifiedBy` +
  `LastModifiedUtc`. The owned-entity check matters: editing only a `LocalizedText` leaves the
  owner `Unchanged`, and without it the timestamp would not move.
- Owned references that are themselves `AuditableEntity` get stamped too.

Values come from `IUser` (implemented in the **API** as `CurrentUser`) and `TimeProvider`
(registered as `TimeProvider.System`, so tests can substitute a fake clock). Outside a request
`IUser.Id` is null, which is why `CreatedBy` is nullable.

### Soft delete

`Car` implements `ISoftDeletable`. Handlers still call `context.Cars.Remove(car)`;
`SoftDeleteInterceptor` flips the state back to `Modified` and stamps `IsDeleted = true`,
`DeletedAtUtc`, `DeletedBy`. Owned references cascaded to `Deleted` with the owner (the
`LocalizedText` JSON column) are reset to `Unchanged`, or EF would null the column. The global
query filter then hides the row from every query: a deleted car is a 404, a second `DELETE` is a
404.

To make another entity soft-deletable: implement `ISoftDeletable`, configure the `IsDeleted`
default and the `DeletedBy` length in its configuration as `CarConfiguration` does, and add a
migration. Nothing else.

### Optimistic concurrency (`xmin`)

`CarConfiguration` and `DriverConfiguration` call `builder.UseXminConcurrencyToken()`
(`Configurations/ConcurrencyTokenExtensions.cs`): a `uint` **shadow** property mapped to
PostgreSQL's `xmin` system column (the id of the transaction that last wrote the row) with
`IsRowVersion()`. No column is added (Npgsql skips system columns), and the domain never sees it.

Every `UPDATE` of a tracked row then carries `WHERE "Id" = @id AND xmin = @original`. If another
transaction changed the row between the handler's load and its `SaveChangesAsync`, zero rows match
and EF throws `DbUpdateConcurrencyException`; `GlobalExceptionHandler` maps it to a localized
**409** (`LocalizationKeys.Concurrency.Conflict`) instead of letting the last writer win. Add the
call to any configuration whose entity is updated by read-modify-write.

The token protects the read-modify-write inside one request. It is not exposed to clients, so it
does not detect a client editing a stale copy fetched minutes earlier; that would need the value in
the DTO plus `If-Match` / `ETag`.

---

## 8. The transactional outbox

Domain events never run inside the request. The flow:

1. An entity method raises an event (`Car.Create` → `CarCreatedDomainEvent`;
   `Driver.AssignCar` → `DriverAssignedToCarDomainEvent`).
2. `OutboxInterceptor` serializes every pending event into an `OutboxMessage` row (`Type` = CLR full
   name, `Content` = JSON) and adds it to the **same** `SaveChanges` (one transaction, so the
   change and its events commit together or not at all), then clears the entity's event list so a
   second save cannot store them twice.
3. `OutboxBackgroundService` calls `OutboxProcessor.ProcessAsync` every
   `Outbox:PollingIntervalSeconds` (5). The processor takes the `IDistributedLock`
   `outbox-processor` (5-minute expiry), so one instance processes at a time, loads up to
   `BatchSize` (20) pending rows in arrival order, and publishes each through MediatR
   (`IPublisher`) in a fresh DI scope.
4. Success sets `ProcessedOnUtc`. A failure increments `Attempts`, stores the exception in `Error`
   and schedules `NextAttemptOnUtc` with exponential backoff (2, 4, 8 … seconds, capped at 5
   minutes). After `MaxAttempts` (5) the row is **parked**: kept, never retried, for someone to
   inspect. Each message is saved individually so a crash re-delivers as little as possible.
5. Processed rows older than `RetentionDays` (7) are deleted at the end of every batch.

Properties to design for:

- **At-least-once.** If the process dies after a handler ran but before the row was marked, the
  handler runs again. Handlers must be idempotent.
- **Eventually.** Handlers run seconds after the request, with no `HttpContext` (`IUser.Id` is
  null) and in a scope of their own.
- **Allow-listed types.** A row is deserialized only into a concrete `DomainEvent` subtype found
  in the Domain assembly; a tampered `Type` column cannot instantiate anything else.
- **`Outbox:Enabled=false`** stops an instance from running the processor (events are still
  written). Use it on instances that should not process.
- Settings (section `Outbox`, validated on start): `Enabled`, `PollingIntervalSeconds` (1–3600),
  `BatchSize` (1–1000), `MaxAttempts` (1–100), `RetentionDays` (1–365).

The `OutboxMessages` table has a partial index on `OccurredOnUtc WHERE "ProcessedOnUtc" IS NULL`.
The integration test `CarsTests.Create_WritesAnOutboxMessage_ThatTheProcessorPublishes` exercises
the whole path.

---

## 9. Initialisation and seeding

`ApplicationDbContextInitialiser`:

| Method | Does |
|---|---|
| `InitialiseAsync` | `Database.MigrateAsync()` when the provider is Npgsql. Logs and rethrows on failure. |
| `SeedAsync` | Delegates to the private `TrySeedAsync` with logging. |
| `TrySeedAsync` (private) | Creates every role in `Roles.All`; the `Seed:Admin` (`Manager`) and `Seed:User` (`User`) accounts (`EmailConfirmed = true`), each only when both its email and password are configured; one sample `Car` only when `Seed:SampleData` is `true` and the table is empty. Every step is guarded by an existence check, so it is idempotent; a failed `IdentityResult` throws instead of being ignored. |
| `ResetDatabaseAsync` | **Destructive.** `EnsureDeletedAsync` then `MigrateAsync`. |

**Who calls it:** `Program.cs` → `app.ApplyMigrationsWithRetryAsync()`
(`Taxi.Api/DependencyInjection.cs`). It honours `Database:ResetOnStartup` (**ignored outside
Development**, with a warning) and retries `InitialiseAsync` + `SeedAsync` up to 5 times, 3
seconds apart, so the API survives starting before PostgreSQL is ready.

**Whether it runs:** `Program.cs` reads `Database:ApplyMigrationsOnStartup`; when unset, the code
default is *on outside Production, off in Production*. `appsettings.Production.json` ships
`"ApplyMigrationsOnStartup": true`, so a Production deployment from this repository **does**
migrate and seed on startup unless you set `Database__ApplyMigrationsOnStartup=false` and run
migrations in your pipeline. When disabled, the API logs that deployment must apply migrations
before traffic is enabled.

`ApplyMigrationsWithRetryAsync` is the single entry point. Do not reintroduce a second path.

**Seed configuration** (`SeedSettings`, section `Seed`) has no defaults in `appsettings.json`: an
environment seeds nothing unless it says so. `appsettings.Development.json` supplies
`admin@taxi.com` / `Admin123!`, the read-only `user@taxi.com` / `User123!` and
`SampleData: true`. The production compose file passes `SEED_ADMIN_EMAIL` /
`SEED_ADMIN_PASSWORD` from `.env` when you want a bootstrap Manager account. **Never commit real
credentials to an `appsettings.*.json`.**

Seeding uses the real `Car.Create` factory and checks `IsSuccess`: seed data goes through the same
invariants as user input. Only a sample car is seeded; there is no sample driver.

---

## 10. Identity and tokens

### `IdentityService : IIdentityService`

Wraps `UserManager<AppUser>`, `IUserClaimsPrincipalFactory<AppUser>` and `IAuthorizationService`
so no ASP.NET Identity type reaches the Application layer. It returns `Result<…>`, never
`IdentityResult`; failures go through `IdentityErrorMapper`, which maps Identity error codes to
`LocalizationKeys` (`Validation.Password*`, `Account.CurrentPassword.Invalid`,
`Account.Token.Invalid`, else `Account.Identity.Failed`) keyed by the offending request property.
**Any new failure here needs a `LocalizationKeys` constant and entries in both resource files.**

- `AuthenticateAsync` is deliberately uniform: an unknown email, a wrong password and a locked-out
  account all return `ApplicationErrors.InvalidCredentials` (**401**), so the login endpoint
  cannot enumerate accounts. A wrong password calls `AccessFailedAsync`; Identity lockout (5
  failures → 15 minutes, set in `AddIdentityCore`) backs up the per-IP rate limiter against
  distributed guessing. `EmailNotConfirmed` (409) is returned only after the password has been
  verified. Emails in `Description` fallbacks are masked through `UtilityService.MaskEmail`.
- Email-confirmation and password-reset tokens are Base64Url-encoded on the way out
  (`Encode`) and decoded on the way in; a malformed token is `AccountTokenInvalid`, identical to
  an unknown user.
- A successful `ResetPasswordAsync` clears the lockout (end date and failure count).
- `SetRolesAsync` removes roles not in the new set and adds the missing ones.

### `TokenProvider : ITokenProvider`

- `GenerateJwtTokenAsync`: HMAC-SHA256 access token from `JwtSettings` (`Secret`, `Issuer`,
  `Audience`, `TokenExpirationInMinutes`) with claims `sub`, `email` and one `ClaimTypes.Role` per
  role.
- **Refresh-token rotation:** `ExecuteDeleteAsync` removes the user's existing rows immediately (its
  own statement), then `RefreshToken.Create(...)` (checked for `IsError`) builds a new one living
  `RefreshTokenExpirationInDays`, which `SaveChangesAsync` inserts. One live refresh token per
  user. The delete and the insert are *not* wrapped in one transaction.
- `GenerateRefreshToken`: 32 random bytes, base64. Opaque, not a JWT.
- `GetPrincipalFromExpiredToken`: validates issuer, audience and signature with
  `ValidateLifetime = false`, so an expired access token can still identify its owner during
  refresh. Rejects any algorithm other than HMAC-SHA256, and returns `null` (not an exception)
  for a malformed or tampered token so the handler answers a client error instead of a 500.

`AppUser : IdentityUser` is empty. Extra profile fields go here (and produce a migration).

---

## 11. Caching and distributed locking

`AddAppCaching(configuration)`:

- Redis **only when** `ConnectionStrings:Redis` is set: one shared `IConnectionMultiplexer`
  (`AbortOnConnectFail = false`, so the API starts while Redis is still coming up and reconnects
  in the background), `AddStackExchangeRedisCache` with instance prefix `taxi:` as HybridCache's
  L2, and `RedisDistributedLock`.
- Without Redis: in-process cache only and `InMemoryDistributedLock`. Correct for a single
  instance.
- `AddHybridCache` with `Expiration = 10 min` and `LocalCacheExpiration = 30 s`
  (`CacheDefaults.LocalExpiration`). HybridCache has **no backplane**: after an eviction on one
  node another node may serve its L1 copy for up to 30 s.

`IDistributedLock.TryAcquireAsync` tries once and returns a handle (`IAsyncDisposable`) or `null`.
Redis: atomic `SET key token NX PX` under the prefix `taxi:lock:`, released only when the stored
token is still ours. In memory: a `ConcurrentDictionary` with expiry takeover. Used by
`[Idempotent]` POSTs and by `OutboxProcessor`.

---

## 12. File storage

`IFileStorage` → `LocalFileStorage` (singleton), rooted at `FileStorage:LocalRootPath` (default
`storage`, relative to the content root, and **outside `wwwroot`** so uploads are never served as
anonymous static files; `/app/storage` in the container).

- Keys are `folder/<guid32>.<ext>`, generated server-side. Every key is matched against
  `^[a-z0-9-]+/[a-f0-9]{32}\.[a-z0-9]{1,5}$` and resolved inside the root before use, so path
  traversal is impossible.
- Files are created with `FileMode.CreateNew`; `DeleteAsync` of a missing key is not an error.
- Disk is per machine: with several instances, mount shared storage at the root or add an
  object-store implementation behind the same interface.

---

## 13. Email

`AddAppEmail` binds `EmailSettings` (section `Email`, validated on start) and registers
`IEmailSender` (transient):

| `Email:Provider` | Sender | Behaviour |
|---|---|---|
| `Log` (default) | `LoggingEmailSender` | Writes the message **and its one-time link** to the log. Never use in production. |
| `Smtp` | `SmtpEmailSender` (MailKit) | One connection per message; requires `Email:Smtp:Host`. `Security` is `None`, `StartTls` (default) or `SslOnConnect`; credentials only when `Username` is set. |

Other keys: `FromAddress` (default `no-reply@taxi.local`, must be a valid address), `FromName`
(`Taxi`), `Smtp:Port` (587). Development compose points the sender at the Mailpit container (inbox
`http://localhost:8025`); production compose reads `SMTP_*` and `EMAIL_FROM_ADDRESS` from `.env`.
The links in account emails come from `ClientApp:ConfirmEmailUrl` / `ResetPasswordUrl`
(Application settings, validated on start).

---

## 14. Health checks

`AddAppHealthChecks` registers checks tagged `HealthCheckTags.Ready`: `postgres`
(`AddDbContextCheck<AppDbContext>`, which calls `CanConnectAsync`) and, when Redis is configured,
`redis` (`DistributedCacheHealthCheck`, one read against `IDistributedCache`; a miss is healthy).
Every registration gets a **5-second timeout**: without it the PostgreSQL check inherits the
context's `EnableRetryOnFailure` policy and takes about 20 s to report an outage.

The API maps them: `/health/live` runs no checks, `/health/ready` runs the tagged ones (Api
blueprint §16).

---

## 15. Settings

| Class | Section | Notes |
|---|---|---|
| `JwtSettings` | `JwtSettings` | `Secret` required, ≥ 32 characters; `Issuer`, `Audience` required; `TokenExpirationInMinutes` 1–1440 (60); `RefreshTokenExpirationInDays` 1–365 (7). `ValidateDataAnnotations().ValidateOnStart()`: a bad value stops the host at boot with an `OptionsValidationException`. |
| `AppSettings` | `AppSettings` | `CorsPolicyName`, `AllowedOrigins` (used by `AddConfiguredCors`, which reads the section directly at startup). `DefaultLanguage` is bound but **no code reads it**; the default language is `Languages.Default`. |
| `SeedSettings` | `Seed` | `Admin`, `User` (each `Email` + `Password`), `SampleData`. No defaults. |
| `EmailSettings` | `Email` | §13. |
| `FileStorageSettings` | `FileStorage` | `LocalRootPath`. |
| `OutboxSettings` | `Outbox` | §8. |
| `ClientAppSettings` | `ClientApp` | Defined in Application; bound and validated here. |

The JwtBearer handler is configured from the same `IOptions<JwtSettings>`
(`AddOptions<JwtBearerOptions>(...).Configure<IOptions<JwtSettings>>`) and `TokenProvider`
consumes it too, so nothing reads `JwtSettings:*` from `IConfiguration` directly and the bearer
handler and token issuer can never disagree.

`ConnectionStrings:DefaultConnection` is read once in `AddInfrastructure`, which throws a
descriptive `InvalidOperationException` when it is missing or blank. Both it and the JWT secret are
blank in `appsettings.json` on purpose; supply them through user secrets, environment variables or
`.env`.

`ForwardedHeadersSettings` and `RateLimitingSettings` live in the **API** project, since both are
HTTP concerns.

---

## 16. `AddInfrastructure(configuration)`

In order:

1. Bind `AppSettings`, `SeedSettings`, `ClientAppSettings` (validated); `AddSingleton(TimeProvider.System)`.
2. Read `ConnectionStrings:DefaultConnection`; throw `InvalidOperationException` if blank (fail
   fast at startup, not on the first request).
3. `AddScoped<ISaveChangesInterceptor, …>()` for `SoftDeleteInterceptor`, then
   `AuditableEntityInterceptor`, then `OutboxInterceptor`.
4. `AddDbContext<AppDbContext>`: attaches the interceptors; `UseNpgsql` with
   `EnableRetryOnFailure(5, 10 s)` and a 60-second command timeout.
5. `AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>())`: the same scoped
   instance behind both types, so one transaction covers a request. `ApplicationDbContextInitialiser`
   (scoped).
6. `JwtSettings` (validated on start) + JWT bearer authentication: validates issuer, audience,
   lifetime and signing key, 30-second clock skew.
7. `AddIdentityCore<AppUser>().AddRoles<IdentityRole>().AddEntityFrameworkStores<AppDbContext>()
   .AddDefaultTokenProviders()`: **`IdentityCore`, not `AddIdentity`**, so no cookie scheme is
   registered, which is correct for a token API. Password rules are deliberately relaxed (6
   characters, no digit / upper / lower / symbol requirement, 1 unique character): tighten before
   production. Lockout: 5 failures → 15 minutes. `RequireUniqueEmail = true`.
8. `AddTransient<IIdentityService, IdentityService>()`, `AddTransient<ITokenProvider, TokenProvider>()`.
9. `AddAppCaching` (§11), `AddAppHealthChecks` (§14), `AddAppEmail` (§13), `AddOutbox` (§8).
10. `FileStorageSettings` (validated) + `AddSingleton<IFileStorage, LocalFileStorage>()` (§12).

Lifetimes: the interceptors and `DbContext` are **scoped**; the identity services are
**transient** but depend on scoped `UserManager` / `IAppDbContext`, which is safe (a transient
resolved inside a scoped request).

---

## 17. Belongs / does not belong

**Belongs:** `DbContext` and EF configurations · migrations and seeding · interceptors ·
implementations of Application interfaces · JWT and Identity plumbing · the outbox · options
classes · DI registration for all of the above.

**Does not belong:** business rules or a `throw` on a business condition (by the time an entity
reaches `Add()`, Domain and Application have already validated it) · `HttpContext` access (use
`IUser`) · defining a new abstraction (declare it in Application, implement it here) · returning
`IdentityResult` or any provider type to an outer layer.

---

## 18. Naming

| Thing | Convention |
|---|---|
| Configuration | `<Entity>Configuration : IEntityTypeConfiguration<Entity>` in `Data/Configurations/` |
| Interceptor | `<Concern>Interceptor : SaveChangesInterceptor` in `Data/Interceptors/` (the outbox one is in `Outbox/`) |
| Service | named after the interface without the `I`: `IIdentityService` → `IdentityService` |
| Settings | `<Area>Settings`, matching the configuration section name |
| Migration | `dotnet ef migrations add <VerbNoun>`: `AddCarImage` |

Members use the `this.` prefix like the rest of the solution; primary-constructor parameters are
assigned to `private readonly` fields without an underscore.

---

## 19. Talking to other layers

| Direction | How |
|---|---|
| **← Application** | Implements the interfaces Application declares. It never declares its own business abstraction. |
| **→ Application** | Returns `Result<T>` and Application DTOs (`AppUserDto`, `TokenResponse`). Never `IdentityResult`, never an EF or Npgsql type. |
| **→ Domain** | Maps entities in `IEntityTypeConfiguration<T>`; seeds through the real `Entity.Create` factories; stamps `AuditableEntity` / `ISoftDeletable` fields; serializes domain events. |
| **→ Contracts** | `LocalizationKeys` for error codes, `Languages.Default` for `AppSettings`. |
| **← Api** | `AddInfrastructure(configuration)` from `Program.cs`, plus `ApplicationDbContextInitialiser` in the startup path (`ApplyMigrationsWithRetryAsync`), plus `HealthCheckTags`. No controller names a type from this project. |
| **← Api (inbound dependency)** | `IUser` is consumed by the interceptors but **implemented in `Taxi.Api`** (`CurrentUser`), because it needs `HttpContext`. |

Adding a capability follows one direction every time: **declare the interface in Application,
implement it here, register it in `AddInfrastructure`.** Never the reverse.

---

## 20. Enforced by tests

| Rule | Test |
|---|---|
| Infrastructure does not depend on Api | `LayerDependencyTests.Infrastructure_DoesNotDependOnApi` |
| Controllers use no Infrastructure, EF Core or Identity type | `ConventionTests.Controllers_DoNotTouchInfrastructureOrEfCore` |
| Every entity property is mapped | `ModelMappingTests` |
| Soft delete keeps the row with audit fields | `CarsTests.Delete_IsSoft_TheRowRemainsWithAuditFields` |
| `xmin` detects a concurrent update (409) | `CarsTests.ConcurrentUpdate_IsDetectedByXmin_AndAnswered409` |
| Outbox write and publish | `CarsTests.Create_WritesAnOutboxMessage_ThatTheProcessorPublishes` |
| Owned collection round trip, unique licence, one driver per car | `DriversTests` |
| Image storage and forged-content rejection | `CarsTests.Image_UploadDownloadAndRejectForgedContent` |
| Refresh rotation, uniform login failure, logout | `AuthTests` |

The integration suite runs against real PostgreSQL through Testcontainers, so it needs Docker.

---

## 21. Deliberate decisions (not bugs)

| Behaviour | Why |
|---|---|
| `AddIdentityCore`, not `AddIdentity` | A token API needs no cookie scheme. |
| Relaxed password rules | Template default; tighten for production (`AddIdentityCore` options). |
| `LoggingEmailSender` writes one-time links to the log | Development convenience; it is the default only because it needs no infrastructure. |
| Refresh-token delete and insert are separate statements | One live token per user; a crash between them just forces a new sign-in. |
| `xmin` is not exposed to clients | It guards read-modify-write inside a request, not stale client copies. |
| `Database:ApplyMigrationsOnStartup` is `true` in `appsettings.Production.json` | Keeps the single-container compose deployment self-contained. Disable it when migrations run in a pipeline. |
| The outbox runs in every instance unless `Outbox:Enabled=false` | The distributed lock keeps it single-flight when Redis is configured. |
| Parked outbox messages are kept indefinitely | Someone has to look at them. The retention purge deletes only *processed* rows. |

**Known limitation:** ASP.NET Core Data Protection keys are neither persisted nor shared (the
solution never calls `AddDataProtection`). Email-confirmation and password-reset tokens are issued
with those keys, so a token issued before a restart, or by another instance, may fail to validate.
Persist and share the key ring (`PersistKeysTo…`) before running more than one instance or relying
on tokens that outlive a restart.

---

## 22. Common mistakes

| ❌ | ✅ |
|---|---|
| `[Table("Cars")]` on the entity | `builder.ToTable("Cars")` in the configuration |
| `builder.Property(c => c.Description)` for a `LocalizedText` | `builder.OwnsOne(c => c.Description, d => d.ToJson())` |
| `ToJson()` for a `LocalizedText` inside `OwnsMany` | Flat `NameEn` / `NameAr` columns (§6) |
| A get-only entity property with no explicit mapping | List it in the configuration; `ModelMappingTests` will fail otherwise |
| Registering a configuration manually | `ApplyConfigurationsFromAssembly` already found it |
| `throw` on a business condition in a service | Return `Error` / `Result<T>` |
| `Error.NotFound("User_Not_Found", ...)` | `Error.NotFound(LocalizationKeys.Auth.UserNotFound, ...)` |
| `IHttpContextAccessor` in a service here | Inject `IUser` |
| Adding an entity to `AppDbContext` only | Add the `DbSet` to `IAppDbContext` too |
| `dotnet ef migrations add X` from the repo root | `-p src/Taxi.Infrastructure -s src/Taxi.Api` |
| `EnsureCreated()` | `MigrateAsync()`: `EnsureCreated` skips the migration history |
| Registering an interceptor in the wrong position | Soft delete → audit → outbox; order is execution order |
| Editing an applied migration | Add a new migration |
| Returning `IdentityResult` to Application | Map it with `IdentityErrorMapper` into `Error`s |
| Reading `JwtSettings:*` from `IConfiguration` | Inject `IOptions<JwtSettings>` |

---

## 23. Future Extensions — NOT IMPLEMENTED

> ⚠️ **None of the following exists in this repository.** Sketches only.

### 23.1 Background jobs

A real one exists: `Outbox/OutboxBackgroundService.cs` (timer loop, scoped work through
`IServiceScopeFactory`, cross-instance exclusion through `IDistributedLock`). Copy its shape; the
sketch below is another example of the same pattern.

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
`AuditableEntityInterceptor` cannot attribute changes here (`IUser.Id` is null outside a request);
supply a system-user constant of your own if attribution matters.

### 23.2 Real-time push (SignalR)

The server side needs `Microsoft.AspNetCore.SignalR` on the API (clients use
`Microsoft.AspNetCore.SignalR.Client`). The hub is a dumb transport; the decision to notify belongs
to Application.

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

### 23.3 Dapper for heavy reads

Only if an EF Core projection is measurably too slow: measure first. Use **`NpgsqlConnection`**,
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

Caveats: raw SQL bypasses the audit interceptor, the soft-delete filter and domain events, so it is
read-only territory; JSONB columns need `->>'En'` extraction by hand; and you now have two schema
truths to keep in sync with migrations. Always parameterize; never interpolate into `sql`.
