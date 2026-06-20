# syntax=docker/dockerfile:1
# MIGRATION: Multi-stage build for DnnMigration.Api (.NET 8 LTS, Linux/Alpine).
# Build context is the repository root (see docker/docker-compose.yml).

# ---- build stage ----
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src

# Restore layer: copy solution + project files first for better layer caching.
COPY backend/DnnMigration.sln ./
COPY backend/Directory.Build.props ./
COPY backend/global.json ./
COPY backend/src/DnnMigration.Domain/DnnMigration.Domain.csproj src/DnnMigration.Domain/
COPY backend/src/DnnMigration.Application/DnnMigration.Application.csproj src/DnnMigration.Application/
COPY backend/src/DnnMigration.Infrastructure/DnnMigration.Infrastructure.csproj src/DnnMigration.Infrastructure/
COPY backend/src/DnnMigration.Api/DnnMigration.Api.csproj src/DnnMigration.Api/
RUN dotnet restore src/DnnMigration.Api/DnnMigration.Api.csproj

# Copy remaining sources and publish a framework-dependent build.
COPY backend/ ./
RUN dotnet publish src/DnnMigration.Api/DnnMigration.Api.csproj \
    -c Release -o /app/publish /p:UseAppHost=false

# ---- runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS final
WORKDIR /app

# Run as a non-root user.
RUN addgroup -S app && adduser -S app -G app
COPY --from=build /app/publish ./
USER app

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true
EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
    CMD wget --quiet --tries=1 --spider http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "DnnMigration.Api.dll"]
