# RESTful Naming Constitution

Route, verb and status-code rules for Taxi_Server. Every rule below is demonstrated by
`src/Taxi.Api/Controllers/CarsController.cs` — when in doubt, open that file.

---

## 1. The five rules

1. **Nouns, not verbs.** The URL names a thing; the HTTP method names the action.
   `POST /api/v1/cars`, never `POST /api/v1/createCar`.
2. **Always plural.** `/cars` for the collection *and* `/cars/{id}` for one member. Never `/car/5`.
3. **Shallow nesting.** At most one level: `/cars/{id}/maintenance-records`. Beyond that, expose
   the sub-resource at its own root and link by id.
4. **Lowercase kebab-case.** `/maintenance-records`, never `/MaintenanceRecords` or
   `/maintenance_records`.
5. **Version everything.** `api/v{version:apiVersion}/...` on every business resource.

---

## 2. Route templates

```csharp
[Route("api/v{version:apiVersion}/cars")]   // ✅ literal, lowercase, plural
[ApiVersion("1.0")]
```

**Write the segment literally.** Do not use `[controller]` — the token derives the segment from the
class name, which means renaming a controller silently changes the public URL, and it gives no
control over casing or hyphenation.

| Resource | Route |
|---|---|
| Cars | `api/v{version:apiVersion}/cars` |
| Auth (version-neutral) | `api/token` |

`IdentityController` is `[Route("api/token")]` + `[ApiVersionNeutral]`. Authentication endpoints
are deliberately outside the versioning scheme — a client that cannot obtain a token cannot
negotiate a version.

Version resolution is `UrlSegmentApiVersionReader`, default `1.0`,
`AssumeDefaultVersionWhenUnspecified = true`, `ReportApiVersions = true`.

---

## 3. Verbs, actions and status codes

The matrix `CarsController` implements:

| Verb | Route | Action | Success | Failure |
|---|---|---|---|---|
| `GET` | `/cars` | `GetCars` | `200` + `List<CarDto>` | — |
| `GET` | `/cars/{id:guid}` | `GetCar` | `200` + `CarDto` | `404` |
| `POST` | `/cars` | `CreateCar` | `201` + `CarDto` + `Location` | `400` |
| `PUT` | `/cars/{id:guid}` | `UpdateCar` | `204` | `400`, `404` |
| `DELETE` | `/cars/{id:guid}` | `RemoveCar` | `204` | `404` |

Additional statuses the pipeline can produce without controller involvement: `401` (no or invalid
token), `403` (role or policy denied), `409` (`ErrorKind.Conflict`), `429` (rate limiter),
`500` (`GlobalExceptionHandler`).

Rules the matrix encodes:

- **`POST` returns 201 with a `Location` header**, produced by
  `CreatedAtAction(nameof(this.GetCar), new { version = "1.0", id = car.Id }, car)`. The
  `version` route value is required — the route template contains a `version` segment, and
  omitting it makes URL generation fail. Pass the **id**, not the DTO, as the route value.
- **`PUT` and `DELETE` return 204**, no body. The command returns `Result<Updated>` /
  `Result<Deleted>` and the controller matches to `NoContent()`.
- **`PUT` carries the id in both the route and the body**, and the action rejects a mismatch:

  ```csharp
  if (id != request.Id)
  {
      return this.BadRequest();
  }
  ```

  This is the one piece of logic allowed in a controller — it is protocol consistency, not
  business rule.
- **404 comes from the handler**, as `CarErrors.NotFound`, not from a controller null check.

---

## 4. Required action attributes

Every action carries all four:

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
| `[Http*("template")]` | Use route constraints — `{id:guid}` rejects malformed input at routing time, so no validator is needed for it |
| `[ProducesResponseType]` | One per reachable status. Success carries the DTO type; failures carry `typeof(ProblemDetails)` |
| `[EndpointName]` | Matches the method name exactly. Feeds `operationId` in OpenAPI, so client generators produce stable method names |
| `[MapToApiVersion]` | On every action, even when the class already declares `[ApiVersion]` |

Method signature: `public async Task<IActionResult>`, always ends with `CancellationToken ct`,
always forwards it into `sender.Send(..., ct)`.

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

Note the deliberate asymmetry: the HTTP action for delete is `RemoveCar`, matching
`RemoveCarCommand`. The verb chosen in the Application layer wins; the controller follows it.

---

## 6. Authorization

- `[Authorize]` sits on the **controller**, not on each action — secure by default, opt out
  per-action with `[AllowAnonymous]`.
- `IdentityController` has no class-level `[Authorize]`; only `GetUserInfo` carries one, since
  token issuance must be reachable unauthenticated.
- `BearerSecurityOperationTransformer` reads this metadata and renders the padlock in OpenAPI on
  exactly the protected endpoints. Getting the attributes right is what makes the generated docs
  accurate.

---

## 7. Headers

| Header | Meaning |
|---|---|
| `Authorization: Bearer <jwt>` | Required on anything under `[Authorize]` |
| `Accept-Language: en` \| `ar` | Selects the language for both content and error messages. Absent or unsupported → `Languages.Default`. Advertised on every operation by `AcceptLanguageOperationTransformer` |
| `api-supported-versions` (response) | Emitted by `ReportApiVersions = true` |

---

## 8. Error response shape

Two shapes, both RFC 7807, produced by `ProblemExtensions`:

**Validation** (every error is `ErrorKind.Validation`) → `ValidationProblemDetails`:

```jsonc
{
  "type": "...", "title": "One or more validation errors occurred.", "status": 400,
  "errors": { "Year": [ "Car year must be between 1886 and 2026." ] },
  "instance": "POST /api/v1/cars",
  "requestId": "0HN7..."
}
```

**Everything else** → `ProblemDetails` built from the first error, with the localized message as
`title`:

```jsonc
{ "title": "Car not found", "status": 404, "instance": "GET /api/v1/cars/…", "requestId": "0HN7..." }
```

The `errors` dictionary is keyed by `Error.PropertyName`, which `ValidationBehavior` copies from
the FluentValidation property name. `title` is the localized `Error.Code`, falling back to
`Error.Description` when the key is missing from the resource files.

---

## 9. Checklist for a new endpoint

- [ ] Literal, lowercase, plural route segment
- [ ] `[ApiVersion]` on the class, `[MapToApiVersion]` on the action
- [ ] Correct verb; body only where a body belongs
- [ ] `[EndpointName]` matching the method name
- [ ] One `[ProducesResponseType]` per reachable status
- [ ] Route constraint on every route parameter (`{id:guid}`)
- [ ] `CancellationToken ct`, forwarded
- [ ] `[FromBody]` binds a **Contracts** request type
- [ ] Body maps to the command by hand, in the controller
- [ ] Returns `result.Match(success, this.Problem)` — nothing else
- [ ] `POST` uses `CreatedAtAction` with `version` **and** the scalar id in the route values
- [ ] `[Authorize]` inherited from the class, or `[AllowAnonymous]` justified
- [ ] Added to `requests/requests.http`
