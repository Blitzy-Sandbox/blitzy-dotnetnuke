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

### 6.1 `AuthService` (Application layer) — login / refresh / logout / current-user

`backend/src/DnnMigration.Application/Services/AuthService.cs` orchestrates authentication, assembled
"from scratch" from `UserController.ValidateUser` (login check order), the `UserMembership.vb`
Approved/LockedOut/LastLoginDate lifecycle, and `PortalSecurity.SignOut` (L77). It depends on the
Domain repositories plus three Application-layer ports (`IPasswordHasher`, `ICredentialStore`,
`IJwtService`); `IMapper` projects `User → CurrentUserDto`. `LoginResponse` is **success-only** —
failures (including the legacy `LOGIN_INSECUREADMINPASSWORD` / `LOGIN_INSECUREHOSTPASSWORD`
"must-change-password" outcomes) surface via `Result.Failure(...)`. All six constructor dependencies
are null-guarded with `ArgumentNullException.ThrowIfNull` and every request method null-guards its
`request` (CP1 review AuthService #1/#2/#5).

- **COORDINATION GAP (resolved):** the ports `IPasswordHasher` and `IJwtService` were **absent** from
  `DnnMigration.Application/Interfaces/` even though the already-committed concrete adapters
  `Infrastructure/Identity/PasswordHasher.cs` and `JwtService.cs` declare `: IPasswordHasher` /
  `: IJwtService` against `DnnMigration.Application.Interfaces`. A baseline `--warnaserror` build proved
  the Infrastructure project failed with `CS0246` (both types unresolved) and could not compile, which
  also blocked `AuthService` and the unit-test project. **Decision:** created the two missing
  Application-layer port interfaces (`Interfaces/IPasswordHasher.cs`, `Interfaces/IJwtService.cs`) with
  signatures matching the concrete adapters exactly (the `GenerateAccessToken` return-tuple element
  names are part of the contract to avoid `CS8141`). The Application project still references **Domain
  only** — no Infrastructure project reference was added.
- **CREDENTIAL-STORE via `ICredentialStore` port (fail-closed this phase):** the `User` entity has **no
  password-hash field** and `IUserRepository` has **no credential lookup**, so the stored hash required
  by `IPasswordHasher.Verify` is sourced from the `ICredentialStore` port —
  `storedHash = await _credentialStore.GetPasswordHashAsync(user.UserId, ct)` at `LoginAsync` step 4.
  This is the **same port** `UserService.CreateAsync` persists the initial hash through (CP1 review
  UserService #5), so the create→verify credential lifecycle is coherent end-to-end (no more hardcoded
  `storedHash = null` placeholder). The concrete `ICredentialStore` adapter (mapping onto the migrated
  membership schema with BCrypt values) is owned by Infrastructure and **deferred to CP2**; until it is
  realized `GetPasswordHashAsync` returns `null`, so the `IsNullOrEmpty(storedHash)` guard short-circuits
  before `IPasswordHasher.Verify` and login still **fails closed** — the exact fail-closed behavior the
  CP1 review accepted (AAP matrix #10). The integration-test `TestAuthHandler` continues to bypass login
  for this reason. **Follow-up (CP2):** implement the `ICredentialStore` adapter; login then succeeds for
  a correct credential with no further `AuthService` change.
- **Insecure default-password parity (CP1 review AuthService #4 — corrected):** transcribed VERBATIM from
  `UserController.ValidateUser` L1144-1153. The admin check fires only for a **non-super-user** success
  **with username `admin`** and password `admin`/`dnnadmin`; the host check fires only for a **super-user**
  success **with username `host`** and password `host`/`dnnhost`. Both username predicates (and the
  non-super-user gate on the admin branch) were previously missing — the check fired on the password alone
  regardless of username — and are now restored. All comparisons use `StringComparison.Ordinal` (legacy
  `Option Compare Binary` compared exact literals), and the username compared is the inbound
  `request.Username`.
- **Verification-code approval parity (CP1 review AuthService #3):** transcribed VERBATIM from
  `AspNetMembershipProvider.UserLogin` L1465-1477. For an **unapproved non-super-user**, when the supplied
  verification code equals the legacy `"{portalId}-{userId}"` pattern the account is approved and
  **persisted (`UpdateUser`) before** credential verification, then login continues; otherwise it fails
  (`LOGIN_USERNOTAPPROVED`). Unapproved **super-users are not blocked** here (legacy gate
  `Approved = False And IsSuperUser = False`). The legacy behavior of persisting the approval even when the
  password subsequently fails is preserved deliberately, not "fixed" (AAP §0.7.2).
- **Deferred authorization helpers:** `PortalSecurity.IsInRole` (L103), `IsInRoles` (L115), and
  `HasNecessaryPermission` (L517-550, `SecurityAccessLevel` switch) are **documented, not implemented**
  in this file (not on `IAuthService`). Role claims are already signed into the access token; these
  move to JWT-claims-driven `[Authorize]` policies at the Api layer in a later phase. The legacy DES
  `Encrypt`/`Decrypt`/`CreateKey` are **not** migrated (replaced by BCrypt + the JWT issuer).

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

## 11. Dependency & Security Decisions

### CP1 — Application references **core `AutoMapper` 12.0.1** + scoped audit suppression for GHSA-rvv3-g6hj-g44x

**Decision.** `DnnMigration.Application` references the **core `AutoMapper`** package directly (not
`AutoMapper.Extensions.Microsoft.DependencyInjection`). The Application layer uses only the core
`Profile` base class (Mapping profiles) and `IMapper` at the service boundary; it calls no
DI-extension API. The DI-extension package remains in the **Api host** composition root
(`DnnMigration.Api.csproj`), where AutoMapper profiles are registered. This keeps the class library
free of DI-container references (split-package discipline).

**Consequence & handling.** Making `AutoMapper` a *direct* reference surfaces NuGet audit advisory
**GHSA-rvv3-g6hj-g44x / CVE-2026-32933** (High, CWE-674 — Denial of Service via uncontrolled
recursion on cyclic/self-referential type maps), which `--warnaserror` promotes to a build error.
A **targeted** `<NuGetAuditSuppress Include="https://github.com/advisories/GHSA-rvv3-g6hj-g44x" />`
is added to `DnnMigration.Application.csproj`. This is advisory-specific (NOT a broad `<NoWarn>` or
`<NuGetAudit>false</NuGetAudit>`), and is justified and bounded as follows:

1. **AAP-pinned, no patched version reachable.** AAP §0.5.1 pins AutoMapper to **12.0.1**. The flaw is
   fixed only in **15.1.1 / 16.1.1+**; the maintainer has publicly confirmed no patch for the
   12.x/13.x/14.x line. Upgrading is out of scope for this migration and impossible offline (only
   12.0.1 is in the warm NuGet cache).
2. **Not introduced by this change.** AutoMapper 12.0.1 was already present at the clean Gate-1
   baseline as a **transitive** dependency of `AutoMapper.Extensions.Microsoft.DependencyInjection`.
   The default NuGet audit mode (`direct`) audits only direct references, so the advisory was simply
   not surfaced before. The CP1-review-mandated split only **exposes** the pre-existing advisory; it
   adds no new runtime risk.
3. **Not exploitable in context.** The vulnerability triggers only on cyclic/self-referential maps
   (Type A → A, or A → B → C → A). Every AutoMapper profile here maps **flat POCO ↔ DTO** shapes;
   response DTOs deliberately omit entity collection navigations (e.g. `ModulePermissions`,
   `TabPermissions`), so no cyclic map exists and the vulnerable recursion path is never built.

**Revisit when** the AAP is permitted to advance AutoMapper to a patched line (15.1.1 / 16.1.1+), at
which point the suppression is removed.

---

## 12. CP1 Domain/Application Lifecycle-Parity Decisions

The CP1 review required the legacy module/tab/role lifecycle and multi-tenant rules to be transcribed
verbatim rather than deferred. The behaviors were ported into the Application services; the items below
record the faithful adaptations made where the legacy DNN runtime model (admin/super-tab pointers,
in-memory ArrayList reordering, separate stored-procedure round-trips) maps onto the simplified,
portal-scoped Clean-Architecture model. Each is annotated inline with `// MIGRATION:` at the call site.

### 12.1 Multi-tenant contract changes (CP1 review — repository + service interfaces)

Portal-scoping was pushed into the ID-based contracts so tenant ownership is enforceable at the
Application boundary (AAP §0.7.1): `IUserRepository`/`IRoleRepository`/`IModuleRepository`/
`ITabRepository` `GetByIdAsync`/`DeleteAsync` (and the role user-role query) now carry `portalId`; the
matching service interfaces (`IUserService`/`IRoleService`/`IModuleService`/`ITabService`) and the
`UsersController`/`RolesController`/`ModulesController`/`TabsController` thread a required `portalId`
query parameter through `GetById`/`Update`/`Delete` (and carry it in `CreatedAtAction` route values).
Not-found messages for these portal-scoped lookups are **opaque** (no raw id echoed) to avoid
cross-tenant enumeration (CP1 review AuthService #7).

### 12.2 Module lifecycle (`ModuleService` — CP1 review ModuleService #3, CRITICAL)

- **Permissions.** Create persists **every** supplied grant with no AllowAccess filter (legacy
  `AddModule` L649-659). Update performs a delete-all-then-re-add diff (the `CompareTo` short-circuit is
  collapsed to an unconditional rebuild, same end state) preserving **two** legacy filters exactly: the
  `InheritViewPermissions && PermissionKey = "VIEW"` skip and the AllowAccess-only persist
  (`UpdateModule` L1099-1118).
- **Ordering.** `ModuleOrder == -1` places the module at the bottom of its pane (max + 2,
  `UpdateModuleOrder` L1160-1173); the tab's placements are then re-sequenced **per pane** to
  `Counter*2-1` (1, 3, 5, …, `UpdateTabModuleOrder` L1197-1209).
- **Default module.** `IsDefaultModule` writes the portal `"defaultmoduleid"`/`"defaulttabid"` settings
  via `IPortalSettingsService.SetSettingAsync` (legacy `UpdateModule` L1126-1130).
- **AllModules propagation.** Copies this module's display settings to the portal's modules
  (`UpdateModule` L1132-1144). **Adaptation:** the legacy code excluded admin tabs, but the migrated
  `Tab` entity dropped the admin/super-tab flag; portal-scoping (host tabs already excluded) is the
  expressible boundary.
- **Delete / multi-instance.** Soft-delete (`IsDeleted = true`, detach `TabId`) then re-sequence the
  former tab. The `moduleId`-scoped contract represents a single placement; the legacy "module already
  in the page" duplicate-placement tolerance (try/catch that ignored the error, `AddModule` L662-665) is
  preserved by performing **no** duplicate-add rejection.

### 12.3 Tab lifecycle (`TabService` — CP1 review TabService #2, CRITICAL)

- **Permissions.** Both create and update persist **only AllowAccess** grants — create iterates the
  supplied collection with the AllowAccess filter (`AddTab` L345), update performs the delete-all-then-
  re-add diff with the AllowAccess filter (`UpdateTab` L799-808). **Unlike Module, there is NO
  `InheritViewPermissions` / `PermissionKey = "VIEW"` skip** — that special case is Module-only
  (`UpdateModule` L1106-1112).
- **Sibling ordering (`ResequencePortalTabOrderAsync`).** Reproduces the **net observable effect** of
  the legacy `UpdatePortalTabOrder` in-memory ArrayList reorder (L550-778): the portal's non-deleted
  tabs are normalized to `1, 3, 5, …` (`(counter*2)-1`; legacy `intDesktopTabOrder` seeded -1, += 2,
  final loop L753-774 which runs even for the delete/`NewParentId = -2` case), with `TabOrder = 0`
  ordered last to preserve the legacy `0 -> 999` push-to-end (L577-579). Run after create, update, and
  delete. **Adaptations:** (a) the admin/super-tab `9999+` special-casing (L755-760) is **not
  expressible** because the migration dropped the admin/super-tab flag from `Tab` (and
  `AdminTabId`/`SuperTabId` are presentation navigation pointers) — portal-scoping already excludes host
  tabs, so all of a portal's tabs are normalized uniformly; (b) the `MoveTab` reparent reordering is
  reflected via the Level-from-parent rule + the `TabPath` cascade rather than the ArrayList shuffle.
- **Level-from-parent.** Create and update set `Level = parent.Level + 1` (root = 0), so a reparent
  updates the depth (`UpdatePortalTabOrder` L587/L601). Descendant `Level`s are **not** recomputed —
  legacy `UpdateChildTabPath` (L306) cascades only `TabPath`, not `Level`, to descendants, so this
  matches the legacy outcome. The recursive `TabPath` rename/reparent cascade is preserved.
- **Deferred (NOT among the CP1 findings; documented adaptations).** (a) `AddTab`'s `AddAllTabsModules`
  `CopyModule` step (L360-367) is a **cross-aggregate module-copy** behavior owned by the Module
  aggregate, not the `/api/tabs` surface; (b) the **shared-tab recycle-bin soft-delete** alternate path
  (`Tab.IsDeleted = true` instead of a permanent delete) — `DeleteTab` performs a permanent delete for
  the normal single-instance case (L451), which is the path implemented.

### 12.4 Role lifecycle (`RoleService` — CP1 review RoleService #3-#6)

- **Billing/trial defaults** (`EditRoles.ascx.vb`): fee/trial fee default to 0, periods to 1, frequency
  to `"N"` unless the full field group is supplied and frequency is not `"N"` — applied on create and
  update before persistence.
- **Auto-assignment** (`RoleController.AutoAssignUsers`): when `AutoAssignment` is true, existing portal
  users are enrolled — invoked after create **and** after update (legacy `UpdateRole` re-invoked it,
  L254-257), preserving the duplicate-swallow behavior.
- **System-role guardrails**: update/delete of a portal's `AdministratorRoleId`/`RegisteredRoleId` is
  rejected with a legacy-equivalent message (loaded via `IPortalRepository` for portal context).

### 12.5 Auth lifecycle (`AuthService` + `RefreshRequestValidator` — CP1 review AuthService #1-#7, IJwtService #1)

The full credential/login parity decisions for §6.1 are consolidated here for traceability; the
verification-code and insecure-default-password sub-decisions are detailed in §6.1.

- **Constructor + request null guards (#1, #2, #5).** All six `AuthService` constructor dependencies use
  `ArgumentNullException.ThrowIfNull` before assignment, and `LoginAsync` / `RefreshAsync` / `LogoutAsync`
  null-guard `request` before any field access — consistent with the other Application services so DI
  misconfiguration or a malformed body fails fast instead of surfacing as a later `NullReferenceException`.
- **Verification-code approval branch (#3).** Step 3 now mirrors `AspNetMembershipProvider.UserLogin`
  L1465-1477 exactly (unapproved non-super-user; verification code `"{portalId}-{userId}"` → approve +
  persist + continue; otherwise not-approved failure; unapproved super-users not blocked). See §6.1.
- **Insecure-default-password username predicates (#4).** Step 5 restores the missing `admin`/`host`
  username predicates (and the non-super-user gate on the admin branch) per `UserController.ValidateUser`
  L1144-1153, `StringComparison.Ordinal`. See §6.1.
- **`ICredentialStore` login-verify wiring.** `LoginAsync` step 4 sources the stored hash from the
  `ICredentialStore` port (the same port `UserService.CreateAsync` persists through), replacing the
  hardcoded `storedHash = null` placeholder while preserving the accepted fail-closed behavior until the
  CP2 Infrastructure adapter is realized. See §6.1.
- **Refresh-token tenant binding (#6, IJwtService #1).** Resolved earlier in the CP1 pass:
  `IJwtService.GenerateRefreshToken(userId, portalId)` and `ValidateRefreshToken → RefreshTokenInfo(UserId,
  PortalId)`; `RefreshAsync` loads the user with the portal-scoped
  `GetByIdAsync(tokenInfo.PortalId, tokenInfo.UserId)`, and `GetCurrentUserAsync(portalId, userId)` /
  `GET /api/auth/me` reads both the `sub` and `portalId` JWT claims (fail-closed 401 on either missing) so
  a refresh/projection cannot cross portals (AAP §0.7.1).
- **Opaque not-found messages (#7).** `AuthService` failure messages carry no raw numeric IDs
  (e.g. "Invalid or expired refresh token.", "The current user could not be found.").
- **`RefreshRequestValidator` (NEW — #5).** `POST /api/auth/refresh` (and the reused logout body) gets a
  synchronous `NotEmpty` validator for the opaque refresh token. The refresh token is a **new JWT-model
  field with no legacy validator baseline**, so `NotEmpty` (which also rejects whitespace) raises none of
  the parity concern that `CreatePortalValidator` did (CP1 review CreatePortalValidator #1) — a
  whitespace-only token is meaningless and must be rejected. Token validity/expiry/revocation and the
  tenant binding remain `IJwtService` concerns, not validator concerns.

### 12.6 Portal lifecycle (`PortalService` - CP1 review PortalService #1-#5)

Ported from `Library/Components/Portal/PortalController.vb` (`CreatePortal` L980/L998-L1019,
`GetPortalSpaceUsedBytes` L1296, `HasSpaceAvailable` L1323-L1341).

- **Constructor + request null guards (#2).** `PortalService` now takes six constructor dependencies
  (the existing four plus `IPasswordHasher` + `ICredentialStore`), all `ArgumentNullException.ThrowIfNull`-guarded;
  `CreateAsync` null-guards `request` before any field access - consistent with the other services.
- **Paging validation + paged repository (#1, #5).** `GetAllAsync` rejects `pageIndex < 0` and `pageSize <= 0`
  with controlled `Result.Failure` messages ("Page index must be zero or greater." / "Page size must be greater
  than zero.") before any repository call, and now uses the new `IPortalRepository.GetPagedAsync(pageIndex,
  pageSize) -> (Items, TotalCount)` so only the requested page plus the total count is materialized (no more
  fetch-all-then-`Skip`/`Take`-in-memory). `PageIndex` stays zero-based for parity with the legacy
  `GetPortalsByName` paging. `GetAllAsync()` (all-portal) is retained for the internal `DeleteAsync` user loop.
- **Admin-user bootstrap (#3).** The legacy `CreatePortal` always created the portal Administrator
  (FirstName/LastName/Username/Password/Email were required parameters) and assigned `portal.AdministratorId`
  (L1015-1016). The deferral is now CLOSED: `CreatePortalRequest` carries an OPTIONAL `Admin*` group
  (`AdminUsername`/`AdminPassword`/`AdminFirstName`/`AdminLastName`/`AdminEmail`); `CreatePortalValidator`
  enforces the whole group together when any one is supplied (the same exact-grouping pattern used for the Role
  billing/trial group, non-trimming `IsNullOrEmpty` parity). When the group is supplied, `CreateAsync` creates
  the admin `User` (`PortalId`, `Username`, `FirstName`, `LastName`, `DisplayName = FirstName + " " + LastName`
  per L1004, `Email`, `IsSuperUser=false`, `IsApproved=true`), hashes `AdminPassword` one-way with BCrypt
  (`IPasswordHasher`, replacing the legacy reversible `Membership.Password`/DES) and persists it via the
  `ICredentialStore` port - the SAME ports `UserService.CreateAsync` uses, so the administrator is never
  credentialless - then sets `portal.AdministratorId = admin.UserId`. A brand-new portal has no existing users
  (no duplicate possible) and no roles yet (the AutoAssignment enrolment would be a no-op), so neither is
  re-checked here. **Documented deviation:** when the admin group is omitted the portal is created without an
  administrator and `AdministratorId` is left unset (the administrator can be provisioned later via the User
  API); the migration team's decision to make the group optional at the DTO level is preserved, while the
  bootstrap capability the review required is now present and tested. The skin/file-system/template/alias
  provisioning around the bootstrap (`CreatePortal` L1027-L1102) remains OUT OF SCOPE per AAP Section 0.6.2.
- **Storage-quota numerator (#4, CRITICAL).** `HasSpaceAvailableAsync` replaces the hardcoded `usedBytes = 0`
  with the real consumed bytes from the new `IPortalRepository.GetSpaceUsedBytesAsync(portalId)` (ports
  `GetPortalSpaceUsedBytes` L1296, which read the persisted "SpaceUsed" column). The quota formula
  `((usedBytes + fileSizeBytes) / 1048576d <= hostSpace) || hostSpace == 0` is preserved verbatim - the
  `1048576d` MB divisor (VB `1024 ^ 2`, floating division) and the `hostSpace == 0` (unlimited) /
  `portalId < 0` (Null.NullInteger) semantics were already correct and are unchanged. The space-used query is
  skipped only when there is no portal, exactly the case where `hostSpace = 0` short-circuits and where the
  legacy `GetPortalSpaceUsedBytes(-1)` returned 0 anyway, so the numeric result is identical.
- **Portal not-found messages.** Left as `Portal {portalId} was not found.` (echoing the route id). Portal is
  the host-level aggregate with no parent tenant, so the review explicitly PASSed it for tenant scope and the
  enumeration concern (CP1 review #7) does not apply - the id is the caller-supplied route resource, not a
  cross-tenant secret.

### 12.7 User-role assignment UI (`RoleAssignmentComponent` - `SecurityRoles.ascx.vb`)

`frontend/src/app/features/role/role-assignment/role-assignment.component.ts` re-expresses the orchestration +
validation of the legacy `Website/admin/Security/SecurityRoles.ascx.vb` (668 lines). Postback / ViewState /
`PortalModuleBase` / `.resx` / `DataCache` / `ClientAPI` machinery is discarded; the rules below are preserved.

- **PROVISIONAL / DEFERRED write endpoints.** `cmdAdd_Click` (L518-551) and `grdUserRoles_Delete` (L565-589)
  wrote user-role assignments via `RoleController.AddUserRole` / `DeleteUserRole`. The CP1 `RolesController`
  exposes ONLY the read-only `GET /api/roles/user/{userId}` lookup; there is NO user-role assignment WRITE
  endpoint (no `IRoleService` write method, no DTO) in this phase. `RoleService.assignUserRole`
  (POST `roles/assignments`) and `RoleService.removeUserRole` (DELETE `roles/assignments/{userRoleId}`) are wired
  to sensible REST sub-paths so the feature compiles and is unit-tested against mocks; the backend write endpoints
  are PENDING and must be implemented before these function at runtime.
- **Default-expiry billing math** (`GetDates` L273-303) preserved verbatim: for a new (user, role) pair with
  `BillingPeriod > 0`, expiry = now + period in days (`D`), days*7 (`W`), months (`M`), or years (`Y`); the
  effective date is left empty for new assignments; an existing assignment shows its stored effective/expiry dates.
- **Admin-account date-guard quirk (documented-not-fixed).** `cmdAdd_Click` L523 compared the integer
  `Role.RoleID` to `PortalSettings.AdministratorRoleId.ToString` - an int-vs-string comparison. The migration
  preserves the *numeric-equality intent* (clear effective/expiry dates only for the portal Administrator account
  on the Administrator role) rather than the literal string coercion, and records the legacy comparison here as a
  documented-not-fixed bug per AAP Section 0.7.2. `administratorId` / `administratorRoleId` arrive as optional
  component inputs from portal context (no `PortalSettings` contract exists this phase); when either is null the
  guard is inactive.
- **Admin-lockout delete guard** (`DeleteButtonVisible` L360-363 -> `RoleController.CanRemoveUserFromRole`,
  `[DNN-4285]`): the portal Administrator cannot be removed from the Administrator role (prevents lockout);
  enforced at the component layer (`canRemove`) and confirmation-gated by `ConfirmationDialogComponent` before any
  delete call.
- **Grid read adaptation.** The legacy role-focused grid used `GetUserRolesByRoleName` (all users-in-role); that
  endpoint is DEFERRED, so the grid is populated from the selected user's assignments via the confirmed
  `getUserRoles(userId)` read. Add-vs-Update labeling (`grdUserRoles_ItemDataBound` L641-664) and the role/user
  mode selection (`Page_Init` L411-421) are preserved at the component layer.

---

## 13. CP2 Infrastructure/Api Review Remediation Decisions

Remediation of the CP2 (Backend Infrastructure + Api + Unit Tests) code review. Each subsection records the
root-cause fix and any documented deviation.

### 13.1 JWT signing-key alignment, fail-fast, and key-strength validation (CP2 review — JwtService #1, Program.cs #1/#2/#5, appsettings #1/#2)

- **Single canonical key name.** The issuer (`JwtService`) previously read `Jwt:SigningKey` while the validator
  (`Program.cs` `AddJwtBearer`) and `appsettings*.json` used `Jwt:Key`, so no key was ever found: issuance threw
  and validation used a different (empty) key. `JwtService` now reads **`Jwt:Key`** — the one canonical name used
  by the issuer, the validator, `appsettings.json`, `appsettings.Development.json`, and the `Jwt__Key`
  environment variable (docker-compose). Issuer and validator now sign/validate with the SAME key.
- **Fail-fast, no hardcoded fallback (Security).** `Program.cs` previously fell back to a hardcoded insecure
  signing key when `Jwt:Key` was blank. That fallback was removed; the host now throws
  `InvalidOperationException` at startup when `Jwt:Key` is missing/blank, so a token is never signed or validated
  with a known key. `appsettings.json` intentionally ships an empty `Jwt:Key` (the secret is supplied per
  environment via configuration/secret store); `appsettings.Development.json` supplies a development key, and
  docker-compose supplies `Jwt__Key` (default ≥ 32 chars), so every runtime environment satisfies fail-fast.
- **Key-strength validation.** Before `AddJwtBearer`, the host validates the configured key is **≥ 32 bytes
  (256-bit)** via `Encoding.UTF8.GetByteCount` and rejects weaker keys, matching the HS256 minimum.

### 13.2 EF Core existing-schema mapping corrections (CP2 review — Module/Tab/permission configs + repositories)

All fixes preserve the AAP constraint that the existing schema is **mapped, not altered** (no EF
migration / `EnsureCreated` / schema SQL). Empirically validated against the SqlServer model (the generated read
SQL targets the correct relations and references only real columns).

- **`Module` → read view `vw_Modules` (ModuleConfiguration #1 / ModuleRepository #1).** The flattened C# `Module`
  merges columns from `[Modules]` + `[TabModules]` + `[ModuleDefinitions]` + `[DesktopModules]` +
  `[ModuleControls]`. The legacy database already exposes exactly this denormalized shape through the existing
  read view `vw_Modules` (`DotNetNuke.Schema.SqlDataProvider`). Mapping changed from `ToTable("Modules")` to
  `ToView("vw_Modules")`, so `ModuleRepository.GetByTabIdAsync` (filters `TabId`) and `GetByDefinitionAsync`
  (filters `FriendlyName`) now generate valid SQL against real view columns with **no repository change**. The
  seven properties NOT projected by the view on the authoritative consolidated schema — `DefaultCacheTime`,
  `SupportsPartialRendering`, `Dependencies`, `Permissions`, `AuthorizedEditRoles`, `AuthorizedViewRoles`,
  `AuthorizedRoles` — are `Ignore()`d so EF never emits SQL for a non-existent column. **Composite writes back to
  the five base tables are NOT supported through this read-view mapping** — EF Core refuses to persist a
  `ToView`-mapped entity to a relational store (fast-fails with `InvalidOperationException: The entity type 'Module'
  is not mapped to a table`); this read/write-split limitation is detailed, with the non-nullable-key fix and
  concrete runtime evidence, in §14.2 below.
- **`Tab` (TabConfiguration #1 / TabRepository #1).** `AuthorizedRoles` and `AdministratorRoles` were DROPPED
  from `[Tabs]` in `03.00.01.SqlDataProvider` (DNN sources these from tab permissions); they are now `Ignore()`d.
  `IsSecure`, by contrast, was ADDED in `04.05.04` and **is** a real column, so it is intentionally left mapped.
- **Permission tables (ModulePermission/TabPermission/FolderPermission #1).** `RoleName`, `Username`,
  `DisplayName` (and, for `FolderPermission`, `FolderPath`) are view/computed values, not physical permission-table
  columns, so they are `Ignore()`d. `FolderPermission.PortalId` had a bogus `HasColumnName("PortalID")` mapping —
  `[FolderPermission]` has no `PortalID` column (CREATE TABLE is `FolderPermissionID, FolderID, PermissionID,
  RoleID, AllowAccess`, plus `UserID` added in `04.05.00`) — so that mapping was removed and the property is
  `Ignore()`d. `AllowAccess` (physical) and `UserID` (physical since `04.05.00`) remain mapped on all three.

### 13.3 Credential & Portal-Settings adapters + DI composition (CP2 review — DependencyInjection #1, Program.cs #4)

The Application services `AuthService`, `UserService`, `PortalService` (via `ICredentialStore`) and
`ModuleService`, `UserService` (via `IPortalSettingsService`) depend on ports that had **no registered
Infrastructure implementation**, so DI activation of those services failed at runtime when controllers resolved
them. Both adapters are now implemented and registered **Scoped** (DbContext-backed) in
`Infrastructure/DependencyInjection.cs`.

- **`ICredentialStore` → `Infrastructure/Identity/CredentialStore.cs`.** The BCrypt credential store that
  REPLACES the legacy `aspnet_Membership` table (AAP §0.5.2). Backed by a new `UserCredential` entity
  (`int UserId` PK, `PasswordHash`, `CreatedDate`, `LastModifiedDate?`) mapped to a dedicated **`[UserCredentials]`
  table**. **Schema-compatibility note:** this is a documented ADDITION alongside the existing DNN schema, NOT an
  alteration of any legacy table and NOT an EF migration — the operator installs this table out-of-band exactly as
  the legacy `aspnet_*` membership tables were installed via `InstallMembership.sql` (so Rules item #2,
  "no schema-altering migration in this phase," remains satisfied). `SetPasswordAsync` is **STAGE-only**
  (create-or-replace via the change-tracker, no `SaveChanges`): the callers `UserService.CreateAsync` (L231→L232)
  and `PortalService` bootstrap (L197→L202) commit via `IUnitOfWork.SaveChangesAsync`, so the credential is
  persisted atomically within the caller's unit of work. `GetPasswordHashAsync` is a tracking-free read returning
  `null` when no credential exists, which `AuthService` treats as fail-closed.
- **`IPortalSettingsService` → `Infrastructure/Settings/PortalSettingsService.cs`.** Faithfully replicates the
  legacy `PortalSettings.vb` indirection: DNN 4.x has **no** name/value "PortalSettings" table; site settings
  physically live in the existing **`[ModuleSettings]`** table scoped to the portal's "Site Settings" module. The
  adapter resolves that module's `ModuleID` from `_context.Modules` (the `vw_Modules` read view) by
  `PortalId + FriendlyName == "Site Settings"` (excluding soft-deleted modules), then reads/writes `[ModuleSettings]`
  rows via a new `ModuleSetting` entity mapped to that existing table (composite key `(ModuleID, SettingName)`,
  only the three physical columns `ModuleID`/`SettingName`/`SettingValue`). `GetSettingAsync` returns `null` when
  the module or row is absent (legacy when-empty parity). `SetSettingAsync` **self-commits** (the ModuleService
  call-sites at L251/L254 have no guaranteed `SaveChanges` afterward) and throws `DomainException` when the portal
  has no Site Settings module to anchor the setting to.
- **DI registration.** `services.AddScoped<ICredentialStore, CredentialStore>()` and
  `services.AddScoped<IPortalSettingsService, PortalSettingsService>()`. A `BuildServiceProvider(ValidateOnBuild:
  true, ValidateScopes: true)` composition check confirms every Application service (`IAuthService`,
  `IUserService`, `IModuleService`, `IPortalService`, `IRoleService`, `ITabService`) now resolves without an
  unresolved-dependency error.

### 13.4 API contract: URL-path v1 versioning, authorization/tenant isolation, runtime validation (CP2 review — all resource controllers + AuthController + Program.cs #3)

- **URL-path `/api/v1` versioning (all controllers + AuthController).** The AAP (§0.1.2 / §0.3.4) requires
  URL-path versioning under `/api/v1/...`, while the AAP §0.3.4 resource table and the Gate-5 integration
  contract reference the literal unversioned paths (`/api/portals`, etc.). No ASP.NET Core API-versioning package
  (`Asp.Versioning.*`) is available in the offline NuGet cache, so each controller carries **dual `[Route]`
  attributes** — `[Route("api/[controller]")]` **and** `[Route("api/v1/[controller]")]` — exposing both contracts
  from one controller. Empirically validated against the running host: Swagger generates **35 paths (17 `/api/v1/*`
  + 17 `/api/*` + `/health`)** with **no `operationId` collision** and clean OpenAPI generation, so no
  `CustomOperationIds` workaround is needed. `HealthController` is unchanged (`/health`, anonymous).
- **Authorization policies (resource-controller authorization findings).** Authentication alone (`[Authorize]`) is
  not authorization. Two policies registered in `Program.cs` reproduce the legacy DNN admin-page access model from
  the claims `JwtService` issues: **`HostAdministrator`** (requires the `isSuperUser` claim — the legacy Host >
  Portals page was SuperUser-only) guards portal CRUD on `PortalsController`; **`PortalAdministrator`** (the portal
  `Administrators` role **or** a host SuperUser) guards `Users`/`Roles`/`Tabs`/`Modules`. `AuthController` keeps a
  plain `[Authorize]` (login/refresh are `[AllowAnonymous]` + rate-limited).
- **Multi-tenant isolation (tenant-isolation findings).** Client-supplied `portalId` (query or body) is no longer
  trusted. `ApiControllerBase.EnforceTenant(int)` (and an `int?` overload for host-level tabs whose `PortalID` is
  the `Null.NullInteger` sentinel/NULL) compares the requested portal against the JWT `portalId` claim before any
  service call: a host SuperUser bypasses the check; any other principal must carry a `portalId` claim equal to the
  request. Applied to **all 22** portal-scoped actions across `Users`/`Roles`/`Tabs`/`Modules`. **Documented
  deviation:** both a tenant *mismatch* and a *missing* `portalId` claim return **403** (not 401) — the principal is
  authenticated but not authorized for that portal, which is the RFC-correct status.
- **Runtime request validation (Program.cs #3).** Registering validators (`AddValidatorsFromAssembly`) did not
  execute them. `AddFluentValidationAutoValidation()` now hooks FluentValidation into MVC model validation, so an
  invalid `[FromBody]` DTO populates `ModelState` and the `[ApiController]` convention returns an RFC 7807
  `ValidationProblemDetails` (400) **before** the action (and the Application service) runs — closing the gap where
  extensively unit-tested validators were never invoked at the API boundary.

### 13.5 AutoMapper advisory formal exception at the Api host + configuration-validation test (CP2 review — DnnMigration.Api.csproj #1, UnitTests.csproj #1)

- **Api-host audit suppression (dependency-security finding).** The Api host's direct package
  `AutoMapper.Extensions.Microsoft.DependencyInjection` 12.0.1 transitively pulls **AutoMapper 12.0.1**, reported
  HIGH by `dotnet list package --vulnerable --include-transitive` for **GHSA-rvv3-g6hj-g44x / CVE-2026-32933**
  (DoS via uncontrolled recursion on cyclic/self-referential maps). `DnnMigration.Api.csproj` now carries the same
  **scoped, advisory-specific** `<NuGetAuditSuppress Include="https://github.com/advisories/GHSA-rvv3-g6hj-g44x"/>`
  already present in `DnnMigration.Application.csproj` (see §11 CP1 note), as a **formal documented exception**:
  (1) AAP §0.5.1 pins AutoMapper/the DI extension to 12.0.1 and the fix exists only in 15.1.1 / 16.1.1+ (no 12.x
  patch; unreachable offline); (2) the advisory was already present transitively at the clean Gate-1 baseline and
  is surfaced only under `--include-transitive` reporting (default `direct` audit mode keeps Gate 1 green);
  (3) **compensating control** — every profile maps flat POCO ↔ DTO shapes with no cyclic/self-referential map.
- **Compensating-control proof (Gate 2 readiness).** A new unit test
  `tests/DnnMigration.UnitTests/Mapping/AutoMapperConfigurationTests.cs` builds the host's profile set
  (`AddMaps(typeof(IPortalService).Assembly)` — the identical assembly `Program.cs` registers) and calls
  `AssertConfigurationIsValid()`. A pass proves the configuration is complete **and** structurally free of the
  cyclic maps that are the precondition of the advisory, turning the "no cyclic maps" claim into an enforced,
  regression-guarded invariant. **Revisit** the suppression when the AAP is permitted to advance AutoMapper to a
  patched line (15.1.1 / 16.1.1+).

## 14. QA-1 Backend Contract Remediation Decisions

Decisions taken to resolve the three defects reported by QA checkpoint **QA-1 (Backend API Contract &
Business Logic)**. Each fix is annotated in source with a `// MIGRATION: (QA-1 Issue #N ...)` comment.

### 14.1 Globalization enabled — `InvariantGlobalization=false` + Alpine `icu-libs` (QA-1 Issue #1, CRITICAL)

**Defect.** `backend/src/DnnMigration.Api/DnnMigration.Api.csproj` set `<InvariantGlobalization>true</InvariantGlobalization>`.
`Microsoft.Data.SqlClient` resolves a `CultureInfo` (e.g. `en-us`) while opening a connection; under
globalization-invariant mode that resolution throws `System.Globalization.CultureNotFoundException`
**before any network I/O**. The failure is therefore *independent of database availability* — it fires
identically against a real, reachable SQL Server — and it broke **every** EF Core SQL operation (all CRUD
endpoints) **and** the authentication login credential lookup. It is NOT the documented/acceptable
"no-DB connectivity 500" (which would be a `SqlException`); the exception *type* (`CultureNotFoundException`,
not `SqlException`) and sub-100 ms timing (no TCP timeout) prove it is a compiled-in config defect. It was
also invisible to the planned gates: EF Core InMemory (Gate 5) bypasses `Microsoft.Data.SqlClient`, and
`/health` (Gate 7) never touches the database.

**Fix.** (1) `DnnMigration.Api.csproj` → `<InvariantGlobalization>false</InvariantGlobalization>`. (2) Because
the runtime base image `mcr.microsoft.com/dotnet/aspnet:8.0-alpine` ships **no full ICU**, `docker/api.Dockerfile`
now installs ICU in the runtime stage (`apk add --no-cache curl icu-libs`); without it the published app would
fail to start once invariant mode is off. The misleading comments in both files (which justified invariant mode
as "safe" / "no ICU packages needed") were corrected. A JSON BFF API still emits ISO/invariant-formatted values
on the wire, so enabling globalization does not change response formatting — it only restores the culture
resolution that the SQL client and any culture-aware code require.

**Follow-up (recommended).** Add an integration test that opens a **real** `SqlConnection` (not InMemory) so this
class of globalization/runtime-config defect is caught by a gate in future; the InMemory-based Gate-5 suite below
cannot detect it because it never exercises `Microsoft.Data.SqlClient`.

### 14.2 Module create path — non-nullable key + `ValueGeneratedOnAdd`, validator hardening, and the documented `ToView` read/write split (QA-1 Issue #2, CRITICAL)

**Defect.** `POST /api/modules` returned **500 for every input** (empty `{}` and fully-populated valid bodies
alike), blocking AAP Gate 5 (`Module POST → 201`). Two distinct root causes were proven at runtime:

1. **Nullable primary key (the actual Gate-5 blocker).** `Module.ModuleId` was `int?` — the only in-scope entity
   with a nullable PK (`Portal`/`User`/`Role`/`Tab` keys are all non-nullable `int`). `ModuleConfiguration` declared
   `HasKey(m => m.ModuleId)` **without** `ValueGeneratedOnAdd`, so EF Core's change tracker rejected the insert with
   `InvalidOperationException: Unable to track an entity of type 'Module' because its primary key property
   'ModuleId' is null`. This error is **provider-agnostic** — it fires under EF Core InMemory too, so the Gate-5
   integration test would have failed. The `ModuleConfiguration` comment claiming the nullable key "CRUD succeeds
   (empirically verified)" was **false** and is corrected; the 313 pre-existing green unit tests passed only because
   they **mock** `IModuleRepository.AddAsync` and never exercise the real change tracker.
2. **No required-field validation.** `CreateModuleValidator` had no `NotEmpty`/`NotNull` rule, so a malformed `{}`
   reached the service and 500'd instead of returning a clean 400 like the other four resources.

**Fix.**
- **`Domain/Entities/Module.cs`** — `public int? ModuleId` → `public int ModuleId` (annotated
  `// MIGRATION: (QA-1 Issue #2 ...)`). Ripple was verified clean: `ModulePermission.ModuleId` and
  `ModuleResponse.ModuleId` intentionally stay `int?` (FK/DTO sentinel layers), `ModuleProfile` ignores `ModuleId`
  on both Create and Update maps, and `ModuleService.UpdateAsync(int portalId, int moduleId, …)` already passes a
  non-nullable value at `module.ModuleId = moduleId`.
- **`Infrastructure/Data/Configurations/ModuleConfiguration.cs`** — added `.ValueGeneratedOnAdd()` to the key so
  the store generates `ModuleId`; corrected the false "empirically verified" comment.
- **`Application/Validators/CreateModuleValidator.cs`** — added `RuleFor(x => x.ModuleDefId).NotNull().GreaterThan(0)`
  (faithful to the legacy `AddModule` contract, where a module cannot exist without its definition), so a malformed
  body now returns **400** with `{"ModuleDefId":["'Module Def Id' must not be empty."]}` rather than 500.

**`ToView` retained — DEVIATION from QA's literal "map to a writable table" suggestion, justified.** The QA fix text
offered two alternatives for the compounding view-mapping concern: *map to a writable table* **or** *split
read-model from write-model*. We deliberately **keep `ToView("vw_Modules")`** (chose the read/write-split path) and
**reject** `ToTable("Modules")` because the flattened `Module` merges columns from five base tables
(`[Modules]`+`[TabModules]`+`[ModuleDefinitions]`+`[DesktopModules]`+`[ModuleControls]`) and:

- `ToTable("Modules")` would **regress** the denormalized reads — `ModuleRepository.GetByTabIdAsync` (filters
  `TabId`) and `GetByDefinitionAsync` (filters `FriendlyName`) reference columns absent from base `[Modules]`,
  producing invalid SQL; and
- it would **still not** enable a correct real-DB write — EF would emit `INSERT [Modules]` including denormalized
  columns that do not exist on that table, failing at SQL execution. A correct write therefore requires a genuine
  separate write-model performing composite inserts across the five base tables — a substantial architectural
  addition beyond the QA-1 defect (the nullable PK + missing validation), which the QA report itself lists as a
  forward-looking concern (Areas of Concern #3) and explicitly offers as the deferrable "split read-model from
  write-model" alternative.

**Real-DB write limitation (documented deferral, with concrete runtime evidence).** With the key fixed, a live
SqlServer `POST /api/modules <valid>` now **gets past the change tracker** (the null-PK error is gone) and instead
fast-fails (~0.24 s, before any DB connection) at `SaveChanges` with
`InvalidOperationException: The entity type 'Module' is not mapped to a table, therefore the entities cannot be
persisted to the database. Call 'ToTable' in 'OnModelCreating'`. This is the **expected** consequence of the
read-view mapping and confirms the null-PK defect is resolved; real-DB Module **writes** are not supported until a
write-model is introduced. This limitation is **invisible to AAP Gate 5**, whose store is **EF Core InMemory**
(per AAP §0.7.4), which ignores table/view mapping entirely — so the InMemory create returns **201**.

**Runtime verification.**
- AAP Gate 5 (InMemory, authoritative): integration test `Module_Crud` → **POST 201 / GET 200 / PUT 200 / DELETE 204**.
- Malformed-body fix: `Module_Create_WithEmptyBody` → **400** (and live-host `POST /api/modules {}` → 400 with the
  `ModuleDefId` error) — proving the validator change on the real pipeline.
- Issue #1 unaffected: live `GET /api/portals` still 500s with `SqlException` (real TCP timeout), not
  `CultureNotFoundException`.
- Unit suite: **316 passed / 0 failed** (313 pre-existing + 3 new `ModuleDefId` validator tests; the two
  `CreateModuleValidatorTests` "valid request" fixtures were updated to include `ModuleDefId = 1` to match the
  corrected contract, per AAP D1 — tests align to corrected behavior, never the reverse).

### 14.3 Gate-5 integration-test suite implemented (QA-1 INFO-2)

QA-1 INFO-2 observed that `DnnMigration.IntegrationTests` contained only a `TestAuthHandler` helper and **no actual
integration tests**, so AAP Gate 5 was unimplemented. A real suite was added under
`backend/tests/DnnMigration.IntegrationTests/ApiTests/`:

- **`CustomWebApplicationFactory.cs`** — `WebApplicationFactory<Program>` that injects in-memory `Jwt:*` config,
  swaps the SqlServer `DnnDbContext` registration for `UseInMemoryDatabase`, and promotes the `TestAuthHandler`
  "Test" scheme as the default authentication scheme (seeds a super-user principal).
- **`PortalCrudTests.cs`, `UserCrudTests.cs`, `ModuleCrudTests.cs`** — full CRUD chains asserting the Gate-5 status
  contract (POST → 201, GET → 200, PUT → 200, DELETE → 204) for Portal, User, and Module, plus a Module
  empty-body → 400 test (the Issue #2 proof). `EnvelopeReader.cs` extracts ids from the `{ data, meta }` success
  envelope.
- **`AssemblyInfo.cs`** — `[assembly: CollectionBehavior(DisableTestParallelization = true)]`. xUnit parallelizes
  test classes by default; multiple `WebApplicationFactory<Program>` hosts building concurrently race in
  `HostFactoryResolver`'s process-wide static state, throwing *"The entry point exited without ever building an
  IHost."* Disabling assembly parallelization serializes host construction and resolves it.

All four integration tests pass. Note (per §14.1) these run on InMemory and therefore **cannot** detect Issue #1's
SqlClient globalization defect nor §14.2's real-DB `ToView` write limitation — both are documented above as
requiring a real-`SqlConnection` gate.

### 14.4 Unified RFC 7807 error envelope across validation, exception, and Result/tenant paths (QA-1 Issue #3, MINOR)

**Defect.** The API emitted three different error-envelope shapes, violating the AAP §0.7.5 requirement of one
consistent RFC 7807 envelope:

1. **Model validation** (`[ApiController]` + FluentValidation auto-validation): the framework-default
   `ValidationProblemDetails` — `Content-Type: application/json`, `type` `…/rfc9110#section-15.5.1`, **no** body
   `correlationId`, and a W3C-activity `traceId` (`00-…`).
2. **Unhandled exceptions** (`ExceptionHandlingMiddleware`): the canonical envelope —
   `application/problem+json`, `urn:dnnmigration:error:*` type, `correlationId` + `traceId` (both the
   correlation GUID). The QA report verified this path as fully RFC 7807 compliant.
3. **Business `Result` / tenant failures** (`ApiControllerBase.Failure` / `TenantForbidden`):
   `type` `https://httpstatuses.io/{code}`, **no** `correlationId`/`traceId`.

**Fix — align (1) and (3) TO the canonical middleware envelope (2).** The middleware is the blessed RFC 7807
producer, so the other two paths were brought into line with it rather than the reverse.

- **`Program.cs` — `AddControllers().ConfigureApiBehaviorOptions(InvalidModelStateResponseFactory = …)`.** Builds a
  `ValidationProblemDetails` with `Type = "urn:dnnmigration:error:validation"`, the field-level `errors` map
  preserved (so existing clients are unaffected), and `correlationId`/`traceId` resolved from the SAME
  `HttpContext.Items["CorrelationId"]` key `CorrelationIdMiddleware` writes (falling back to `TraceIdentifier`).
- **`ApiControllerBase` — `Failure`/`TenantForbidden` refactored onto one `BuildProblemResult` helper** that uses
  the `urn:dnnmigration:error:{bad-request|not-found|forbidden}` type scheme and adds the `errors`/`correlationId`/
  `traceId` extensions. **Decision (deliberate reconciliation of the "third variant"):** the QA suggested fix only
  named the validation factory, but AAP §0.7.5 demands uniformity across *all* error responses, so the
  `https://httpstatuses.io/{code}` envelope was also reconciled — otherwise Issue #3 would be only partially
  resolved. RFC 7807 requires one `title` per `type` URI, so titles were aligned to the middleware's
  ("Bad Request" / "Not Found" / "Forbidden"); the more verbose legacy titles ("Request Could Not Be Processed",
  "Resource Not Found") were dropped in favor of one title per type. The flat business-error list
  (`Result.Errors`) is preserved as the `errors` array (validation errors are field-keyed; business `Result`
  errors are field-less, so a flat array is the faithful shape).
- **Content type forced via `ContentResult` (not `ObjectResult`).** MVC content negotiation let the JSON output
  formatter emit its default `application/json` even when `ObjectResult.ContentTypes` requested
  `application/problem+json` (confirmed at runtime). Both new producers therefore return a `ContentResult` with an
  explicit `ContentType`, serializing the `ProblemDetails` with the SAME options the middleware uses — exposed as
  `internal static ExceptionHandlingMiddleware.ProblemJsonOptions` (camelCase + ignore-null) and
  `ProblemJsonContentType` — so the three envelopes are byte-consistent from one source of truth.

**Runtime verification (all three paths, Development host).**
- Validation: `POST /api/auth/login {}` → 400, `application/problem+json`, `type=urn:dnnmigration:error:validation`,
  `errors={Password,Username}`, `correlationId==traceId==X-Correlation-ID`.
- Exception: `GET /api/portals` (super-user JWT) → 500, `application/problem+json`,
  `type=urn:dnnmigration:error:internal`, Dev `detail` shows the (acceptable, no-DB) `SqlException`.
- Tenant: `GET /api/users?portalId=99` with a non-super-user `Administrators` JWT carrying `portalId=5` →
  403 (pre-DB, via `EnforceTenant`), `application/problem+json`, `type=urn:dnnmigration:error:forbidden`,
  `errors={}`, `correlationId==traceId==X-Correlation-ID`.
- Static: build `--warnaserror` 0/0; unit 316/0; integration 4/0 (no regressions).


---

_Maintained per AAP §0.1.2, §0.6.1, and §0.7.2. This is a living log — keep entries concise and
append new decisions, schema-change notes, and documented legacy bugs to the relevant tables as the
migration progresses._

