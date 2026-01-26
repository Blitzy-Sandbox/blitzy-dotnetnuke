# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src

# Copy csproj files and restore dependencies
COPY src/DnnMigration.Domain/DnnMigration.Domain.csproj ./src/DnnMigration.Domain/
COPY src/DnnMigration.Application/DnnMigration.Application.csproj ./src/DnnMigration.Application/
COPY src/DnnMigration.Infrastructure/DnnMigration.Infrastructure.csproj ./src/DnnMigration.Infrastructure/
COPY src/DnnMigration.Api/DnnMigration.Api.csproj ./src/DnnMigration.Api/
COPY DnnMigration.sln ./

RUN dotnet restore

# Copy everything else and build
COPY . .
RUN dotnet publish src/DnnMigration.Api/DnnMigration.Api.csproj -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS runtime
WORKDIR /app

# Install curl for healthchecks
RUN apk add --no-cache curl

# Copy published output
COPY --from=build /app/publish .

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
  CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "DnnMigration.Api.dll"]
