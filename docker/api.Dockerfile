# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src

COPY backend/ .
RUN dotnet restore
# MIGRATION: publish the API project explicitly (not the whole solution) so the
# runtime image contains only the DnnMigration.Api closure. A bare `dotnet publish`
# resolves to DnnMigration.sln and would emit the xunit/Moq/FluentAssertions test
# assemblies (DnnMigration.UnitTests/IntegrationTests) into /app/publish.
RUN dotnet publish src/DnnMigration.Api/DnnMigration.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS runtime
WORKDIR /app

RUN adduser -D -u 1000 appuser
USER appuser

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD wget --no-verbose --tries=1 --spider http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "DnnMigration.Api.dll"]
