# RESTful Naming Constitution

Route, verb and status-code rules for Taxi_Server. Every rule below is demonstrated by
`src/Taxi.Api/Controllers/CarsController.cs` (and, for sub-resources, `DriversController.cs`).
When in doubt, open those files. The full endpoint list is in the
[Api blueprint §6](../src/Taxi.Api/Api_Layer_Blueprint.md).

---

## 1. The five rules

1. **Nouns, not verbs.** The URL names a thing; the HTTP method names the action.
   `POST /api/v1/cars`, never `POST /api/v1/createCar`.
2. **Always plural.** `/cars` for the collection *and* `/cars/{id}` for one member. Never `/car/5`.
3. **Shallow nesting.** At most one level: `/drivers/{id}/certifications`. Beyond that, expose the
   sub-resource at its own root and link by id.
4. **Lowercase kebab-case.** `/maintenance-records`, never `/MaintenanceRecords` or
   `/maintenance_records`.
5. **Version every business resource.** `api/v{version:apiVersion}/...`.

---

## 2. Route templates

```csharp
[Route("api/v{version:apiVersion}/cars")]   // ✅ literal, lowercase, plural
[ApiVersion("1.0", Deprecated = true)]
[ApiVersion("2.0")]
```

**Write the segment literally.** Do not use `[controller]`: the token derives the segment from the
class name, so renaming a controller silently changes the public URL, and it gives no control over
casing or hyphenation.

| Resource | Route | Versions |
|---|---|---|
| Cars | `api/v{version:apiVersion}/cars` | v1 (deprecated), v2 |
| Drivers | `api/v{version:apiVersion}/drivers` | v1 |
| Users (administration) | `api/v{version:apiVersion}/users` | v1 |
| Auth (version-neutral) | `api/token` | none |
| Account self-service (version-neutral) | `api/account` | none |

`IdentityController` (`api/token`) and `AccountController` (`api/account`) are
`[ApiVersionNeutral]`: authentication endpoints are deliberately outside the versioning scheme,
because a client that cannot obtain a token cannot negotiate a version.

`AccountController`'s actions (`register`, `confirm-email`, `resend-confirmation`,
`forgot-password`, `reset-password`, `change-password`, `logout`) and `IdentityController`'s
(`generate`, `refresh-token`, `user-info`) are the sanctioned exception to rule 1: they are
operations on the caller's own credentials, not resources, so they are `POST` (or `GET` for
`user-info`) + a kebab-case verb. Endpoints that take an email answer **202** whether or not the
account exists, so they cannot be used to enumerate users.

Version resolution is `UrlSegmentApiVersionReader`, default `1.0`,
`AssumeDefaultVersionWhenUnspecified = true`, `ReportApiVersions = true`. A route with an unknown
version (`/api/v3/cars`) is a 404 for an authenticated caller.

---

## 3. Verbs, actions and status codes

The matrix `CarsController` implements:

| Verb | Route | Action | Success | Failure |
|---|---|---|---|---|
| `GET` | `/cars?page=&pageSize=&search=&make=&year=&sortBy=&sortDirection=` | `GetCars` (v1), `GetCarsV2` (v2) | `200` + `PaginatedList<CarDto>` / `PaginatedList<CarDetailsDto>` | `400` |
| `GET` | `/cars/{id:guid}` | `GetCar` (v1), `GetCarV2` (v2) | `200` + `CarDto` / `CarDetailsDto` | `404` |
| `POST` | `/cars` | `CreateCar` | `201` + `CarDto` + `Location` | `400`, `403`, `409`, `422` |
| `PUT` | `/cars/{id:guid}` | `UpdateCar` | `204` | `400`, `403`, `404`, `409` |
| `DELETE` | `/cars/{id:guid}` | `RemoveCar` | `204` | `403`, `404`, `409` |
| `PUT` | `/cars/{id:guid}/image` | `UploadCarImage` | `204` | `400`, `403`, `404`, `413` |
| `GET` | `/cars/{id:guid}/image` | `GetCarImage` | `200` + image bytes | `404` |
| `DELETE` | `/cars/{id:guid}/image` | `RemoveCarImage` | `204` | `403`, `404` |

`DriversController` adds the two sub-resource shapes, each one level deep (rule 3):

| Verb | Route | Meaning | Success | Failure |
|---|---|---|---|---|
| `POST` | `/drivers/{id:guid}/certifications` | add a member of a child collection | `201` | `400`, `404`, `409` |
| `DELETE` | `/drivers/{id:guid}/certifications/{certificationId:guid}` | remove it | `204` | `404` |
| `PUT` | `/drivers/{id:guid}/car` | set a to-one relationship (body: `carId`) | `204` | `400`, `404`, `409` |
| `DELETE` | `/drivers/{id:guid}/car` | clear it | `204` | `404`, `409` |

