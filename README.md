# blitzy-dotnetnuke

> DotNetNuke 4.x VB.NET to .NET 8 + Angular 19 complete migration

A full, ground-up rewrite of the legacy **DotNetNuke (DNN) `4.9.0.85`** portal
framework — originally **VB.NET / ASP.NET Web Forms on .NET Framework 2.0** — into
two modern, independently deployable applications:

1. **`DnnMigration` API** — an **ASP.NET Core 8** Web API written in **C# 12**,
   organized with **Clean Architecture** and the **Backend-for-Frontend (BFF)**
   pattern. It exposes a versioned REST surface (`/api/v1/...`) and replaces the
   legacy Web Forms presentation tier entirely.
2. **`DnnMigration` SPA** — an **Angular 19** single-page application built on
   **standalone components**, signals, and typed reactive forms, consuming the API.

The two applications are packaged as a **two-container Docker Compose topology**
(API + nginx-served SPA) targeting **Linux**.

The migration preserves the core domain logic and achieves functional parity for the
**Portal, Module, and User** management subsystems together with the **Role,
Permission, and Tab/Page** subsystems. **Portal, Module, User, and Role** are each
delivered end-to-end as both a REST API resource and an Angular SPA feature area;
**Tab/Page** is delivered as a REST API resource only (`/api/v1/tabs`) and
intentionally has **no** Angular feature slice — the sidebar surfaces a disabled,
non-routing **"Tabs"** entry (see `frontend/src/app/app.routes.ts` and
[`MIGRATION_NOTES.md`](./MIGRATION_NOTES.md)). One bounded scope reduction applies to the User membership surface:
legacy account **Unlock** and durable persistence of the **Approved** / **LockedOut**
flags are **not** reproduced, because they live on the GUID-keyed `aspnet_Membership`
table — not a modeled Phase-1 entity, and one that ADR-002 forbids altering; **Force
Password Change** (backed by the real `dbo.Users.UpdatePassword` column) **is**
reproduced end-to-end. See [`MIGRATION_NOTES.md`](./MIGRATION_NOTES.md) entries
**D-034** / **D-041** for the full rationale. Business logic is ported as-is for behavioral
equivalence; the one sanctioned behavior change is the security layer — **JWT Bearer
tokens + BCrypt password hashing** replace the legacy Forms Authentication + DES.

> **Legacy source coexists in this repository.** The original `Library/` and
> `Website/` VB.NET trees are retained **as read-only reference material only**. They
> are not compiled and emit no target artifacts; all new code lives under `backend/`,
> `frontend/`, and `docker/`.

---

## Tech Stack

| Tier | Technologies |
|------|--------------|
| **Backend** | .NET 8 LTS · C# 12 · ASP.NET Core 8 · Entity Framework Core 8 · JWT Bearer authentication · BCrypt password hashing · AutoMapper · FluentValidation · Serilog |
| **Frontend** | Angular 19 (standalone components, signals, typed reactive forms, lazy loading) · TypeScript 5.6 · RxJS 7.8 · SCSS |
| **Deployment** | Docker (multi-stage builds, Alpine base images) · Docker Compose · nginx (SPA hosting + `/api/*` reverse proxy + CSP) |
| **Database** | SQL Server (existing DNN `4.9.0.85` schema, mapped **unchanged** via EF Core Fluent API) |

---

## Repository Layout

