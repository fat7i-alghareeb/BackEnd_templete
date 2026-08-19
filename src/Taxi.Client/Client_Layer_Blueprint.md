# Client Layer Blueprint — `Taxi.Client`

> Start at [AGENTS.md](../../AGENTS.md) — it carries the rules and the add-a-feature
> checklist. This file is the deep reference for the Client layer.
> Map of all docs: [NAVIGATION.md](../../docs/NAVIGATION.md).
>
> **Status: scaffold.** This project is a Blazor WebAssembly shell containing `Program.cs` and
> `wwwroot` static assets. There are no components, no pages, no authentication handler, no state
> management, and no hub client. `Taxi.Api` references the project but **does not host it**.
> Sections 1–10 describe what exists and the rules that apply. Section 11 sketches how to build it
> out correctly — none of section 11 exists today.

---

## 1. What is actually here

```text
src/Taxi.Client/
├── Program.cs
├── Taxi.Client.csproj
├── wwwroot/
│   ├── favicon.png
│   ├── icon-192.png
│   └── lib/bootstrap/...
└── Client_Layer_Blueprint.md
```

```csharp
// Program.cs — the entire application
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddHttpClient(
    "TaxiServerClient",
    client => client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress));

await builder.Build().RunAsync();
```

One named `HttpClient` pointed at the host's base address. No root component is registered
(`builder.RootComponents` is untouched), so the app renders nothing.

---

## 2. Dependency rules

**References:** `Taxi.Contracts` only.

**Never** reference `Taxi.Domain`, `Taxi.Application` or `Taxi.Infrastructure`. A WebAssembly
build ships to the browser — a Domain reference would put business rules, and an Infrastructure
reference would put connection strings and Identity internals, into a publicly downloadable
assembly.

Referencing Contracts is safe and is the point: request shapes, `LocalizationKeys` and
`Languages` are shared with the server, so the client cannot drift on a property name or a
translation key.

**Referenced by:** `Taxi.Api` — but the reference is unused. See §6.

Packages referenced, none currently used in code: `Blazored.LocalStorage`, `Humanizer`,
`Microsoft.AspNetCore.Components.WebAssembly(.Authentication)`, `Microsoft.Extensions.Http`,
`System.IdentityModel.Tokens.Jwt`. They telegraph the intended design — token storage, a bearer
handler, JWT parsing — but none of it is written.

---

## 3. What the client can and cannot see

| Server concept | Visible to the client? |
|---|---|
| `CreateCarRequest`, `UpdateCarRequest` | ✅ via Contracts |
| `LocalizationKeys`, `Languages` | ✅ via Contracts |
| `CarDto`, `TokenResponse`, `AppUserDto` | ❌ — Application types; the client would need its own copy |
| `Car`, `Result<T>`, `Error` | ❌ and must stay that way |

The response gap is real: the Car endpoints return `CarDto` from `Taxi.Application`, which the
client cannot reference. A client consuming `GET /api/v1/cars` today must declare a local mirror
type. The alternative is to route responses through Contracts — discussed in the
[Contracts blueprint, Future Extensions](../Taxi.Contracts/Contracts_Layer_Blueprint.md).

---

## 4. Contract with the API

Whatever gets built must speak the API's existing conventions:

- **Base route:** `api/v1/<plural>`; auth is version-neutral at `api/token`.
- **Auth:** `POST /api/token/generate` → `{ accessToken, refreshToken, expiresOnUtc }`.
  Send `Authorization: Bearer <accessToken>`. On 401, `POST /api/token/refresh-token` with the
  expired access token plus the refresh token. One refresh token per user — the server deletes the
  previous one on every issue, so a second concurrent refresh will fail.
- **Language:** send `Accept-Language: en|ar`. The server resolves it via
  `UseRequestLocalization` and returns the matching text and translated error messages.
- **Errors:** always RFC 7807. Validation failures are `ValidationProblemDetails` with an `errors`
  dictionary keyed by property name; everything else is `ProblemDetails` with a localized `title`.
  Both carry `requestId` and an `instance` of `"{METHOD} {path}"`.
- **CORS:** the server allows only the origins in `AppSettings:AllowedOrigins`
  (`http://localhost:5002`, `https://localhost:7008` by default) with credentials enabled.
- **Rate limit:** 100 requests/minute globally; expect 429.

---

## 5. Running it

