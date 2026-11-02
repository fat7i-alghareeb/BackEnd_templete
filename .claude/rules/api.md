---
paths:
  - "src/Taxi.Api/**"
  - "tests/Taxi.Api.IntegrationTests/**"
  - "requests/**"
---

# API rules (loaded when you touch `src/Taxi.Api/`)

Copy `Controllers/CarsController.cs` (versioned reads/writes, uploads) and `DriversController.cs`
(sub-resources). Routes, verbs and statuses: `docs/RESTful_Naming_Constitution.md`.

- An action only builds the command/query, `await this.sender.Send(..., ct)`, and
  `return result.Match(success, this.Problem);`. No loops, business `if`, logging, data access or
  `try/catch`. Controllers are `sealed`, inherit `ApiController`, and never use Infrastructure types.
- Route is literal, lowercase, plural: `[Route("api/v{version:apiVersion}/cars")]`, never `[controller]`.
  `[ApiVersion]` on the class; `[MapToApiVersion]` on every action.
- Every action: `[EndpointName("GetCar")]` (= method name), one `[ProducesResponseType]` per reachable
  status (`ProblemDetails` for failures), route constraints (`{id:guid}`), `CancellationToken ct`
  forwarded.
- Bind a Contracts request and map to the command by hand. Update takes the id from the route only.
- `POST` that creates: `[Idempotent]` and
  `CreatedAtAction(nameof(this.GetX), new { version = this.RequestedApiVersion, id = x.Id }, x)`;
  never a literal version, never the DTO as the route value.
- Auth: class-level `[Authorize(Policy = Policies.CanReadX)]`, writes add `CanManageX`; anonymous needs
  `[AllowAnonymous]`; never `[Authorize(Roles = ...)]`. A new capability = a `Policies.cs`
  constant + its mapping in `AddAppAuthorization` (`DependencyInjection.cs`).
- Middleware order lives in `UseCoreMiddlewares`; never register middleware in `Program.cs`. The
  rate limiter sits after authentication on purpose.
- Errors become HTTP only in `Extensions/ProblemExtensions.cs`. Unauthenticated unknown routes answer
  401 (fallback policy) — that is deliberate.
- New endpoint → add calls to `requests/requests.http` and an integration test (copy `CarsTests`).
- Integration tests need Docker (Testcontainers `postgres:17-alpine`).

Details only if needed: `src/Taxi.Api/Api_Layer_Blueprint.md` §5–§9, §11–§14.
