# DnnMigration Project Guide

## Executive Summary

**Status: FINAL — full migration delivered; production-ready pending two documented, environment-bounded items (see Known Limitations).**

This guide covers the DotNetNuke 4.x (VB.NET / .NET Framework 2.0 / Web Forms) → **C# 12 / .NET 8** ASP.NET Core Web API (BFF) + **Angular 19** SPA migration. The migration is **complete**: the backend solution builds end-to-end and its unit and integration suites pass; the Angular application builds for production and its unit specs pass. The backend Domain, Application, Infrastructure, and Api layers are all present and wired, the EF Core `DnnDbContext` maps the existing DNN schema without altering it, and the Angular SPA (bootstrap, routing, features, layout, shared, and core) is fully delivered together with the Docker/nginx deployment manifests.

Two items remain **environment-bounded** rather than open defects, and are documented in Known Limitations and in `MIGRATION_NOTES.md`:

1. **Docker container gates (Gates 6–7)** cannot be executed on this Windows/Kubernetes host (no Docker daemon; the Alpine images are Linux). The manifests are complete and must be built/started in a Linux Docker CI environment.
2. **Angular 19 production dependency advisories** — the pinned `^19.0.0` line (`AAP §0.5.1`) carries known advisories whose only upstream fix is a semver-major (20.x/21.x) upgrade, which is out of scope for this migration. Accepted as an **AAP-constrained residual risk** (details in `MIGRATION_NOTES.md §8.6`).

### Key Metrics
| Metric | Value |
|--------|-------|
| Migration status | FINAL — backend + frontend delivered |
| Backend build | `dotnet build -c Release --warnaserror` → **0 errors / 0 warnings** |
| Backend unit tests | **352 / 352 pass** (`DnnMigration.UnitTests`) |
| Backend integration tests | **110 / 110 pass** (`DnnMigration.IntegrationTests`, EF Core InMemory) |
| Frontend build | `ng build --configuration production` → **0 errors / 0 warnings** (`dist/dnn-migration/browser`) |
| Frontend unit tests | **254 / 254 pass** (`ng test`, ChromeHeadless) |
| Backend layers | Domain, Application, Infrastructure, Api (+ UnitTests, IntegrationTests) — all present and wired |
| Frontend | Standalone Angular 19 SPA — bootstrap (`main.ts`, `app.config.ts`, `app.routes.ts`), core/shared/features/layout all present |
| Docker manifests | Present (`api.Dockerfile`, `frontend.Dockerfile`, `docker-compose.yml` incl. `Jwt__SecretKey`, `nginx.conf`) |
| Container gates (6–7) | Not confirmed here — require a Linux Docker CI environment |

### Delivered Scope

The migration delivers full functional parity for **Portal, Module, User, Role, and Tab** management plus **authentication** (login / refresh / logout / current-user), following the target architecture in the technical specification:

- Backend **Domain** layer (entities, enums, repository interfaces) as C# 12 POCOs with nullable reference types enabled.
- Backend **Application** layer (services holding the extracted business rules, DTOs, AutoMapper profile, FluentValidation validators).
- Backend **Infrastructure** layer (EF Core `DnnDbContext` + `IEntityTypeConfiguration<T>` Fluent mappings to the existing DNN/`aspnet_*` schema, repositories, JWT + BCrypt identity).
- Backend **Api** layer (REST controllers, exception-handling and correlation-id middleware, `Program.cs` host with DI, JWT bearer, CORS, rate limiting, and OpenAPI).
- Backend **test projects** — `DnnMigration.UnitTests` and `DnnMigration.IntegrationTests`.
- Angular **SPA** — standalone-component `core` (auth service/guard/interceptor, API service, models), `shared` (data table, form controls, confirmation dialog, loading spinner, pipes, directives), `features` (portal, module, user, role, auth), and `layout` (header, sidebar, footer), with lazy-loaded routes, typed reactive forms, Signals, and design-system tokens in `styles.scss`.
- **Docker** deployment manifests and hardened **nginx** configuration.
- `MIGRATION_NOTES.md` documenting all significant migration decisions.

