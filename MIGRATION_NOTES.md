# Migration Notes — DotNetNuke 4.x → .NET 8 + Angular 19

> **Living document.** This file is the canonical decision log for the full-rewrite tech-stack
> migration of the legacy **DotNetNuke (DNN) 4.x** content-management framework into a modern,
> decoupled stack. It records architecture decisions, transformation conventions,
> documented-but-unfixed legacy bugs, and schema-compatibility notes for a **full rewrite executed
> in a single phase** — not an incremental, page-by-page port. Downstream backend and frontend
> agents **append** entries to the living sections (notably the bug-tracking and schema-change
> tables) as they implement details and discover legacy behavior.

**Resolved target project name:** `DnnMigration`. Legacy `DotNetNuke.*` namespaces are remapped to `DnnMigration.*`.

**Authoritative sources for this log**

| Source | Role |
|--------|------|
| `docs/technical-specifications.md` | Full technical specification / Agent Action Plan (AAP) governing the migration |
| `docs/project-guide.md` | Completion / assessment guide with metrics and architecture overview |
| `Library/AssemblyInfo.vb` | Legacy assembly baseline (`AssemblyTitle("DotNetNuke")`, `AssemblyVersion("4.9.0.85")`) |

## 1. Migration Overview

This migration converts the legacy DNN 4.x framework (VB.NET, .NET Framework / CLR 2.0, ASP.NET Web
Forms) into two decoupled, modern applications plus a container topology:

- A **C# 12 / .NET 8 LTS** Backend-for-Frontend (BFF) **ASP.NET Core 8 Web API**.
- An **Angular 19** single-page application (SPA) that consumes the API over a versioned JSON REST contract.
- **Docker** containerization targeting Linux (Alpine base images).

### Legacy Baseline

| Attribute | Value | Source |
|-----------|-------|--------|
| Assembly | `DotNetNuke.dll` | `Library/AssemblyInfo.vb` (`AssemblyTitle("DotNetNuke")`) |
| Assembly version | `4.9.0.85` | `Library/AssemblyInfo.vb` (`AssemblyVersion("4.9.0.85")`) |
| Target runtime | .NET Framework / CLR 2.0 | `DotNetNuke_VS2008.sln` |
| VB.NET source files | ~634 `.vb` | AAP §0.2.1 |
| VB project files | 20 `.vbproj` | AAP §0.2.1 |
| Schema / migration scripts | 91 `.SqlDataProvider` | AAP §0.2.1 |

### Repository Layout Decision

The legacy `Library/` (core business logic, entities, data access) and `Website/` (Web Forms
presentation) trees are **retained in place as migration reference** and are **never edited in
place**. All new code is created alongside them under new top-level trees:

- `backend/` — the .NET 8 solution (`DnnMigration.sln`): Domain, Application, Infrastructure, Api, and test projects.
- `frontend/` — the Angular 19 SPA.
- `docker/` — Dockerfiles, `docker-compose.yml`, and `nginx.conf`.

## 2. Migration Strategy & Transformation Dimensions

The rewrite spans five simultaneous transformation dimensions (AAP §0.1.1):

| Dimension | Current | Target |
|-----------|---------|--------|
| Language | VB.NET (`Option Strict On`, `Option Explicit On`) | Idiomatic C# 12 with nullable reference types |
| Framework | .NET Framework / CLR 2.0 | .NET 8 LTS (`net8.0`) |
| Presentation | ASP.NET Web Forms (`.aspx`/`.ascx`, ViewState, postback) | ASP.NET Core 8 Web API (BFF) + Angular 19 SPA |
| Data Access | ADO.NET via `DataProvider` / `SqlDataProvider` / `SqlHelper` | EF Core 8 (Code-First to existing schema, Fluent API) |
| Deployment | Windows / IIS monolith (`DotNetNuke.dll`) | Linux containers (Docker, Alpine bases) |

### One-Phase Execution Decision (AAP §0.4.5)

The original prompt's multi-phase "implementation sequence" is treated **only as logical
work-streams for comprehension**, not as a time-ordered schedule. All target files — backend,
frontend, Docker, and documentation — are created together in a **single phase**; the migration is
not split into incremental releases.

