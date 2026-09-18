<!-- markdownlint-disable MD033 MD041 MD013 -->
<div align="center">

<img src="assets/banner.svg" alt="Backend Template: production-ready ASP.NET Core starter" width="100%"/>

<br/>

<img src="https://img.shields.io/badge/.NET-10-512BD4?style=for-the-badge&logo=dotnet&logoColor=white" alt=".NET 10"/>
<img src="https://img.shields.io/badge/C%23-latest-7C3AED?style=for-the-badge&logo=csharp&logoColor=white" alt="C#"/>
<img src="https://img.shields.io/badge/ASP.NET_Core-Web_API-5C2D91?style=for-the-badge&logo=dotnet&logoColor=white" alt="ASP.NET Core"/>
<img src="https://img.shields.io/badge/PostgreSQL-16-4169E1?style=for-the-badge&logo=postgresql&logoColor=white" alt="PostgreSQL 16"/>
<img src="https://img.shields.io/badge/Docker-Compose-2496ED?style=for-the-badge&logo=docker&logoColor=white" alt="Docker Compose"/>

<img src="https://img.shields.io/badge/EF_Core-10-6D28D9?style=flat-square" alt="EF Core 10"/>
<img src="https://img.shields.io/badge/MediatR-14-F43F5E?style=flat-square" alt="MediatR 14"/>
<img src="https://img.shields.io/badge/FluentValidation-12-F59E0B?style=flat-square" alt="FluentValidation 12"/>
<img src="https://img.shields.io/badge/Auth-JWT_%2B_Refresh-000000?style=flat-square&logo=jsonwebtokens&logoColor=white" alt="JWT"/>
<img src="https://img.shields.io/badge/Cache-HybridCache-38BDF8?style=flat-square" alt="HybridCache"/>
<img src="https://img.shields.io/badge/Logs-Serilog_%2B_Seq-2DD4BF?style=flat-square" alt="Serilog + Seq"/>
<img src="https://img.shields.io/badge/OpenTelemetry-traces_%26_metrics-425CC7?style=flat-square&logo=opentelemetry&logoColor=white" alt="OpenTelemetry"/>
<img src="https://img.shields.io/badge/Prometheus-metrics-E6522C?style=flat-square&logo=prometheus&logoColor=white" alt="Prometheus"/>
<img src="https://img.shields.io/badge/Grafana-dashboards-F46800?style=flat-square&logo=grafana&logoColor=white" alt="Grafana"/>
<img src="https://img.shields.io/badge/Tests-xUnit-34D399?style=flat-square" alt="xUnit"/>
<img src="https://img.shields.io/badge/Style-StyleCop-64748B?style=flat-square" alt="StyleCop"/>
<img src="https://img.shields.io/badge/i18n-English_%7C_العربية-FBBF24?style=flat-square" alt="English and Arabic"/>

<h3>Clone it. Rename it. Ship features on day one.</h3>

<p>
A reusable <b>.NET 10</b> backend template with <b>Clean Architecture</b> and <b>CQRS</b>, and the
production concerns already wired in: <b>authentication</b>, <b>caching</b>, <b>logging</b>,
<b>monitoring</b> and <b>testing</b>.
</p>

<a href="../README.md"><b>README</b></a> ·
<a href="ARCHITECTURE.md"><b>Architecture</b></a> ·
<a href="../AGENTS.md"><b>Feature recipe</b></a> ·
<a href="RESTful_Naming_Constitution.md"><b>REST rules</b></a> ·
<a href="https://github.com/fat7i-alghareeb/BackEnd_templete"><b>GitHub</b></a>

</div>

<br/>

<img src="assets/stats.svg" alt="6 projects, 5 pipeline stages, 2 languages, 17-step feature recipe, 5 Docker services, 7 error kinds" width="100%"/>

> [!NOTE]
> **This is not a taxi app.** The projects use `Taxi.*` as a placeholder root namespace, and the
> `Car` entity is a small **reference feature** that shows the full pattern end to end. Rename the
> namespace, delete `Car` when you have your own feature, and the plumbing stays.

---

## Contents

