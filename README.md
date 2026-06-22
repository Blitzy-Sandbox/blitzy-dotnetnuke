# DnnMigration

> DotNetNuke 4.x VB.NET to .NET 8 + Angular 19 complete migration

A complete, ground-up rewrite of the legacy **DotNetNuke (DNN) `4.9.0.85`** portal
framework — originally **VB.NET / ASP.NET Web Forms on .NET Framework 2.0** — into two
new, independently deployable applications:

1. **`DnnMigration.Api`** — an **ASP.NET Core 8 Web API** written in **C# 12**, following
   **Clean (Onion) Architecture** and the **Backend-for-Frontend (BFF)** pattern.
2. **`DnnMigration` SPA** — an **Angular 19** single-page application built entirely from
   **standalone components** (no `NgModule`s), using signals and typed reactive forms.

Both applications are packaged as a **two-container Docker Compose topology** that runs on
Linux (multi-stage Alpine images fronted by nginx).

The migration preserves the core domain logic and achieves functional parity for the
**Portal**, **Module**, and **User** management subsystems together with the **Role**,
**Permission**, and **Tab/Page** subsystems. The single sanctioned behavior change is the
security upgrade from Forms Authentication + DES to **JWT Bearer tokens + BCrypt** password
hashing.

> **Legacy code is retained for reference only.** The original `Library/` and `Website/`
> VB.NET trees remain in this repository as **read-only source/reference material**; they
> are **not compiled** and emit **no target artifacts**. All new work lives under
> `backend/`, `frontend/`, and `docker/`.

## Tech Stack

| Tier | Technologies |
|------|--------------|
| **Backend** | .NET 8 LTS · C# 12 · ASP.NET Core 8 Web API · EF Core 8 (Code-First, Fluent API) · AutoMapper · FluentValidation · Serilog · JWT Bearer auth · BCrypt password hashing |
| **Frontend** | Angular 19 (standalone components, signals, typed reactive forms) · TypeScript 5.6 · RxJS 7.8 · component-scoped SCSS |
| **Deployment** | Docker (multi-stage, Alpine images) · nginx (SPA host + `/api/*` reverse proxy + CSP) · Docker Compose |
| **Data** | SQL Server 2019+ (existing DNN schema mapped unchanged per ADR-002) |

## Table of Contents

