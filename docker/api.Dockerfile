# syntax=docker/dockerfile:1
# =============================================================================
# api.Dockerfile — DnnMigration.Api (ASP.NET Core 8) multi-stage Linux image
# Build context: repository root (compose `context: ..`; or `docker build -f docker/api.Dockerfile .`)
# =============================================================================

# ---- Stage 1: build & publish -----------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src

# Copy shared props + csproj first for restore-layer caching.
COPY backend/Directory.Build.props backend/
COPY backend/src/DnnMigration.Domain/DnnMigration.Domain.csproj backend/src/DnnMigration.Domain/
COPY backend/src/DnnMigration.Application/DnnMigration.Application.csproj backend/src/DnnMigration.Application/
COPY backend/src/DnnMigration.Infrastructure/DnnMigration.Infrastructure.csproj backend/src/DnnMigration.Infrastructure/
COPY backend/src/DnnMigration.Api/DnnMigration.Api.csproj backend/src/DnnMigration.Api/
RUN dotnet restore backend/src/DnnMigration.Api/DnnMigration.Api.csproj

# Copy the remaining source and publish the API project (pulls in referenced projects).
COPY backend/ backend/
RUN dotnet publish backend/src/DnnMigration.Api/DnnMigration.Api.csproj \
        -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---- Stage 2: runtime -------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS final
WORKDIR /app

# curl for the container HEALTHCHECK (alpine base has no curl).
RUN apk add --no-cache curl

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true

EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=10s --start-period=30s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

USER $APP_UID

ENTRYPOINT ["dotnet", "DnnMigration.Api.dll"]
