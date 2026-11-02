---
paths:
  - "src/Taxi.Infrastructure/**"
---

# Infrastructure rules (loaded when you touch `src/Taxi.Infrastructure/`)

Copy `Data/Configurations/CarConfiguration.cs` and `DriverConfiguration.cs`.

- Implement interfaces that Application declares; never define a business abstraction here. Register
  in `AddInfrastructure`. Anything needing `HttpContext` belongs in `Taxi.Api/Services/` instead.
- One `IEntityTypeConfiguration<T>` per entity in `Data/Configurations/`; it is found automatically.
  All mapping lives here — no EF attributes in the Domain.
- Root `LocalizedText`: `builder.OwnsOne(x => x.F, b => { b.ToJson(); ... })`. Inside `OwnsMany`
  EF cannot nest JSON: map flat `...En` / `...Ar` columns.
- **Every get-only entity property must be listed explicitly** (not mapped by convention);
  `ModelMappingTests` fails otherwise.
- Relationships to other aggregates are by id: `HasOne<Other>().WithMany().HasForeignKey(...)`
  with `Restrict`; one-to-one-in-practice rules are filtered unique indexes.
- `builder.UseXminConcurrencyToken()` on entities updated by read-modify-write; soft-deletable
  entities implement `ISoftDeletable` and set `IsDeleted` default `false`.
- Add the `DbSet` to **both** `AppDbContext` and `IAppDbContext`.
- Migrations: `dotnet ef migrations add <Name> -p src/Taxi.Infrastructure -s src/Taxi.Api`; read
  the generated file for an unintended `DropColumn`; never edit an applied migration, its
  `.Designer.cs` or the model snapshot.
- Interceptor order is execution order: soft delete → audit → outbox. Domain events are written to
  the outbox in the same transaction and published later, at least once.
- Settings classes are validated on start (`ValidateDataAnnotations().ValidateOnStart()`); secrets
  stay blank in `appsettings.json`.
- Return `Result<T>` and Application DTOs outward — never `IdentityResult` or an EF type.

Details only if needed: `src/Taxi.Infrastructure/Infrastructure_Layer_Blueprint.md` §6–§10.
