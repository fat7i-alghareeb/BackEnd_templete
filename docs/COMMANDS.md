# Commands

Every command and script worth knowing, in one place. Run from the repository root.

## Build and test

| Command | Use |
|---|---|
| `dotnet build Taxi_Server.slnx -c Debug` | Build everything; must report 0 warnings (StyleCop runs here) |
| `dotnet build Taxi_Server.slnx -c Release -warnaserror` | What CI runs |
| `dotnet test tests/Taxi.Domain.UnitTests` | Entity guards and `Result<T>`; fast, no Docker |
| `dotnet test tests/Taxi.Application.UnitTests` | Handlers, validators, mappers, behaviours; no Docker |
| `dotnet test tests/Taxi.ArchitectureTests` | Layer arrows, conventions, the Claude setup guard; no Docker |
| `dotnet test tests/Taxi.Api.IntegrationTests` | The real API on PostgreSQL (Testcontainers); needs Docker, ~25 s |
| `dotnet test Taxi_Server.slnx` | Everything; run at the end of a feature, not after every edit |
| `dotnet list package --vulnerable --include-transitive` | CI fails on any hit |
| `dotnet test Taxi_Server.slnx --settings coverlet.runsettings --collect:"XPlat Code Coverage" --results-directory ./coverage` | Coverage as CI collects it |
| `npx markdownlint-cli2` | Documentation lint (prose wraps at 100 columns) |

## Run

| Command | Use |
|---|---|
| `docker compose up -d --build` | Everything in Docker; API at <http://localhost:5001> |
| `docker compose up -d postgres` | Database only, for `dotnet run` |
| `dotnet run --project src/Taxi.Api` | API at <http://localhost:5000>, Swagger UI at `/swagger` (Development) |
| `docker compose -f docker-compose.yml -f docker-compose.prod.yml up -d --build` | Production topology; needs `.env` |
| `docker compose config -q` | Validate the compose files |

## Database

| Command | Use |
|---|---|
| `dotnet tool install --global dotnet-ef` | Once |
| `dotnet ef migrations add <Name> -p src/Taxi.Infrastructure -s src/Taxi.Api` | New migration; read it before committing |
| `dotnet ef migrations remove -p src/Taxi.Infrastructure -s src/Taxi.Api` | Undo the last unapplied migration |
| `dotnet ef database update -p src/Taxi.Infrastructure -s src/Taxi.Api` | Apply to the configured database (startup also does this outside Production) |

## Scripts (`tool/`, PowerShell 7)

| Command | Use |
|---|---|
| `pwsh ./tool/generate-feature.ps1 <Name> -Fields "A:text,B:decimal"` | Generate a whole feature in the `Car` shape (`-Plural`, `-HardDelete`, `-Migrate`, `-SkipFormat`, `-DryRun`) |
| `pwsh ./tool/catalog.ps1 <word>` | List existing types and localization keys containing the word |
| `pwsh ./rename.ps1 -Name Acme -WhatIf` | Preview renaming the template; drop `-WhatIf` to do it |
| `tool/claude-dotnet-filter.ps1` | Claude Code hook; not run by hand |

## Claude Code commands

| Command | Use |
|---|---|
| `/first-run` | Once per project: baseline, questions, record |
| `/build-project <description>` | Interview, spec, approval, build feature by feature |
| `/new-feature <name - purpose>` | One feature |
| `/finish` | Wrap up a feature: docs, leftovers, checks |
| `/add-key <key> <English> <Arabic>` | One localization key in all three places |
| `/release compose` | Deployment verification checklist |

Guide: [USING_CLAUDE.md](USING_CLAUDE.md).

## Smoke test of a running API

Use `requests/requests.http` or:

1. `POST /api/token/generate` with `admin@taxi.com` / `Admin123!` (the Development `Seed:Admin`
   account): 200 with `accessToken`, `refreshToken`, `expiresOnUtc`.
2. `POST /api/v1/cars` with the bearer token: 201 and `Location: /api/v1/cars/{guid}`.
3. `GET /api/v1/cars/{id}` twice: the first logs `Caching result for GetCarByIdQuery`; the second logs
   only `Checking cache for GetCarByIdQuery` and runs no EF command.
4. `GET /api/v1/cars` with `Accept-Language: ar`, then `en`: different `description`.
5. `PUT /api/v1/cars/{id}` then `GET /api/v1/cars`: the new value is visible (tag invalidation).
6. An invalid payload: 400 `ValidationProblemDetails` keyed by property, in the requested language.
7. `GET /health/ready`: 200 `Healthy`.

OpenAPI in Development: `/openapi/v2.json`, `/openapi/v1.json`, Swagger UI at `/swagger`, Scalar at
`/scalar/v2` and `/scalar/v1`.
