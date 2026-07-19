# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081
# Make wwwroot writable for the non-root app user. Individual runtime-created
# folders are created as needed by the app — no folder needs baking in here.
# In production a named volume overlays wwwroot and its ownership is fixed by
# the taxi.api-init compose service.
RUN mkdir -p /app/wwwroot && chmod -R 777 /app/wwwroot
USER $APP_UID


# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Directory.Packages.props", "."]
COPY ["Directory.Build.props", "."]
COPY ["src/Taxi.Api/Taxi.Api.csproj", "src/Taxi.Api/"]
COPY ["src/Taxi.Client/Taxi.Client.csproj", "src/Taxi.Client/"]
COPY ["src/Taxi.Contracts/Taxi.Contracts.csproj", "src/Taxi.Contracts/"]
COPY ["src/Taxi.Application/Taxi.Application.csproj", "src/Taxi.Application/"]
COPY ["src/Taxi.Domain/Taxi.Domain.csproj", "src/Taxi.Domain/"]
COPY ["src/Taxi.Infrastructure/Taxi.Infrastructure.csproj", "src/Taxi.Infrastructure/"]
RUN dotnet restore "./src/Taxi.Api/Taxi.Api.csproj"
COPY . .
WORKDIR "/src/src/Taxi.Api"
RUN dotnet build "./Taxi.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Taxi.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
# The publish copy overlays a root-owned wwwroot (read-only for $APP_UID), so the
# app can't create runtime upload sub-dirs there. Restore write access after the
# copy, then drop back to the non-root app user.
# NOTE: in production /app/wwwroot is a persisted named volume that overlays this
# layer and keeps its OWN ownership, so this build-time chmod does not reach it —
# runtime write access there is guaranteed by the `taxi.api-init` compose service.
USER root
RUN mkdir -p /app/wwwroot && chmod -R 777 /app/wwwroot
USER $APP_UID
ENTRYPOINT ["dotnet", "Taxi.Api.dll"]