```text
.
├── backend/                     # ASP.NET Core 8 Clean Architecture solution (C# 12)
│   ├── DnnMigration.sln
│   ├── Directory.Build.props
│   ├── global.json              # pins the .NET 8 SDK band
│   ├── src/
│   │   ├── DnnMigration.Domain/         # POCO entities, enums, repository interfaces (no framework deps)
│   │   ├── DnnMigration.Application/     # services, DTOs, AutoMapper profiles, FluentValidation validators
│   │   ├── DnnMigration.Infrastructure/  # EF Core DbContext, repositories, JWT + BCrypt identity
│   │   └── DnnMigration.Api/             # REST controllers, middleware, Program.cs, appsettings.json
│   └── tests/
│       ├── DnnMigration.UnitTests/        # xUnit + Moq + FluentAssertions
│       └── DnnMigration.IntegrationTests/ # Mvc.Testing + EF Core InMemory
│
├── frontend/                    # Angular 19 standalone-component SPA
│   ├── package.json
│   ├── angular.json
│   ├── tsconfig.json / tsconfig.app.json / tsconfig.spec.json
│   └── src/app/
│       ├── core/                # auth (service, guard, interceptor), api.service, models
│       ├── shared/              # data-table, form-controls, confirmation-dialog, loading-spinner, pipes, directives
│       ├── features/            # portal, module, user, role, auth (lazy-loaded feature areas)
│       └── layout/              # header, sidebar, footer application shell
│
├── docker/                      # Two-container Linux deployment topology
│   ├── api.Dockerfile           # multi-stage: sdk:8.0-alpine -> aspnet:8.0-alpine
│   ├── frontend.Dockerfile      # node build -> nginx:alpine
│   ├── nginx.conf               # SPA serve + /api/* reverse proxy + CSP
│   └── docker-compose.yml       # api + frontend services with HEALTHCHECK wiring
│
├── docs/                        # Technical specification and project guide
│   ├── index.md
│   ├── project-guide.md
│   └── technical-specifications.md
│
├── MIGRATION_NOTES.md           # Minimal Change Clause deviation log (every // MIGRATION: decision)
│
├── Library/                     # LEGACY DNN VB.NET class library — reference only (not compiled)
└── Website/                     # LEGACY DNN VB.NET web application — reference only (not compiled)
```

---

## Prerequisites

| Requirement | Version | Purpose |
|-------------|---------|---------|
| .NET SDK | **8.0 LTS** | Backend development and build |
| Node.js | **20.x LTS** | Frontend development and build |
| npm | **10.x** | Frontend package management |
| Docker | **24.x+** | Container image builds |
| Docker Compose | **2.x** | Multi-container orchestration |
| SQL Server | **2019+** | Database (or run a containerized instance) |
| Chrome / Chromium | latest | Required by `ChromeHeadless` for Angular unit tests |

---

## Backend — Setup, Build & Run

All commands are run from the repository root unless otherwise noted.

```bash
# 1. Enter the backend solution
cd backend

# 2. Restore NuGet packages
dotnet restore DnnMigration.sln

# 3. Build in Release with warnings treated as errors
#    Expected: Build succeeded — 0 Warning(s), 0 Error(s)
#    (CS8618 nullable-reference warnings are excluded per the validation gate.)
dotnet build DnnMigration.sln --configuration Release --warnaserror

# 4. Run the API (composition root: src/DnnMigration.Api/Program.cs)
dotnet run --project src/DnnMigration.Api
```

The API listens on `http://localhost:5000` by default. When running with the
`Development` environment, **Swagger UI** is served for interactive exploration of
the OpenAPI surface. A liveness/readiness probe is available at **`/health`**.

### Backend Configuration

Runtime settings live in `backend/src/DnnMigration.Api/appsettings.json` and are
overridden per environment by `appsettings.Development.json`. Two sections matter:

- **`ConnectionStrings:Default`** — the SQL Server connection string pointing at the
  existing DNN `4.9.0.85` database. The schema is mapped **unchanged** (no EF Core
  migrations, no schema edits — see ADR-002).
- **`Jwt`** — token issuance settings: `Issuer = DnnMigration`, `Audience =
  DnnMigration`, a signing key (minimum 32 bytes), and a **60-minute** access-token
  lifetime.

> These settings are derived from the legacy `Website/development.config` and
> `Website/release.config`. All runtime configuration for the new API now lives in
> `appsettings.json` / `appsettings.Development.json` — there is no legacy ASP.NET XML
> configuration file to edit in the modernized backend.

Illustrative `appsettings.Development.json` (replace the placeholder values — never
commit a real secret):

```json
{
  "ConnectionStrings": {
    "Default": "Server=localhost,1433;Database=DotNetNuke;User Id=sa;Password=__REPLACE_WITH_LOCAL_PASSWORD__;TrustServerCertificate=True"
  },
  "Jwt": {
    "Key": "__REPLACE_WITH_A_LOCAL_DEV_SIGNING_KEY_AT_LEAST_32_BYTES__",
    "Issuer": "DnnMigration",
    "Audience": "DnnMigration",
    "AccessTokenExpirationMinutes": 60,
    "RefreshTokenExpirationDays": 7
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

---

## Frontend — Setup, Build & Run

```bash
# 1. Enter the Angular workspace
cd frontend

