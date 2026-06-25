# DnnMigration — DotNetNuke 4.x → .NET 8 + Angular 19

A complete, full-rewrite migration of the legacy **DotNetNuke 4.x** VB.NET CMS framework
(.NET Framework 2.0 / ASP.NET Web Forms) into a modern, decoupled
**ASP.NET Core 8 Backend-for-Frontend (BFF) Web API** (`DnnMigration`) and an
**Angular 19 single-page application (SPA)**. The migration preserves the
portal / module / user / role / permission functionality and maps to the
**existing SQL Server schema** without altering table structures.

> Project codename: **DnnMigration**. Database name: **DotNetNuke** (schema preserved).

## Table of Contents

- [Overview](#overview)
- [Architecture](#architecture)
- [Tech Stack](#tech-stack)
- [Repository Structure](#repository-structure)
- [Prerequisites](#prerequisites)
- [Backend](#backend)
- [Frontend](#frontend)
- [Docker Deployment](#docker-deployment)
- [API Reference](#api-reference)
- [Validation Gates](#validation-gates)
- [Troubleshooting](#troubleshooting)
- [Migration Notes and Documentation](#migration-notes-and-documentation)

## Overview

The application is split into two independently deployable tiers that communicate
exclusively over a versioned JSON REST contract. The Angular SPA never talks to the
database directly; every request flows through the API, which delegates business rules to
Application services and data access to EF Core 8 repositories. **Multi-tenant isolation is
preserved** — every entity and query remains scoped by `PortalId`, so each portal continues
to behave as an independent site.

## Architecture

```
Browser
  │  HTTPS
  ▼  Angular 19 SPA (nginx) — standalone components, signals, typed reactive forms
  │  REST / JSON over /api/v1/...
  ▼  ASP.NET Core 8 BFF API (Kestrel) — Controllers → Application Services
  ▼  EF Core 8 Repositories — DnnDbContext (Fluent API → legacy tables/columns)
  ▼  SQL Server — existing schema (Database: DotNetNuke)
```

The backend follows Clean / Onion architecture with the dependency direction
**Domain → Application → Infrastructure → Api**: no business logic lives in controllers,
and all data access sits behind repository interfaces.

## Tech Stack

**Backend** — C# 12 on .NET 8 LTS (`net8.0`) with nullable reference types:

- ASP.NET Core 8 Web API (REST/JSON, URL-path versioning `/api/v1/...`)
- EF Core 8 — SqlServer provider `8.0.11` (Code-First mapped to the existing schema)
- Auth: JWT Bearer (`Microsoft.AspNetCore.Authentication.JwtBearer` `8.0.11`) + BCrypt hashing (`BCrypt.Net-Next` `4.0.3`)
- AutoMapper `12.0.1`, FluentValidation `11.3.0`, Serilog (`Serilog.AspNetCore` `8.0.3`, `Serilog.Sinks.Console` `6.0.0`)
- Swashbuckle.AspNetCore `6.9.0` — OpenAPI 3.0 / Swagger UI

**Frontend** — Angular 19 SPA:

- Standalone components (no NgModules), signals, `inject()` DI, OnPush change detection
- Typed Reactive Forms, lazy-loaded feature routes
- RxJS `^7.8.1`, zone.js `^0.15.0`, TypeScript `^5.6`; Karma + Jasmine tests (ChromeHeadless)

**Infrastructure** — Docker multi-stage builds for Linux:

- API: `mcr.microsoft.com/dotnet/sdk:8.0` (build) → `mcr.microsoft.com/dotnet/aspnet:8.0-alpine` (runtime)
- Frontend: `node:20-alpine` (build) → `nginx:alpine` (serve)
- `docker-compose` orchestration (API on `8080`, frontend on `4200`)

## Repository Structure

```
.
├── backend/                          # ASP.NET Core 8 BFF API — DnnMigration.sln (Clean/Onion)
│   ├── src/
│   │   ├── DnnMigration.Domain/         # Entities, repository interfaces, enums
│   │   ├── DnnMigration.Application/    # Services, DTOs, AutoMapper profiles, validators
│   │   ├── DnnMigration.Infrastructure/ # EF Core 8 DbContext, repositories, JWT + BCrypt
│   │   └── DnnMigration.Api/            # Controllers, middleware, Program.cs, appsettings
│   └── tests/
│       ├── DnnMigration.UnitTests/         # Service + validator unit tests (xUnit + Moq)
│       └── DnnMigration.IntegrationTests/  # API integration tests (Mvc.Testing + EF InMemory)
├── frontend/                         # Angular 19 SPA — src/app/{core,shared,features,layout}, environments/
├── docker/                           # api.Dockerfile, frontend.Dockerfile, docker-compose.yml, nginx.conf
├── docs/                             # index.md, project-guide.md, technical-specifications.md
├── MIGRATION_NOTES.md                # Migration decision log
├── catalog-info.yaml, mkdocs.yml     # Repository metadata (unchanged)
├── Library/                          # LEGACY DNN 4.x VB.NET core — reference only (not built/deployed)
├── Website/                          # LEGACY DNN 4.x Web Forms  — reference only (not built/deployed)
└── DotNetNuke.sln, DotNetNuke_VS2008.sln   # LEGACY solutions — reference only
```

> The active application is `backend/` + `frontend/` + `docker/`. The legacy `Library/` and
> `Website/` trees (and the `DotNetNuke*.sln` files) are retained **only** as a migration
> reference — they are not compiled, tested, or deployed.

## Prerequisites

| Requirement | Version | Purpose |
|-------------|---------|---------|
| .NET SDK | 8.0 LTS | Backend development and build |
| Node.js | 20.x LTS | Frontend development and build |
| npm | 10.x | Package management |
| Docker | 24.x+ | Container builds |
| Docker Compose | 2.x | Multi-container orchestration |
| SQL Server | 2019+ | Database (or use containerized) |

## Backend

All commands are run from the repository root.

```bash
cd backend
dotnet restore DnnMigration.sln                                          # Restore NuGet packages
dotnet build DnnMigration.sln --configuration Release --warnaserror      # Gate 1 → 0 warnings / 0 errors
dotnet test DnnMigration.sln --configuration Release                     # Gate 2 → 100% pass
cd src/DnnMigration.Api && dotnet run --configuration Release            # Run the API
```

The API starts on `http://localhost:5000` in local development (the Docker container exposes
port `8080`); the health endpoint is available at `/health`.

### Configuration

Provide runtime settings in `backend/src/DnnMigration.Api/appsettings.Development.json`
(`ConnectionStrings`, `Jwt`, `Logging`). **Use the connection-string key defined by the API's
`appsettings.json`** (see `backend/src/DnnMigration.Api/`) — match the key name in the
committed file rather than assuming one. The example below is illustrative:

```json
{
  "ConnectionStrings": {
    "Default": "Server=localhost;Database=DotNetNuke;User Id=sa;Password=<your-password>;TrustServerCertificate=true"
  },
  "Jwt": {
    "Secret": "<256-bit-secret-min-32-chars-from-env-or-secret-manager>",
    "Issuer": "DnnMigration",
    "Audience": "DnnMigration",
    "ExpirationMinutes": 60
  },
  "Logging": { "LogLevel": { "Default": "Information", "Microsoft.AspNetCore": "Warning" } }
}
```

> **Never commit real secrets.** The JWT `Secret` must be a **≥ 32-character (256-bit)** value
> supplied via an environment variable or a secret manager (e.g. Azure Key Vault, AWS Secrets
> Manager). The database name is `DotNetNuke`.

## Frontend

```bash
cd frontend
npm install                                                          # or `npm ci` for clean, lockfile-exact installs (Gate 3)
npm test -- --watch=false --browsers=ChromeHeadless --code-coverage  # Gate 4 → 100% pass
npm run build -- --configuration production                          # Gate 3 → output: dist/dnn-migration/browser
npm start                                                            # Dev server → http://localhost:4200
```

> The frontend API base URL is configured in `frontend/src/environments/environment*.ts` (the
> `apiUrl` property). The backend CORS policy must allow the Angular origin.

## Docker Deployment

```bash
cd docker
docker-compose build                    # Gate 6 → both images built
docker-compose up -d                    # Gate 7 → all services running
curl -f http://localhost:8080/health    # → HTTP 200
```

A healthy API returns HTTP 200 with a JSON body such as `{"status":"Healthy","version":"1.0.0.0"}`.
Service ports: **API `8080`**, **frontend `4200`** (`http://localhost:4200`).

## API Reference

REST/JSON, URL-path versioned under `/api/v1/...`, with an OpenAPI 3.0 contract exposed through
Swagger UI.

| Endpoint | Methods | Description |
|----------|---------|-------------|
| `/api/portals` | GET, POST | Portal list and creation |
| `/api/portals/{id}` | GET, PUT, DELETE | Portal CRUD by ID |
| `/api/modules` | GET, POST | Module list and creation |
| `/api/modules/{id}` | GET, PUT, DELETE | Module CRUD by ID |
| `/api/users` | GET, POST | User list and creation |
| `/api/users/{id}` | GET, PUT, DELETE | User CRUD by ID |
| `/api/roles` | GET, POST | Role list and creation |
| `/api/roles/{id}` | GET, PUT, DELETE | Role CRUD by ID |
| `/api/tabs` | GET, POST | Tab/Page list and creation |
| `/api/tabs/{id}` | GET, PUT, DELETE | Tab CRUD by ID |
| `/api/auth/login` | POST | User authentication |
| `/api/auth/refresh` | POST | Token refresh (rotation) |
| `/api/auth/logout` | POST | User logout |
| `/api/auth/me` | GET | Current user info |
| `/health` | GET | Health check endpoint |

**Response envelopes** — success: `{ "data": { ... }, "meta": { ... } }`; error (RFC 7807
`ProblemDetails`): `{ "type", "title", "status", "detail", "errors": { } }`. **CRUD status codes**
(Gate 5): `POST → 201`, `GET → 200`, `PUT → 200`, `DELETE → 204`.

## Validation Gates

The migration is complete only when all seven gates pass.

| Gate | Name | Command | Pass Condition |
|------|------|---------|----------------|
| 1 | API Compilation | `dotnet build --configuration Release --warnaserror` | Exit 0; zero errors/warnings (excluding CS8618 nullable) |
| 2 | API Unit Tests | `dotnet test --configuration Release --no-build` | Exit 0; 100% pass |
| 3 | Angular Build | `npm ci` + `ng build --configuration production` | Exit 0; zero errors/warnings |
| 4 | Angular Unit Tests | `ng test --watch=false --browsers=ChromeHeadless --code-coverage` | Exit 0; 100% pass |
| 5 | API Integration Tests | `dotnet test --filter "Category=Integration"` | POST→201, GET→200, PUT→200, DELETE→204 (Portal, Module, User) |
| 6 | Container Build | `docker-compose build` | Exit 0; both images built |
| 7 | Container Startup | `docker-compose up -d` + `curl -f http://localhost:8080/health` and `http://localhost:4200` | Both return HTTP 200 |

## Troubleshooting

| Issue | Cause | Solution |
|-------|-------|----------|
| `dotnet: command not found` | .NET SDK not installed | Install the .NET 8 SDK |
| `npm: command not found` | Node.js not installed | Install Node.js 20 LTS |
| Connection string error | Database not configured | Update `appsettings.json` |
| CORS errors | API URL mismatch | Update `environment.ts` `apiUrl` |
| Health check 401 | Missing `AllowAnonymous` | Already fixed in the codebase |
| Chrome not found | ChromeHeadless missing | Install Chrome / Chromium |

## Migration Notes and Documentation

- [`MIGRATION_NOTES.md`](MIGRATION_NOTES.md) — migration decisions, conventions, and documented-but-unfixed legacy behavior (annotated with `// MIGRATION:` comments in code).
- [`docs/project-guide.md`](docs/project-guide.md) — detailed build/run/test/docker guide, architecture overview, and human task list.
- [`docs/technical-specifications.md`](docs/technical-specifications.md) — full technical specification: scope, dependency versions, and validation gates.

**Migration status / legacy code.** The active application lives entirely in `backend/`,
`frontend/`, and `docker/`. The legacy `Library/` and `Website/` VB.NET sources are kept as a
reference for behavioral parity only and are excluded from the build and deployment of the
migrated stack.
