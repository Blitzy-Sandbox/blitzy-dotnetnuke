# =============================================================================
# DnnMigration API Multi-Stage Dockerfile
# =============================================================================
# This Dockerfile creates an optimized, minimal Linux container for the 
# DnnMigration ASP.NET Core 8 BFF API. It uses multi-stage builds for:
# - Efficient layer caching
# - Smaller final image size
# - Security hardening with non-root user
#
# Build context should be the repository root (not backend/ folder)
# Usage: docker build -f docker/api.Dockerfile -t dnnmigration-api .
# =============================================================================

# -----------------------------------------------------------------------------
# Stage 1: BUILD
# -----------------------------------------------------------------------------
# Uses the full .NET 8 SDK to restore packages and compile the solution.
# Copies .csproj files first for optimal Docker layer caching - dependencies
# only re-download when project files change, not on every code change.
# -----------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

# Copy solution file first
COPY backend/DnnMigration.sln ./

# Copy all project files maintaining folder structure for efficient layer caching
# Domain layer
COPY backend/src/DnnMigration.Domain/DnnMigration.Domain.csproj ./src/DnnMigration.Domain/

# Application layer
COPY backend/src/DnnMigration.Application/DnnMigration.Application.csproj ./src/DnnMigration.Application/

# Infrastructure layer
COPY backend/src/DnnMigration.Infrastructure/DnnMigration.Infrastructure.csproj ./src/DnnMigration.Infrastructure/

# API layer
COPY backend/src/DnnMigration.Api/DnnMigration.Api.csproj ./src/DnnMigration.Api/

# Test projects
COPY backend/tests/DnnMigration.UnitTests/DnnMigration.UnitTests.csproj ./tests/DnnMigration.UnitTests/
COPY backend/tests/DnnMigration.IntegrationTests/DnnMigration.IntegrationTests.csproj ./tests/DnnMigration.IntegrationTests/

# Restore NuGet packages for all projects
# This layer is cached until any .csproj file changes
RUN dotnet restore DnnMigration.sln

# Copy the entire backend source code
COPY backend/. .

# Build the solution in Release mode with warnings treated as errors
# This ensures code quality and catches potential issues during build
RUN dotnet build DnnMigration.sln -c Release --no-restore --warnaserror

# -----------------------------------------------------------------------------
# Stage 2: TEST
# -----------------------------------------------------------------------------
# Runs all unit and integration tests to verify the build.
# Tests run in Release mode using the already-compiled binaries.
# This stage can be skipped in production builds using --target publish
# -----------------------------------------------------------------------------
FROM build AS test

# Run all tests with verbose output
# --no-build uses the binaries from the build stage
# --verbosity normal provides useful test execution feedback
RUN dotnet test --no-build -c Release --verbosity normal

# -----------------------------------------------------------------------------
# Stage 3: PUBLISH
# -----------------------------------------------------------------------------
# Creates the deployment-ready published output.
# Publishes only the API project with all its dependencies.
# -----------------------------------------------------------------------------
FROM build AS publish

# Publish the API project to /app/publish directory
# --no-build uses the already-compiled binaries
# -c Release ensures production-optimized output
RUN dotnet publish src/DnnMigration.Api/DnnMigration.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-build

# -----------------------------------------------------------------------------
# Stage 4: FINAL (Runtime)
# -----------------------------------------------------------------------------
# Minimal Alpine-based runtime image for production deployment.
# Contains only the .NET ASP.NET Core runtime, no SDK.
# Uses non-root user for security hardening.
# -----------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS final

# Set working directory
WORKDIR /app

# Expose the application port
# The API listens on port 8080 for HTTP traffic
EXPOSE 8080

# Set environment variables for ASP.NET Core
# ASPNETCORE_URLS: Configure Kestrel to listen on port 8080
# DOTNET_RUNNING_IN_CONTAINER: Optimize for container environment
ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    ASPNETCORE_ENVIRONMENT=Production

# Create a non-root user for security
# Running as non-root is a container security best practice
# This prevents potential container escape vulnerabilities
RUN adduser -D appuser

# Copy the published application from the publish stage
COPY --from=publish /app/publish .

# Change ownership of application files to the non-root user
RUN chown -R appuser:appuser /app

# Switch to non-root user
USER appuser

# Configure health check for container orchestration
# Uses wget (available in Alpine) to check the /health endpoint
# --interval: Time between health checks (30 seconds)
# --timeout: Maximum time for a health check to complete (3 seconds)
# --start-period: Grace period before first check (5 seconds)
# --retries: Number of consecutive failures before unhealthy (3)
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD wget --no-verbose --tries=1 --spider http://localhost:8080/health || exit 1

# Set the entry point for the container
# Runs the API using the dotnet runtime
ENTRYPOINT ["dotnet", "DnnMigration.Api.dll"]
