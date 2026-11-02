# Troubleshooting

Symptoms seen when running, testing or deploying the template, with the cause and the fix. Every
entry comes from behaviour in the code; if you hit something new, add it here.

If a behaviour looks like a bug but is documented, check
[AGENT_REFERENCE.md §11 and §12](AGENT_REFERENCE.md) first.

---

## Startup

| Symptom | Cause | Fix |
|---|---|---|
| `InvalidOperationException: ConnectionStrings:DefaultConnection is not configured` | The key is blank in `appsettings.json` on purpose | Run in Development (`appsettings.Development.json` has a value), or set it with user secrets, `ConnectionStrings__DefaultConnection`, or `.env` |
| `OptionsValidationException` mentioning `JwtSettings` | `Secret` is blank or shorter than 32 characters; `Issuer`/`Audience` blank; a lifetime out of range | Set `JwtSettings:Secret` to 32+ characters. Same family of failure for `Email`, `Outbox`, `FileStorage`, `ClientApp`, `RateLimiting`: they are validated at boot |
| `Email:Smtp:Host is required when Email:Provider is Smtp` | Provider switched to `Smtp` without a host | Set `Email__Smtp__Host`, or use `Email__Provider=Log` |
| Log: `Database migration attempt 1 failed. Retrying in 3s` (up to 5) | PostgreSQL is not accepting connections yet, or the connection string is wrong | Start the database (`docker compose up -d postgres`) and check host, port and password. After 5 attempts the host exits |
| Log: `Database migrations are disabled at API startup` | `Database:ApplyMigrationsOnStartup` is `false` | Apply migrations yourself (`dotnet ef database update -p src/Taxi.Infrastructure -s src/Taxi.Api`) or set it to `true` |
| `Database:ResetOnStartup=true ignored` | The reset only runs in Development | Run with `ASPNETCORE_ENVIRONMENT=Development`, or drop the database by hand |
| Log: `Failed to determine the https port for redirect` | `UseHttpsRedirection` has no HTTPS endpoint (plain `http://localhost:5000`, or the container on 8080) | Harmless. Use the `https` launch profile for local HTTPS, or terminate TLS at your proxy |
| `dotnet ef` says the command is not found | The EF tool is not installed | `dotnet tool install --global dotnet-ef` |

## Docker and ports

| Symptom | Cause | Fix |
|---|---|---|
| `port is already allocated` on 5433, 6379 or 5001 | Another container or database already uses the host port | Stop the other one, or change the host side of the mapping in `docker-compose.override.yml` and the matching connection string |
| `An attempt was made to access a socket in a way forbidden by its access permissions` (Windows) | The port is inside a range Windows reserves for Hyper-V/WSL | Pick another port; list reserved ranges with `netsh interface ipv4 show excludedportrange protocol=tcp` |
| `docker compose up -d` also started an API container, and `dotnet run` now fails or double-processes | Both ran against one database | Pick one way to run it (README Quick start): either everything in Docker, or `docker compose up -d postgres` + `dotnet run` |
| Prometheus and Grafana are not up yet | They wait for the API container to report healthy (up to about a minute on first start) | Wait, then `docker compose ps` |
| Grafana is empty | No data source or dashboard is provisioned | Log in (`admin` / `YourStrongPwd` in development), add a Prometheus data source at `http://prometheus:9090` |
| No logs in Seq | The Seq sink is configured only in `appsettings.Development.json` | Run in Development, or add a Seq sink to your production configuration |
| `taxi.api` keeps restarting | It waits for healthy PostgreSQL and Redis, and has `restart: on-failure` | `docker compose logs taxi.api`; most often a database or secret problem |
| Uploaded car images disappear after recreating the container | The development compose has no volume for `/app/storage` | Use the production compose (it mounts `api-storage`) or add a volume |
| Production compose: `variable is not set` warnings | `.env` is missing or incomplete | `cp .env.example .env` and fill every value |

## Authentication and authorization

| Symptom | Cause | Fix |
|---|---|---|
| 401 on every request | No `Authorization: Bearer <accessToken>` header, or the token expired (60 minutes by default) | Sign in again or call `/api/token/refresh-token` |
| 401 on a URL that does not exist | The fallback policy requires authentication everywhere, so even an unmatched route answers 401 when you are signed out | Sign in; an authenticated caller gets the 404 |
| 401 from `/api/token/generate` with the right password | Unknown email, wrong password and a locked-out account answer identically; five failures lock an account for 15 minutes | Wait 15 minutes, or reset the password through `/api/account/forgot-password` (a reset clears the lockout) |
| 409 from `/api/token/generate` | The password was right but the email is not confirmed | Confirm it (link in Mailpit or the console log), or call `/api/account/resend-confirmation`. Seeded accounts are already confirmed |
| 409 from `/api/token/refresh-token` | Invalid or tampered access token, wrong user, or the refresh token expired or was already replaced (only one is live per user) | Sign in again. Logout, password change and password reset also revoke refresh tokens |
| 403 on `POST`/`PUT`/`DELETE` | The caller has the `User` role, which is read-only | Use a `Manager` account (`admin@taxi.com` in development) or change roles via `PUT /api/v1/users/{userId}/roles`; the new role applies from the next token |
| A role change does not take effect | Roles are baked into the access token | Sign in again or refresh |
| 409 `You cannot remove the Manager role from your own account` | Self-demotion guard | Have another Manager do it |
| Email confirmation or reset link says the token is invalid after a restart | Data Protection keys are not persisted, so tokens issued before the restart (or by another instance) cannot be read | Request a new link. Persist and share the key ring before running several instances |
| 429 with `Retry-After` | Rate limit: 100 requests/minute per user or IP by default, 10/minute per IP on credential and account endpoints | Slow down, or raise `RateLimiting:*`. Behind a proxy, set `ForwardedHeaders:KnownProxies` / `KnownIPNetworks` or every caller shares one bucket |

