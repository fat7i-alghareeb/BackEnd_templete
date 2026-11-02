# AGENTS.md — Taxi_Server

**Read this file first. It is the pattern.** Every rule is derived from code in this repository.
It is loaded at the start of every agent chat, so it stays short; the detail is in
[docs/AGENT_REFERENCE.md](docs/AGENT_REFERENCE.md) and the layer blueprints, read **on demand**.

- **`Car` is the reference feature.** Open the matching `Car` file and copy its shape. **`Driver`**
  adds a child collection, a relationship, a cross-aggregate rule and a domain event.
  `Features/Account/` and `Features/Identity/` are the reference for anonymous flows and email.
- If this document and the code disagree, **the code wins**; then fix the document.
- Trust the output of `dotnet build` and `dotnet test` over any number written in a document.
- The fastest start is `pwsh ./tool/generate-feature.ps1` (see §3), not typing ~25 files by hand.

---

## 0. What to read for which task

Read **only** the row that matches. Everything else stays closed.

| Your task | Read |
|---|---|
| A plain entity with CRUD endpoints | §3 below, then `pwsh ./tool/generate-feature.ps1` |
| Where a validation rule goes | [AGENT_REFERENCE §4](docs/AGENT_REFERENCE.md) |
| Returning an error, choosing a status | [AGENT_REFERENCE §5](docs/AGENT_REFERENCE.md) |
| A cached query or an eviction | [AGENT_REFERENCE §6](docs/AGENT_REFERENCE.md) |
| A bilingual (English/Arabic) field | [AGENT_REFERENCE §7](docs/AGENT_REFERENCE.md) |
| Which real file to copy | [AGENT_REFERENCE §8](docs/AGENT_REFERENCE.md) |
| Middleware, behaviour or interceptor order | [AGENT_REFERENCE §9](docs/AGENT_REFERENCE.md) |
| A child collection, a relationship between aggregates | [Domain §6](src/Taxi.Domain/Domain_Layer_Blueprint.md), [Infrastructure §6](src/Taxi.Infrastructure/Infrastructure_Layer_Blueprint.md) |
| A domain event | [Domain §9](src/Taxi.Domain/Domain_Layer_Blueprint.md), [Application §12](src/Taxi.Application/Application_Layer_Blueprint.md), [Infrastructure §8](src/Taxi.Infrastructure/Infrastructure_Layer_Blueprint.md) |
| A pipeline behaviour, a new Application interface | [Application §8](src/Taxi.Application/Application_Layer_Blueprint.md), [§15](src/Taxi.Application/Application_Layer_Blueprint.md) |
| Routes, verbs, versioning, OpenAPI | [docs/RESTful_Naming_Constitution.md](docs/RESTful_Naming_Constitution.md), [Api §6, §11](src/Taxi.Api/Api_Layer_Blueprint.md) |
| Auth, JWT, seeding, migrations | [Infrastructure §6, §9, §10](src/Taxi.Infrastructure/Infrastructure_Layer_Blueprint.md), [Api §7](src/Taxi.Api/Api_Layer_Blueprint.md) |
| Paging, filtering, sorting | [Contracts §7](src/Taxi.Contracts/Contracts_Layer_Blueprint.md) |
| File upload, idempotent POST, rate limiting | [Api §12, §13, §14](src/Taxi.Api/Api_Layer_Blueprint.md) |
| Email, account flows | [Infrastructure §13](src/Taxi.Infrastructure/Infrastructure_Layer_Blueprint.md), [Application §13](src/Taxi.Application/Application_Layer_Blueprint.md) |
| Tracing, metrics, logs, health | [Api §16](src/Taxi.Api/Api_Layer_Blueprint.md) |
| Behaviour you cannot explain | [AGENT_REFERENCE §11, §12](docs/AGENT_REFERENCE.md); do not assume a bug |
| Finding a file, a command, a past decision | [docs/PROJECT_MAP.md](docs/PROJECT_MAP.md), [docs/COMMANDS.md](docs/COMMANDS.md), [docs/DECISIONS.md](docs/DECISIONS.md) |
| Setup, configuration keys, deployment | [README.md](README.md) |

---

## 1. Layer map

| Project | References | Owns |
|---|---|---|
| `Taxi.Contracts` | nothing | request DTOs, `LocalizationKeys`, `Languages`, sort whitelists |
| `Taxi.Domain` | `Contracts` | entities, `<Entity>Errors`, `Result<T>`, `LocalizedText`, domain events |
| `Taxi.Application` | `Domain` | commands, queries, handlers, validators, DTOs, MediatR behaviours, interfaces |
| `Taxi.Infrastructure` | `Application` | EF Core, Identity, JWT, outbox, cache, files, email |
| `Taxi.Api` | `Application`, `Contracts`, `Infrastructure` | controllers, middleware, OpenAPI, composition root |

`Domain → Contracts` is intentional (error codes are `LocalizationKeys`): **do not "fix" it**
([Domain §2](src/Taxi.Domain/Domain_Layer_Blueprint.md)). **Never** add a reference that reverses
an arrow; `tests/Taxi.ArchitectureTests` fails the build when a type does.