# 2. Install dependencies
npm install

# 3. Start the dev server (hot reload) on http://localhost:4200
#    API requests are proxied to the backend during development.
npm start            # equivalent to: ng serve

# 4. Production build — output is written to dist/dnn-migration/browser
npx ng build --configuration production

# 5. Run unit tests once in headless Chrome
npm test -- --watch=false --browsers=ChromeHeadless
```

> **API base URL.** Configure the backend endpoint the SPA talks to via the `apiUrl`
> property in `src/environments/environment.ts` (and `environment.prod.ts` for
> production builds).

---

## Docker — Build & Run

The deployment is a two-container topology defined in `docker/docker-compose.yml`:
an **`api`** service (.NET 8) and a **`frontend`** service (the Angular SPA served by
nginx, which also reverse-proxies `/api/*` to the API).

```bash
# 1. Enter the docker directory
cd docker

# 2. Build both images
docker-compose build

# 3. Start both services in the background
docker-compose up -d

# 4. Verify the API health endpoint
curl -f http://localhost:8080/health
# Expected: {"status":"Healthy","timestamp":"<ISO-8601 UTC>"}
```

The SPA is then available at **`http://localhost:4200`**.

- **Services & ports:** `api` is published on host port **`8080`**; `frontend` is
  published on host port **`4200`** (nginx listening on container port 8080).
- **HEALTHCHECK wiring:** the `api` container's health is polled at `/health`, and the
  `frontend` service waits for the API to report healthy before starting
  (`depends_on: service_healthy`).
- **Environment variables provided to the API container:** the two required secrets
  `ConnectionStrings__Default` (SQL Server connection string) and `Jwt__Key` (signing
  key, ≥ 32 bytes) — injected via `:?` required interpolation in `docker-compose.yml`,
  so startup aborts if either is missing. `Jwt:Issuer` and `Jwt:Audience` are baked into
  `appsettings.json`, and the token lifetimes default to `Jwt:AccessTokenExpirationMinutes`
  (60) and `Jwt:RefreshTokenExpirationDays` (7). Always supply real values via the
  environment or a secret manager — never bake secrets into images.

> **Host note.** The images are Linux/Alpine based and must be built and run on a
> Linux-capable Docker host.

---

## API Overview

All resource endpoints are **URL-path versioned** under `/api/v1/`. Authentication
endpoints and the health probe are intentionally unversioned.

### Resource Endpoints

| Resource | Collection (`/api/v1/...`) | Item (`/api/v1/.../{id}`) |
|----------|----------------------------|----------------------------|
| Portals | `GET`, `POST` `/api/v1/portals` | `GET`, `PUT`, `DELETE` `/api/v1/portals/{id}` |
| Modules | `GET`, `POST` `/api/v1/modules` | `GET`, `PUT`, `DELETE` `/api/v1/modules/{id}` |
| Users | `GET`, `POST` `/api/v1/users` | `GET`, `PUT`, `DELETE` `/api/v1/users/{id}` |
| Roles | `GET`, `POST` `/api/v1/roles` | `GET`, `PUT`, `DELETE` `/api/v1/roles/{id}` |
| Tabs | `GET`, `POST` `/api/v1/tabs` | `GET`, `PUT`, `DELETE` `/api/v1/tabs/{id}` |

### Authentication & Health Endpoints

| Method | Endpoint | Purpose |
|--------|----------|---------|
| `POST` | `/api/auth/login` | Authenticate a user; issues access + refresh tokens |
| `POST` | `/api/auth/refresh` | Rotate tokens using a valid refresh token |
| `POST` | `/api/auth/logout` | Invalidate the current session |
| `GET` | `/api/auth/me` | Return the currently authenticated user |
| `GET` | `/health` | Liveness/readiness health probe |

### Response Conventions

- **Success envelope:** responses carry `{ "data": ..., "meta": ... }`, where `meta`
  conveys paging and contextual metadata for collection results.
- **Errors:** follow **RFC 7807 Problem Details** — `{ type, title, status, detail,
  errors }` — emitted by a global exception-handling middleware.
- **Authentication:** **JWT Bearer** tokens (short-lived access tokens with
  refresh-token rotation); the API is stateless, carrying identity in JWT claims.
- **CORS:** restricted to the Angular SPA origin only.
- **Rate limiting:** the authentication endpoints are rate-limited.

---

## Testing & Validation Gates

The migration is considered complete only when all seven validation gates pass. The
commands below are reproduced exactly.

> **Angular CLI note (Gates 3 & 4).** The `ng` CLI is installed locally in
> `frontend/node_modules`, not globally. Run the Gate 3/4 commands from the `frontend/`
> directory as `npx ng …` (or use the equivalent `npm run build` / `npm test` scripts),
> or install the CLI globally with `npm install -g @angular/cli`.

**Gate 1 — API compilation** (exit 0; zero errors and zero warnings, excluding
`CS8618` nullable warnings):

```bash
dotnet build --configuration Release --warnaserror
```

**Gate 2 — API unit tests** (exit 0; 100% pass):

```bash
dotnet test --configuration Release
```

**Gate 3 — Angular build** (exit 0; zero errors/warnings):

```bash
npx ng build --configuration production
```

**Gate 4 — Angular unit tests** (exit 0; 100% pass):

```bash
npx ng test --watch=false --browsers=ChromeHeadless
```

**Gate 5 — API integration tests** (Portal/Module/User CRUD pass — `POST` 201,
`GET` 200, `PUT` 200, `DELETE` 204):

```bash
dotnet test --filter Category=Integration
```

**Gate 6 — Container build** (exit 0; both images built):

```bash
docker-compose build
```

**Gate 7 — Container startup** (both probes return HTTP 200):

```bash
docker-compose up -d
curl -f http://localhost:8080/health   # API
curl -f http://localhost:4200          # Angular SPA
```

---

## Migration Highlights

- **Authentication modernized (the single sanctioned behavior change).** Legacy Forms
  Authentication and 56-bit DES encryption are replaced by **JWT Bearer** tokens and
  **BCrypt** adaptive password hashing.
- **Data access modernized.** The ADO.NET `SqlHelper` / `SqlDataProvider`
  stored-procedure layer and the reflection-based `CBO` object hydration are replaced
  by **Entity Framework Core 8** entity materialization with `IEntityTypeConfiguration<T>`
  Fluent API mappings.
- **Schema preserved (ADR-002).** The existing DNN `4.9.0.85` database schema is mapped
  **unchanged** — no table/column changes, no EF Core migrations, and no data migration
  in Phase 1.
- **Presentation re-platformed.** ASP.NET Web Forms (`.aspx`/`.ascx`, postback,
  ViewState) is replaced by a stateless REST API plus a client-rendered Angular SPA.

Every deviation from legacy behavior and every ported pre-existing bug is annotated
with a `// MIGRATION:` comment in the source and recorded in
[`MIGRATION_NOTES.md`](./MIGRATION_NOTES.md).

## Further Reading

- [`MIGRATION_NOTES.md`](./MIGRATION_NOTES.md) — Minimal Change Clause deviation log.
- [`docs/technical-specifications.md`](./docs/technical-specifications.md) — the
  architecture contract (layers, patterns, EF Core model, API and Angular design).
- [`docs/project-guide.md`](./docs/project-guide.md) — development guide, verification
  steps, and operational task list.

## Troubleshooting

| Issue | Cause | Resolution |
|-------|-------|------------|
| `dotnet: command not found` | .NET SDK not installed | Install the .NET 8 LTS SDK and re-open the shell |
| `npm: command not found` | Node.js not installed | Install Node.js 20.x LTS |
| Connection string error on startup | Database not configured / unreachable | Set `ConnectionStrings:Default` in `appsettings.Development.json` (or `ConnectionStrings__Default` for containers) |
| CORS errors in the browser | API origin mismatch | Update `apiUrl` in `src/environments/environment.ts` and confirm the API's allowed origin |
| `ChromeHeadless` fails to launch | Chrome/Chromium not found | Install Chrome/Chromium (set `CHROME_BIN` if needed) before running Angular tests |