## Requests and responses

| Symptom | Cause | Fix |
|---|---|---|
| English text in an Arabic response | The key is missing from `SharedResource.ar.json` (Development logs `Missing localization key`), or the message is not localized by design | Add the key to both files. Not localized by design: the 401/403 titles, the `title` of a validation problem, and the `title` of a 500 |
| 400 whose field name is empty | The error carries no `PropertyName` (a domain or conflict error) | Read `title`; field-keyed errors come from validators and DataAnnotations |
| 413 | Body over `Kestrel:Limits:MaxRequestBodySize` (1 MB), or an image over 5 MB | Send less, or raise the limit deliberately |
| 400 on an image upload that "is" a PNG | The format is detected from the leading bytes, not the extension or `Content-Type`; only JPEG, PNG and WebP are accepted | Re-export the file |
| 422 on `POST` | The same `Idempotency-Key` was sent with a different body | Use a new key per distinct request |
| 409 on `POST` with an `Idempotency-Key` | The first request with that key is still running | Retry shortly |
| 409 on `PUT /api/v1/cars/{id}` | Someone changed the row between your read and your write (`xmin` concurrency) | Reload and retry |
| 409 on `DELETE /api/v1/cars/{id}` | A driver is assigned to the car | `DELETE /api/v1/drivers/{id}/car` first |
| 404 on `/api/v3/...` | Only versions 1 and 2 exist (drivers and users are v1 only) | Use a supported version |
| Response has `Sunset` and `api-deprecated-versions` headers | You called v1 of the cars API, which is deprecated | Move to v2 |
| A change is not visible in a list | The cached read is served until a write evicts its tag; with Redis another instance can lag up to 30 seconds | Check the command calls `RemoveByTagAsync`; see AGENT_REFERENCE.md §6 |
| `/swagger`, `/scalar` or `/openapi` return 404 | They are mapped only in Development | Run with `ASPNETCORE_ENVIRONMENT=Development` |

## Email

| Symptom | Cause | Fix |
|---|---|---|
| No email arrives | `Email:Provider` is `Log` (the default) | Read the API console: the message and its link are logged. In the development compose emails go to Mailpit at <http://localhost:8025> |
| The emailed link opens `localhost:5002` | `ClientApp:ConfirmEmailUrl` / `ResetPasswordUrl` default to a local front end | Set them for your front end (production compose does not) |
| Register says 202 but nothing is created | The email already exists; its owner receives an "already registered" mail instead | Expected; this prevents account enumeration |

## Tests

| Symptom | Cause | Fix |
|---|---|---|
| Integration tests fail to start | Docker is not running (Testcontainers needs it, image `postgres:17-alpine`) | Start Docker Desktop. The other three suites need no Docker |
| `ModelMappingTests` fails | A public entity property is not mapped by EF Core; get-only properties are not mapped by convention | List it in the entity's configuration |
| `SensitiveRequestTests` fails | A request with a password, token or stream is not marked `ISensitiveRequest` | Implement the marker |
| `PipelineRegistrationTests` fails | Behaviour registration order changed | Restore the order in `AddApplication()` (AGENT_REFERENCE.md §9) |
| An architecture test fails | A type crossed a layer rule or broke a convention; the message lists the offenders | Fix the type; do not edit the test |
| Build fails only in CI | CI compiles with `-warnaserror`, and also fails on vulnerable packages | Fix the warning or upgrade the package; reproduce with `dotnet build -c Release -warnaserror` |
| Markdown lint fails | Prose wraps at 100 columns; tables, code blocks and headings are exempt | `npx markdownlint-cli2` shows the line |

## Renaming the template

| Symptom | Cause | Fix |
|---|---|---|
| `The git working tree has uncommitted changes` | `rename.ps1` refuses a dirty tree | Commit or stash, or pass `-Force` |
| Build errors about `using` order after renaming | The new name sorts differently from `Taxi` (StyleCop SA1210) | Run without `-SkipFormat`, or `dotnet format analyzers --diagnostics SA1210` |
| The old database is ignored | The default database name changes to `<Name>Db`, and outbox rows store CLR type names | Start from an empty database (`docker compose down -v` deletes the volumes, so only on disposable data) |
