# TAXI_Server

Enterprise Taxi Management System built with .NET 10, Clean Architecture, and CQRS.

## 🚀 Quick Start

### Prerequisites

- .NET 10 SDK
- Docker Desktop (for PostgreSQL and Seq)

### 1. Provision Infrastructure

```bash
docker-compose up -d
```

### 2. Update Database

The application automatically runs migrations and seeds initial data (Admin user) on startup.

### 3. Run the API

The application automatically runs migrations and seeds initial data (Admin user) on startup.

- **Application**: CQRS Handlers, MediatR Pipelines, and Use Case logic.
- **Infrastructure**: EF Core, Identity, Caching, and external service implementations.
- **Contracts**: Shared DTOs, Requests, and Responses.
- **Api**: REST Controllers, Middleware, and API configuration.
- **Client**: Blazor WebAssembly frontend.

For deep architectural details, refer to [ARCHITECTURE.md](./ARCHITECTURE.md).

## 🛠️ Tech Stack

- **Backend**: .NET 10, MediatR, FluentValidation, EF Core (PostgreSQL)
- **Frontend**: Blazor WebAssembly
- **Observability**: Serilog, Seq, OpenTelemetry
- **Documentation**: Scalar, Swagger (OpenAPI)
- **Real-time**: SignalR

## 🔒 Identity & Security

- **Authentication**: JWT-based with Refresh Tokens.
- **Authorization**: Role-based (Admin, Driver, Customer) and Policy-based.
- **Seeding**: Default Admin user is created on first run (`admin@taxi.com` / `Admin123!`).

## 🌍 Localization

The system supports bilingual data (English/Arabic) using JSON-based localization and a custom `LocalizedText` value object for database fields.
