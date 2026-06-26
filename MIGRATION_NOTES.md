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


## 15. QA-4 Entity↔Schema Fidelity Remediation Decisions

**Checkpoint context.** QA-4 ("CRUD Round-Trips & Entity-Schema Alignment") confirmed every CRUD status-code
contract, success/error envelope, pagination, multi-tenant isolation, relationship, and round-trip behavior passes
(Gate 5: 47/47) **under the EF Core InMemory vehicle** — but flagged a CRITICAL violation of the AAP's
non-negotiable "map to the EXISTING schema; table/column names preserved; schema unchanged" constraint
(§0.1.2 / §0.3.5 / §0.7.1). An offline `DbContext.Database.GenerateCreateScript()` cross-check against the legacy
`Website/Providers/DataProviders/SqlDataProvider/*.SqlDataProvider` schema revealed **26 phantom columns + 3
case-only divergences + 1 unmapped real column** across six entities.

**Root cause (overarching).** The migration gates run on EF Core InMemory, which stores by CLR property and treats
ALL relational mapping (`ToTable`/`HasColumnName`/`ToView`/inheritance strategy) as a no-op. Column-name fidelity
against the real SQL Server schema is therefore NEVER exercised by the gates; the divergences surface only when the
model emits a relational `CREATE` script. Because no live SQL Server is provisioned, every divergence was latent and
green at Gate 5.