`dotnet run --project src/Taxi.Client` starts the standalone WASM host. It renders a blank page —
there is no root component. Nothing in the solution depends on the client running.

---

## 6. The unused API reference

`Taxi.Api.csproj` contains
`<ProjectReference Include="..\Taxi.Client\Taxi.Client.csproj" />`, and the API also references
`Microsoft.AspNetCore.Components.WebAssembly.Server` — the setup for hosted Blazor. But
`Program.cs` never calls `MapRazorComponents`, so the client is compiled and then ignored.

Two honest options:

1. **Host it** — implement §11.1 and add the mapping (see the
   [API blueprint, Future Extensions](../Taxi.Api/Api_Layer_Blueprint.md)).
2. **Detach it** — remove the project reference and the `.WebAssembly.Server` package, and deploy
   the client separately.

Leaving it as-is costs build time and misleads readers.

---

## 7. Belongs / does not belong

The project is empty, so there is no existing code to imply the rules. That makes this the most
important section in the file — everything here is enforced by one fact: **a WebAssembly assembly
is downloaded by the browser and can be decompiled by anyone.**

**Belongs:** Razor components and pages · layouts · typed API clients wrapping the named
`HttpClient` · a `DelegatingHandler` that attaches the bearer token · token storage ·
`AuthenticationStateProvider` for UI state · client-side formatting and display logic ·
view models shaped for a screen.

**Does not belong:**

| Never put here | Why |
|---|---|
| A reference to `Taxi.Domain`, `Taxi.Application` or `Taxi.Infrastructure` | Ships business rules, EF configuration and Identity internals to the browser |
| Business rules or invariant checks | The server is the only authority; duplicating a rule guarantees the two versions drift |
| Authorization decisions | Client-side claims are trivially forged. `[AuthorizeView]` hides UI; it does not secure anything |
| Connection strings, JWT secrets, API keys of any kind | Everything in this project is public |
| Re-translation of server messages | The API already returned text in the requested language |
| Direct `HttpClient` construction | Use the named `TaxiServerClient` through `IHttpClientFactory` so the auth handler is applied |

---

## 8. Naming

| Thing | Convention | Example |
|---|---|---|
| Page component | `<Plural><Action>.razor` | `CarList.razor`, `CarEdit.razor` |
| Reusable component | noun | `CarCard.razor`, `LanguageSwitcher.razor` |
| Typed API client | `<Entity>Client` | `CarClient`, `TokenClient` |
| Client-side model | `<Entity>Summary` / `<Entity>Detail` | `CarSummary` |
| Folder | plural, matching the API resource | `Pages/Cars/` |
| Named `HttpClient` | already fixed | `"TaxiServerClient"` |

Match the server's vocabulary. If the API calls it `/api/v1/cars` and the command is
`RemoveCarCommand`, the client method is `RemoveCarAsync` — not `DeleteCarAsync`.

---

## 9. Talking to other layers

The client talks to exactly one thing: **the API, over HTTP.** It has no other channel.

| Direction | How |
|---|---|
| **→ API** | `IHttpClientFactory.CreateClient("TaxiServerClient")`. Attach `Authorization: Bearer` and `Accept-Language` on every call. |
| **→ Contracts** | Compile-time reference for request types, `LocalizationKeys` and `Languages` — the only project reference allowed. |
| **← API** | JSON. Success bodies are Application DTOs the client cannot reference (see §3), so declare a local mirror type. Failures are `ProblemDetails` / `ValidationProblemDetails`. |
| **← anything else** | Nothing. No database, no message bus, no shared memory with the server. |

---

## 10. Common mistakes

| ❌ | ✅ |
|---|---|
| `<ProjectReference Include="..\Taxi.Domain\..." />` | Contracts only |
| Re-implementing `Car.Create`'s year check to "save a round trip" | Let the server validate; render the returned `errors` dictionary |
| Treating a parsed JWT claim as an authorization decision | UI hint only; the server enforces it |
| Translating `problemDetails.title` in the client | It arrived already translated per `Accept-Language` |
| `if (culture == "ar")` | `if (culture == Languages.Ar)` |
| `new HttpClient()` | `IHttpClientFactory.CreateClient("TaxiServerClient")` |
| Firing several requests that each hit a 401 and each try to refresh | Serialise refresh through one `SemaphoreSlim` — the server keeps **one** refresh token per user, so the second refresh fails |
| Storing the token in a plain variable that dies on reload | Persist it (`Blazored.LocalStorage` is already referenced), accepting the XSS trade-off |
| Ignoring 429 | The API rate-limits at 100 requests/minute globally |