Tests: `Domain.UnitTests` and `Application.UnitTests` need nothing; `ArchitectureTests` enforces the
arrows and the rules marked **(test)** below; `Api.IntegrationTests` runs the real API against
PostgreSQL and **needs Docker**.

---

## 2. Hard rules

Rules marked **(test)** fail the build when broken.

### Domain

- **MUST** make every entity constructor `private` **(test)**; create through
  `public static Result<T> Create(...)`, mutate through named methods returning `Result<Updated>`.
  Keep a private parameterless constructor for EF Core.
- **MUST** use `private set` or no setter **(test)**; only `AuditableEntity` / `ISoftDeletable`
  members are public, because interceptors stamp them.
- **MUST** let the caller supply the id (`Guid.NewGuid()` in the handler).
- **NEVER** throw for a business-rule failure; return an `Error` (`return CarErrors.NotFound;`).
- **MUST** put errors in a sibling `<Entity>Errors.cs` as `public static readonly Error` (a method
  when the message takes arguments), each code a `LocalizationKeys` constant.
- **MUST** use `LocalizedText` for translatable fields, never `NameEn` / `NameAr` on an entity.
- **MUST** make a domain event a `record` of serializable values, raised after the guards pass.
- **NEVER** reference EF Core, ASP.NET or `HttpContext` **(test)**.

### Contracts

- **MUST** be plain classes with `DataAnnotations` and **no methods** **(test)**; every
  `ErrorMessage` is a `LocalizationKeys` constant (also `[StringLength]`, `[Range]`).
- **NEVER** reference another project **(test)**; **NEVER** put an `Id` in an update request.

### Application

- **MUST** give each use case its own folder: `Features/<Plural>/Commands|Queries/<UseCase>/`, one
  type per file; name handlers `...Handler` and validators sealed `...Validator` **(test)**.
- **MUST** return `Result<T>` from every handler (`Result<Updated>`, `Result<Deleted>` or
  `Result<Success>` when there is nothing to return); anything that writes is a `Command`.
- **MUST** mark a request carrying a password, token or stream `ISensitiveRequest` **(test)**.
- **MUST** use the database only through `IAppDbContext`, `.AsNoTracking()` in queries, and map to
  a DTO before returning.
- **MUST** call `RemoveByTagAsync(CacheTags.X, ...)` in every command that changes cached data.
- **MUST** enforce a rule that spans aggregates in the handler, not in an entity.
- **NEVER** reference `Microsoft.AspNetCore`, Infrastructure or Api **(test)**.

### Infrastructure

- **MUST** implement Application interfaces and never define a business abstraction.
- **MUST** map entities in `Data/Configurations/<X>Configuration.cs`; a root `LocalizedText` is
  `OwnsOne(...).ToJson()`, inside an `OwnsMany` it is flat `...En` / `...Ar` columns.
- **MUST** list every get-only entity property in its configuration **(test)**.
- **MUST** add and read a migration for every schema change; never edit an applied one.
- **NEVER** reference `Taxi.Api` **(test)**.

### API

- **MUST** keep an action to: build the command, `await this.sender.Send(...)`,
  `return result.Match(success, this.Problem);`. No loops, business `if`, logging or data access.
- **MUST** bind a **Contracts** request and map it to the command by hand.
- **MUST** make controllers `sealed` and inherit `ApiController` **(test)**; never inject an
  Infrastructure type **(test)**; never `try/catch`.
- **MUST** protect endpoints with `[Authorize(Policy = Policies.X)]` (constants in
  `src/Taxi.Application/Common/Security/Policies.cs`, mapped in `AddAppAuthorization`), never roles.
  Anonymous needs an explicit `[AllowAnonymous]`.
- **MUST** put `[EndpointName]`, one `[ProducesResponseType]` per reachable status,
  `[MapToApiVersion]` and a forwarded `CancellationToken ct` on every action, and `[Idempotent]` on
  a `POST` that creates. `CreatedAtAction` passes `version = this.RequestedApiVersion` and the id.

### Everywhere

- **MUST** prefix instance members with `this.` (a convention; the StyleCop rule is off, so
  reviewers catch a miss) and assign primary-constructor parameters to `private readonly` fields.
- **MUST** take the current user from `IUser`, never from a payload or `HttpContext` outside the Api.
- **MUST** add NuGet packages to `Directory.Packages.props` with `Version`, and none in `.csproj`.
- **MUST** add every localization key to **both** `SharedResource.en.json` and `.ar.json`; a
  missing key silently degrades to English and nothing in the build catches it.
- **MUST** keep the build at zero warnings (CI uses `-warnaserror`).
- **NEVER** commit a real secret.

---

## 3. Add a feature

**Fast path:** `pwsh ./tool/generate-feature.ps1 <Name> -Fields "Title:text,Price:decimal,Seats:int"`
writes steps 1–13, 15, 15a, 16 and 17 below in the `Car` shape (field types: `string`, `text`
bilingual, `int`, `decimal`, `bool`), then you finish by hand: translate the `TODO-AR` values,
`dotnet ef migrations add`, real business rules, integration tests. Run `pwsh ./tool/catalog.ps1
<word>` first to see whether it already exists.

