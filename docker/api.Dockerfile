# MIGRATION: Multi-stage Dockerfile for the ASP.NET Core 8 BFF Web API (DnnMigration.Api),
# replacing the legacy DotNetNuke Windows/IIS deployment of DotNetNuke.dll (legacy build automation:
# Website/DotNetNuke.build; legacy runtime config: Website/release.config). The BUILD CONTEXT is the
# repository ROOT, so all COPY source paths are relative to the repo root (backend/...). Usage:
#   docker build -f docker/api.Dockerfile -t dnnmigration-api .
#   (compose) services.api.build = { context: .., dockerfile: docker/api.Dockerfile }

# ---------- Stage 1: build & publish ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /source

# Restore layer-caching: copy ONLY the .csproj files first so `dotnet restore` is cached unless a
# project file changes. Restoring the Api project transitively restores its ProjectReferences
# (Domain, Application, Infrastructure). The test projects under backend/tests are intentionally
# NOT restored or published into the runtime image. (Paths below resolve under /source, mirroring
# the backend solution's own src/ subfolder => /source/src/<Project>/.)
COPY ["backend/src/DnnMigration.Domain/DnnMigration.Domain.csproj", "src/DnnMigration.Domain/"]
COPY ["backend/src/DnnMigration.Application/DnnMigration.Application.csproj", "src/DnnMigration.Application/"]
COPY ["backend/src/DnnMigration.Infrastructure/DnnMigration.Infrastructure.csproj", "src/DnnMigration.Infrastructure/"]
COPY ["backend/src/DnnMigration.Api/DnnMigration.Api.csproj", "src/DnnMigration.Api/"]
RUN dotnet restore "src/DnnMigration.Api/DnnMigration.Api.csproj"

# Copy the remaining backend source, then publish (already restored above => --no-restore).
COPY backend/ .
RUN dotnet publish "src/DnnMigration.Api/DnnMigration.Api.csproj" \
        -c Release \
        -o /app/publish \
        --no-restore \
        /p:UseAppHost=false

# ---------- Stage 2: runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS final
WORKDIR /app

# curl is required by the docker-compose healthcheck (curl -f http://localhost:8080/health).
# MIGRATION (QA-1 Issue #1, CRITICAL fix): icu-libs supplies ICU on the Alpine base image. The API now runs with
# globalization ENABLED (InvariantGlobalization=false in DnnMigration.Api.csproj) because Microsoft.Data.SqlClient
# resolves a CultureInfo while opening a SQL connection, which throws CultureNotFoundException under invariant mode
# and broke ALL SQL data access and the auth login. The aspnet:8.0-alpine image ships no full ICU, so without
# icu-libs the published app would fail to start once invariant mode is off. Documented in MIGRATION_NOTES.md §14.
# Create and run as a non-root user (security best practice; port 8080 is unprivileged).
RUN apk add --no-cache curl icu-libs \
    && addgroup -S appgroup \
    && adduser -S appuser -G appgroup

COPY --from=build --chown=appuser:appgroup /app/publish .

# Kestrel listens on HTTP :8080 (no in-container TLS; HTTPS is terminated upstream — matches
# Program.cs). Globalization is ENABLED (InvariantGlobalization=false); ICU is provided by the icu-libs
# package installed above so culture-dependent code (Microsoft.Data.SqlClient connection-open) works on Alpine.
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true

EXPOSE 8080
USER appuser

ENTRYPOINT ["dotnet", "DnnMigration.Api.dll"]