---

## Validation Results Summary

The migration defines seven validation gates (see the technical specification). Gates 1–5 are executed and **pass** in this environment; Gates 6–7 require a Linux Docker environment and are executed in Docker CI.

### Validation Gate Status
| Gate | Description | Status |
|------|-------------|--------|
| 1 | API compiles (`dotnet build -c Release --warnaserror`, 0/0) | ✅ PASS — 0 errors / 0 warnings across all six projects |
| 2 | API unit tests pass | ✅ PASS — 352 / 352 |
| 3 | Angular production build (`ng build --configuration production`) | ✅ PASS — 0 errors / 0 warnings; bundle emitted to `dist/dnn-migration/browser` |
| 4 | Angular unit tests pass | ✅ PASS — 254 / 254 (ChromeHeadless, with code coverage) |
| 5 | API integration tests (Portal/Module/User CRUD) | ✅ PASS — 110 / 110 (POST → 201, GET → 200, PUT → 200, DELETE → 204) |
| 6 | Container build (`docker-compose build`) | ⚠️ NOT CONFIRMED HERE — Docker unavailable on this Windows/K8s host; run in Linux Docker CI |
| 7 | Container startup health checks (`/health`, `/`) | ⚠️ NOT CONFIRMED HERE — depends on Gate 6 |

> **Environment note.** Gates 6–7 require a Linux Docker environment (the images are `*-alpine`). On a Windows-only host without a Docker daemon they are executed in a Linux Docker CI environment; Gates 1–5 run locally and the integration tests use `EFCore.InMemory`, so they need no external SQL Server.

---

## Known Limitations (Environment-Bounded)

These are not open code defects; they are documented constraints of the current build/validation environment and of the AAP-pinned dependency set.

1. **Docker Gates 6–7 unconfirmed in this environment.** No Docker daemon is available on the Windows/Kubernetes host, and the delivered images are Linux Alpine. The manifests are complete (`docker-compose.yml` now supplies `ConnectionStrings__Default` **and** `Jwt__SecretKey`, so the API no longer fails fast at startup). Build and start them in a Linux Docker CI environment:
   `docker-compose build` (Gate 6) then `curl -f http://localhost:8080/health` and `curl -f http://localhost:4200` (Gate 7).
2. **Angular 19 production dependency advisories (AAP-constrained residual).** `npm audit --omit=dev` reports 8 advisories (7 high, 1 moderate) against the Angular runtime packages resolved at `19.2.25`. Every advisory's vulnerable range spans the entire 19.x line, and the only upstream fix is a **semver-major** upgrade to 20.x/21.x. `AAP §0.5.1` pins Angular at `^19.0.0`, so a major-version upgrade is out of scope for this migration; the risk is **accepted and documented** (`MIGRATION_NOTES.md §8.6`). Follow-up: schedule a dedicated Angular 20/21 upgrade.

### Scope Reconciliation — Password Recovery (forgot-password)

Password recovery (a "forgot password" screen) is **intentionally out of scope** and is **not** a missing deliverable:

- The delivered authentication surface is exactly **login / refresh / logout / current-user** (`AAP §0.3.1`); there is no password-recovery API endpoint.
- The legacy provider-based password-recovery path (`Website/admin/Security/SendPassword.ascx`, MembershipProvider) is explicitly out of scope (`AAP §0.2.2`).
- This decision is recorded in `frontend/src/app/features/auth/auth.routes.ts`, and **no placeholder route or component is retained** (no-stub rule).
- A `forgot-password.component.ts` / `.spec.ts` pair appears in an earlier processed-file inventory; because implementing it would require a backend endpoint that is out of scope (and would otherwise be a stub), these files are **deliberately absent at HEAD**. The entry is a scope-metadata discrepancy, reconciled here to the delivered, no-stub implementation.

---

## Comprehensive Development Guide

