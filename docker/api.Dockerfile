# syntax=docker/dockerfile:1
#
# DnnMigration API — multi-stage Linux build for the .NET 8 ASP.NET Core Web API.
# Build context is the repository root (see docker/docker-compose.yml).
#
# NOTE (setup): Container image builds require the backend source (Program.cs, controllers,
# etc.) authored by file-implementation agents AND a Linux-capable Docker daemon. They cannot
# be built on the Windows-container CI host used during environment setup.

# ---- Build stage ----
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src

# Copy solution + project manifests first to maximize restore-layer caching.
COPY backend/global.json ./backend/
COPY backend/Directory.Build.props ./backend/
COPY backend/DnnMigration.sln ./backend/
COPY backend/src/DnnMigration.Domain/DnnMigration.Domain.csproj ./backend/src/DnnMigration.Domain/
COPY backend/src/DnnMigration.Application/DnnMigration.Application.csproj ./backend/src/DnnMigration.Application/
COPY backend/src/DnnMigration.Infrastructure/DnnMigration.Infrastructure.csproj ./backend/src/DnnMigration.Infrastructure/
COPY backend/src/DnnMigration.Api/DnnMigration.Api.csproj ./backend/src/DnnMigration.Api/
COPY backend/tests/DnnMigration.UnitTests/DnnMigration.UnitTests.csproj ./backend/tests/DnnMigration.UnitTests/
COPY backend/tests/DnnMigration.IntegrationTests/DnnMigration.IntegrationTests.csproj ./backend/tests/DnnMigration.IntegrationTests/
RUN dotnet restore ./backend/src/DnnMigration.Api/DnnMigration.Api.csproj

# Copy the remaining source and publish a framework-dependent build.
COPY backend/ ./backend/
RUN dotnet publish ./backend/src/DnnMigration.Api/DnnMigration.Api.csproj \
    -c Release -o /app/publish /p:UseAppHost=false

# ---- Runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS runtime
WORKDIR /app

# curl is used by the container HEALTHCHECK below.
RUN apk add --no-cache curl

COPY --from=build /app/publish ./

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true

EXPOSE 8080

# Validation Gate 7: GET /health must return HTTP 200.
HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
  CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "DnnMigration.Api.dll"]