## 3. Architecture & Design-Pattern Decisions

**Backend dependency direction (Clean / Onion architecture):**
`Domain → Application → Infrastructure → Api`. Plain C# POCO entities live in Domain; business rules
extracted from the legacy `*Controller.vb` classes live in Application; EF Core repositories and the
`DnnDbContext` live in Infrastructure; controllers and middleware live in Api.

Each target pattern replaces a specific legacy construct (AAP §0.3.3):

| Pattern | Replaces (legacy construct) |
|---------|-----------------------------|
| Clean / Onion architecture | Monolithic `DotNetNuke.dll` `Library` assembly |
| Repository + Unit of Work | `DataProvider.Instance()` + `SqlHelper` |
| Service Layer | Business logic inside `*Controller.vb` |
| DTO + AutoMapper | Direct serialization of `*Info` objects |
| Built-in Microsoft DI | `Framework.Reflection.CreateObject` singletons |
| Middleware pipeline (`ExceptionHandlingMiddleware` RFC 7807, `CorrelationIdMiddleware`) | Web Forms global error handling |
| BFF + JWT | `AspNetSqlMembershipProvider` + Forms authentication |
| Options pattern (`IOptions<T>`) | `web.config` `<appSettings>` / provider blocks |

**Frontend standards (Angular 19):** standalone components (no NgModules except third-party);
signals for state; `inject()` dependency injection; the new control flow (`@if`/`@for` with `track`,
and `@switch`); typed Reactive Forms (`FormGroup<T>`); OnPush change detection; feature-organized
folders with lazy-loaded routes; Angular services restricted to API communication only.

## 4. Namespace & Entity Remapping

**Namespace remap (AAP §0.1.3, §0.5.3):**

| Legacy namespace | Target namespace |
|------------------|------------------|
| `DotNetNuke.Entities.*` | `DnnMigration.Domain.Entities` |
| `DotNetNuke.Data` | `DnnMigration.Infrastructure.Data` |
| `DotNetNuke.Security.*` | `DnnMigration.Domain` / `DnnMigration.Application` |
| `DotNetNuke.Services.*` | `DnnMigration.Application.Services` |

**Entity rename (AAP §0.1.3, §0.2.2):**

| Legacy `*Info` class | Target entity | Notes |
|----------------------|---------------|-------|
| `PortalInfo` | `Portal` | Multi-tenant site container |
| `ModuleInfo` | `Module` | Pluggable content component |
| `UserInfo` | `User` | Credentials moved to Identity |
| `RoleInfo` | `Role` | Permission grouping |
| `TabInfo` | `Tab` | Page / navigation node |
| `UserRoleInfo` | `UserRole` | User ↔ Role join entity |
| `Permission` | `Permission` | Name retained |
| `ModulePermission` | `ModulePermission` | Name retained |
| `TabPermission` | `TabPermission` | Name retained |
| `FolderPermission` | `FolderPermission` | Name retained |

All entities **drop XML-serialization attributes** and adopt **nullable reference types**. The DNN
`Null.NullInteger` sentinel (the `-1` value used throughout `PortalInfo.vb`) is preserved as `int?`
— or a retained sentinel — wherever comparison logic depends on the exact value; `Null.NullString`
and `Null.NullDate` map to `string?` and `DateTime?` respectively.

## 5. Data Access & Schema-Compatibility Notes

### Legacy Call Chain

```text
*Controller.vb → DataProvider.Instance() → SqlDataProvider → SqlHelper → stored procedure → IDataReader → FillInfo() → *Info object
```

### Target Data Access

Constructor-injected `I*Repository` interfaces (defined in Domain) are backed by EF Core repository
implementations (in Infrastructure) over a single `DnnDbContext`. Queries use **async LINQ**;
transaction boundaries formerly handled by `DataProvider` become EF Core
`IUnitOfWork` / `SaveChangesAsync` boundaries.

### Schema-Compatibility (CRITICAL — AAP §0.1.2, §0.3.5, §0.7.1)

- EF Core 8 uses **Code-First mapped to the EXISTING SQL Server schema**. Table structures are
  **NOT altered** in this phase.