### System Prerequisites

| Requirement | Version | Purpose |
|-------------|---------|---------|
| .NET SDK | 8.0 LTS | Backend development and build |
| Node.js | 20.x LTS | Frontend development and build |
| npm | 10.x | Package management |
| Docker | 24.x+ | Container builds (Linux host) |
| Docker Compose | 2.x | Multi-container orchestration |
| SQL Server | 2019+ | Database (or use containerized) |

### Environment Setup

#### 1. Clone Repository
```bash
git clone <repository-url>
cd DnnMigration
git checkout <branch>
```

#### 2. Backend Setup
```bash
# Navigate to backend directory
cd backend

# Restore NuGet packages
dotnet restore DnnMigration.sln

# Build solution (Release mode with warnings as errors)
dotnet build DnnMigration.sln --configuration Release --warnaserror

# Expected output:
# Build succeeded.
#     0 Warning(s)
#     0 Error(s)
```

#### 3. Configure Backend Environment
Configuration is layered: `backend/src/DnnMigration.Api/appsettings.json` holds the base configuration, and `backend/src/DnnMigration.Api/appsettings.Development.json` layers development-specific overrides on top of it. Create/update `backend/src/DnnMigration.Api/appsettings.Development.json`:
```json
{
  "ConnectionStrings": {
    "Default": "Server=localhost;Database=DotNetNuke;User Id=sa;Password=YourPassword;TrustServerCertificate=true"
  },
  "Jwt": {
    "SecretKey": "your-256-bit-secret-key-here-minimum-32-characters",
    "Issuer": "DnnMigration",
    "Audience": "DnnMigration.Client",
    "AccessTokenExpirationMinutes": 15,
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

> **Secrets handling:** Never commit real secrets. The JWT `SecretKey` and database credentials shown above are placeholders — supply real values via **environment variables** (e.g., `ConnectionStrings__Default`, `Jwt__SecretKey`) or **.NET user-secrets** (`dotnet user-secrets`), and keep them out of source control. `Program.cs` **fails fast** outside the Development environment if `Jwt:SecretKey` is missing or shorter than 256 bits (32 bytes). Note that the **DES-encrypted secrets from the legacy DNN configuration cannot be carried over** to the new stack; connection strings and host secrets must be re-supplied fresh for the .NET 8 application.

#### 4. Run Backend Tests
```bash
# Run all backend tests (unit + integration)
dotnet test DnnMigration.sln --configuration Release

# Observed output:
# Passed!  - Failed: 0, Passed: 352, Skipped: 0, Total: 352   (DnnMigration.UnitTests)
# Passed!  - Failed: 0, Passed: 110, Skipped: 0, Total: 110   (DnnMigration.IntegrationTests)
```

#### 5. Start Backend API
```bash
# Run API in development mode
cd src/DnnMigration.Api
dotnet run --configuration Release

# API will start on http://localhost:5000 (or configured port)
# Health check available at: http://localhost:5000/health
```

> **Note:** In a published/containerized deployment the API is launched from its compiled **entrypoint assembly `DnnMigration.Api.dll`** (i.e. `dotnet DnnMigration.Api.dll`), which is exactly what `docker/api.Dockerfile` runs via `ENTRYPOINT ["dotnet", "DnnMigration.Api.dll"]`. The `dotnet run` command above is for local development only.

#### 6. Frontend Setup
```bash
# Navigate to frontend directory
cd ../../frontend

# Install npm dependencies (lockfile-exact)
npm ci

# Expected output: added xxx packages
```

#### 7. Run Frontend Tests
```bash
# Run Angular tests in CI mode
npm test -- --watch=false --browsers=ChromeHeadless

# Observed output: 254/254 specs pass, 0 failures
```

#### 8. Build Frontend for Production
```bash
# Production build
npm run build -- --configuration production