A **singleton sub-resource** (`/cars/{id}/image`, `/drivers/{id}/car`) is set with `PUT`, read with
`GET` and cleared with `DELETE`; it never has its own id.

Other controllers: `PUT /users/{userId}/roles` (replace a set, `204`); `POST /token/generate` and
`POST /token/refresh-token` (`200` + `TokenResponse`); `GET /token/user-info`; and the seven
`POST /account/*` actions (`202` or `204`).

### Status codes the API can return

| Status | When |
|---|---|
| `200` | Reads; token issuance |
| `201` | A creating `POST`, with a `Location` header |
| `202` | Account actions that take an email and must not reveal whether the account exists |
| `204` | `PUT`, `DELETE`, and account actions with nothing to return |
| `400` | Validation (any tier), including a weak password |
| `401` | No or invalid token; wrong credentials (uniform) |
| `403` | Authenticated but the policy denies it |
| `404` | Missing resource; unknown version; an unmatched route for an authenticated caller |
| `409` | `ErrorKind.Conflict` (assigned car, taken licence, email not confirmed, refresh failures, cannot remove own Manager role), a concurrency conflict, or an idempotent request still running |
| `413` | Request body over the limit |
| `422` | `Idempotency-Key` reused with a different body |
| `429` | Rate limit exceeded (`Retry-After`) |
| `500` | Unhandled exception (`GlobalExceptionHandler`) |
| `503` | `/health/*` when a check fails |

Rules the matrix encodes:

- **`POST` returns 201 with a `Location` header**, produced by:

  ```csharp
  this.CreatedAtAction(nameof(this.GetCar), new { version = this.RequestedApiVersion, id = car.Id }, car)
  ```

  The `version` route value is required: the route template contains a `version` segment, and
  omitting it makes URL generation fail. Use `RequestedApiVersion` (on `ApiController`), never a
  literal: it is the version the client called, so a v2 `POST` answers `Location: /api/v2/cars/{id}`.
  Pass the **id**, not the DTO, as the route value. When the created thing has no `GET` of its
  own (a certification), point at its parent.
- **`PUT` and `DELETE` return 204**, no body. The command returns `Result<Updated>` /
  `Result<Deleted>` and the controller matches to `NoContent()`.
- **`PUT` carries the id in the route only.** The request body (`UpdateCarRequest`) has no `Id`,
  and the action passes the route id into the command, so there is no route/body mismatch to
  check and no logic in the controller.
- **404 comes from the handler**, as `CarErrors.NotFound`, not from a controller null check.
- **A `POST` that creates is `[Idempotent]`**: with an `Idempotency-Key` header a retry replays the
  first response instead of creating a second row.
- **A list is paged**: it returns the `PaginatedList<T>` envelope, never a bare array.
- **A new API version** reuses the same route and verb; a changed read gets a new action named
  `<Name>V2` mapped with `[MapToApiVersion("2.0")]`.

---

## 4. Required action attributes

Every action on a versioned controller carries all four:

```csharp
[HttpGet("{id:guid}")]
[ProducesResponseType(typeof(CarDto), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
[EndpointName("GetCar")]
[MapToApiVersion("1.0")]
public async Task<IActionResult> GetCar(Guid id, CancellationToken ct)
```

| Attribute | Rule |
|---|---|
| `[Http*("template")]` | Use route constraints: `{id:guid}` rejects malformed input at routing time, so no validator is needed for it |
| `[ProducesResponseType]` | One per reachable status. Success carries the DTO type; failures carry `typeof(ProblemDetails)` (`ValidationProblemDetails` for a 400) |
| `[EndpointName]` | Matches the method name exactly. Feeds `operationId` in OpenAPI, so client generators produce stable method names |
| `[MapToApiVersion]` | On every action of a versioned controller, even when the class already declares `[ApiVersion]`; one per version the action serves |

The version-neutral controllers (`IdentityController`, `AccountController`) have no
`[MapToApiVersion]`; everything else applies. Add `[Idempotent]` to a creating `POST`, and
`[AllowAnonymous]` / `[Authorize(Policy = ...)]` as §6 says.

Method signature: `public async Task<IActionResult>`, always ends with `CancellationToken ct`,
always forwards it into `this.sender.Send(..., ct)`.

---

## 5. Naming alignment across layers

One concept, one name, all the way down:

| Layer | Name |
|---|---|
| Route | `/api/v1/cars` |
| Controller | `CarsController` |
| Action / endpoint name | `GetCar`, `CreateCar`, `UpdateCar`, `RemoveCar` |
| Request contract | `CreateCarRequest`, `UpdateCarRequest` |
| Command / query | `CreateCarCommand`, `GetCarByIdQuery` |
| Feature folder | `Features/Cars/` |
| Entity | `Car` |
| Errors | `CarErrors` |
| Localization keys | `LocalizationKeys.Car.*`, values `"Car.Make.Required"` |
| Cache tag | `CacheTags.Cars` |
| Policies | `Policies.CanReadCars`, `Policies.CanManageCars` |