**By hand**, outside-in, copying the named file:

| # | Create / edit | Copy from |
|---|---|---|
| 1 | `src/Taxi.Contracts/Common/LocalizationKeys.cs`: nested class + keys | `Car` class |
| 2 | `src/Taxi.Api/Resources/SharedResource.en.json` **and** `.ar.json` | `Car.*` entries |
| 3 | `src/Taxi.Contracts/Requests/<Plural>/Create<X>Request.cs` (+ Update, Get) | `Requests/Cars/` |
| 4 | `src/Taxi.Domain/<Plural>/<X>.cs` | `src/Taxi.Domain/Cars/Car.cs`, `Drivers/Driver.cs` |
| 5 | `src/Taxi.Domain/<Plural>/<X>Errors.cs` | `src/Taxi.Domain/Cars/CarErrors.cs` |
| 6 | `src/Taxi.Application/Features/<Plural>/Dtos/<X>Dto.cs` | `Features/Cars/Dtos/CarDto.cs` |
| 7 | `.../Mappers/<X>Mapper.cs` | `Features/Cars/Mappers/CarMapper.cs` |
| 8 | `.../Commands/Create<X>/{Command,CommandHandler,CommandValidator}.cs` | `Features/Cars/Commands/CreateCar/` |
| 9 | `.../Queries/Get<X>ById/{Query,QueryHandler,QueryValidator}.cs` (+ `Get<Plural>/`) | `Features/Cars/Queries/GetCarById/`, `GetCars/` |
| 10 | `src/Taxi.Application/Common/Caching/CacheTags.cs`: a tag if cached | `Cars` constant |
| 11 | `src/Taxi.Application/Common/Interfaces/IAppDbContext.cs`: `DbSet<X>` | `Cars` property |
| 12 | `src/Taxi.Infrastructure/Data/AppDbContext.cs`: `DbSet<X>` | `Cars` property |
| 13 | `src/Taxi.Infrastructure/Data/Configurations/<X>Configuration.cs` | `CarConfiguration.cs` |
| 14 | `dotnet ef migrations add Add<X> -p src/Taxi.Infrastructure -s src/Taxi.Api` (read it) | n/a |
| 15 | `src/Taxi.Api/Controllers/<Plural>Controller.cs` | `Controllers/CarsController.cs` |
| 15a | New capability only: `Policies.cs` constant + `AddAppAuthorization` mapping | `CanReadCars` |
| 16 | `tests/Taxi.Domain.UnitTests/<Plural>/<X>Tests.cs`; handler and integration tests | `CarTests.cs`, `CarsTests.cs` |
| 17 | `requests/requests.http`: calls for the new endpoints | car calls |

Naming is mechanical: `<UseCase>Command|Query`, `...Handler`, `...Validator`. A validator is optional
when a route constraint already guarantees the input (`RemoveCar` has none).

Easy to forget: both `SharedResource` files · `RemoveByTagAsync` in **every** mutating command ·
the `DbSet` in **both** `IAppDbContext` and `AppDbContext` (and `TestAppDbContext` in the Application
unit tests) · mapping a get-only property in EF · `ISensitiveRequest` · `[AllowAnonymous]` ·
reading the migration for a `DropColumn` · `[Idempotent]` on a creating `POST`.

---

## 10. Verify your change

```powershell
dotnet build Taxi_Server.slnx -c Debug       # 0 warnings; StyleCop runs here
dotnet test tests/Taxi.Domain.UnitTests      # the smallest check that proves the change first
dotnet test Taxi_Server.slnx                 # the whole suite: end of a feature; integration needs Docker
npx markdownlint-cli2                        # if you touched any .md
docker compose up -d postgres                # database only, for dotnet run
dotnet run --project src/Taxi.Api            # http://localhost:5000, Swagger UI at /swagger
```

Development sign-in: `admin@taxi.com` / `Admin123!` (role `Manager`). Full command list and the
manual smoke test: [docs/COMMANDS.md](docs/COMMANDS.md).

---

## 13. Working agreement

1. **Read before you write**: the matching reference file (§3) and only the §0 row you need.
2. **Smallest change that satisfies the request**; do not refactor neighbours or "fix" anything
   listed in [AGENT_REFERENCE §11](docs/AGENT_REFERENCE.md).
3. **Stop and ask** when the request conflicts with the pattern; do not invent a new one.
4. **Verify, then report** with real command output; say so if you skipped something.
5. **Docs are part of the change**: a new convention, folder, signature or key updates this file and
   the affected blueprint ([prompts/update-docs.md](prompts/update-docs.md)).
6. **Never** commit or push unless asked, edit an applied migration, put a secret in a committed
   file, run `docker compose down -v` against someone's data, or set `Database:ResetOnStartup` outside
   a disposable development database. Do not touch generated files (`*.Designer.cs`, the model
   snapshot, `bin/`, `obj/`).