# Output directory (relative to frontend/): dist/dnn-migration/browser
# Full repository-relative path: frontend/dist/dnn-migration/browser
# (this is the path docker/frontend.Dockerfile copies into nginx)
```

#### 9. Start Frontend Dev Server
```bash
# Development server with hot reload
npm start

# Frontend available at: http://localhost:4200
# API calls proxied to backend
```

### Docker Deployment

> Docker builds require a **Linux** Docker host (the images are `*-alpine`). Provision `DB_CONNECTION_STRING` and `JWT_SECRET_KEY` in the deployment environment before `docker-compose up` — the compose `api` service maps them to `ConnectionStrings__Default` and `Jwt__SecretKey`.

#### Build and Run with Docker Compose
```bash
# From repository root
cd docker

# Build all images
docker-compose build

# Start all services
docker-compose up -d

# Verify API health (expect HTTP 200)
curl -f http://localhost:8080/health

# Expected response:
# {"status":"Healthy","version":"1.0.0.0"}

# Verify frontend is served (expect HTTP 200)
curl -f http://localhost:4200
```

#### Individual Container Commands
```bash
# Build API image only
docker build -f docker/api.Dockerfile -t dnnmigration-api .

# Build Frontend image only
docker build -f docker/frontend.Dockerfile -t dnnmigration-frontend .

# Run API container
docker run -d -p 8080:8080 \
  -e "ConnectionStrings__Default=Server=host.docker.internal;Database=DotNetNuke;..." \
  -e "Jwt__SecretKey=your-secret-key" \
  dnnmigration-api

# Run Frontend container
docker run -d -p 80:80 dnnmigration-frontend
```

### Verification Steps

| Step | Command | Expected Result |
|------|---------|-----------------|
| Backend Build | `dotnet build --configuration Release --warnaserror` | 0 errors, 0 warnings |
| Backend Tests | `dotnet test --configuration Release` | 352 unit + 110 integration pass |
| Frontend Build | `npm run build -- --configuration production` | Build successful; bundle emitted |
| Frontend Tests | `npm test -- --watch=false --browsers=ChromeHeadless` | 254 specs pass |
| API Health Check | `curl -f http://localhost:8080/health` | HTTP 200, JSON response |
| Frontend Check | `curl -f http://localhost:4200` | HTTP 200 |
| Docker Build | `docker-compose build` | Both images built (Linux Docker CI) |
| Docker Run | `docker-compose up -d` | All containers running (Linux Docker CI) |

### Common Troubleshooting

| Issue | Cause | Solution |
|-------|-------|----------|
| `dotnet: command not found` | .NET SDK not installed | Install .NET 8 SDK |
| `npm: command not found` | Node.js not installed | Install Node.js 20 LTS |
| Connection string error | Database not configured | Update `appsettings.Development.json` / `ConnectionStrings__Default` |
| API exits immediately in Production | `Jwt:SecretKey` missing/short | Supply a ≥256-bit `Jwt__SecretKey` (fails fast by design) |
| CORS errors | API URL mismatch | Update `environment.ts` `apiUrl` / API `Cors:AllowedOrigins` |
| Health check 401 | Missing AllowAnonymous | Already handled — `/health` is `[AllowAnonymous]` |
| Chrome not found | ChromeHeadless missing | Install Chrome/Chromium; set `CHROME_BIN` |

---

## Human Task List

The migration is code-complete; the tasks below are deployment/operational-readiness items and the two environment-bounded follow-ups.

### High Priority Tasks (Production Blockers)

