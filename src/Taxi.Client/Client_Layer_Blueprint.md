# 🏛️ Architectural Constitution: Client Layer Blueprint

## 1. Executive Summary & Layer Purpose

The WebAssembly Client Layer (`MechanicShop.Client`) serves as the active UI consumption frontier of our Clean Architecture. Operating as an isolated Single-Page Application (SPA) executed entirely within the end user's browser sandbox, it possesses absolutely no awareness of databases, external third-party SDKs, or inner business rules.

Its singular responsibility is to communicate securely and efficiently with the API Layer utilizing asynchronous HTTP/REST queries and real-time SignalR WebSockets, subsequently mapping Server-sent DTOs and `ProblemDetails` into reactive visual states utilizing Blazor WebAssembly.

---

## 2. Dependency Rules & Boundaries

### Inward Pointing Dependencies

The Client Layer explicitly depends on ONLY one external project:

1. **Contracts Layer:** To tightly couple the request payloads (`CreateCustomerRequest`) and response DTOs (`CustomerDto`), eliminating "Magic String" property mapping failures between the browser and the API.

> [!WARNING]
> The Client Layer must NEVER reference the `Domain`, `Application`, or `Infrastructure` layers. Doing so would leak server-side cryptographic configurations, EF Core contexts, and proprietary business validations directly into the client's public binary stream.

### Outward Pointing Dependencies

**Nothing.** Neither the API, nor the Application, nor the Domain are natively aware of the Blazor Client's existence. The frontend acts exclusively as a humble consumer to the API host.

---

## 3. Directory Structure & Anatomy

Forensic extraction of the Client directory reveals a strict segregation of networking, authentication, and presentation logic:

```text
src/MechanicShop.Client/
├── Components/         # Reusable atomic Blazor UI fragments (Cards, Buttons, Modals)
├── Extensions/         # Client-side helpers (e.g., timezone data conversions)
├── Hubs/               # SignalR WebSocket connection managers (WorkOrderHubClient.cs)
├── Identity/           # Security abstraction & interceptor handlers
│   ├── BearerTokenHandler.cs
│   ├── CustomAuthenticationStateProvider.cs
│   └── UserInfo.cs
├── Layout/             # Global structural blueprints (MainLayout.razor, NavMenu.razor)
├── Models/             # Client-exclusive view models supplementing Contracts
├── Pages/              # Routable components mapping to specific URLs (/customers)
├── Services/           # HTTP wrappers abstracting HttpClient details (ServiceApi.cs)
├── Routes.razor        # Declarative URL routing engine and Authorization wrapper
└── Program.cs          # The WebAssembly host bootstrap
```

---

## 4. Core Architectural Mechanics

Based on microscopic, file-by-file analysis of the ingested Client codebase, the following advanced enterprise-grade mechanics govern the Blazor front-end architecture.

### 4.1. The Intercepting Authentication Handler (DelegatingHandler)

Rather than manually injecting JWTs into every single outgoing HTTP request inside our logic layer, the architecture enforces a strictly automated `DelegatingHandler` named `BearerTokenHandler.cs`.

Registered in the `Program.cs` pipeline directly into the system's primary `HttpClient`, this handler intercepts EVERY outbound network request seamlessly.

**The Mechanics:**

1. It asynchronously retrieves the stored Access Token from local storage.
2. If present, it injects the `Authorization: Bearer <token>` header natively.
3. If the server responds with a `401 Unauthorized` HTTP code, the handler immediately pauses the execution pipeline. It triggers a **Silent Refresh Token Request** to the API. If successful, it attaches a custom `X-Retry` header (to prevent infinite loops) and replays the exact original paused request seamlessly, ensuring the user experiences zero interruption.

```csharp
protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
{
    // ... Token injection logic ...
    var response = await base.SendAsync(request, cancellationToken);

    // 401 Interception & Retry Logic
    if (response.StatusCode == HttpStatusCode.Unauthorized && !request.Headers.Contains("X-Retry"))
    {
        var newTokenResponse = await _accountManagement.RefreshTokenAsync();
        if (newTokenResponse is not null)
        {
            var newRequest = CloneRequest(request);
            newRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", newTokenResponse.AccessToken);
            newRequest.Headers.Add("X-Retry", "true");
            return await base.SendAsync(newRequest, cancellationToken);
        }
        await _accountManagement.LogoutAsync();
    }
    return response;
}
```

### 4.2. JWT Claim Extraction Sandbox (`CustomAuthenticationStateProvider.cs`)

Because Blazor WebAssembly runs exclusively in the browser, the standard ASP.NET Identity Cookies system does not apply. The architecture completely overrides the native `AuthenticationStateProvider`.

When the user logs in, the `CustomAuthenticationStateProvider` reaches out to the secure `/identity/current-user/claims` endpoint. It unpacks the authenticated JSON payload, parses the data, and constructs a completely isolated, browser-side `ClaimsPrincipal`. By iterating through roles, it dynamically injects `ClaimTypes.Role` properties, enabling native `<AuthorizeView Roles="Manager">` controls perfectly.

### 4.3. The Anti-Corruption API Gateway (`ServiceApi.cs`)

The Client codebase fundamentally rejects allowing raw `HttpClient.GetAsync()` calls natively inside `.razor` pages. Doing so would bleed HTTP Status Code management, JSON serialization configurations, and Network failure scenarios directly into the UI.

In its place stands the `ServiceApi.cs` class. This monolithic gateway is an Anti-Corruption Layer that wraps every single network call internally translating raw REST results into a generic `ApiResult<T>` struct mimicking the Domain's `Result` pattern.

