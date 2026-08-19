# TAXI_Server

Enterprise Taxi Management System built with .NET 10, Clean Architecture and CQRS.

## Quick start

### Prerequisites

- .NET 10 SDK
- Docker Desktop (PostgreSQL)

### 1. Provision infrastructure

```bash
docker compose up -d
```

Postgres is exposed on **5433**, matching `appsettings.Development.json`.

### 2. Configure secrets

For local development nothing is needed — `appsettings.Development.json` already carries a working
connection string and JWT secret.

> **The dev JWT secret is a public placeholder committed to this repository.** It exists so the
> template runs on a fresh clone, and it signs tokens anyone reading this repo can forge. Override
> `JwtSettings:Secret` via user secrets or environment variables before any run that is not on
> your own machine.

Everywhere else, `ConnectionStrings:DefaultConnection` and `JwtSettings:Secret` are intentionally
blank in `appsettings.json`. Supply them via user secrets, environment variables, or a `.env` file
— see [.env.example](.env.example).

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5433;Database=TaxiDb;Username=postgres;Password=..." --project src/Taxi.Api
dotnet user-secrets set "JwtSettings:Secret" "<at least 32 characters>" --project src/Taxi.Api
```

### 3. Run the API

```bash
dotnet run --project src/Taxi.Api
```

Migrations are applied and seed data inserted on startup in any non-Production environment
(controlled by `Database:ApplyMigrationsOnStartup`, with retry).

In Development:

- OpenAPI document — `/openapi/v1.json`
- Swagger UI — `/swagger`
- Scalar — `/scalar/v1`

Sample requests live in [requests/requests.http](requests/requests.http).

## Documentation

**Lost? [docs/NAVIGATION.md](docs/NAVIGATION.md) says which file to open.**

| Document | Purpose |
|---|---|
| [AGENTS.md](AGENTS.md) | **Start here to add a feature.** Hard rules, the 17-step checklist, and a routing table to everything else. |
| [docs/NAVIGATION.md](docs/NAVIGATION.md) | The map — every file, what it is, when it changes. |
| [prompts/](prompts/README.md) | Templates for briefing an agent — one per task type. |
| [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) | System overview, layer responsibilities, request lifecycle. |
| [docs/RESTful_Naming_Constitution.md](docs/RESTful_Naming_Constitution.md) | Routes, verbs, status codes, required attributes. |
| `src/Taxi.<Layer>/<Layer>_Layer_Blueprint.md` | Deep reference per layer, beside the code it describes. |

## Projects

| Project | Role |
|---|---|
| `Taxi.Domain` | Entities, invariants, `<Entity>Errors`, `Result<T>` / `Error` |
| `Taxi.Contracts` | Request DTOs, `LocalizationKeys`, `Languages` |
| `Taxi.Application` | Commands, queries, handlers, validators, DTOs, MediatR behaviours |
| `Taxi.Infrastructure` | EF Core + PostgreSQL, Identity, JWT, HybridCache registration |
| `Taxi.Api` | Controllers, middleware, OpenAPI, composition root |
| `Taxi.Client` | Blazor WebAssembly shell — scaffold only, not hosted |

## Tech stack

**In use:** .NET 10 · MediatR 14 · FluentValidation 12 · EF Core 10 + Npgsql ·
ASP.NET Identity + JWT bearer · `Microsoft.Extensions.Caching.Hybrid` ·
`My.Extensions.Localization.Json` · Asp.Versioning · OpenAPI with Swagger UI and Scalar ·
Serilog (console + rolling file) · StyleCop.Analyzers.

**Observability:** traces and metrics via OpenTelemetry, logs via Serilog. `docker compose up -d`
brings up Seq (logs + traces, `:8081`), Prometheus (`:9090`, scraping `/metrics`) and Grafana
(`:3000`).

## Identity and security

- **Authentication:** JWT bearer (HMAC-SHA256) with rotating refresh tokens — one live refresh
  token per user, 7-day expiry.
- **Authorization:** `[Authorize]` at controller level; roles are carried as JWT claims.
- **Seeding:** on first run, the `Manager` role and `admin@taxi.com` / `Admin123!` are created.
  Change the password before any deployment; password rules are relaxed by default
  (6 characters, no complexity requirements).

## Localization

English and Arabic. Translatable entity fields use the `LocalizedText` value type, persisted as a
single JSONB column. Error codes and validation messages are `LocalizationKeys` constants resolved
at the API boundary against `src/Taxi.Api/Resources/SharedResource.{en,ar}.json`. Clients select a
language with the `Accept-Language` header.

## Building and testing

```bash
dotnet build Taxi_Server.slnx -c Debug
dotnet test Taxi_Server.slnx
dotnet list package --vulnerable --include-transitive
```

StyleCop runs as part of the build. Tests live in `tests/`:

| Project | Covers |
|---|---|
| `Taxi.Domain.UnitTests` | `Car` and `RefreshToken` factory guards, `Result<T>` conversions |
| `Taxi.Application.UnitTests` | MediatR pipeline registration order, `Result<T>` cache serialization |