| Task ID | Task Description | Action Steps | Hours | Priority | Severity |
|---------|------------------|--------------|-------|----------|----------|
| H1 | Database Environment Setup | 1. Provision SQL Server instance with the existing DNN schema<br>2. Validate the EF Core Fluent mappings against the existing DNN schema (EF migrations are used only for greenfield/test databases, never against the production DNN schema)<br>3. Configure connection string<br>4. Verify database connectivity | 4 | HIGH | Critical |
| H2 | JWT Secret Configuration | 1. Generate secure 256-bit secret (legacy DES-encrypted DNN secrets cannot be carried over and must be re-supplied fresh)<br>2. Store in Azure Key Vault/AWS Secrets<br>3. Configure via environment variables / user-secrets (`Jwt__SecretKey`, `ConnectionStrings__Default`) — never commit<br>4. Rotate secrets policy | 2 | HIGH | Critical |
| H3 | SSL/TLS Certificate Setup | 1. Obtain SSL certificate<br>2. Configure HTTPS redirection<br>3. Update nginx for HTTPS<br>4. Test certificate chain | 3 | HIGH | Critical |
| H4 | Production Environment Variables | 1. Define all required env vars<br>2. Configure in deployment platform<br>3. Document required variables<br>4. Validate on staging | 2 | HIGH | High |
| H5 | Execute Docker Gates 6–7 in Linux Docker CI | 1. Run `docker-compose build` on a Linux Docker host<br>2. Provision `DB_CONNECTION_STRING` + `JWT_SECRET_KEY`<br>3. `docker-compose up -d`<br>4. Confirm `/health` and `/` return HTTP 200 | 2 | HIGH | High |

**High Priority Subtotal: 13 hours**

### Medium Priority Tasks (Operational Readiness)

| Task ID | Task Description | Action Steps | Hours | Priority | Severity |
|---------|------------------|--------------|-------|----------|----------|
| M1 | CI/CD Pipeline Configuration | 1. Create build pipeline (GitHub Actions/Azure DevOps)<br>2. Configure test automation<br>3. Set up deployment stages<br>4. Add approval gates | 8 | MEDIUM | High |
| M2 | Application Monitoring Setup | 1. Configure Serilog sinks<br>2. Set up Application Insights/Datadog<br>3. Create dashboards<br>4. Configure alerts | 6 | MEDIUM | Medium |
| M3 | Error Tracking Integration | 1. Set up Sentry/AppInsights<br>2. Configure error grouping<br>3. Set up notifications<br>4. Test error capture | 3 | MEDIUM | Medium |
| M4 | CORS Policy Finalization | 1. Define allowed origins<br>2. Configure production CORS<br>3. Test cross-origin requests<br>4. Document policy | 2 | MEDIUM | Medium |
| M5 | Security Headers Configuration | 1. Add CSP headers<br>2. Configure X-Frame-Options<br>3. Add HSTS<br>4. Verify with security scanner | 2 | MEDIUM | Medium |
| M6 | Angular 20/21 Upgrade (dependency advisories) | 1. Plan a dedicated semver-major upgrade (breaking; outside AAP `^19.0.0` pin)<br>2. Update Angular framework + toolchain to a patched major<br>3. Regenerate lockfile; rerun `npm audit`, build, and tests<br>4. Verify no functional regression | 8 | MEDIUM | High |

**Medium Priority Subtotal: 29 hours**

### Low Priority Tasks (Optimization)

| Task ID | Task Description | Action Steps | Hours | Priority | Severity |
|---------|------------------|--------------|-------|----------|----------|
| L1 | Load Testing | 1. Set up load testing tool<br>2. Define test scenarios<br>3. Execute performance tests<br>4. Document results | 4 | LOW | Low |
| L2 | Database Query Optimization | 1. Review EF Core queries<br>2. Add necessary indexes<br>3. Optimize N+1 queries<br>4. Benchmark improvements | 4 | LOW | Low |
| L3 | API Documentation Enhancement | 1. Review Swagger annotations<br>2. Add example requests/responses<br>3. Generate API docs<br>4. Publish documentation | 2 | LOW | Low |
| L4 | Operations Runbook | 1. Document deployment procedures<br>2. Create troubleshooting guide<br>3. Define escalation procedures<br>4. Review with team | 2 | LOW | Low |
| L5 | Caching Implementation | 1. Evaluate caching needs<br>2. Implement response caching<br>3. Add Redis if needed<br>4. Test cache invalidation | 4 | LOW | Low |

**Low Priority Subtotal: 16 hours**