- Entity-to-table binding is achieved via the **Fluent API** — `ToTable`, `HasKey`, `HasColumnName`,
  `HasConstraintName`, and relationships via `HasOne` / `WithMany` / `HasForeignKey` — declared in
  per-entity `IEntityTypeConfiguration<T>` classes applied in `OnModelCreating`. Fluent API
  configuration carries the highest precedence, preserving legacy table and column names.
- Legacy table / column names are sourced from the **91 `.SqlDataProvider` scripts** and the
  `*Info.vb` field definitions. Those 91 scripts are treated as the **authoritative schema
  reference** and are **not re-executed or modified** by this migration.
- The active connection moves from the legacy `SiteSqlServer` connection (`Website/release.config`,
  database `DotNetNuke`) to `ConnectionStrings:DefaultConnection` in `appsettings.json`.

**Schema changes required for EF Core compatibility:** _none required at authoring time._ Any schema
change strictly required for EF Core compatibility MUST be recorded in the table below by the agent
that introduces it.

| Date | Change | Reason | Introduced By |
|------|--------|--------|---------------|
| — | _(none yet)_ | — | — |

## 6. Security Migration Decisions

- **Authentication:** Forms authentication + `AspNetSqlMembershipProvider` (`Website/release.config`)
  → **JWT Bearer** (`Microsoft.AspNetCore.Authentication.JwtBearer`) with **short-lived (60-minute)
  access tokens** and **refresh-token rotation**. The user → role → permission model is preserved.
- **Credentials:** legacy **DES** Encrypt/Decrypt in `PortalSecurity.vb` → **BCrypt**
  (`BCrypt.Net-Next`). This implies a credential-handling transition: **legacy DES-stored password
  values cannot be verified by BCrypt**, so a data-migration / forced-reset strategy is required for
  existing credentials (recorded here as a migration consideration).
- **Non-functional security requirements:** HTTPS enforcement; CORS restricted to the Angular
  origin; anti-forgery for cookie-based flows; rate limiting on authentication endpoints; structured
  logging (Serilog) with correlation IDs and **no sensitive-data leakage**; CSP headers served by
  nginx; secure token storage on the frontend (httpOnly cookies preferred, or memory-only).

## 7. Dependency Replacement Log

**Removed legacy dependencies → replacement (AAP §0.5.2):**

| Legacy dependency | Replacement |
|-------------------|-------------|
| `Microsoft.ApplicationBlocks.Data` (`SqlHelper`) | EF Core 8 (`DnnDbContext`) |
| `System.Data.SqlClient` | `Microsoft.Data.SqlClient` (via EF Core SqlServer provider) |
| `System.Web` / `System.Web.UI` (Web Forms) | `Microsoft.AspNetCore.*` (Web API) |
| `System.Web.Security` (`AspNetSqlMembershipProvider`) | ASP.NET Core JWT auth + BCrypt |
| `System.Web.Extensions` (ASP.NET AJAX) | Angular `HttpClient` |
| `Microsoft.VisualBasic` (VB runtime) | Removed — idiomatic C#, no VB runtime |
| `DotNetNuke.WebControls` | Angular components |
| `SharpZipLib` | Not required in core scope (`System.IO.Compression` if needed) |
| Telerik RadControls | Excluded — replaced by Angular shared components |

**Key new packages with pinned versions (AAP §0.5.1):**

| Package | Version |
|---------|---------|
| `Microsoft.EntityFrameworkCore.SqlServer` / `.Design` / `.Tools` | 8.0.11 |
| `Microsoft.AspNetCore.Authentication.JwtBearer` | 8.0.11 |
| `Microsoft.AspNetCore.OpenApi` | 8.0.11 |
| `Swashbuckle.AspNetCore` | 6.9.0 |
| `AutoMapper.Extensions.Microsoft.DependencyInjection` | 12.0.1 |
| `FluentValidation.AspNetCore` | 11.3.0 |
| `Serilog.AspNetCore` | 8.0.3 |
| `BCrypt.Net-Next` | 4.0.3 |
| `@angular/*` | ^19.0.0 |
| `rxjs` | ^7.8.1 |
| `zone.js` | ^0.15.0 |

## 8. Migration Comment Convention & Bug Tracking

**Mandated conventions (AAP §0.7.2, §0.7.4):**