**Permanent regression guard added.** `backend/tests/DnnMigration.UnitTests/Infrastructure/SchemaFidelityTests.cs`
generates the SqlServer DDL **offline** (a non-connecting connection string; `GenerateCreateScript()` never opens it)
and asserts, per entity, that the EF model demands only the columns/tables/casing present in the authoritative legacy
schema. For `ToView`-mapped read models — which `GenerateCreateScript` omits entirely — a complementary
**model-metadata guard** (`GetViewMappedColumns` + `StoreObjectIdentifier.View`) asserts that every EF-mapped column
is one the backing legacy view actually projects. This is the CI schema assertion the QA report requested
(Areas of Concern #2/#4); it drove the remediation from a 10-failure baseline to **14/14 green**. Verification
command: `dotnet test tests\DnnMigration.UnitTests\DnnMigration.UnitTests.csproj -c Release --filter "FullyQualifiedName~SchemaFidelityTests"`.

> **Note on `builder.Ignore()` semantics (applies throughout §15).** `Ignore()` drops ONLY the EF *column mapping*;
> the CLR property REMAINS on the entity. DTOs, AutoMapper profiles, services, and the InMemory gates are therefore
> unaffected — the property simply stops participating in relational SQL generation. This is why every fix below is
> behavior-preserving for the InMemory Gate-5 suite while correcting the real-DB schema contract.

### 15.1 Portal — 5 phantom columns Ignore()d + `[GUID]`/`[TimezoneOffset]` legacy casing (Issue #1 CRITICAL, Issue #2 MINOR)

**Defect.** `PortalConfiguration` relied on EF convention for all non-key scalars, so the generated `CREATE TABLE
[Portals]` carried five columns that do not exist on the legacy 31-column `[Portals]` table:
`AdministratorRoleName`, `RegisteredRoleName`, `SuperTabId` (computed stored-proc/view aliases) and `Email`,
`Version` (not `[Portals]` columns at all). Against real SQL Server, every Portal GET/POST/PUT would emit
`[Portals].[AdministratorRoleName]` → `Invalid column name` → HTTP 500. Additionally `Guid`/`TimeZoneOffset` mapped
by convention to `[Guid]`/`[TimeZoneOffset]`, diverging by case from the legacy `[GUID]`/`[TimezoneOffset]` (breaks
under a case-sensitive collation), and `Portal.cs` carried an inaccurate comment claiming a Fluent `GUID` mapping
that did not exist.

**Fix.** In `PortalConfiguration.cs`, `builder.Ignore()` the five non-columns (mirroring the team's own pattern of
`Ignore()`-ing `Users`/`Pages`), and add `builder.Property(p => p.Guid).HasColumnName("GUID")` and
`builder.Property(p => p.TimeZoneOffset).HasColumnName("TimezoneOffset")`. Corrected the inaccurate `Portal.cs`
comment. The five Ignored CLR properties remain available to the Application/DTO layer (they are computed/derived
there), consistent with the legacy proc-alias semantics.

**Verification.** Build `--warnaserror` 0/0; `Portal_table_has_no_phantom_columns` and
`Portal_guid_and_timezone_use_legacy_casing` GREEN; `Portal_real_columns_are_present` (29-column control) stays GREEN.

### 15.2 User — mapped to the legacy read view `vw_Users` + 7 non-projected fields Ignore()d (Issue #3 CRITICAL)

**Defect.** The flattened C# `User` mapped by convention to `[Users]`, emitting eight phantom columns
(`FullName`, `PortalId`, `IsApproved`, `CreatedDate`, `LastLoginDate`, `LastActivityDate`, `LastLockoutDate`,
`LockedOut`) onto the legacy 9-column `[Users]` table. Against real SQL Server, every User read/write → `Invalid
column name` → HTTP 500. `PortalId` is the hard case: it is used by **five** repository LINQ `WHERE` clauses (tenant
scoping), so it cannot simply be `Ignore()`d (that breaks SQL translation), yet it is not a `[Users]` column (it
lives in `[UserPortals]`).

**Fix (two parts).** (1) Map `User` to the **existing** legacy read view `vw_Users` via `builder.ToView("vw_Users")`
— the same blessed read-model pattern applied to `Module → vw_Modules` (§13.2/§14.2). The legacy `vw_Users`
(defined in `DotNetNuke.Schema.SqlDataProvider`) is:

```sql
CREATE VIEW vw_Users AS
  SELECT U.UserId, UP.PortalId, U.Username, U.FirstName, U.LastName, U.DisplayName, U.IsSuperUser,
         U.Email, U.AffiliateId, U.UpdatePassword, UP.Authorised
  FROM Users U LEFT OUTER JOIN UserPortals UP ON U.UserId = UP.UserId
```

It projects exactly **11 columns** and joins **ONLY `[UserPortals]`** — it does **NOT** join `[aspnet_Membership]`
or `[aspnet_Users]`. Crucially it **does** expose `PortalId` (from the `[UserPortals]` join), so `PortalId` stays a
first-class, filterable view column and the five tenant-scoping repository queries translate to valid SQL with zero
repository/service churn. The nine `User` properties the view projects (`UserId`, `PortalId`, `Username`,
`FirstName`, `LastName`, `DisplayName`, `IsSuperUser`, `Email`, `AffiliateId`) remain EF-mapped.

(2) The **seven** `User` properties `vw_Users` does NOT project — `FullName` (computed `FirstName + ' ' + LastName`),
`IsApproved`/`CreatedDate`/`LastLoginDate`/`LastLockoutDate`/`LockedOut` (legacy `[aspnet_Membership]`) and
`LastActivityDate` (legacy `[aspnet_Users]`) — are explicitly `builder.Ignore()`d. Without this, EF maps them by
convention and emits `[vw_Users].[IsApproved]` etc. on every User read → `Invalid column name`. They are verified
**not** referenced in any repository LINQ predicate (they appear only on already-materialized entities in
`AuthService`, in object initializers, and in DTO/AutoMapper layers — `UserProfile` already `ForMember(...).Ignore()`s
all seven), so `Ignore()`ing them is SQL-translation-safe.

**Correction of a prior inaccuracy.** The interim Phase-4 comment claimed `vw_Users` "joins
Users + aspnet_Membership/aspnet_Users + UserPortals so EVERY property is a real view column." That was **false** —
the real `vw_Users` joins only `[UserPortals]`. The `UserConfiguration` comment now embeds the actual view DDL and
documents the seven `Ignore()`s. (This is precisely the inaccurate-comment defect class the QA report repeatedly
flagged; it is corrected here at the source.)

**Read/write split (documented deferral).** Mapping to a view makes `User` read-only at the EF layer (EF refuses to
persist a `ToView` entity to a relational store — the same fast-fail documented for `Module` in §14.2). Real-DB
persistence of account-status/membership fields and credentials is handled by the Infrastructure **Identity** layer
(JWT + BCrypt, plus the documented `UserCredentials` table in §13.3), not this read view — consistent with AAP §0.7.6.

**Harness blind-spot closed.** Because `GenerateCreateScript()` emits nothing for a `ToView` entity, the table-level
DDL parser cannot see read-model column fidelity. A new metadata guard
(`User_view_mapping_references_only_columns_vw_users_projects`) asserts every EF-mapped `User` column is one
`vw_Users` actually projects, that `PortalId` stays mapped, and that the seven membership fields are dropped — this
test would have caught the interim fix's incompleteness.

**Verification.** Build `--warnaserror` 0/0; `User_is_not_emitted_as_a_physical_users_table` and the new metadata
guard GREEN; User integration suite **11/11** (Gate-5 CRUD 201/200/200/204 + parity/edge cases) — no regression.

### 15.3 Permission family — TPC inheritance → composition + `[ModuleDefID]` casing (Issue #4 CRITICAL, Issue #2 MINOR)

> **Supersedes the permission portion of §13.2.** §13.2's `Ignore()`s of the computed `RoleName`/`Username`/
> `DisplayName`/`FolderPath` values were correct and are retained, but its claim that the permission mapping
> "references only real columns" predated the `GenerateCreateScript` harness and missed the inheritance-induced
> base-column leakage corrected here.

**Defect.** `ModulePermission`/`TabPermission`/`FolderPermission` each derived from a base `Permission`
(`: Permission`). With a distinct `ToTable` per concrete type, EF Core applied a **Table-Per-Concrete (TPC)**
strategy that **duplicated the four base attributes** (`PermissionCode`, `ModuleDefId`, `PermissionKey`,
`PermissionName`) onto every child table (12 phantom columns total), introduced a `[PermissionSequence]` sequence
object, and changed each child PK to `PermissionID` (legacy PK is `{X}PermissionID`). Against real SQL Server, any
permission load → `Invalid column name`. The base `Permission.ModuleDefId` also diverged by case from legacy
`[ModuleDefID]`.

**Fix — composition, not inheritance.** A whole-backend grep proved there is **no polymorphic usage** of the
hierarchy (no `List<Permission>` of mixed children, no `as Permission`/`(Permission)` casts; collections are
strongly typed `ICollection<ModulePermission>`/`<TabPermission>`). The only members referenced on children are
`PermissionId` and `PermissionKey`. So the `: Permission` inheritance was removed; each child now declares its own
`public int PermissionId` (the legacy `[PermissionID]` **FK**, not a base inheritance column), and `ModulePermission`/
`TabPermission` additionally declare `public string? PermissionKey`. In configuration: removed
`builder.UseTpcMappingStrategy()`; added `HasColumnName("ModuleDefID")` on `Permission.ModuleDefId`; on each child
added `HasKey({X}PermissionId)` (restores the legacy own-identity PK), `Property(PermissionId).HasColumnName("PermissionID")`,
and (Module/Tab) `Ignore(PermissionKey)`; retained the existing `RoleName`/`Username`/`DisplayName` (and
`FolderPermission` `FolderPath`/`PortalId`) `Ignore()`s. Each child table now emits exactly its legacy 6 columns
(`{X}PermissionID`, `{Module|Tab|Folder}ID`, `PermissionID`, `RoleID`, `AllowAccess`, `UserID`); base attributes
resolve via the `PermissionID` FK to `[Permission]`.

**Verification.** Build `--warnaserror` 0/0; `Child_permission_tables_do_not_carry_base_permission_columns`
(Theory ×3), `Base_permission_table_has_legacy_columns_with_correct_casing`, and
`No_permission_sequence_object_is_generated` GREEN; Module/Tab unit **27/27** + Module/Tab integration **19/19**
(strongly-typed permission collections + relationships intact) — no regression.

### 15.4 UserRole — phantom `[Subscribed]` Ignore()d (Issue #5 CRITICAL)

**Defect.** `UserRole.Subscribed` mapped by convention to `[UserRoles].[Subscribed]`, which is a computed proc
alias, not a physical column (legacy `[UserRoles]` = `UserRoleID, UserID, RoleID, ExpiryDate, IsTrialUsed,
EffectiveDate`). `RoleRepository.GetUserRolesAsync` (central to the user→role→permission model) would emit
`[UserRoles].[Subscribed]` → `Invalid column name` → HTTP 500 on real SQL Server.

**Fix.** `builder.Ignore(ur => ur.Subscribed)` in `UserRoleConfiguration.cs` (CLR property retained — `RoleProfile`
still maps `UserRole → UserRoleDto.Subscribed`); corrected the comment to state `Subscribed` is computed/not stored.

**Verification.** Build `--warnaserror` 0/0; `UserRoles_table_has_no_subscribed_column_and_keeps_legacy_columns`
GREEN; Role unit **16/16** (incl. AutoMapper `UserRole → UserRoleDto`) + Role integration **9/9** — no regression.

### 15.5 Tab — real `[Level]` column restored (Issue #6 MINOR)

**Defect.** `TabConfiguration` `Ignore()`d `Tab.Level` under an inaccurate "computed/runtime/not stored" comment.
`[Level]` is in fact a real, persisted `[Tabs]` column (`int NOT NULL DEFAULT 0`, never dropped in any of the 88
scripts). `Ignore()`-ing it meant the `Level = parent.Level + 1` value computed by `TabService` (§12.3) was never
persisted or read — tab depth would always read 0 from the real DB (a behavioral-parity gap).

**Fix.** Removed `builder.Ignore(t => t.Level)` so `Level` maps by convention; corrected the comment. Genuinely
computed `HasChildren` stays `Ignore()`d; truly-dropped `AuthorizedRoles`/`AdministratorRoles` (DROPped in
`03.00.01`) stay `Ignore()`d; truly-added `IsSecure` (`04.05.04`) stays mapped.

**Verification.** Build `--warnaserror` 0/0; `Tabs_table_maps_the_real_level_column` GREEN; Tab unit **15/15**
(path/level computation) + Tab integration **9/9** — no regression.

### 15.6 INFO / coverage items (Issues #7–#9)

- **`User.UpdatePassword` left unmapped — by design (INFO).** `[Users].[UpdatePassword]` is a real legacy column,
  but it is credential-adjacent state. Per AAP §0.7.6 (no credential material on the Domain `User`; credentials live
  in the Infrastructure Identity layer with BCrypt), the property is intentionally not mapped. This is a sanctioned
  exclusion, not a phantom mapping, and is recorded here for traceability.
- **Explicit two-portal isolation regression test added (coverage, QA Areas of Concern #4).**
  `backend/tests/DnnMigration.IntegrationTests/Isolation/PortalIsolationTests.cs` seeds two portals in one store and
  asserts cross-portal exclusion at the repository query-scoping layer (`GetByPortalIdAsync`,
  `GetByIdAsync(portalId, id)`, `GetUserRolesAsync(portalId, userId)`) — hardening against future tenant leakage
  (3/3 PASS). The shipped suite previously proved isolation only via unique-portal-per-test.
- **CI schema-fidelity assertion added (coverage, QA Areas of Concern #2/#4).** `SchemaFidelityTests` (§15 intro) is
  the permanent `GenerateCreateScript`-vs-legacy guard, now including the `ToView` metadata guard, so phantom-column
  / casing / view-projection regressions fail the unit suite automatically.

### 15.7 "Schema unchanged" affirmation and final tally

The AAP §0.6.2 / §5 "schema changes required: none" affirmation **still holds**. Every fix maps to objects that
already exist in the legacy schema — physical tables (`[Portals]`, `[UserRoles]`, `[Permission]`, the three child
permission tables, `[Tabs]`, `[Roles]`, `[ModuleSettings]`) and existing legacy **views** (`vw_Users`, `vw_Modules`,
both defined in `DotNetNuke.Schema.SqlDataProvider`). No table structure is created, altered, or dropped; the
`UserCredentials` table remains the only documented additive object (§13.3), unchanged by QA-4. The remediation
removes **26 phantom columns** (Portal 5, User 8, permission children 12, UserRole 1), corrects **3 case-only
divergences** (`[GUID]`, `[TimezoneOffset]`, `[ModuleDefID]`), and restores **1 wrongly-unmapped real column**
(`Tabs.Level`) — exactly the QA-4 totals — verified by `SchemaFidelityTests` (14/14) and full static + InMemory
runtime re-verification with no regressions.


## 16. CP3 Frontend SPA Review Remediation Decisions

Remediation of the CP3 (Frontend SPA: core, shared, layout, and all features) code review. The
governing decision for the whole checkpoint (AAP D1 precedence): the CP1/CP2 backend Web API is the
**FROZEN authoritative contract** (AAP Section 0.3.4). Where the SPA diverged from it, the frontend
is **ALIGNED to the backend** - frontend calls to deliberately-omitted endpoints are removed or
deferred, the required `portalId` tenant query is threaded onto tenant-scoped calls, and DTO field
names are corrected - rather than adding new backend endpoints (which would mutate the frozen
contract and is out of scope this phase). Each subsection records the root-cause fix.

### 16.1 Bootstrap files + role-list workflow (Gate 3 blocker - angular.json #1, tsconfig.app.json #1, role.routes.ts #1)

- **Browser entrypoint restored.** `frontend/src/main.ts` was missing while `angular.json`
  (`build.options.browser`) and `tsconfig.app.json` (`files`) both referenced it, so
  `ng build --configuration production` failed immediately with TS6053 / esbuild entrypoint
  resolution and Gate 3 could not start. Added `main.ts` (`bootstrapApplication(AppComponent,
  appConfig)`), `app.config.ts` (`provideRouter` with `withComponentInputBinding()`,
  `provideHttpClient(withInterceptors([tokenInterceptor, errorInterceptor]))`, zone change
  detection), and `app.routes.ts` (flat feature routes: `auth` public; `portals`/`users`/`roles`/
  `modules` behind `authGuard`; default + wildcard redirects).
- **Missing role-list workflow.** `features/role/role.routes.ts` lazy-imported a non-existent
  `role-list` component, so the AAP Section 0.3.6 / 0.4.2 role list workflow was absent and the route
  could not resolve. Implemented `features/role/role-list/role-list.component.{ts,html,scss,spec.ts}`
  (OnPush, signals, `inject()`, new control flow, lists roles through `RoleService.list(portalId)`).

### 16.2 Auth envelope handling + forgot-password deferral (auth.service.ts #1, forgot-password.component.ts #1/#2)

- **Envelope unwrapping.** `AuthService` called `HttpClient` directly for login/refresh/`me` but read
  token and current-user fields from the response root. Backend wraps every success in
  `{ data, meta }` (the `/health` endpoint is the only RAW exception). `AuthService` now unwraps
  `response.data` for all three calls, so access/refresh tokens and the hydrated current user are
  populated and refresh rotation works. Specs updated to flush enveloped `{ data, meta }` shapes.
- **forgot-password endpoint deferral.** No `POST /api/auth/forgot-password` exists in the frozen
  backend contract (the auth surface is login/refresh/logout/`me` only). The component no longer
  injects `ApiService` directly (which also resolved the architecture finding that feature components
  must call feature services, not the HTTP gateway): it consumes
  `AuthService.requestPasswordReset()`, a documented client-side **DEFERRAL** that performs no network
  call and surfaces the standard "a reset link has been sent if the account exists" UX. It will be
  wired to a real endpoint if/when the backend adds password reset. `// MIGRATION:` annotations were
  added to the forgot-password SCSS, the spec, and `login.component.html` (CP3 migration-traceability
  findings, AAP Section 0.7.2).

### 16.3 Portal contract (portal.service.ts #1/#2, portal.model.ts #1)

- **`processorPassword` read-model removal.** The backend `PortalDto` intentionally OMITS
  `processorPassword` and accepts it only as a write-only field on update. The frontend `Portal` read
  model carried `processorPassword`, a misleading/sensitive-field drift. Removed from the read model
  (zero consumers confirmed by grep); the write-only value is carried only on `UpdatePortalRequest`.
- **Create vs Update request split.** `CreatePortalRequest` now sends the backend-required `email`
  plus the admin bootstrap fields; `UpdatePortalRequest` carries the write-only `processorPassword`
  and intentionally has NO `email` (matching the backend DTOs). `portal-form` adds the `email` and
  admin controls in create mode only and builds the mode-specific payload.
- **`portals/expired` removal.** The service called `portals/expired` get/delete endpoints that do not
  exist on the host-level Portals controller; removed those calls and the corresponding Expired filter
  and delete-expired action/dialog from `portal-list`.
- Hardcoded SCSS color literals replaced with the shared `--color-*` tokens (internal consistency,
  AAP Section 0.3.7).

### 16.4 Module contract (module.service.ts #1/#2/#3, module.model.ts #1)

- **Permission field rename.** The frontend write request and read model used `modulePermissions`
  while the backend `Create/UpdateModuleRequest` expect `permissions` (`List<ModulePermissionDto>`)
  and `ModuleResponse` exposes `permissions` as a `string?` (the permission COLLECTION is omitted from
  the read shape). Removed `modulePermissions` from the model (and the now-unused `ModulePermission`
  import) and renamed the write field to `permissions`, eliminating silent permission-edit data loss.
- **Tenant `portalId` query.** `getById`, `update`, and `remove` are tenant-scoped (`EnforceTenant`)
  but dropped the required `portalId`. The query is now threaded onto all three.
- **Import/export deferral.** `modules/{id}/import` and `modules/{id}/export` have no backend
  endpoints; those calls were removed and the `import-export` component converted to a deferral notice
  (reads still resolve the module via `getById` with `portalId`).
- **Root-cause API helper.** To thread `portalId` as a query parameter without re-implementing it per
  service, a backward-compatible optional `params?` argument was added to `ApiService.put` and
  `ApiService.delete` (matching the existing `get`); reused by the module, role, and user services.

### 16.5 User contract (user.service.ts #1/#2/#3, user.model.ts #1)

- **`roles[]` alignment.** The `User` model used `userRoles` and omitted the backend `roles` string
  array present on `UserResponse` / `CurrentUserDto`, which could leave role-based UI state empty.
  Removed `userRoles` (and its `UserRole` import) and added `roles: string[]`.
- **DTO field names.** Create now sends `portalId` in the body plus `confirm` (not `confirmPassword`);
  update sends `isApproved` and `lockedOut` (not `authorize`, and no password), matching the backend
  `Create/UpdateUserRequest`. Local-only form controls are mapped to these outbound names before
  POST/PUT.
- **Tenant `portalId` query.** `getById`, `update`, and `delete` now carry the required `portalId`
  (no longer optional) for the tenant-scoped user endpoints.
- **Profile deferral.** `users/{id}/profile` get/update endpoints do not exist on the backend; removed
  them (and the `ProfilePropertyValue` shape) and converted the `profile` component to a deferral
  notice. The DNN membership extras (random password, security question/answer, notify-on-create,
  CAPTCHA, create-time authorize) remain client-only / deferred until a backend profile contract
  exists.

### 16.6 Role contract (role.service.ts #1/#2)

- **Tenant `portalId` query.** `getById`, `update`, `delete`, and `getUserRoles` are tenant-scoped
  (`EnforceTenant`) and now carry the required `portalId`. `role-list.onConfirmDelete` was
  ripple-fixed to source `portalId` from the current user and pass it to `delete(id, portalId)`.
- **Assignment-write deferral.** The backend role surface has NO user-role assignment WRITE
  endpoint/DTO (assignment reads are exposed via `roles/user/{userId}`). The frontend
  `assignUserRole` / `removeUserRole` methods and `AssignUserRoleRequest` were removed; the
  `role-assignment` component renders a `writesDeferred` notice while keeping the read paths
  (`getById`, `getUserRoles`) live. To be implemented when a backend assignment-write contract is
  added.
- `role.model.ts` needed NO change: System.Text.Json `JsonSerializerDefaults.Web` camelCases
  `RSVPCode` to `rsvpCode`, which the model already used (verified against `Create/UpdateRoleRequest`
  and `UserRoleDto`).

### 16.7 Tab model drift (tab.model.ts #1)

- The frontend `Tab` read model carried `tabPermissions: TabPermission[]`, but the backend
  `TabResponse` (READ) OMITS the permission collection entirely - it represents tab access via the
  `authorizedRoles` / `administratorRoles` strings (only the write-side `Create/UpdateTabRequest`
  carry `permissions: List<TabPermissionDto>`). Removed the drifting `tabPermissions` field and its
  unused import (zero consumers; the `Tab` type itself is currently unconsumed), so the model mirrors
  `TabResponse` exactly - the same disposition as the portal `processorPassword` read-model removal
  (Section 16.3). The SPA has no tab CRUD feature (features are portal/user/role/module/auth), so a
  write-only `permissions` request interface would be introduced only if/when a tab feature is added.

### 16.8 Accessibility - native label association (form-control.component.ts, INFO)

- The shared `form-control` wrapper previously associated its rendered `<label>` to the projected
  control via `aria-labelledby` only. Per the review's "prefer native `for`/`id`, keep ARIA
  descriptors" guidance, the wiring effect now also performs **native** association: it reuses an
  author-supplied control `id` when present, otherwise assigns a stable `${fieldKey}-control` id, and
  sets the label's `for` to match; `aria-labelledby` and `aria-describedby` (hint/errors) are
  retained.
- **Root cause of the wiring race (fixed).** The effect found the label with a non-reactive
  `querySelector('.form-control__label')`. Because the label sits inside `@if (label())`, the effect
  observed the new `label()` signal value (and wired the already-projected control's `id` /
  `aria-labelledby`) one render-tick before the `@if` created the `<label>` node, so `querySelector`
  returned null and the `for` was never set - and being non-reactive, it never re-ran. The label is
  now obtained through a reactive `viewChild('labelEl')` signal read up front in the effect, so the
  effect re-runs the moment the label is actually rendered. All dependencies are read before the
  control null-check so the dependency set is always registered. (11/11 form-control specs pass.)

### 16.9 Internal design tokens + responsive coverage (MINOR)

- **Tokens.** Hardcoded SCSS color literals in the flagged feature styles (import-export, portal-form,
  portal-list, role-assignment, profile) were replaced with the shared `--color-*` custom properties
  from `styles.scss` - internal visual consistency in lieu of an external design system
  (AAP Section 0.3.7).
- **Responsive.** Added `@media (max-width: 640px)` coverage for the dense admin forms and
  data-heavy tables using the shared `--space-*` tokens: the shared `data-table` scrolls horizontally
  inside its container with single-line cells and a stacked toolbar/pager (covering every list view
  that consumes it); the portal/user/module/role forms stack their action rows full-width; the module
  permissions table scrolls horizontally; and the role-assignment lookup fields and actions stack.

### 16.10 Frontend dependency security - npm audit (MAJOR, AAP-constrained deferral)

- `npm audit` reports **29 vulnerabilities (16 high, 11 moderate, 2 low, 0 critical)** - grown from
  the review's 25 as new advisories were published, notably against `@angular/core`, `@angular/common`
  and `@angular/compiler` themselves (client-hydration DOM clobbering, `formatDate` DoS,
  `HttpTransferCache` weak cache key, two-way-binding sanitization bypass).
- npm's ONLY offered remediation is `npm audit fix --force`, which installs `@angular/*@21.2.17` - a
  **breaking Angular 21 upgrade**. `npm audit fix` (non-force) makes ZERO changes, because Angular
  19.2's pinned `@angular/build` and `@angular-devkit/build-angular` block every non-breaking path
  (verified: manifests unchanged).
- **AAP Section 0.5.1 pins Angular to `^19.0.0`** and Gates 3/4 require Angular 19; the installed
  `@angular/core` is already `19.2.25` (latest 19.x - there is no patched 19.x line for the core
  advisories). Per the migration precedence (the frozen AAP overrides the security heuristic), an
  Angular 21 upgrade is OUT OF SCOPE this phase: it would violate the AAP, break Gates 3/4, and force
  a re-migration/re-validation. No dependency or lockfile change was made.
- **Production-surface analysis.** The majority of advisories are dev-server-only (vite,
  webpack-dev-server, sockjs, http-proxy-middleware, launch-editor - exercised only by `ng serve`) or
  build-time-only (esbuild, @babel/core, serialize-javascript, piscina, tar, pacote, @sigstore,
  @angular/cli - exercised only on the build host). NONE of these ship in the production static bundle
  that nginx serves (AAP Section 0.3.1). The runtime-relevant `@angular/core` advisories require
  Angular 20/21 to patch; the SPA's separately-validated XSS posture (no `bypassSecurityTrust*`, no
  unsafe `[innerHTML]`) and its non-use of SSR client hydration / `HttpTransferCache` reduce practical
  exposure.
- **Decision:** deferred to a future Angular LTS-line upgrade (the AAP itself anticipates a subsequent
  .NET / Angular upgrade). Tracked here as an AAP-constrained item; re-run `npm audit` and Gates 3/4
  after any future framework-version bump.

## 17. CP4 — System Integration, Containerization & Application Bootstrap Review Remediation Decisions

CP4 reviewed the system-integration surface: the Gate-5 integration-test suite, the Docker/orchestration
files, and the Angular application bootstrap/routing. The build, Gate-5 run, and frontend type-check were all
green at the CP4 baseline; the findings concern test-harness rigor, dependency-advisory hygiene in the test
closure, frontend build reproducibility, and one navigation defect. No production runtime behavior, API
contract, message text, or database schema was changed by these remediations.

### 17.1 AutoMapper advisory — integration-test transitive closure (CP4 review — DnnMigration.IntegrationTests.csproj, MAJOR)

- **Finding.** `dotnet list backend/tests/DnnMigration.IntegrationTests/DnnMigration.IntegrationTests.csproj
  package --vulnerable --include-transitive` reports **AutoMapper 12.0.1 — High** (GHSA-rvv3-g6hj-g44x /
  CVE-2026-32933, CWE-674 uncontrolled-recursion DoS). The test project pulls it **transitively** through its
  `DnnMigration.Api` + `DnnMigration.Infrastructure` project references (which carry the pinned
  `AutoMapper.Extensions.Microsoft.DependencyInjection` 12.0.1 composition-root extension).
- **Decision (AAP precedence — D1).** Mirror the **identical scoped suppression** already present in
  `DnnMigration.Api.csproj` and `DnnMigration.Application.csproj`:
  `<NuGetAuditSuppress Include="https://github.com/advisories/GHSA-rvv3-g6hj-g44x" />`. Because the SDK default
  audit mode is `direct` (only top-level packages are audited), the transitive AutoMapper does **not** fail the
  default build — which is why the Gate-1/Gate-5 baseline build was already 0/0. Under the strictest
  `NuGetAuditMode=all`, however, the advisory surfaces in **every** project whose closure carries AutoMapper.
  Api and Application already suppressed it, but `DnnMigration.Infrastructure.csproj` (transitive via Application)
  and `DnnMigration.UnitTests.csproj` (transitive via Application/Infrastructure, plus the direct AutoMapper
  reference in `AutoMapperConfigurationTests`) did **not**. To treat the advisory **consistently across the
  entire build graph** (the root cause of the finding) the same scoped suppression was therefore added to the
  integration-test project **and** to `Infrastructure` and `UnitTests`. Verified: the full-solution Release
  build is **0 warnings / 0 errors under both the default audit mode and `NuGetAuditMode=all --warnaserror`**.
- **Why not upgrade/remove (the finding's first option).** AAP §0.5.1 PINS AutoMapper + the DI extension to
  **exactly 12.0.1** (do-not-float). The advisory is fixed **only** in AutoMapper 15.1.1 / 16.1.1+ — which also
  require a **paid license** — and **no patch ships for the 12.x MIT line** (AutoMapper issue #4618). Only
  12.0.1 is in the offline NuGet cache. Upgrading would therefore violate the frozen AAP, change the licensing
  posture, and is impossible offline. Removing AutoMapper would violate the AAP §0.3.3 DTO+AutoMapper mapping
  pattern. Per the migration precedence rule, the frozen AAP overrides the security heuristic, so the
  finding's documented-mitigation option is the correct resolution.
- **Compensating control.** The DoS requires **cyclic / self-referential** type maps (A→A or A→B→C→A) and
  ~25,000 recursion levels to exhaust the stack. Every `DnnMigration.Application` profile maps **flat
  POCO↔DTO** shapes, so no cyclic map exists and the vulnerable recursion path is never built. This is
  **proven** by `tests/DnnMigration.UnitTests/Mapping/AutoMapperConfigurationTests.cs`, whose
  `AssertConfigurationIsValid()` pass confirms the host's profile set contains no cyclic maps. The integration
  suite additionally exercises only the real, flat request/response DTOs end-to-end.
- **Residual.** `dotnet list --vulnerable` will still *list* AutoMapper 12.0.1 because the package remains in
  the graph (mandated by the pin); the suppression silences the **audit warning** and documents the accepted,
  bounded risk. Revisit when the AAP is allowed to advance AutoMapper to a patched line.


---

_Maintained per AAP §0.1.2, §0.6.1, and §0.7.2. This is a living log — keep entries concise and
append new decisions, schema-change notes, and documented legacy bugs to the relevant tables as the
migration progresses._