- [Repository Layout](#repository-layout)
- [Prerequisites](#prerequisites)
- [Backend: Setup, Build and Run](#backend-setup-build-and-run)
- [Frontend: Setup, Build and Run](#frontend-setup-build-and-run)
- [Docker: Build and Run](#docker-build-and-run)
- [API Overview](#api-overview)
- [Testing and Validation Gates](#testing-and-validation-gates)
- [Further Reading](#further-reading)

## Repository Layout

```text
.
├── backend/                     # .NET 8 Clean Architecture solution (NEW)
│   ├── DnnMigration.sln
│   ├── Directory.Build.props
│   ├── global.json              # pins the .NET 8 SDK band
│   ├── src/
│   │   ├── DnnMigration.Domain/          # POCO entities, enums, repository interfaces (zero framework deps)
│   │   ├── DnnMigration.Application/      # services, DTOs, AutoMapper profiles, FluentValidation validators
│   │   ├── DnnMigration.Infrastructure/   # EF Core DbContext, repositories, JWT + BCrypt identity
│   │   └── DnnMigration.Api/              # REST controllers, middleware, Program.cs, appsettings.json
│   └── tests/
│       ├── DnnMigration.UnitTests/        # xUnit + Moq + FluentAssertions
│       └── DnnMigration.IntegrationTests/ # Mvc.Testing + EF Core InMemory
├── frontend/                    # Angular 19 standalone-component SPA (NEW)
│   ├── package.json
│   ├── angular.json
│   ├── tsconfig*.json
│   └── src/app/
│       ├── core/                # auth (service, guard, interceptor), api.service, models
│       ├── shared/              # data-table, form-controls, confirmation-dialog, loading-spinner, pipes, directives
│       ├── features/            # portal, module, user, role, auth feature slices (lazy-loaded)
│       └── layout/              # header, sidebar, footer application shell
├── docker/                      # Two-container deployment (NEW)
│   ├── api.Dockerfile           # multi-stage: sdk:8.0-alpine -> aspnet:8.0-alpine
│   ├── frontend.Dockerfile      # node build -> nginx:alpine
│   ├── nginx.conf               # SPA serve + /api/* reverse proxy + CSP
│   └── docker-compose.yml       # api + frontend services with HEALTHCHECK wiring
├── docs/                        # Technical specification and project guide
│   ├── technical-specifications.md
│   ├── project-guide.md
│   └── index.md
├── Library/                     # LEGACY DNN VB.NET source — read-only reference (not built)
├── Website/                     # LEGACY DNN Web Forms site — read-only reference (not built)
├── MIGRATION_NOTES.md           # Minimal Change Clause deviation log
└── README.md                    # You are here
```

## Prerequisites

| Requirement | Version | Purpose |
|-------------|---------|---------|
| .NET SDK | **8.0 LTS** | Backend build, test, and run |
| Node.js | **20.x LTS** | Frontend build and tooling |
| npm | **10.x** | Frontend package management |
| Docker | **24.x+** | Container image builds |
| Docker Compose | **2.x** | Multi-container orchestration |
| SQL Server | **2019+** | Database (local instance or containerized) |
| Chrome / Chromium | latest | Required by `ChromeHeadless` for Angular unit tests |

## Backend: Setup, Build and Run

All backend commands run from the `backend/` directory.

```bash
cd backend

# 1. Restore NuGet packages (all six projects).
dotnet restore DnnMigration.sln

# 2. Build in Release with warnings treated as errors.
#    Expect 0 warnings and 0 errors. (CS8618 non-nullable warnings are
#    intentionally excluded via Directory.Build.props, per the AAP gate.)
dotnet build DnnMigration.sln --configuration Release --warnaserror

# 3. Run the Web API. It listens on http://localhost:5000 by default.
#    Swagger UI is served in the Development environment; the health
#    probe is available at /health.
dotnet run --project src/DnnMigration.Api
```

### Backend Configuration

Runtime configuration lives in **`backend/src/DnnMigration.Api/appsettings.json`** and is
overridden per environment by **`appsettings.Development.json`**. These files carry the
database connection string under `ConnectionStrings:Default` and a `Jwt` section
(`Issuer = DnnMigration`, `Audience = DnnMigration`, a signing `Key`, and a 60-minute
access-token lifetime).

> These settings are the modern .NET 8 successors to the legacy
> `Website/development.config` and `Website/release.config` files — the only configuration
> sources this migration uses.

An illustrative `appsettings.Development.json` (replace every placeholder with your own
value; **never commit real secrets**):

```json
{
  "ConnectionStrings": {
    "Default": "Server=localhost;Database=DotNetNuke;User Id=sa;Password=CHANGE_ME_LOCAL_DEV_ONLY;TrustServerCertificate=true;MultipleActiveResultSets=true"
  },
  "Jwt": {
    "Issuer": "DnnMigration",
    "Audience": "DnnMigration",
    "Key": "REPLACE_WITH_A_LOCAL_DEV_SIGNING_KEY_OF_AT_LEAST_32_BYTES",
    "AccessTokenExpirationMinutes": 60,
    "RefreshTokenExpirationDays": 7
  }
}
```

> The JWT signing key must be **at least 32 bytes**. In containers and CI, supply these
> values through the `ConnectionStrings__Default` and `Jwt__Key` environment variables
> instead of editing the JSON files.

## Frontend: Setup, Build and Run

All frontend commands run from the `frontend/` directory.

```bash
cd frontend

# 1. Install dependencies.
npm install

# 2. Start the dev server (hot reload) on http://localhost:4200.
#    Equivalent to `ng serve`; API calls are proxied to the backend.
npm start

# 3. Production build. Output is emitted to dist/dnn-migration/browser.
ng build --configuration production

# 4. Run unit tests once in headless Chrome (CI mode).
npm test -- --watch=false --browsers=ChromeHeadless
```

> Configure the API base URL the SPA talks to via `src/environments/environment.ts` (the
> `apiUrl` property); the production override lives in `environment.prod.ts`.

## Docker: Build and Run

The deployment is a two-container topology — an **`api`** service and a **`frontend`**
service — orchestrated by Docker Compose. All commands run from the `docker/` directory.

```bash
cd docker

# Build both images (multi-stage; Alpine-based).
docker-compose build

# Start both services in the background.
docker-compose up -d

# Verify the API is healthy.
curl -f http://localhost:8080/health
# => {"status":"Healthy","timestamp":"<ISO8601 UTC>"}
```

Once running:

- **API** is published on `http://localhost:8080` (container port `8080`).
- **Frontend** (the Angular SPA served by nginx) is published on `http://localhost:4200`.

The `frontend` service waits for the `api` service to report **healthy** before it starts,
and both services declare a Docker `HEALTHCHECK`. The API container receives its secrets
through the `ConnectionStrings__Default` and `Jwt__Key` environment variables only — these are
the sole values injected into the container (see `docker/docker-compose.yml`). The JWT `Issuer`
and `Audience` are **not** container environment variables; they are baked into `appsettings.json`
(both default to `DnnMigration`). No database container is bundled, so point
`ConnectionStrings__Default` at a reachable SQL Server instance at deploy time.

> **Required secrets (no defaults).** `ConnectionStrings__Default` and `Jwt__Key` are **mandatory** —
> the compose file ships **no** fallback values, so `docker-compose build`/`up` (and even
> `docker-compose config`) **fail fast** with an explicit message if either is unset or empty. This
> prevents a deployment from ever silently starting with a known placeholder DB password or JWT
> signing key. Supply them via the host environment, an `.env` file in `docker/`, `--env-file`, or a
> secret store; `Jwt__Key` must be **at least 32 characters**. For example:
>
> ```bash
> export ConnectionStrings__Default="Server=db.example.com,1433;Database=DotNetNuke;User Id=app;Password=<strong-secret>;TrustServerCertificate=True;MultipleActiveResultSets=True"
> export Jwt__Key="<a-strong-random-key-of-at-least-32-characters>"
> docker-compose up -d
> ```
>
> The `frontend` container runs nginx as the **non-root** `nginx` user on the unprivileged container
> port `8080`; the published host port remains `http://localhost:4200`.

## API Overview

All resource endpoints are **URL-path versioned under `/api/v1/`**. The authentication
endpoints (`/api/auth/*`) and the health probe (`/health`) are intentionally unversioned.

| Endpoint | Methods | Description |
|----------|---------|-------------|
| `/api/v1/portals` | `GET`, `POST` | List portals / create a portal |
| `/api/v1/portals/{id}` | `GET`, `PUT`, `DELETE` | Read / update / delete a portal |
| `/api/v1/modules` | `GET`, `POST` | List modules / create a module |
| `/api/v1/modules/{id}` | `GET`, `PUT`, `DELETE` | Read / update / delete a module |
| `/api/v1/users` | `GET`, `POST` | List users / create a user |
| `/api/v1/users/{id}` | `GET`, `PUT`, `DELETE` | Read / update / delete a user |
| `/api/v1/roles` | `GET`, `POST` | List roles / create a role |
| `/api/v1/roles/{id}` | `GET`, `PUT`, `DELETE` | Read / update / delete a role |
| `/api/v1/tabs` | `GET`, `POST` | List tabs (pages) / create a tab |
| `/api/v1/tabs/{id}` | `GET`, `PUT`, `DELETE` | Read / update / delete a tab |
| `/api/auth/login` | `POST` | Authenticate; returns a JWT access token in the body and sets the refresh token as an `HttpOnly` cookie |
| `/api/auth/refresh` | `POST` | Rotate the access + refresh token pair using the `HttpOnly` refresh-token cookie (no request body) |
| `/api/auth/logout` | `POST` | Clear the `HttpOnly` refresh-token cookie; the client discards its in-memory access token. Stateless — no server-side session is stored or revoked |
| `/api/auth/me` | `GET` | Return the current authenticated user |
| `/health` | `GET` | Liveness / readiness health probe |

**Response contract**

- **Success** responses use a uniform envelope: `{ "data": ..., "meta": ... }`.
- **Errors** follow **RFC 7807 Problem Details** (`{ type, title, status, detail, errors }`).
- Protected endpoints require a **JWT Bearer** token (`Authorization: Bearer <token>`).
- **CORS** is restricted to the Angular origin only.
- **Auth endpoints are rate-limited.**

## Testing and Validation Gates

The implementation must pass all seven validation gates below. The commands are reproduced
exactly as specified.

| # | Gate | Command | Expected result |
|---|------|---------|-----------------|
| 1 | API compilation | `dotnet build --configuration Release --warnaserror` | exit 0; 0 errors, 0 warnings (excluding `CS8618` nullable warnings) |
| 2 | API unit tests | `dotnet test --configuration Release` | exit 0; 100% pass |
| 3 | Angular build | `ng build --configuration production` | exit 0; 0 errors / 0 warnings |
| 4 | Angular unit tests | `ng test --watch=false --browsers=ChromeHeadless` | exit 0; 100% pass |
| 5 | API integration tests | `dotnet test --filter Category=Integration` | Portal/Module/User CRUD pass (`POST` 201, `GET` 200, `PUT` 200, `DELETE` 204) |
| 6 | Container build | `docker-compose build` | exit 0; both images built |
| 7 | Container startup | `docker-compose up -d` | `curl -f http://localhost:8080/health` (API) and `http://localhost:4200` (Angular) → HTTP 200 |

> Gates 1–2 run from `backend/`; gates 3–4 run from `frontend/`; gates 6–7 run from
> `docker/`. Gates 6–7 require a **Linux Docker host** (the Alpine images are Linux-based).

## Further Reading

- **[`MIGRATION_NOTES.md`](MIGRATION_NOTES.md)** — the Minimal Change Clause deviation log:
  every migration decision and every deliberately preserved (ported) legacy bug.
- **[`docs/technical-specifications.md`](docs/technical-specifications.md)** — the full
  architecture contract.
- **[`docs/project-guide.md`](docs/project-guide.md)** — the detailed development and
  assessment guide.

### Migration Highlights

- **Authentication modernized** — legacy Forms Authentication + DES encryption are replaced
  by **JWT Bearer tokens + BCrypt** password hashing. This is the *single sanctioned
  behavior change* in the migration.
- **Data access modernized** — the ADO.NET `SqlDataProvider` / `SqlHelper` stored-procedure
  layer and the reflection-based `CBO` hydration are replaced by **EF Core 8** entity
  materialization with `AsNoTracking()` reads and async `SaveChangesAsync()` writes.
- **Schema preserved (ADR-002)** — the existing DNN `4.9.0.85` database schema is mapped
  **unchanged** via EF Core Fluent API: no table/column changes, no EF migrations, and no
  data migration in this phase.
- **Domain semantics preserved** — `Portal`, `Module`, `User`, `Role`, and `Tab` retain
  their public contracts, and all enum values are carried over verbatim.

### Troubleshooting

| Symptom | Likely cause | Resolution |
|---------|--------------|------------|
| `dotnet: command not found` | .NET 8 SDK not installed | Install the **.NET 8 LTS** SDK and re-open the shell |
| `npm: command not found` | Node.js not installed | Install **Node.js 20 LTS** (bundles npm 10.x) |
| Connection-string / database errors | DB not reachable or `ConnectionStrings:Default` unset | Set a valid connection string in `appsettings.Development.json` or via `ConnectionStrings__Default` |
| CORS errors in the browser | API base URL mismatch | Align `apiUrl` in `src/environments/environment.ts` with the API origin allowed by CORS |
| `ng test` fails to launch a browser | Chrome / Chromium missing | Install Chrome/Chromium so `ChromeHeadless` can start |
| JWT errors at startup | Signing key missing or shorter than 32 bytes | Provide a ≥ 32-byte key via `Jwt:Key` (or the `Jwt__Key` environment variable) |