- Backend changes are annotated with `// MIGRATION: [explanation]`.
- Frontend changes are annotated with `// MIGRATION: [explanation]`.
- Business rules are extracted **exactly** as implemented in VB.NET — they are **not** optimized,
  refactored, or "improved" during conversion.
- Bugs discovered in the legacy code are **documented, not fixed**: each is captured as a
  `// MIGRATION:` comment at the call site and recorded in the table below, unless the bug **blocks**
  the migration (in which case the applied fix is documented here as well).

### Documented-but-Unfixed Legacy Bugs

Downstream backend and frontend agents **append** entries to this table as they discover and
document legacy bugs during conversion.

| ID | Location (legacy source) | Description | Decision | Migration Comment Ref |
|----|--------------------------|-------------|----------|-----------------------|
| — | _(none recorded yet)_ | — | — | — |

## 9. Behavior Preservation & Scope Boundaries

### Preservation Requirements (AAP §0.7.1)

- Functional parity for all **portal, module, user, role, and permission** management workflows.
- **Identical outcomes for identical inputs** — including validation rules and error messages.
- **Multi-tenant isolation:** every entity and query remains scoped by `PortalId`; portals stay independent sites.
- Module **registration / lifecycle** semantics (install, configure, remove per portal) are preserved.
- Content versioning behavior is preserved where present.

### Out of Scope (AAP §0.6.2)

Telerik RadControls; DNN 4.x authentication providers; deprecated DNN APIs (`HostSettings.vb`,
`Globals.vb`, the legacy module-loader, `DotNetNuke.Services.Scheduling`); COM interop / VB6 /
ActiveX; Web Forms postback infrastructure (ViewState, `ScriptManager` / `UpdatePanel`) and all
`.aspx` / `.ascx` / `.master` / `.asmx` markup; server-side HTML rendering (no Razor / SSR); legacy
`.vbproj` and `.sln` files; non-core subsystems (Skinning/Container, Search, Caching, FriendlyUrl,
Newsletter/Messaging, Vendors/Banners, and the DNN Logging Provider — replaced by Serilog); and
database schema changes.

### Retain-Legacy Decision (AAP §0.6.3)

The legacy `DotNetNuke.sln`, `DotNetNuke_VS2008.sln`, all `.vbproj` projects, and the `Library/` and
`Website/` trees are **retained as reference (not deleted)**. Existing repository metadata
(`mkdocs.yml`, `catalog-info.yaml`) is **left as-is**.

## 10. Validation Gates

The migration is considered complete only when all seven gates pass (AAP §0.1.2):

| Gate | Name | Command / Criterion | Pass Condition |
|------|------|---------------------|----------------|
| Gate 1 | API Compilation | `dotnet build --configuration Release --warnaserror` | Exit 0; zero errors/warnings (excluding CS8618 nullable) |
| Gate 2 | API Unit Tests | `dotnet test --configuration Release --no-build` | Exit 0; 100% pass |
| Gate 3 | Angular Build | `npm ci` + `ng build --configuration production` | Exit 0; zero errors/warnings |
| Gate 4 | Angular Unit Tests | `ng test --watch=false --browsers=ChromeHeadless --code-coverage` | Exit 0; 100% pass |
| Gate 5 | API Integration Tests | `dotnet test --filter "Category=Integration"` | POST `/api/portals` → 201, GET `/api/portals/{id}` → 200, PUT → 200, DELETE → 204 (equivalent for Module, User) |
| Gate 6 | Container Build | `docker-compose build` | Exit 0; both images built |
| Gate 7 | Container Startup | `docker-compose up -d` + `curl -f http://localhost:8080/health` and `http://localhost:4200` | Both return HTTP 200 |

### API Response Envelopes

- **Success:** `{ "data": {...}, "meta": {...} }`
- **Error (RFC 7807):** `{ "type", "title", "status": 400, "detail", "errors": {} }`

The CRUD status-code contract validated by Gate 5 is: **POST → 201, GET → 200, PUT → 200, DELETE → 204**.

---

_Maintained per AAP §0.1.2, §0.6.1, and §0.7.2. This is a living log — keep entries concise and
append new decisions, schema-change notes, and documented legacy bugs to the relevant tables as the
migration progresses._