Note the deliberate asymmetry: the HTTP action for delete is `RemoveCar`, matching
`RemoveCarCommand`. The verb chosen in the Application layer wins; the controller follows it.

---

## 6. Authorization

- A fallback policy requires an authenticated user on every endpoint: secure by default, opt out
  per-action with `[AllowAnonymous]`. (A side effect: an unmatched route answers 401 to an
  unauthenticated caller.)
- Capabilities are **named policies**, never role names: `CanReadCars`, `CanManageCars`,
  `CanReadDrivers`, `CanManageDrivers`, `CanManageUsers`. The read policy sits on the
  **controller**; write actions add the stricter one.
- Roles are `Manager` (all policies) and `User` (the two read policies). The mapping lives in
  `AddAppAuthorization`.
- The anonymous actions are `generate`, `refresh-token`, `register`, `confirm-email`,
  `resend-confirmation`, `forgot-password` and `reset-password`; `user-info`, `change-password` and
  `logout` need any authenticated user.
- `BearerSecurityOperationTransformer` reads this metadata and renders the padlock in OpenAPI on
  exactly the protected endpoints. Getting the attributes right is what makes the generated docs
  accurate.

---

## 7. Headers

| Header | Direction | Meaning |
|---|---|---|
| `Authorization: Bearer <jwt>` | request | Required on anything not `[AllowAnonymous]` |
| `Accept-Language: en` \| `ar` | request | Selects the language for both content and error messages. Absent or unsupported → `Languages.Default`. Advertised on every operation by `AcceptLanguageOperationTransformer` |
| `Idempotency-Key` | request | Optional on `[Idempotent]` POSTs; 1–128 characters |
| `Location` | response | On `201`; absolute URL of the created resource |
| `Idempotency-Replayed: true` | response | The stored response of an earlier request with the same key |
| `Retry-After` | response | Seconds, on `429` |
| `WWW-Authenticate: Bearer` | response | On `401` |
| `api-supported-versions`, `api-deprecated-versions` | response | Emitted by `ReportApiVersions = true` (a v1 response: supported `2.0`, deprecated `1.0`) |
| `Sunset`, `Link: …; rel="sunset"` | response | On deprecated versions: the retirement date (2027-12-31 in the template) and the policy link |
| `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`, `Permissions-Policy`, `Cross-Origin-Opener-Policy`, `Content-Security-Policy` | response | Added to every response by `SecurityHeadersMiddleware` |

---

## 8. Error response shape

Two shapes, both RFC 7807, produced by `ProblemExtensions`:

**Validation** (every error is `ErrorKind.Validation`) → `ValidationProblemDetails`:

```jsonc
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": { "Year": [ "Car year must be between 1886 and 2026." ] },
  "instance": "POST /api/v1/cars",
  "traceId": "00-…",
  "requestId": "0HN7…"
}
```

**Everything else** → `ProblemDetails` built from the first error, with the localized message as
`title`:

```jsonc
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
  "title": "Car not found",
  "status": 404,
  "instance": "GET /api/v1/cars/…",
  "traceId": "00-…",
  "requestId": "0HN7…"
}
```

The `errors` dictionary is keyed by `Error.PropertyName`, which `ValidationBehavior` copies from the
FluentValidation property name. The messages inside it and the `title` of a non-validation problem
are the localized `Error.Code`, falling back to `Error.Description` when the key is missing from the
resource files. The `title` of a *validation* problem is the fixed framework string and is not
localized. The 401/403 bodies written by the status-code pages carry the English reason phrase as
`title`.

---

## 9. Checklist for a new endpoint

- [ ] Literal, lowercase, plural route segment
- [ ] `[ApiVersion]` on the class, `[MapToApiVersion]` on the action (versioned controllers)
- [ ] Correct verb; body only where a body belongs
- [ ] `[EndpointName]` matching the method name
- [ ] One `[ProducesResponseType]` per reachable status
- [ ] Route constraint on every route parameter (`{id:guid}`)
- [ ] `CancellationToken ct`, forwarded
- [ ] `[FromBody]` / `[FromQuery]` binds a **Contracts** request type
- [ ] Body maps to the command by hand, in the controller
- [ ] Returns `result.Match(success, this.Problem)`: nothing else
- [ ] `POST` uses `CreatedAtAction` with `version` **and** the scalar id in the route values
- [ ] A creating `POST` is `[Idempotent]`
- [ ] A named policy from `Policies`, inherited from the class or set on the action;
      `[AllowAnonymous]` justified
- [ ] A new capability has its `Policies` constant and its `AddAppAuthorization` mapping
- [ ] Added to `requests/requests.http`
- [ ] Covered by an integration test