### Task Summary

| Priority | Task Count | Total Hours |
|----------|------------|-------------|
| High | 5 | 13 |
| Medium | 6 | 29 |
| Low | 5 | 16 |
| **Total** | **16** | **58** |

---

## Risk Assessment

### Technical Risks

| Risk | Severity | Likelihood | Impact | Mitigation |
|------|----------|------------|--------|------------|
| Database schema mismatch with legacy DNN | Medium | Low | High | EF Core Fluent API configured to match the existing schema (verbatim table/column/FK names); validated with integration tests |
| EF Core query performance | Medium | Medium | Medium | Use `AsNoTracking` for reads; monitor query execution plans; add indexes as needed |
| Angular bundle size | Low | Low | Low | Tree-shaking enabled; lazy loading implemented; production build optimized |

### Security Risks

| Risk | Severity | Likelihood | Impact | Mitigation |
|------|----------|------------|--------|------------|
| JWT secret exposure | Critical | Low | Critical | Use secret management (Key Vault/Secrets Manager); never commit secrets; rotate regularly; `Program.cs` fails fast on missing/weak key |
| SQL injection | Low | Very Low | Critical | EF Core parameterized queries; FluentValidation input validation |
| XSS vulnerabilities | Low | Low | Medium | Angular sanitization enabled; CSP headers configured in nginx |
| Angular 19 production dependency advisories | High | Medium | Medium | AAP-constrained residual (`^19.0.0` pin); documented in `MIGRATION_NOTES.md §8.6`; remediated by the tracked Angular 20/21 upgrade (M6) |

### Operational Risks

| Risk | Severity | Likelihood | Impact | Mitigation |
|------|----------|------------|--------|------------|
| Database connectivity failure | High | Low | Critical | Implement connection resilience; health checks; circuit breaker pattern |
| Container resource exhaustion | Medium | Low | High | Configure resource limits; horizontal scaling; monitoring alerts |
| Missing monitoring | Medium | High | Medium | Implement Serilog; integrate APM tool before production |
| Docker gates unverified in non-Linux env | Medium | Medium | Medium | Build/start images in Linux Docker CI (H5); manifests are complete incl. `Jwt__SecretKey` |

### Integration Risks

| Risk | Severity | Likelihood | Impact | Mitigation |
|------|----------|------------|--------|------------|
| Legacy data migration | Medium | Medium | High | Test with production data subset; rollback plan; parallel running period; forward-hash-on-login for legacy credentials |
| Third-party service dependencies | Low | Low | Medium | API timeouts configured; circuit breakers; fallback strategies |

---

## Architecture Overview

### Backend Structure
```
backend/
├── src/
│   ├── DnnMigration.Domain/          # Entities, Interfaces, Enums
│   │   ├── Entities/                 # Portal, Module, User, Role, Tab, Permission, TabModule, UserPortal, ...
│   │   ├── Interfaces/               # Repository interfaces
│   │   └── Enums/                    # UserRegistrationType, BannerType, SecurityAccessLevel, ...
│   │
│   ├── DnnMigration.Application/     # Services, DTOs, Mapping, Validators
│   │   ├── Services/                 # PortalService, ModuleService, UserService, RoleService, TabService, AuthService
│   │   ├── DTOs/                     # Request/Response DTOs for all entities
│   │   ├── Interfaces/               # Service + port interfaces (IPortalContextAccessor, IRefreshTokenStore, ...)
│   │   ├── Mapping/                  # AutoMapper profile
│   │   └── Validators/               # FluentValidation validators (Portal, Module, User, Auth, ChangePassword)
│   │
│   ├── DnnMigration.Infrastructure/  # Data Access, Identity
│   │   ├── Data/                     # DnnDbContext, Configurations, Migrations
│   │   ├── Repositories/             # EF Core repository implementations
│   │   └── Identity/                 # JWT token service, BCrypt password hasher, refresh-token store, portal-context default
│   │
│   └── DnnMigration.Api/             # REST Controllers, Middleware, host
│       ├── Controllers/              # Portals, Modules, Users, Roles, Tabs, Auth, Health
│       ├── Middleware/               # Exception handling, correlation id
│       ├── Identity/                 # HttpPortalContextAccessor (host/alias → PortalID)
│       └── Program.cs                # Application entry point (replaces Global.asax)
│
└── tests/
    ├── DnnMigration.UnitTests/       # Service/domain/validator unit tests (352)
    └── DnnMigration.IntegrationTests/ # API integration tests (110)
```

