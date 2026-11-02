# Build:  docker build -f src/Taxi.Api/Dockerfile -t taxi-api .
# Run:    docker compose up (see docker-compose*.yml) — the image listens on 8080 as a non-root user.

# ---- runtime base -----------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
# Uploaded files (FileStorage:LocalRootPath) live in /app/storage, owned by the non-root app user.
# In production a named volume overlays it and its ownership is fixed by the taxi.api-init service.
RUN mkdir -p /app/storage && chown $APP_UID /app/storage
USER $APP_UID

# ---- build ------------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
# Restore first, from the project files only, so the layer is cached until a dependency changes.
COPY ["Directory.Packages.props", "Directory.Build.props", "./"]
COPY ["src/Taxi.Api/Taxi.Api.csproj", "src/Taxi.Api/"]
COPY ["src/Taxi.Contracts/Taxi.Contracts.csproj", "src/Taxi.Contracts/"]
COPY ["src/Taxi.Application/Taxi.Application.csproj", "src/Taxi.Application/"]
COPY ["src/Taxi.Domain/Taxi.Domain.csproj", "src/Taxi.Domain/"]
COPY ["src/Taxi.Infrastructure/Taxi.Infrastructure.csproj", "src/Taxi.Infrastructure/"]
RUN dotnet restore "src/Taxi.Api/Taxi.Api.csproj"
COPY . .
RUN dotnet publish "src/Taxi.Api/Taxi.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false --no-restore

# ---- final ------------------------------------------------------------------------------------
FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .

# Liveness only (/health/live runs no dependency checks), so a database outage never restarts a
# healthy container. The runtime image has no curl/wget; bash's /dev/tcp needs nothing extra.
HEALTHCHECK --interval=30s --timeout=5s --start-period=60s --retries=3 \
    CMD bash -c 'exec 3<>/dev/tcp/127.0.0.1/8080 && printf "GET /health/live HTTP/1.0\r\nHost: localhost\r\n\r\n" >&3 && head -n 1 <&3 | grep -q " 200 "'

ENTRYPOINT ["dotnet", "Taxi.Api.dll"]