|   |   |   |
|:--|:--|:--|
| [1 · At a glance](#1--at-a-glance) | [5 · Authentication & RBAC](#5--authentication--rbac) | [9 · Observability](#9--observability) |
| [2 · Architecture](#2--architecture) | [6 · English / Arabic](#6--english--arabic) | [10 · Feature workflow](#10--feature-workflow) |
| [3 · Request lifecycle](#3--request-lifecycle) | [7 · Caching](#7--caching) | [11 · Quality & CI](#11--quality--ci) |
| [4 · Result pattern & errors](#4--result-pattern--errors) | [8 · Persistence](#8--persistence) | [12 · The whole map](#12--the-whole-map) |

<img src="assets/divider.svg" alt="" width="100%"/>

## 1 · At a glance

<table>
<tr>
<td width="33%" valign="top">

### 🧅 Clean Architecture

Six projects, dependencies pointing inward. The domain has no idea HTTP, EF Core or JWT exist.

</td>
<td width="33%" valign="top">

### ⚡ CQRS + vertical slices

One folder per use case: command or query, handler, validator. MediatR carries it through a
five-stage pipeline.

</td>
<td width="33%" valign="top">

### 🧾 Result pattern

Business failures are values, not exceptions. `Result<T>` turns into a 2xx or an RFC 7807
`ProblemDetails`, in one place.

</td>
</tr>
<tr>
<td valign="top">

### 🔐 JWT + refresh + RBAC

HMAC-SHA256 access tokens, rotating refresh tokens (one live per user, 7 days), and role claims
ready for `[Authorize]`.

</td>
<td valign="top">

### 🌍 English / العربية

Errors and validation messages are keys. Bilingual data is one JSONB column. `Accept-Language`
picks the language.

</td>
<td valign="top">

### 🚀 Cache + observability

HybridCache per query, split by language. Serilog, OpenTelemetry, Seq, Prometheus and Grafana
come up with one `docker compose`.

</td>
</tr>
<tr>
<td valign="top">

### 🐘 PostgreSQL + EF Core

Migrations, retry on startup, audit stamping, domain event dispatch, and JSONB for localized
fields.

</td>
<td valign="top">

### 🧪 Tests + analyzers

xUnit suites lock down domain guards and pipeline order. StyleCop runs on every build, and GitHub
Actions builds and tests every push.

</td>
<td valign="top">

### 📚 Docs + workflow

A doc map, per-layer blueprints, REST naming rules, a 17-step feature recipe, and prompt templates
for AI agents.

</td>
</tr>
</table>

<img src="assets/divider.svg" alt="" width="100%"/>

## 2 · Architecture

<img src="assets/architecture.svg" alt="Clean Architecture layers: Domain, Application, Infrastructure, Api, with Contracts shared" width="100%"/>

### Project references, exactly as they are in the `.csproj` files

```mermaid
flowchart LR
    API["🌐 Taxi.Api<br/><sub>controllers · middleware · OpenAPI</sub>"]
    INF["🏗️ Taxi.Infrastructure<br/><sub>EF Core · Identity · JWT · cache</sub>"]
    APP["⚙️ Taxi.Application<br/><sub>commands · queries · behaviours</sub>"]
    DOM["💎 Taxi.Domain<br/><sub>entities · Result#lt;T#gt;</sub>"]
    CON["📜 Taxi.Contracts<br/><sub>DTOs · LocalizationKeys</sub>"]
    CLI["🖥️ Taxi.Client<br/><sub>Blazor WASM shell</sub>"]

    API --> INF --> APP --> DOM --> CON
    API --> APP
    API --> CON
    API -.-> CLI
    CLI --> CON

    classDef api fill:#312E81,stroke:#818CF8,color:#fff
    classDef inf fill:#3B0764,stroke:#C084FC,color:#fff
    classDef app fill:#4C1D95,stroke:#A78BFA,color:#fff
    classDef dom fill:#6D28D9,stroke:#EDE9FE,color:#fff
    classDef con fill:#78350F,stroke:#FBBF24,color:#fff
    classDef cli fill:#1E293B,stroke:#64748B,color:#CBD5E1
    class API api
    class INF inf
    class APP app
    class DOM dom
    class CON con
    class CLI cli
```

| Layer | Owns | Must never |
|:--|:--|:--|
| 💎 **Domain** | Entities (private ctor + `Create` factory), `<Entity>Errors`, `LocalizedText`, `Result<T>` | throw for a business rule, touch EF / ASP.NET |
| ⚙️ **Application** | Use-case folders, validators, DTOs, mappers, MediatR behaviours, interfaces | reference `HttpContext` or Infrastructure types |
| 🏗️ **Infrastructure** | `AppDbContext`, configurations, migrations, seeding, Identity, JWT, HybridCache | define business abstractions |
| 🌐 **Api** | Thin controllers, ProblemDetails, versioning, OpenAPI, rate limiting, telemetry | contain `try/catch`, business `if`s or data access |
| 📜 **Contracts** | Request DTOs with `DataAnnotations`, `LocalizationKeys`, `Languages` | reference Domain or Application |

> [!IMPORTANT]
> `Taxi.Domain → Taxi.Contracts` is intentional. Error codes are `LocalizationKeys` constants, and
> Contracts is the one project the Domain, the API and the Client can all see. The reasoning is in
> the [Domain blueprint](../src/Taxi.Domain/Domain_Layer_Blueprint.md).

<img src="assets/divider.svg" alt="" width="100%"/>

## 3 · Request lifecycle

<img src="assets/pipeline.svg" alt="MediatR pipeline: exception logging, validation, performance, caching, handler" width="100%"/>

### One request, start to finish

```mermaid
sequenceDiagram
    autonumber
    actor C as Client
    participant M as Middleware
    participant K as CarsController
    participant P as MediatR pipeline
    participant H as Handler
    participant D as Domain (Car)
    participant DB as PostgreSQL

    C->>M: POST /api/v1/cars  (Bearer, Accept-Language: ar)
    Note over M: localization → log context → exception handler<br/>→ CORS → rate limiter → authN → authZ
    M->>K: bind CreateCarRequest (DataAnnotations)
    K->>P: sender.Send(CreateCarCommand)
    P->>P: validate · time · (cache for queries)
    P->>H: Handle(command)
    H->>D: Car.Create(...)
    alt invariant broken
        D-->>H: CarErrors.InvalidYear
        H-->>K: Result.Errors
        K-->>C: 400 ProblemDetails (Arabic title)
    else valid
        D-->>H: Car
        H->>DB: SaveChangesAsync (audit stamp + domain events)
        H-->>K: Result#lt;CarDto#gt;
        K-->>C: 201 Created + CarDto
    end
```

### Middleware order

```mermaid
flowchart LR
    A(["Request"]) --> B["Forwarded headers"] --> C["Request localization"] --> D["Log context"]
    D --> E["Exception handler"] --> F["Status code pages"] --> G["HTTPS redirect"]
    G --> H["Serilog request log"] --> I["CORS"] --> J["Rate limiter<br/><sub>sliding-window policy</sub>"]
    J --> K["Authentication"] --> L["Authorization"] --> M(["Controllers"])

    style A fill:#0F172A,stroke:#94A3B8,color:#F8FAFC
    style M fill:#7C3AED,stroke:#C4B5FD,color:#fff
    style J fill:#78350F,stroke:#FBBF24,color:#fff
    style K fill:#134E4A,stroke:#2DD4BF,color:#fff
    style L fill:#134E4A,stroke:#2DD4BF,color:#fff
```

> [!TIP]
> A controller action does three things: build the command, `await sender.Send(...)`,
> `return result.Match(ok, this.Problem)`. Anything more belongs in a handler or a behaviour.

<img src="assets/divider.svg" alt="" width="100%"/>

## 4 · Result pattern & errors

```csharp
public static Result<Car> Create(Guid id, string make, string model, int year, string descriptionEn, string descriptionAr)
{
    if (year < 1886)
    {
        return CarErrors.InvalidYear;          // an Error converts to a failed Result<Car>
    }

    return new Car(id, make, model, year, new LocalizedText(descriptionEn, descriptionAr));
}
```

### `ErrorKind` → HTTP status

```mermaid
flowchart LR
    R{{"Result#lt;T#gt;"}} -->|IsSuccess| OK["✅ 200 / 201 / 204"]
    R -->|IsError| E(("Error.Type"))
    E -->|Validation| S400["400 Bad Request"]
    E -->|Unauthorized| S401["401 Unauthorized"]
    E -->|Forbidden| S403["403 Forbidden"]
    E -->|NotFound| S404["404 Not Found"]
    E -->|Conflict| S409["409 Conflict"]
    E -->|Failure / Unexpected| S500["500 Server Error"]

    style OK fill:#064E3B,stroke:#34D399,color:#fff
    style R fill:#4C1D95,stroke:#C4B5FD,color:#fff
    style E fill:#1E293B,stroke:#94A3B8,color:#fff
    style S400 fill:#78350F,stroke:#FBBF24,color:#fff
    style S401 fill:#7C2D12,stroke:#FB923C,color:#fff
    style S403 fill:#7C2D12,stroke:#FB923C,color:#fff
    style S404 fill:#1E3A8A,stroke:#60A5FA,color:#fff
    style S409 fill:#581C87,stroke:#D8B4FE,color:#fff
    style S500 fill:#4C0519,stroke:#FB7185,color:#fff
```

### Same error, two languages

<table>
<tr>
<th width="50%">🇬🇧 <code>Accept-Language: en</code></th>
<th width="50%">🇸🇦 <code>Accept-Language: ar</code></th>
</tr>
<tr>
<td valign="top">

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
  "title": "Car not found",
  "status": 404,
  "instance": "GET /api/v1/cars/3f2a…",
  "requestId": "0HN7…:00000001"
}
```

</td>
<td valign="top">

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
  "title": "السيارة غير موجودة",
  "status": 404,
  "instance": "GET /api/v1/cars/3f2a…",
  "requestId": "0HN7…:00000001"
}
```

</td>
</tr>
</table>

### Validation, in three tiers

| | Tier | Lives in | Catches | Fails as |
|:-:|:--|:--|:--|:--|
| 1️⃣ | **Contract** | `DataAnnotations` on request DTOs | shape: required, length, format | 400, before MediatR runs |
| 2️⃣ | **Application** | FluentValidation beside each use case | ranges, cross-field rules | 400 `ValidationProblemDetails`, per property |
| 3️⃣ | **Domain** | guards inside `Create` / behaviour methods | invariants, for every caller | `Result` failure with `<Entity>Errors` |

<img src="assets/divider.svg" alt="" width="100%"/>

## 5 · Authentication & RBAC

```mermaid
sequenceDiagram
    autonumber
    actor U as Client
    participant A as IdentityController
    participant S as IdentityService
    participant T as TokenProvider
    participant DB as PostgreSQL

    rect rgba(124, 58, 237, 0.12)
    Note over U,DB: Sign in
    U->>A: POST /api/token/generate { email, password }
    A->>S: GenerateTokenCommand
    S->>DB: check user, password, roles
    S->>T: issue tokens
    T->>DB: replace refresh token (one live per user)
    T-->>U: accessToken (60 min) + refreshToken (7 days)
    end

    rect rgba(45, 212, 191, 0.12)
    Note over U,DB: Access token expired
    U->>A: POST /api/token/refresh-token { expiredAccessToken, refreshToken }
    A->>T: read claims from expired token
    T->>DB: match stored refresh token, check expiry
    T->>DB: rotate: new refresh token replaces the old one
    T-->>U: fresh accessToken + refreshToken
    end
```

### Refresh token lifecycle

```mermaid
stateDiagram-v2
    direction LR
    [*] --> Active: sign in
    Active --> Rotated: refresh-token call
    Rotated --> Active: new token issued
    Active --> Expired: 7 days pass
    Active --> Replaced: user signs in again
    Expired --> [*]
    Replaced --> [*]
```

| 🔑 Piece | How it works |
|:--|:--|
| **Access token** | JWT, HMAC-SHA256, lifetime from `JwtSettings:TokenExpirationInMinutes` (60 by default) |
| **Refresh token** | stored in the database, **one live token per user**, 7-day expiry, rotated on every refresh |
| **Roles (RBAC)** | ASP.NET Identity roles are written into the JWT as `role` claims, ready for `[Authorize(Roles = "...")]` |
| **Current user** | `IUser` gives handlers the user id; the id never comes from a request body |
| **Seed** | first run creates the `Manager` role and `admin@taxi.com` / `Admin123!` |

> [!CAUTION]
> The development JWT secret and the seeded admin password are public placeholders. Replace
> `JwtSettings:Secret` and the admin password before running anywhere other than your own machine.

<img src="assets/divider.svg" alt="" width="100%"/>

## 6 · English / Arabic

```mermaid
flowchart TB
    H["Accept-Language: ar"] --> RL["UseRequestLocalization<br/><sub>en · ar · default en</sub>"]
    RL --> LC["ILanguageContext<br/><sub>handlers read the language</sub>"]
    RL --> LOC["IStringLocalizer#lt;SharedResource#gt;"]

    subgraph DATA["Bilingual data"]
      direction LR
      DB[("JSONB column<br/>{ en: …, ar: … }")] --> LT["LocalizedText"] --> DTO["CarDto.Description<br/><sub>one string, in the asked language</sub>"]
    end

    subgraph MSG["Bilingual messages"]
      direction LR
      ERR["Error.Code<br/><sub>LocalizationKeys.Car.NotFound</sub>"] --> JSON["SharedResource.ar.json"] --> OUT["title: السيارة غير موجودة"]
    end

    LC --> DATA
    LOC --> MSG

    style H fill:#78350F,stroke:#FBBF24,color:#fff
    style OUT fill:#064E3B,stroke:#34D399,color:#fff
    style DTO fill:#064E3B,stroke:#34D399,color:#fff
```

<table>
<tr><th>Key</th><th>🇬🇧 English</th><th>🇸🇦 العربية</th></tr>
<tr><td><code>Car.NotFound</code></td><td>Car not found</td><td dir="rtl">السيارة غير موجودة</td></tr>
<tr><td><code>Car.Year.Invalid</code></td><td>Car year is invalid</td><td dir="rtl">سنة السيارة غير صالحة</td></tr>
<tr><td><code>Auth.RefreshToken.Expired</code></td><td>Refresh token expired</td><td dir="rtl">انتهت صلاحية رمز التنشيط</td></tr>
<tr><td><code>Auth.User.NotFound</code></td><td>User not found</td><td dir="rtl">المستخدم غير موجود</td></tr>
</table>

> [!NOTE]
> There is no English prose in the failure path. Every message is a `LocalizationKeys` constant
> that must exist in **both** `SharedResource.en.json` and `SharedResource.ar.json`.

<img src="assets/divider.svg" alt="" width="100%"/>

## 7 · Caching

```mermaid
flowchart LR
    Q["GetCarsQuery<br/><sub>implements ICachedQuery</sub>"] --> CB{"CachingBehavior<br/>key: cars_ar"}
    CB -->|hit| RET["⚡ return cached Result"]
    CB -->|miss| HD["Handler → PostgreSQL"] --> ST["store if success<br/><sub>tagged: cars</sub>"] --> RET2["return Result"]

    CMD["Create / Update / RemoveCar"] -.->|RemoveByTagAsync cars| CB

    style RET fill:#064E3B,stroke:#34D399,color:#fff
    style CB fill:#0C4A6E,stroke:#38BDF8,color:#fff
    style CMD fill:#4C0519,stroke:#FB7185,color:#fff
```

- **Opt-in per query** with `ICachedQuery<T>`: `CacheKey`, `Tags`, `Expiration`, `IsCultureAware`.
- **Split by language**: culture-aware keys become `{CacheKey}_{language}`, so Arabic and English never mix.
- **Only successes are cached.** Validation runs first, so bad input never reaches the cache.
- **Invalidation by tag**: every command that changes cached data calls `RemoveByTagAsync`.
- **HybridCache defaults**: 10-minute expiration, 30-second in-memory window.

<img src="assets/divider.svg" alt="" width="100%"/>

## 8 · Persistence

```mermaid
erDiagram
    AspNetUsers ||--o| RefreshTokens : "has one live"
    AspNetUsers }o--o{ AspNetRoles : "AspNetUserRoles"
    Cars {
        uuid Id PK
        string Make
        string Model
        int Year
        jsonb Description "LocalizedText { En, Ar }"
        timestamptz CreatedAtUtc
        string CreatedBy
        timestamptz LastModifiedUtc
        string LastModifiedBy
    }
    RefreshTokens {
        uuid Id PK
        string Token
        string UserId FK
        timestamptz ExpiresOnUtc
    }
    AspNetUsers {
        string Id PK
        string Email
    }
    AspNetRoles {
        string Id PK
        string Name "Manager"
    }
```

| ⚙️ Mechanism | What it does |
|:--|:--|
| `AppDbContext : IdentityDbContext<AppUser>` | Identity and business tables share one context and one transaction |
| `AuditableEntityInterceptor` | stamps `CreatedAtUtc`, `CreatedBy`, `LastModifiedUtc`, `LastModifiedBy` from `IUser` |
| `SaveChangesAsync` override | publishes domain events through MediatR before saving |
| `OwnsOne(...).ToJson()` | stores `LocalizedText` as one JSONB column |
| Startup migrations | applied with 5 retries in non-Production; switch with `Database:ApplyMigrationsOnStartup` |
| Npgsql resilience | retry on failure ×5, 60-second command timeout |

<img src="assets/divider.svg" alt="" width="100%"/>

## 9 · Observability

```mermaid
flowchart LR
    subgraph compose["🐳 docker compose up -d"]
      direction LR
      API["🌐 Taxi.Api<br/><b>:5001</b>"]
      PG[("🐘 PostgreSQL 16<br/><b>:5433</b>")]
      SEQ["📜 Seq<br/><b>:8081</b> UI · :5341 ingest"]
      PR["📈 Prometheus<br/><b>:9090</b>"]
      GR["📊 Grafana<br/><b>:3000</b>"]
    end

    API -->|EF Core| PG
    API -->|Serilog logs| SEQ
    API -->|OTLP traces| SEQ
    PR -->|scrapes /metrics| API
    GR -->|queries| PR

    style API fill:#4C1D95,stroke:#C4B5FD,color:#fff
    style PG fill:#1E3A8A,stroke:#60A5FA,color:#fff
    style SEQ fill:#134E4A,stroke:#2DD4BF,color:#fff
    style PR fill:#7C2D12,stroke:#FB923C,color:#fff
    style GR fill:#78350F,stroke:#FBBF24,color:#fff
```

| Signal | Source | Destination |
|:--|:--|:--|
| 📜 **Logs** | Serilog, with request log context | Console · rolling file · Seq |
| 🧵 **Traces** | OpenTelemetry: ASP.NET Core + HttpClient | Seq over OTLP |
| 📈 **Metrics** | OpenTelemetry: ASP.NET Core + HttpClient | `/metrics` → Prometheus → Grafana |
| 🐢 **Slow requests** | `PerformanceBehaviour` | warning log above 500 ms |

<img src="assets/divider.svg" alt="" width="100%"/>

## 10 · Feature workflow

Every new feature follows the same 17 steps, each one copying a real `Car` file. The full table
is in [AGENTS.md §3](../AGENTS.md).

```mermaid
flowchart LR
    subgraph S1["📜 Contracts"]
      direction TB
      a1["1 · LocalizationKeys"] --> a2["2 · en.json + ar.json"] --> a3["3 · Create/Update request"]
    end
    subgraph S2["💎 Domain"]
      direction TB
      b1["4 · Entity"] --> b2["5 · EntityErrors"]
    end
    subgraph S3["⚙️ Application"]
      direction TB
      c1["6 · DTO"] --> c2["7 · Mapper"] --> c3["8 · Commands"] --> c4["9 · Queries"] --> c5["10 · CacheTags"] --> c6["11 · IAppDbContext"]
    end
    subgraph S4["🏗️ Infrastructure"]
      direction TB
      d1["12 · DbSet"] --> d2["13 · Configuration"] --> d3["14 · Migration"]
    end
    subgraph S5["🌐 Api + checks"]
      direction TB
      e1["15 · Controller"] --> e2["16 · Domain tests"] --> e3["17 · requests.http"]
    end
    S1 --> S2 --> S3 --> S4 --> S5
```

### What a feature looks like on disk

```text
src/Taxi.Application/Features/Cars/
├── Commands/
│   ├── CreateCar/   CreateCarCommand · CreateCarCommandHandler · CreateCarCommandValidator
│   ├── UpdateCar/   UpdateCarCommand · UpdateCarCommandHandler · UpdateCarCommandValidator
│   └── RemoveCar/   RemoveCarCommand · RemoveCarCommandHandler
├── Queries/
│   ├── GetCarById/  GetCarByIdQuery · GetCarByIdQueryHandler · GetCarByIdQueryValidator
│   └── GetCars/     GetCarsQuery · GetCarsQueryHandler
├── Dtos/            CarDto
└── Mappers/         CarMapper
```

### Prompt templates for AI agents

| Template | Use it when |
|:--|:--|
| 🆕 [add-feature.md](../prompts/add-feature.md) | a new entity, full slice from contract to controller |
| ✏️ [modify-feature.md](../prompts/modify-feature.md) | adding a field or changing a rule |
| 🎯 [single-layer-change.md](../prompts/single-layer-change.md) | work that stays inside one project |
| 🐛 [fix-bug.md](../prompts/fix-bug.md) | something is broken |
| 📝 [update-docs.md](../prompts/update-docs.md) | syncing the docs after a code change |

<img src="assets/divider.svg" alt="" width="100%"/>

## 11 · Quality & CI

```mermaid
flowchart LR
    P(["git push / PR"]) --> R["dotnet restore"] --> B["dotnet build -c Release<br/><sub>StyleCop analyzers</sub>"] --> T["dotnet test"] --> G(["✅ green"])
    style P fill:#1E293B,stroke:#94A3B8,color:#fff
    style B fill:#4C1D95,stroke:#C4B5FD,color:#fff
    style T fill:#134E4A,stroke:#2DD4BF,color:#fff
    style G fill:#064E3B,stroke:#34D399,color:#fff
```

| Suite | Locks down |
|:--|:--|
| 🧪 `Taxi.Domain.UnitTests` | every `Car` and `RefreshToken` factory guard, `Result<T>` conversions |
| 🧪 `Taxi.Application.UnitTests` | MediatR pipeline order, `Result<T>` cache serialization round trip |

- **Central package management**: every version lives in `Directory.Packages.props`.
- **Shared build settings**: `net10.0`, nullable, implicit usings, StyleCop in `Directory.Build.props`.
- **Security check**: `dotnet list package --vulnerable --include-transitive`.

<img src="assets/divider.svg" alt="" width="100%"/>

## 12 · The whole map

```mermaid
mindmap
  root((Backend Template))
    Architecture
      Clean Architecture
      CQRS with MediatR
      Vertical slices
      Result pattern
    Security
      JWT bearer
      Rotating refresh tokens
      Role claims
      Rate limit policy
      Forwarded headers
    Data
      PostgreSQL 16
      EF Core 10
      JSONB LocalizedText
      Audit interceptor
      Domain events
    API
      Versioning v1
      ProblemDetails
      OpenAPI
      Swagger UI and Scalar
    Operations
      Docker Compose
      Serilog and Seq
      OpenTelemetry
      Prometheus and Grafana
    Team
      AGENTS.md recipe
      Layer blueprints
      REST naming rules
      Prompt templates
      xUnit and CI
```

<br/>

<div align="center">

<img src="assets/divider.svg" alt="" width="100%"/>

**Ready to build?** Start with the [README](../README.md), then open [AGENTS.md](../AGENTS.md).

<sub>Built by <a href="https://github.com/fat7i-alghareeb">Fathi Alghareeb</a> · .NET 10 · Clean Architecture · CQRS</sub>

</div>