---

## 11. Future Extensions — NOT IMPLEMENTED

> ⚠️ **None of the following exists.** Sketches for building the client out correctly against this
> API. Do not treat them as existing pattern.

### 11.1 Minimum viable structure

```text
src/Taxi.Client/
├── App.razor              root component + Router
├── Routes.razor
├── _Imports.razor
├── Layout/MainLayout.razor
├── Pages/                 @page components
├── Components/            reusable fragments
├── Services/              typed API clients
├── Identity/              BearerTokenHandler, CustomAuthenticationStateProvider, UserInfo
└── Program.cs
```

`Program.cs` then needs `builder.RootComponents.Add<App>("#app")` plus a matching
`wwwroot/index.html`.

### 11.2 Bearer token handler with silent refresh

```csharp
// src/Taxi.Client/Identity/BearerTokenHandler.cs
public sealed class BearerTokenHandler(ILocalStorageService storage, ITokenClient tokenClient)
    : DelegatingHandler
{
    private readonly ILocalStorageService storage = storage;
    private readonly ITokenClient tokenClient = tokenClient;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var accessToken = await this.storage.GetItemAsStringAsync("access_token", cancellationToken);

        if (!string.IsNullOrEmpty(accessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            return response;
        }

        // one refresh attempt, then replay the original request
        var refreshed = await this.tokenClient.RefreshAsync(cancellationToken);

        if (!refreshed)
        {
            return response;
        }

        var newToken = await this.storage.GetItemAsStringAsync("access_token", cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", newToken);

        return await base.SendAsync(request, cancellationToken);
    }
}
```

Registered with `builder.Services.AddHttpClient("TaxiServerClient", ...)
.AddHttpMessageHandler<BearerTokenHandler>();`.

Three constraints this API imposes: replaying an `HttpRequestMessage` requires buffering the
content first (a streamed body cannot be re-sent); the server issues **one** refresh token per
user, so concurrent 401s must be serialised through a single refresh (a `SemaphoreSlim`); and
`localStorage` is readable by any script on the origin — accept the XSS exposure or move to a
cookie-based scheme, which would require server changes.

### 11.3 Authentication state

```csharp
// src/Taxi.Client/Identity/CustomAuthenticationStateProvider.cs
public sealed class CustomAuthenticationStateProvider(ILocalStorageService storage)
    : AuthenticationStateProvider
{
    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var token = await storage.GetItemAsStringAsync("access_token");

        if (string.IsNullOrEmpty(token))
        {
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        }

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        var identity = new ClaimsIdentity(jwt.Claims, "jwt");

        return new AuthenticationState(new ClaimsPrincipal(identity));
    }

    public void NotifyAuthenticationStateChanged()
        => this.NotifyAuthenticationStateChanged(this.GetAuthenticationStateAsync());
}
```

Client-side claim parsing is for **UI decisions only** — showing a menu item, routing away from a
page. It is trivially forged. Every real check happens server-side via `[Authorize]`. The server
emits roles as `ClaimTypes.Role`, so `AuthorizeView Roles="Manager"` works once the identity is
constructed with the matching role claim type.

### 11.4 Typed API clients

One class per aggregate in `Services/`, wrapping the named `HttpClient`, returning a client-side
result type rather than throwing:

```csharp
public sealed class CarClient(IHttpClientFactory factory)
{
    public async Task<IReadOnlyList<CarSummary>> GetCarsAsync(string language, CancellationToken ct)
    {
        var client = factory.CreateClient("TaxiServerClient");
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/v1/cars");
        request.Headers.AcceptLanguage.Add(new StringWithQualityHeaderValue(language));

        var response = await client.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<List<CarSummary>>(cancellationToken: ct) ?? [];
    }
}
```

Deserialize `ProblemDetails` / `ValidationProblemDetails` on failure and surface the localized
`title` — the server has already translated it into the requested language, so the client should
never re-translate.

### 11.5 Real-time

A hub client belongs in `Hubs/` and needs `Microsoft.AspNetCore.SignalR.Client` added back. It
pairs with
a server-side hub, which does not exist either — see the
[Infrastructure blueprint, Future Extensions](../Taxi.Infrastructure/Infrastructure_Layer_Blueprint.md).
Build the server side first.
