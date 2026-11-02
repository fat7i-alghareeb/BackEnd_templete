# Project map

Where things are. Read this instead of searching the tree.

## Repository layout

| Path | What |
|---|---|
| `src/` | Five projects: `Taxi.Contracts`, `Taxi.Domain`, `Taxi.Application`, `Taxi.Infrastructure`, `Taxi.Api`; each holds its `<Layer>_Layer_Blueprint.md` |
| `tests/` | `Taxi.Domain.UnitTests`, `Taxi.Application.UnitTests`, `Taxi.ArchitectureTests`, `Taxi.Api.IntegrationTests` |
| `docs/` | Architecture, REST rules, agent reference, this map, decisions, style, commands, troubleshooting, Claude guide, `assets/` |
| `prompts/` | Templates for briefing an agent |
| `requests/` | `requests.http` (the API samples) and `car.png` (upload sample) |
| `tool/` | `generate-feature.ps1`, `catalog.ps1`, `claude-dotnet-filter.ps1` |
| `containers/` | Prometheus scrape config; Seq data directory |
| `.claude/` | `settings.json`, `rules/` (per layer), `skills/` (slash commands) |
| `.github/` | CI workflow, Dependabot, CODEOWNERS, PR template, Copilot pointer |
| root | `AGENTS.md`, `CLAUDE.md`, `README.md`, `Taxi_Server.slnx`, compose files, `rename.ps1`, `.env.example`, `Directory.*.props` |

## Features

| Feature | Domain | Application | Controller | Cache tag |
|---|---|---|---|---|
| Cars (v1 deprecated, v2) | `src/Taxi.Domain/Cars/` | `src/Taxi.Application/Features/Cars/` | `CarsController` | `Cars` |
| Drivers | `src/Taxi.Domain/Drivers/` | `src/Taxi.Application/Features/Drivers/` | `DriversController` | `Drivers` |
| Account (self-service) | none | `src/Taxi.Application/Features/Account/` | `AccountController` | none |
| Identity (tokens) | `src/Taxi.Domain/Identity/` | `src/Taxi.Application/Features/Identity/` | `IdentityController` | none |
| Users (roles) | none | `src/Taxi.Application/Features/Users/` | `UsersController` | none |

`/build-project` and `/finish` add a row here for every feature they build.

## Where is…

| Looking for | Open |
|---|---|
| Middleware order, DI registrations | `src/Taxi.Api/DependencyInjection.cs` (`AddPresentation`, `UseCoreMiddlewares`) |
| MediatR behaviour order | `src/Taxi.Application/DependencyInjection.cs` |
| Infrastructure registrations | `src/Taxi.Infrastructure/DependencyInjection.cs` |
| Policy → role mapping | `AddAppAuthorization` in `src/Taxi.Api/DependencyInjection.cs` |
| Policy and role names | `src/Taxi.Application/Common/Security/` |
| Error → HTTP | `src/Taxi.Api/Extensions/ProblemExtensions.cs` |
| Localization keys and translations | `src/Taxi.Contracts/Common/LocalizationKeys.cs`, `src/Taxi.Api/Resources/SharedResource.{en,ar}.json` |
| Cache tags | `src/Taxi.Application/Common/Caching/CacheTags.cs` |
| Use-case failures (not entity errors) | `src/Taxi.Application/Common/Errors/ApplicationErrors.cs` |
| EF mappings, migrations, seeding | `src/Taxi.Infrastructure/Data/` |
| Outbox | `src/Taxi.Infrastructure/Outbox/` |
| Configuration keys | `src/Taxi.Api/appsettings*.json`, [README](../README.md) |
| Sample requests | `requests/requests.http` |
| Test doubles for handlers | `tests/Taxi.Application.UnitTests/TestDoubles/` |