### Frontend Structure
```
frontend/src/app/
├── core/                             # Auth, Services, Models
│   ├── auth/                         # AuthService, AuthGuard, AuthInterceptor
│   ├── services/                     # ApiService
│   └── models/                       # User, Auth models
│
├── shared/                           # Reusable Components
│   ├── components/                   # DataTable, FormControls, ConfirmationDialog, LoadingSpinner
│   ├── pipes/                        # DateFormatPipe
│   └── directives/                   # Autofocus, HasPermission, Tooltip, ValidationHighlight
│
├── features/                         # Feature areas (lazy-loaded, standalone components)
│   ├── portal/                       # Portal management (list, form)
│   ├── module/                       # Module management (list, form)
│   ├── user/                         # User management (list, form, change-password)
│   ├── role/                         # Role management (list, form)
│   └── auth/                         # Authentication (login only; password recovery is out of scope — see Scope Reconciliation)
│
├── layout/                           # Application Shell
│   ├── header/                       # Header component
│   ├── sidebar/                      # Navigation sidebar
│   └── footer/                       # Footer component
│
├── app.component.ts                  # Root component
├── app.config.ts                     # Application configuration (providers)
└── app.routes.ts                     # Route definitions (lazy-loaded features)
```

### API Endpoints

| Endpoint | Methods | Description |
|----------|---------|-------------|
| `/api/portals` | GET, POST | Portal list and creation |
| `/api/portals/{id}` | GET, PUT, DELETE | Portal CRUD by ID |
| `/api/modules` | GET, POST | Module list and creation |
| `/api/modules/{id}` | GET, PUT, DELETE | Module CRUD by ID |
| `/api/modules/by-definition` | GET | Get module by portal + definition friendly name (query: `portalId`, `friendlyName`) |
| `/api/users` | GET, POST | User list and creation |
| `/api/users/{id}` | GET, PUT, DELETE | User CRUD by ID |
| `/api/users/by-username` | GET | Get user by portal + username (query: `portalId`, `username`) |
| `/api/users/{id}/change-password` | POST | Change a user's password |
| `/api/roles` | GET, POST | Role list and creation |
| `/api/roles/{id}` | GET, PUT, DELETE | Role CRUD by ID |
| `/api/tabs` | GET, POST | Tab/Page list and creation |
| `/api/tabs/{id}` | GET, PUT, DELETE | Tab CRUD by ID |
| `/api/auth/login` | POST | User authentication (issues JWT access + refresh) |
| `/api/auth/refresh` | POST | Token refresh (single-use rotation) |
| `/api/auth/logout` | POST | Revoke refresh token(s) |
| `/api/auth/me` | GET | Current user info (portal-scoped) |
| `/health` | GET | Health check endpoint (`[AllowAnonymous]`) |

---

## What Was Delivered

### Backend Domain (VB.NET → C# 12)
- Core domain entities and enums modeled as C# 12 POCOs with nullable reference types enabled.
- Legacy VB semantics preserved (e.g., `UserMembership.Approved` defaults to `true`; `DesktopModule` feature flags computed over the `SupportedFeatures` bitmask; `UserProfile` exposes a read-only `ProfilePropertyDefinitionCollection`).
- Generic repository interface (`IRepository<T>`) plus per-entity repository interfaces in the Domain layer.