**Advanced ProblemDetails Parsing:**
When an endpoint fails (e.g., throwing a `400 Bad Request` or `409 Conflict`), the `ServiceApi` intercepts the payload, deserializes the JSON natively as RFC 7807 `ProblemDetails`, and populates the `ApiResult.Failure` properties so the UI can cleanly map Validation Failures sequentially.

```csharp
private static async Task<ApiResult<T>> HandleErrorResponseAsync<T>(HttpResponseMessage response)
{
    string content = await response.Content.ReadAsStringAsync();
    var problemDetails = JsonSerializer.Deserialize<ProblemDetails>(content, ...);

    if (problemDetails is not null)
    {
        // Smoothly translates HTTP Problems into client-friendly structs
        return ApiResult<T>.Failure(
            message: problemDetails.Title ?? "An error occurred",
            detail: problemDetails.Detail ?? "Error",
            statusCode: problemDetails.Status ?? (int)response.StatusCode,
            validationErrors: problemDetails.Errors);
    }
}
```

### 4.4. Resilient SignalR WebSockets (`WorkOrderHubClient.cs`)

For complex scheduling matrices (e.g., mechanics dragging and dropping vehicles on a daily timeline), the system bypasses heavy HTTP polling entirely via the `WorkOrderHubClient`.

This service is registered as Scoped in DI. It encapsulates the `HubConnectionBuilder`, connecting securely to `hubs/workorders` and establishing the `.WithAutomaticReconnect()` pipeline. It surfaces a public asynchronous delegate `StartAsync(Func<Task> onWorkOrdersChanged)` enabling UI components to painlessly wire up `StateHasChanged` renders whenever the API broadcasts a global mutation event.

### 4.5. Declarative Routing & Cascading Authentication (`Routes.razor`)

The Client enforces strict route authorization natively via `Routes.razor`. By enclosing the `<Router>` object entirely within a `<CascadingAuthenticationState>`, it guarantees that no component renders without a resolved Identity state.

Furthermore, instead of relying on custom HTTP redirect traps, it implements the `<AuthorizeRouteView>` handler. When a component with an `[Authorize]` attribute attempts to render for an unauthenticated user, the structural `<NotAuthorized>` markup seamlessly displays the `Access Denied` UI card without executing the component's underlying `OnInitializedAsync` logic, preventing sensitive JSinterop executions.

### 4.6. Client-Side Localization Strategy

The Client layer shares the `LocalizationKeys` from the Contracts layer.

**Mechanics:**

- **Shared Vocabulary**: UI labels, error messages, and button text are all referenced via `LocalizationKeys`.
- **Languages Registry**: The client utilizes the `Languages` class from the Contracts project to ensure strongly-typed culture selection and switching logic.
- **Localizer Implementation**: The client uses a matching `IStringLocalizer` implementation that loads the same `SharedResource.json` files as the API.
- **Culture Propagation**: The client automatically sends the `Accept-Language` header (retrieved from browser settings or user preference) in every request via the `BearerTokenHandler`.
- **ProblemDetails Translation**: When `ServiceApi` receives a `ProblemDetails` response, it uses the returned `Error.Code` (or `Description` for validation) as a key to look up the local translation, ensuring the user sees the message in their preferred language.

---

## 5. Anti-Patterns & Rejection Criteria

### Immediate PR Rejection Checklist for the Client Layer

1. 🚨 **In-Component HTTP Requests:**
   - **The Wrong Way:** Calling `HttpClient.GetFromJsonAsync("api/customers")` deep within `Customers.razor`.
   - **Why it's rejected:** Bypasses central API Result parsing, problem details translations, loading states abstractions, and prevents proper mocking during test executions. **Always** use `ServiceApi.cs`.
2. 🚨 **Local TimeZone Forgetting:**
   - **The Wrong Way:** Assuming `DateTime.Now` evaluates logically in the browser without offset translation.
   - **Why it's rejected:** The Server runs in UTC; the UI operates locally. Models parsing Dates must utilize `item.AdjustTimeToLocal()` as evidenced in the `ServiceApi.GetWorkOrdersAsync` implementation natively to prevent temporal shifting.
3. 🚨 **Manual Header Manipulation:**
   - **The Wrong Way:** Hardcoding `request.Headers.Add("Authorization", "Bearer xXx")` inside services.
   - **Why it's rejected:** It totally breaks the `BearerTokenHandler` silent-refresh interceptor pipeline, resulting in instant 401 loops when a token expires naturally.

---

## 6. The Composition Root (Program.cs)

The Client environment strictly configures its DI containers and HTTP channels exclusively inside `Program.cs`.

```csharp
var builder = WebAssemblyHostBuilder.CreateDefault(args);

// Initializes Blazor's native cascading Auth infrastructure
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthenticationStateProvider>();

// Maps interfaces to implementations natively
builder.Services.AddScoped(sp => (IAccountManagement)sp.GetRequiredService<AuthenticationStateProvider>());

// Registers the HTTP Interceptor mechanism
builder.Services.AddTransient<BearerTokenHandler>();

// Binds the heavily decorated HttpClient into the service loop natively spanning the base URI
builder.Services.AddHttpClient(
    "MechanicShopClient",
    client => client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress))
    .AddHttpMessageHandler<BearerTokenHandler>();

// Registers isolated Storage and HTTP translation APIs
builder.Services.AddBlazoredLocalStorage();
builder.Services.AddScoped<ServiceApi>();
builder.Services.AddScoped<WorkOrderHubClient>();

await builder.Build().RunAsync();
```