### Application & API
- Application **services** holding the business rules extracted from the legacy `*Controller.vb` classes (static `Shared` members converted to DI instance methods), request/response **DTOs** (including a credential-safe `UserDto`), an AutoMapper **profile**, and **FluentValidation** validators for create/update/auth/change-password DTOs.
- **API** controllers for Portals, Modules, Users, Roles, Tabs, Auth, and an `[AllowAnonymous]` Health controller; exception-handling and correlation-id **middleware**; and a `Program.cs` host wiring DI, JWT bearer, CORS, rate limiting, FluentValidation auto-validation, and OpenAPI. Controllers stay thin (no business logic) and never return EF entities.
- RFC 7807 Problem Details for errors; the `{ "data": ..., "meta": ... }` envelope for success.

### Infrastructure (ADO.NET/SqlDataProvider → EF Core 8)
- `DnnDbContext` with `IEntityTypeConfiguration<T>` Fluent mappings to the **existing** DNN/`aspnet_*` schema (verbatim table, column, and FK-constraint names; no schema change).
- EF Core repositories with `AsNoTracking()` on read paths; identity components — `JwtTokenService`, BCrypt `PasswordHasher`, `InMemoryRefreshTokenStore` (single-use refresh-token rotation + revocation), and the host/alias-aware `HttpPortalContextAccessor`.

### Angular 19 Frontend
- Standalone-component SPA: `core` (auth service/guard/interceptor, API service, models including the `{ data, meta }` envelope contract), `shared` components/directives/pipes, `features` (portal/module/user/role/auth), and `layout`, bootstrapped via `main.ts` + `app.config.ts` + `app.routes.ts` with lazy-loaded feature routes.
- Global design-system tokens in `styles.scss`, consumed consistently by components (no divergent or hardcoded values); typed reactive forms whose validation mirrors the original ASPX validators; a JWT auth interceptor with silent refresh on 401.

### Docker Deployment
- Multi-stage `api.Dockerfile` (sdk → aspnet alpine, non-root user) and `frontend.Dockerfile` (node build → nginx serve).
- `docker-compose.yml` orchestration supplying `ConnectionStrings__Default` **and** `Jwt__SecretKey` to the API, with a health-gated frontend dependency.
- Hardened `nginx.conf` reverse-proxy configuration (SPA fallback, `/api` proxy, static caching, `server_tokens off`, and security headers including a Content-Security-Policy).

### Cross-Cutting Quality
- Clean/Onion architecture layering with dependencies pointing inward.
- Structured logging (Serilog) and consistent error handling with correlation-id propagation.
- Secrets kept out of source control (the base `appsettings.json` ships with empty connection string and JWT secret; real values are supplied via environment variables / user-secrets; `Program.cs` fails fast on a missing/weak production key).

---

## Conclusion

The DnnMigration project has migrated the legacy DotNetNuke 4.x VB.NET/Web Forms codebase to a modern **C# 12 / .NET 8** ASP.NET Core Web API (BFF) plus an **Angular 19** SPA, preserving Portal/Module/User/Role/Tab domain semantics and the existing relational schema while replacing the language, framework, data-access mechanism, and presentation model. The solution builds clean under `--warnaserror`, the backend unit (352) and integration (110) suites pass, and the Angular production build and unit specs (254) pass.

Two items are **environment-bounded**, not open defects: the Docker container gates (6–7) must be executed in a Linux Docker CI environment, and the Angular 19 production dependency advisories are an AAP-constrained residual pending a dedicated 20/21 upgrade. Both are documented here and in `MIGRATION_NOTES.md`.

### Next Steps
1. **Containers**: Execute Gates 6–7 in a Linux Docker CI environment (H5); provision `DB_CONNECTION_STRING` and `JWT_SECRET_KEY`.
2. **Environment**: Configure the database connection and JWT secret via environment variables / user-secrets (never commit them).
3. **Dependencies**: Schedule the Angular 20/21 upgrade (M6) to clear the production advisories.
4. **Operational readiness**: Set up CI/CD, monitoring, security hardening, and performance/load testing before production.
