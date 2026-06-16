# Migration Notes — DotNetNuke 4.x → .NET 8 + Angular 19

> **Scope of this document.** This is the root-level **deviation log** mandated by the
> Minimal Change Clause. It records *every* intentional departure from the legacy
> DotNetNuke (DNN) `4.9.0.85` behavior introduced by the VB.NET → C# 12 / .NET 8 +
> Angular 19 rewrite, the rationale for each, and every **ported (intentionally
> reproduced) bug**. It is the human-readable counterpart to the in-code
> `// MIGRATION:` comment convention described in [Section 2](#2-the--migration-annotation-convention).
>
> The migration is a **full, ground-up rewrite**, *not* an incremental, in-place
> modernization. Apart from the single sanctioned authentication/cryptography change
> documented in [Section 3](#3-sanctioned-behavior-change-authentication--cryptography),
> **all domain logic is preserved exactly**.

For the authoritative architecture, technology pins, and screen mapping, see
[`docs/technical-specifications.md`](docs/technical-specifications.md).

---

## Table of Contents

1. [Minimal Change Clause](#1-minimal-change-clause)
2. [The `// MIGRATION:` Annotation Convention](#2-the--migration-annotation-convention)
3. [Sanctioned Behavior Change: Authentication & Cryptography](#3-sanctioned-behavior-change-authentication--cryptography)
4. [Behavior-Preserving Conversions](#4-behavior-preserving-conversions)
   - 4.1 [Data Access: ADO.NET / `SqlHelper` / `CBO` → EF Core 8](#41-data-access-adonet--sqlhelper--cbo--ef-core-8)
   - 4.2 [Schema Preservation (ADR-002)](#42-schema-preservation-adr-002)
   - 4.3 [Null Sentinels → C# Nullable Types](#43-null-sentinels--c-nullable-types)
   - 4.4 [VB.NET → C# 12 Construct Catalog](#44-vbnet--c-12-construct-catalog)
   - 4.5 [Enum Verbatim Preservation](#45-enum-verbatim-preservation)
5. [Presentation Re-platforming](#5-presentation-re-platforming)
6. [Ported Bugs & Deviation Index](#6-ported-bugs--deviation-index)
   - 6.1 [Ported Bugs](#61-ported-bugs)
   - 6.2 [Deviation Index](#62-deviation-index)
   - 6.3 [Per-Entity Delete Strategy](#63-per-entity-delete-strategy)
7. [Dependency, Secret, and Authorization Decisions](#7-dependency-secret-and-authorization-decisions)
   - 7.1 [Pinned Framework Versions and Accepted Security Advisories](#71-pinned-framework-versions-and-accepted-security-advisories)
   - 7.2 [Secret Externalization](#72-secret-externalization)
   - 7.3 [Fail-Closed Authorization](#73-fail-closed-authorization)
   - 7.4 [`DisplaySyndicate` Scope](#74-displaysyndicate-scope)
8. [References](#8-references)

---

## 1. Minimal Change Clause

This project is a **complete rewrite**: legacy VB.NET / ASP.NET Web Forms on
.NET Framework 2.0 is replaced by a C# 12 / .NET 8 ASP.NET Core 8 Web API (Clean
Architecture, Backend-for-Frontend) plus an Angular 19 standalone-component SPA.
The legacy `Library/` and `Website/` trees are retained **as reference source only**
and produce no compiled target artifacts.

The clause governs every decision in the rewrite:

- **Domain logic is preserved exactly.** The behavior and semantics of the five
  aggregate value objects — `PortalInfo`, `ModuleInfo`, `UserInfo`, `RoleInfo`, and
  `TabInfo` — are carried over unchanged, including their public contracts and
  **verbatim enum values**.
- **Behavioral equivalence is required.** Identical inputs must produce identical
  outputs. Business rules are ported as-is and are **not** "improved."
- **UI functional parity is required.** Every in-scope legacy administrative workflow
  (Portal, Module, User, Role, and Tab/Page management) is reproduced in Angular,
  including its field-level validation rules and error messages.
- **Do not optimize or improve business logic.** Pre-existing bugs are **reproduced
  and documented** (see [Section 6.1](#61-ported-bugs)) rather than silently fixed.
  A fix is applied **only** when a bug blocks compilation or a validation gate, and
  any such fix is itself recorded as a deviation.

> **Why log deviations at all?** Because behavioral equivalence is the contract,
> every place where the new code *cannot* be byte-for-byte identical to the legacy
> code (new platform idioms, the sanctioned security upgrade, or a reproduced bug)
> must be visible and justified. This file is that ledger.

---

## 2. The `// MIGRATION:` Annotation Convention

Every deviation or intentionally reproduced bug in the C# / TypeScript source is
annotated **at the call site** with a `// MIGRATION:` comment. This document is the
central index of those annotations: each entry in the [Deviation Index](#62-deviation-index)
and [Ported Bugs](#61-ported-bugs) tables corresponds to one or more `// MIGRATION:`
comments in the code.

The convention has three flavors:

| Prefix | Meaning |
|---|---|
| `// MIGRATION:` | A deliberate platform/idiom change with no behavior impact (e.g., `CBO` reflection replaced by EF Core materialization). |
| `// MIGRATION (SANCTIONED):` | The one approved behavior change — authentication/cryptography modernization (see [Section 3](#3-sanctioned-behavior-change-authentication--cryptography)). |
| `// MIGRATION (PORTED BUG):` | A legacy defect reproduced on purpose to preserve behavioral equivalence; cross-referenced in [Section 6.1](#61-ported-bugs). |

Illustrative example (the code fences below are **illustrative only**, not compiled):

```csharp
// MIGRATION: Legacy CBO.FillObject used Activator.CreateInstance + reflection
// (Library/Components/Shared/CBO.vb, ~L50-L90). EF Core materializes the entity
// directly; the manual IDataReader->object mapping is removed. Behavior preserved.
var portal = await _db.Portals.AsNoTracking()
    .FirstOrDefaultAsync(p => p.PortalId == portalId, ct);
```

```typescript
// MIGRATION (SANCTIONED): Legacy Forms Authentication (PortalSecurity.vb L79,
// FormsAuthentication.SignOut) is replaced by stateless JWT. Logout clears the
// client token and revokes the refresh token server-side; no server session exists.
logout(): void { this.tokenStore.clear(); }
```

---

## 3. Sanctioned Behavior Change: Authentication & Cryptography

> **This is the single sanctioned behavior change in the entire migration.** It is an
> explicit **security upgrade**, not a preserve-as-is item. Every other change in this
> document is behavior-preserving. All code implementing this change is annotated with
> `// MIGRATION (SANCTIONED):`.

The legacy security model lives in `Library/Components/Security/PortalSecurity.vb`
(651 lines). Three legacy mechanisms are modernized:

### 3.1 Forms Authentication → stateless JWT Bearer tokens

- **Legacy.** Session management uses ASP.NET Forms Authentication —
  `System.Web.Security.FormsAuthentication.SignOut()`
  (`PortalSecurity.vb`, line 79).
- **New.** Stateless **JWT Bearer** tokens: a **60-minute** access token plus a
  **signed JWT refresh token** with rotation. The server retains **no session state**,
  which enables horizontal scaling (BFF pattern).
- **Refresh-token format (CP2 auth-chain fix).** The refresh token is a **signed JWT**
  (not an opaque random string), signed with the **same** key/issuer/audience as the
  access token and carrying a `token_type=refresh` claim plus the user subject and a
  longer lifetime (`Jwt:RefreshTokenExpirationDays`, default **7 days**). This makes it
  validatable by `JwtService.ValidateToken`, which `AuthService.RefreshAsync` calls to
  rotate the pair. `RefreshAsync` additionally asserts `token_type == refresh` so an
  access token cannot be replayed at `/api/auth/refresh` (token-type confusion); access
  tokens carry `token_type=access` for the same reason. An earlier CP2 revision issued
  an **opaque** refresh token that `ValidateToken` could never validate, so refresh
  always failed and the frontend 401-recovery flow was broken; this was corrected per
  the CP2 code-review finding. No server-side refresh-token store is used (stateless,
  Phase-1 scope); consequently `LogoutAsync` is a stateless no-op acknowledgement
  (revocation would require a server-side token store, out of Phase-1 scope).
- **Signing-key guard (CP2 hardening).** `JwtService`'s constructor rejects a missing
  or shorter-than-32-byte (256-bit) `Jwt:Key` with a clear `InvalidOperationException`
  at construction/composition time, instead of deferring to the opaque `IDX10653` that
  `SymmetricSecurityKey` would otherwise throw at first token issuance.
- **Target code.** `DnnMigration.Infrastructure/Identity/JwtService.cs` (issue /
  validate / rotate) and `DnnMigration.Application/Services/AuthService.cs`
  (orchestration), exposed via `/api/auth/{login,refresh,logout,me}`.
- **Client.** The Angular `core/auth/auth.interceptor.ts` functional
  `HttpInterceptorFn` injects the explicit `Authorization: Bearer <accessToken>`
  header on outgoing API requests - replacing the implicit, browser-managed
  Forms-Auth cookie - and transparently recovers from a `401 Unauthorized` by
  calling `AuthService.refresh()` and retrying the original request exactly once.
  The `/auth/login` and `/auth/refresh` endpoints are skipped (no header, no
  refresh) to prevent refresh recursion.
- **Client logout recursion fix (CP2 auth-chain fix).** On a **failed** refresh the
  interceptor now calls `AuthService.clearSession()` - a **local-only** method that
  clears the in-memory signals + `localStorage` and redirects to `/auth/login`
  exactly once **without issuing any HTTP request** - instead of `AuthService.logout()`.
  An earlier CP2 revision called `logout()` from the refresh-failure branch; because
  `logout()` first issued a protected `POST /api/auth/logout` (which is **not** a
  skipped fragment) while the stale refresh token was still present, that request
  could `401` and re-enter the `401 -> refresh -> logout` recovery, producing an
  infinite `logout -> 401 -> refresh -> logout` loop. Calling the HTTP-free
  `clearSession()` makes refresh failure terminate deterministically. Correspondingly,
  `AuthService.logout()` (the user-initiated path) was reordered to clear the local
  session **first** (via `clearSession()`) and only **then** fire a best-effort,
  fire-and-forget `POST /api/auth/logout`; with the tokens already cleared a `401` on
  that courtesy call finds a null `refreshToken()` and cannot trigger recovery. This
  was corrected per the CP2 code-review finding.

### 3.2 DES symmetric encryption → BCrypt adaptive password hashing

- **Legacy.** Symmetric encryption uses the weak **56-bit DES** algorithm via
  `DESCryptoServiceProvider` + `CryptoStream` inside the `Encrypt` (line 138) and
  `Decrypt` (line 175) methods. The supplied key is padded/truncated to **16
  characters**, then split into an **8-byte key** (`Left(strKey, 8)`) and an
  **8-byte IV** (`Right(strKey, 8)`).
- **New.** Password handling uses **BCrypt.Net-Next 4.0.3** adaptive hashing in
  `DnnMigration.Infrastructure/Identity/PasswordHasher.cs`. BCrypt is a one-way
  adaptive hash; the reversible DES `Encrypt`/`Decrypt` round-trip is **not** carried
  over for credentials.

### 3.3 `HasNecessaryPermission` / `SecurityAccessLevel` → ASP.NET Core authorization

- **Legacy.** Authorization is expressed through the `SecurityAccessLevel` enum and a
  set of `HasNecessaryPermission` overloads (`PortalSecurity.vb`, lines ~469–547) that
  switch on that level.
- **New.** Server-side authorization uses **ASP.NET Core authorization
  policies/handlers**; the Angular `has-permission` directive gates UI affordances by
  RBAC. The permission keys `VIEW` / `EDIT` / `DELETE` / `MANAGE_SETTINGS` are honored;
  an **unknown permission key yields `403`** (`ForbiddenException`).
- The legacy `SecurityAccessLevel` integer values are preserved for reference and
  comparison fidelity (see [Section 4.5](#45-enum-verbatim-preservation)).

---

## 4. Behavior-Preserving Conversions

Everything in this section is a **platform/idiom change with no behavior impact**.
These are *not* deviations in the behavioral sense; they are mechanical
re-expressions of the same logic on the new stack. They are documented here so the
reader can distinguish "the code looks different" from "the code behaves
differently." Each is annotated in source with a plain `// MIGRATION:` comment.

### 4.1 Data Access: ADO.NET / `SqlHelper` / `CBO` → EF Core 8

- **Legacy.** The data layer is built on Microsoft.ApplicationBlocks.Data
  `SqlHelper` with a stored-procedure-per-operation convention. In
  `Library/Providers/DataProviders/SqlDataProvider/SqlDataProvider.vb`, generic
  `ExecuteReader` / `ExecuteNonQuery` / `ExecuteScalar` overrides invoke a named
  procedure composed as `DatabaseOwner & ObjectQualifier & "<ProcName>"` across
  **255 stored-procedure call sites**. Returned `IDataReader` rows are hydrated into
  objects by `Library/Components/Shared/CBO.vb` using reflection —
  `Activator.CreateInstance` plus `HydrateObject` `PropertyInfo` name-matching
  against reader columns (~L50–L90), or `IHydratable.Fill` when the type implements
  it.
- **New.** The `SqlHelper` + `CBO` reflection pipeline is replaced **wholesale** by
  **EF Core 8 entity materialization** with `IEntityTypeConfiguration<T>` Fluent API.
  **No manual `IDataReader`→object code survives.** Read queries apply
  `AsNoTracking()`; writes use change tracking with `SaveChangesAsync()`.
- **Retained-procedure fallback.** Where a stored procedure encapsulates complex
  set-based logic that is risky to re-express in LINQ, the procedure is **preserved**
  and invoked through `FromSqlRaw` / `ExecuteSqlRawAsync`, keeping behavioral
  equivalence.

### 4.2 Schema Preservation (ADR-002)

- The existing DNN `4.9.0.85` database schema is mapped **UNCHANGED**: **no
  table/column changes, no EF Core migrations, and no data migration** in Phase 1.
- The multi-tenant `ObjectQualifier` / `DatabaseOwner` prefix is expressed in the
  `ToTable()` mappings of each `IEntityTypeConfiguration<T>`.
- The authoritative source of table and column names is the four install scripts —
  `InstallCommon.sql`, `InstallRoles.sql`, `InstallProfile.sql`,
  `InstallMembership.sql` (under `Website/Providers/DataProviders/SqlDataProvider/`) —
  together with `SqlDataProvider.vb`.

**Non-physical `Portals` projection fields.** Seven properties on the `Portal`
domain entity are **not** physical columns of the DNN `4.9.0.85` `dbo.Portals` table
and are therefore explicitly excluded from the EF Core persistence model with
`builder.Ignore(...)` in
`DnnMigration.Infrastructure/Persistence/Configurations/PortalConfiguration.cs`:

| Property | Why it is not a physical `Portals` column | Legacy origin (how it is obtained) |
|---|---|---|
| `Email` | Looked up from the portal administrator's user record | join to the administrator `Users` row |
| `AdministratorRoleName` | Looked up from the role named by `AdministratorRoleId` | join to the `Roles` row |
| `RegisteredRoleName` | Looked up from the role named by `RegisteredRoleId` | join to the `Roles` row |
| `SuperTabId` | A **Host-level** tab id; not stored per-portal | host settings |
| `Users` | On-demand **aggregate** (count of portal users) | `UserController.GetUserCountByPortal` |
| `Pages` | On-demand **aggregate** (count of portal tabs) | `TabController.GetTabCount` |
| `Version` | Framework/product version string; not persisted on `Portals` | framework constant |

- **Decision & rationale.** These fields are **retained on the `Portal` domain
  entity** so the service/repository layer can still populate them for read
  projections (preserving the legacy `PortalInfo` public contract), but they **must
  not be mapped as physical columns**. Mapping them with `HasColumnName` previously
  emitted `SELECT`/`INSERT` against columns that **do not exist** in the authoritative
  schema and would fail against real SQL Server. Excluding them via `Ignore()` is the
  schema-faithful resolution: it upholds **ADR-002** (map the existing schema
  unchanged — no table/column changes, no migrations) and keeps the model
  **InMemory-provider safe** for the integration-test gate.
- **Authoritative schema source.** The physical `Portals` and `PortalAlias` columns
  were cross-checked against
  `Website/Providers/DataProviders/SqlDataProvider/DotNetNuke.Schema.SqlDataProvider`
  (the install DDL); only real columns are mapped via `HasColumnName` / `HasMaxLength`.
- **Code cross-reference.** The `// MIGRATION:` annotation on the `Ignore(...)` block
  in `PortalConfiguration.cs` points back to this subsection (§4.2).

**Denormalized `Modules` fat-object fields (IGNORED — non-physical).** The legacy
`ModuleInfo` is a **fat, denormalized** object that DNN hydrated from a JOIN across
`Modules + TabModules + ModuleControls + DesktopModules + ModuleDefinitions`. Only
**11** of the `Module` entity's properties are physical columns of the `dbo.Modules`
table; the remainder are join-sourced. Like the non-physical `Portals` projection
fields above, these join-sourced properties are **`Ignore()`d** so EF never issues
`SELECT`/`INSERT`/`UPDATE` against columns that **do not exist** on `dbo.Modules`
(which would fail against the real SQL Server schema). This upholds **ADR-002** (map
the existing schema unchanged — no table/column changes, no migrations). The
join-sourced data lives on its own tables (`TabModules` / `ModuleControls` /
`DesktopModules` / `ModuleDefinitions`); the repository/DTO projection layer (CP3)
rehydrates those fields from explicit joins when a placed-module view is required.

> **CP2 review correction.** An earlier CP2 revision *carried* these join-sourced
> properties as scalar columns on `dbo.Modules` for InMemory round-trip convenience.
> That violated ADR-002 (it mapped non-existent physical columns) and was corrected
> to `Ignore()` per the CP2 code-review finding (ModuleConfiguration.cs schema
> fidelity). Configured in
> `DnnMigration.Infrastructure/Persistence/Configurations/ModuleConfiguration.cs`:

| `Module` property group | Legacy source table | Phase-1 mapping |
|---|---|---|
| `ModuleID` (PK), `ModuleDefID`, `ModuleTitle`, `AllTabs`, `IsDeleted`, `InheritViewPermissions`, `Header`, `Footer`, `StartDate`, `EndDate`, `PortalID` | **`dbo.Modules`** (11 real columns) | mapped verbatim (physical columns) |
| `TabModuleID`, `TabID`, `PaneName`, `ModuleOrder`, `CacheTime`, `Alignment`, `Color`, `Border`, `IconFile`, `Visibility`, `ContainerSrc`, `DisplayTitle`, `DisplayPrint`, `DisplaySyndicate` | `dbo.TabModules` | **`Ignore()`** (non-physical) |
| `ModuleControlId`, `ControlSrc`, `ControlType`, `ControlTitle`, `HelpUrl`, `SupportsPartialRendering` | `dbo.ModuleControls` | **`Ignore()`** (non-physical) |
| `DesktopModuleID`, `FriendlyName`, `FolderName`, `Description`, `Version`, `IsPremium`, `IsAdmin`, `BusinessControllerClass`, `ModuleName`, `SupportedFeatures` | `dbo.DesktopModules` / `dbo.ModuleDefinitions` | **`Ignore()`** (non-physical) |

- **`Module.IsDeleted` soft-delete flag preserved.** `IsDeleted` is a real `bit NOT NULL`
  column on `dbo.Modules` and is **mapped (not ignored)** — it is the soft-delete flag the
  `ModuleService` / `ModuleRepository` list queries filter on (delete strategy in §6.3).
- **`Visibility` is a TabModules field (Ignored).** `Module.Visibility` is the
  `VisibilityState` enum (`Maximized=0`, `Minimized=1`, `None=2`) sourced from
  `TabModules.[Visibility]`, not `dbo.Modules`; it is therefore `Ignore()`d with the
  rest of the TabModules group above. When the projection layer (CP3) rehydrates it
  from a `TabModules` join, EF maps the enum to its underlying `int` by convention
  (no `HasConversion` needed).
- **`Module` → `ModulePermission` relationship.** The principal side is declared in
  `ModuleConfiguration.cs` as `HasMany(m => m.ModulePermissions).WithOne()
  .HasForeignKey(mp => mp.ModuleID).OnDelete(DeleteBehavior.Cascade)`. `ModulePermission`
  is detached from the `Permission` inheritance hierarchy and mapped as an independent root
  entity in `PermissionConfiguration.cs` (via `HasBaseType((Type?)null)`); EF merges the two
  configurations. `Cascade` reproduces the legacy permanent-delete behavior
  (`ModuleController.DeleteModule` removes a module's permission rows in the same
  transaction) — this is **referential** cleanup of the dependent permission rows and is
  distinct from the **soft delete** of the module *record* itself (§6.3). There is only one
  relationship into `ModulePermission`, so `Cascade` raises no multiple-cascade-path
  concern, and the InMemory provider ignores delete behavior (safe for Gate 5).

**Non-physical `DesktopModules` / `ModuleDefinitions` fields (IGNORED).**
`DesktopModuleInfo` derived `IsUpgradeable` / `IsPortable` / `IsSearchable` from the
`SupportedFeatures` bitmask (`DesktopModuleSupportedFeature`), and `Dependencies` /
`Permissions` are **absent** from the 4.9 `dbo.DesktopModules` baseline; none is a
physical `dbo.DesktopModules` column, so all five are **`Ignore()`d** (the
service/DTO layer computes the three feature flags from `SupportedFeatures` when
needed). `ModuleDefinition.TempModuleID` is a **runtime-only** transient identifier
(used during import/installation), not a physical `dbo.ModuleDefinitions` column; it
too is **`Ignore()`d**. The eleven real `dbo.DesktopModules` columns and the four real
`dbo.ModuleDefinitions` columns are mapped verbatim.

- **CP2 review correction.** These fields were previously *carried* as scalar
  columns; that mapped non-existent physical columns (ADR-002 violation) and was
  corrected to `Ignore()` per the CP2 schema-fidelity finding.
- **Code cross-reference.** The `// MIGRATION:` annotations in `ModuleConfiguration.cs`
  point back to this subsection (§4.2).

**Tab aggregate — non-physical, computed, and self-FK mapping decisions.** The
`Tab` domain entity (a DotNetNuke "Tab" == a site page) is mapped onto the existing
`dbo.Tabs` table by
`DnnMigration.Infrastructure/Persistence/Configurations/TabConfiguration.cs`. Its
column set was cross-checked against the `[Tabs]` `CREATE TABLE` DDL in
`Website/Providers/DataProviders/SqlDataProvider/DotNetNuke.Schema.SqlDataProvider`,
the `04.05.04` upgrade that adds `IsSecure`, and the `AddTab` / `UpdateTab` stored
procedures in `SqlDataProvider.vb` (the `GetNull(...)` call sites confirm column
nullability). The following non-default decisions are recorded:

| `Tab` property | Decision | Why |
|---|---|---|
| `TabType` | **`Ignore()`** (the only ignored member) | Computed read-only enum (`Globals.GetURLType(_Url)`); has no column or backing field — mapping it fails the EF model build |
| `IsDeleted` | **Mapped** (`bit NOT NULL`); soft-delete flag **preserved** | Real column and the soft-delete sentinel `TabService` / `TabRepository` filter on (DNN tabs are logically deleted; see §6.3) |
| `IsSecure` | **Mapped** (`bit NOT NULL`) | Real column added by the `04.05.04` upgrade (`ALTER TABLE Tabs ADD IsSecure ... DEFAULT(0)`) and set by `04.09.00`; passed by `AddTab` / `UpdateTab` |
| `ParentId` | **Mapped as a plain nullable scalar** (`int?`); **no** EF self-relationship | Nullable self-FK to `Tabs.TabID` (`FK_Tabs_Tabs`), but the entity exposes no `Parent` / `Children` navigation, matching the legacy `TabInfo` flat shape |
| `HasChildren`, `AuthorizedRoles`, `AdministratorRoles` | **`Ignore()`** (non-physical) | Computed / permission-derived runtime projections in legacy DNN, not physical `Tabs` columns. Mapping them would issue CRUD against non-existent columns (ADR-002 violation), so they are ignored and rehydrated by the repository/service projection layer (CP3). **CP2 review correction**: previously carried as scalars |

- **`Tab` → `TabPermission` relationship.** The principal side of the one-to-many is
  declared in `TabConfiguration` as
  `HasMany(t => t.TabPermissions).WithOne().HasForeignKey(tp => tp.TabID)` with
  `OnDelete(DeleteBehavior.NoAction)`. The dependent `TabPermission` is detached from
  the `Permission` inheritance hierarchy and mapped as an independent root entity in
  `PermissionConfiguration` (`HasBaseType((Type?)null)`), and EF merges the two
  configurations. `WithOne()` declares no inverse navigation. `NoAction` is used
  deliberately because `Tab` is **soft-deleted** (`IsDeleted`), so a hard cascade
  delete of permissions is never the operative path and the assembled model stays
  free of SQL-Server multiple-cascade-path conflicts (the InMemory provider ignores
  delete behavior, so Gate 5 is unaffected). This is a **behavior-preserving**
  platform re-expression (default `N` in the [Deviation Index](#62-deviation-index)).
- **Code cross-reference.** The `// MIGRATION:` annotations in `TabConfiguration.cs`
  point back to this subsection (§4.2) and to the per-entity delete strategy (§6.3).

**Permission junctions — non-physical display/lookup fields (IGNORED).** The three
permission junction entities are mapped onto their singular physical tables
(`dbo.ModulePermission`, `dbo.TabPermission`, `dbo.FolderPermission`) by
`PermissionConfiguration.cs`. Legacy DNN populated several **display/lookup** fields
on these objects through JOINs and views (e.g. `vw_ModulePermissions`), so they are
**not** physical columns of the junction tables and are **`Ignore()`d** per ADR-002:

| Junction entity | Ignored (non-physical) properties | Legacy origin |
|---|---|---|
| `ModulePermission` | `RoleName`, `Username`, `DisplayName` | JOIN to `Roles` / `Users` (e.g. `vw_ModulePermissions`) |
| `TabPermission` | `RoleName`, `Username`, `DisplayName` | JOIN to `Roles` / `Users` |
| `FolderPermission` | `PortalID`, `FolderPath`, `RoleName`, `Username`, `DisplayName` | `PortalID` / `FolderPath` live on `Folders`; the rest JOIN to `Roles` / `Users` |

- The real physical columns of each junction (`{X}PermissionID` identity PK, the owning
  resource FK `ModuleID` / `TabID` / `FolderID`, `PermissionID`, `RoleID`, `AllowAccess`,
  and the post-baseline `UserID`) remain mapped verbatim; the four base-only `Permission`
  scalars (`PermissionCode`, `ModuleDefID`, `PermissionKey`, `PermissionName`) stay
  `Ignore()`d on each derived type as before.
- **CP2 review correction.** These display/lookup fields were previously *carried* as
  scalar columns on the junction tables; that mapped non-existent physical columns
  (ADR-002 violation) and was corrected to `Ignore()` per the CP2 schema-fidelity
  finding (PermissionConfiguration.cs). The repository/DTO projection layer (CP3)
  populates them via the `Roles` / `Users` / `Folders` joins.


### 4.3 Null Sentinels → C# Nullable Types

The legacy `Library/Components/Shared/Null.vb` defines reserved "magic" sentinel
values returned by `IDataReader` projections to represent "no value." Each maps to a
C# **nullable type** (`int?`, `bool?`, …) or `default`; the sentinel comparison logic
(`Null.IsNull`, `Null.SetNull`) is replaced by ordinary `null` checks.

| Legacy sentinel (`Null.vb`) | Legacy value | C# 12 representation |
|---|---|---|
| `NullInteger` | `-1` | `int?` (`null`) |
| `NullByte` | `255` | `byte?` (`null`) |
| `NullSingle` | `Single.MinValue` | `float?` (`null`) |
| `NullDouble` | `Double.MinValue` | `double?` (`null`) |
| `NullDecimal` | `Decimal.MinValue` | `decimal?` (`null`) |
| `NullDate` | `Date.MinValue` | `DateTime?` (`null`) |
| `NullString` | `""` (empty string) | `string?` (`null`) |
| `NullBoolean` | `False` | `bool?` (`null`) |
| `NullGuid` | `Guid.Empty` | `Guid?` (`null`) |

> **Behavior-preservation note.** Where a legacy column physically stores a sentinel
> (e.g., `-1` for an absent integer FK) and that value is compared elsewhere, the
> mapping retains the comparison semantics so persisted data continues to round-trip
> correctly. Any place this is not a clean `null` mapping is annotated with
> `// MIGRATION:` at the call site.

### 4.4 VB.NET → C# 12 Construct Catalog

The legacy projects compile with `Option Explicit On` and `Option Strict On`. The
target enables **nullable reference types** and a zero-warning **`--warnaserror`**
policy (the `CS8618` non-nullable-uninitialized warning is excluded, per the
validation gate). The following catalog governs the mechanical conversion:

| Legacy VB.NET construct | Target C# 12 |
|---|---|
| `Implements IInterface` | `: IInterface` |
| Private backing field + `Property Get/Set` | Auto-property `{ get; set; }` |
| `Null.NullInteger` / `Null.NullBoolean` sentinels | Nullable types (`int?`, `bool?`) / `default` |
| `<Browsable(False)>`, `<Required(True), MaxLength(128)>` attributes | DataAnnotations / FluentValidation rules |
| `CType(obj, T)` | `(T)obj` or `obj as T` |
| `Is Nothing` | `is null` / `== null` |
| `AndAlso` / `OrElse` | `&&` / `\|\|` |
| `Inherits BaseClass` | `: BaseClass` |
| `Shared` member / `ReadOnly Property` | `static` member / get-only property |
| Module-level `Imports` | File-scoped namespace + global usings |
| `ByRef` / `ParamArray` | `ref` / `params` |

### 4.5 Enum Verbatim Preservation

Enum **values are carried over exactly** so that any persisted or compared integer
remains valid. The tables below record the verbatim legacy values as they appear in
source.

**`UserLoginStatus`** — `Library/Components/Users/Membership/UserLoginStatus.vb`:

| Member | Value |
|---|---|
| `LOGIN_FAILURE` | `0` |
| `LOGIN_SUCCESS` | `1` |
| `LOGIN_SUPERUSER` | `2` |
| `LOGIN_USERLOCKEDOUT` | `3` |
| `LOGIN_USERNOTAPPROVED` | `4` |
| `LOGIN_INSECUREADMINPASSWORD` | `5` |
| `LOGIN_INSECUREHOSTPASSWORD` | `6` |

**`UserCreateStatus`** — `Library/Components/Users/Membership/UserCreateStatus.vb`
(carried over verbatim, `0`–`17`):

| Member | Value | Member | Value |
|---|---|---|---|
| `AddUser` | `0` | `InvalidProviderUserKey` | `9` |
| `UsernameAlreadyExists` | `1` | `InvalidQuestion` | `10` |
| `UserAlreadyRegistered` | `2` | `InvalidUserName` | `11` |
| `DuplicateEmail` | `3` | `ProviderError` | `12` |
| `DuplicateProviderUserKey` | `4` | `Success` | `13` |
| `DuplicateUserName` | `5` | `UnexpectedError` | `14` |
| `InvalidAnswer` | `6` | `UserRejected` | `15` |
| `InvalidEmail` | `7` | `PasswordMismatch` | `16` |
| `InvalidPassword` | `8` | `AddUserToPortal` | `17` |

**`VisibilityState`** — `Library/Components/Modules/ModuleInfo.vb` (implicit ordinals):

| Member | Value |
|---|---|
| `Maximized` | `0` |
| `Minimized` | `1` |
| `None` | `2` |

> **Accuracy note.** The source declares `VisibilityState` in the order
> `Maximized, Minimized, None`, yielding `Maximized = 0`, `Minimized = 1`,
> `None = 2`. (The AAP narrative abbreviated the list as "None/Minimized/Maximized";
> the **verbatim source ordinals above are authoritative** and are preserved exactly.)

**`SecurityAccessLevel`** — `Library/Components/Security/PortalSecurity.vb` (lines
45–53). Recorded verbatim (the AAP §0.6.2 narrative abbreviated this to
Anonymous/View/Edit/Admin/Host; the full set below is authoritative):

| Member | Value |
|---|---|
| `ControlPanel` | `-3` |
| `SkinObject` | `-2` |
| `Anonymous` | `-1` |
| `View` | `0` |
| `Edit` | `1` |
| `Admin` | `2` |
| `Host` | `3` |

---

## 5. Presentation Re-platforming

The legacy ASP.NET **Web Forms** presentation tier — postback, ViewState, server
controls, and the `.aspx` / `.ascx` code-behind event model — is **fully replaced**
by a stateless REST API under `/api/v1/...` consumed by a client-rendered **Angular 19
SPA**. There is no server-side HTML rendering and no SSR. This is a platform change,
not a behavior change: each in-scope administrative workflow is reproduced with
functional parity (see [Section 1](#1-minimal-change-clause)).

- **Error handling.** `Website/ErrorPage.aspx.vb` is **superseded by**
  `ExceptionHandlingMiddleware` (RFC 7807 Problem Details) rather than ported as a
  page.
- **Uniform API envelope.** Success responses carry `{ data, meta }`; error responses
  follow **RFC 7807 Problem Details** — `{ type, title, status, detail, errors }`.
- **Standard resource mapping pattern:**

  | Legacy Web Forms construct | REST endpoint | Angular surface |
  |---|---|---|
  | List/Grid page | `GET /api/v1/{entity}` | list component |
  | Detail/View | `GET /api/v1/{entity}/{id}` | detail component |
  | Create | `POST /api/v1/{entity}` | create form |
  | Edit | `PUT /api/v1/{entity}/{id}` | edit form |
  | Delete | `DELETE /api/v1/{entity}/{id}` | confirmation dialog |
  | Search/Filter | `GET /api/v1/{entity}?query=…` | filtered list |

The authoritative source-to-target screen map (each Angular component traced to its
originating `.ascx.vb` control) lives in
[`docs/technical-specifications.md`](docs/technical-specifications.md). The login
component is **new** (no legacy equivalent), replacing Forms Authentication.

---

## 6. Ported Bugs & Deviation Index

Both tables below are **living logs**. They are seeded here and extended as the
rewrite proceeds; every row corresponds to one or more `// MIGRATION:` annotations in
the source.

### 6.1 Ported Bugs

Legacy defects that are **intentionally reproduced** to preserve behavioral
equivalence. A bug is fixed **only** when it blocks compilation or a validation gate;
such a fix is then moved to the [Deviation Index](#62-deviation-index) with its
rationale. Each row is annotated in source with `// MIGRATION (PORTED BUG):`.

| ID | Location (file) | Legacy Behavior | Why Preserved | `// MIGRATION:` ref |
|---|---|---|---|---|
| _(none yet)_ | — | — | — | — |

> Entries are added here as ported bugs are encountered during the rewrite. An empty
> table means no behavior-preserving defect has yet been identified — **not** that the
> legacy code is defect-free.

### 6.2 Deviation Index

The master index of every behavioral deviation. Only the authentication/cryptography
modernization is **sanctioned** (`Y`); all other rows default to **`N`**
(behavior-preserving) and exist only to record platform/idiom re-expression.

| ID | Area | Legacy | New | Rationale | Sanctioned? (Y/N) |
|---|---|---|---|---|---|
| D-001 | Authentication | Forms Authentication (`FormsAuthentication.SignOut`, `PortalSecurity.vb` L79) | Stateless JWT Bearer (60-min access token + refresh rotation) | Explicit security upgrade; enables horizontal scaling (BFF) | **Y** |
| D-002 | Cryptography | 56-bit DES (`DESCryptoServiceProvider`, `Encrypt`/`Decrypt` L138–211) | BCrypt.Net-Next 4.0.3 adaptive hashing | Explicit security upgrade; DES is cryptographically weak | **Y** |
| D-003 | Authorization | `SecurityAccessLevel` + `HasNecessaryPermission` switch | ASP.NET Core policies/handlers + Angular `has-permission` directive | Idiomatic RBAC for the new stack; permission keys preserved, unknown key → `403` | **Y** |
| D-004 | Data access | `SqlHelper` + `CBO` reflection hydration (255 proc call sites) | EF Core 8 materialization (`IEntityTypeConfiguration<T>`) | Platform re-expression; **no behavior change** | N |
| D-005 | Persistence | ADO.NET sentinel values (`Null.vb`) | C# nullable types / `default` | Platform re-expression; **no behavior change** | N |
| D-006 | Presentation | Web Forms (`.aspx`/`.ascx`, postback/ViewState) | REST `/api/v1/…` + Angular 19 SPA | Platform re-expression; functional parity preserved | N |
| D-007 | Portal create defaults | `CreatePortal` seeded ExpiryDate/HostFee/HostSpace/PageQuota/UserQuota/SiteLogHistory/Currency from `Common.Globals.HostSettings` (`PortalController.vb` L326–377) | Omitted; client supplies these via `CreatePortalDto` | Host namespace OUT OF SCOPE (AAP §0.2.2); behavior-preserving for in-scope fields | N |
| D-008 | Portal update cache | `DataCache.ClearPortalCache(PortalId, True)` after `UpdatePortalInfo` (`PortalController.vb` L1573) | Omitted | Cache Provider OUT OF SCOPE (AAP §0.2.2) | N |
| D-009 | Portal delete filesystem | `DeletePortal` removed `.resx` files, child portal folder, upload dir, `HomeDirectoryMapPath` (`PortalController.vb` L162–204) | Omitted; DB-only hard delete via `IPortalRepository.DeleteAsync` | FileSystem OUT OF SCOPE (AAP §0.2.2) | N |
| D-010 | Portal delete last-portal guard | `DeletePortal` set `strMessage="LastPortal"` and **silently skipped** deletion when `GetPortalCount() ≤ 1` (`PortalController.vb` L162–204) | Throws `InvalidOperationException` ("Cannot delete the last remaining portal"), surfaced as RFC 7807; count via `GetAllAsync()` | Guard intent preserved; error **surfaced explicitly** instead of silently swallowed | N |
| D-011 | Portal update load | `UpdatePortalInfo` called `DataProvider.UpdatePortalInfo` without first loading the row (`PortalController.vb` L1524–1575) | Loads via `GetByIdAsync`, maps onto the tracked entity, then saves; throws `KeyNotFoundException` if absent | EF Core change-tracking requires a loaded entity | N |
| D-012 | User create (`UserService.CreateAsync`) | `CreateUser` auto-assigned a new non-superuser to every AutoAssignment portal role: it enumerated `GetPortalRoles(PortalID)` and, for each role with `AutoAssignment = True`, called `AddUserRole(PortalID, UserID, RoleID, Null.NullDate, Null.NullDate)` with null effective/expiry dates (`UserController.vb` L166–180) | **Preserved.** After persisting the user, `UserService.CreateAsync` reproduces the branch for non-superusers via the injected Domain `IRoleRepository`: `GetByPortalAsync(portalId)` filtered on `AutoAssignment`, then `AddUserRoleAsync` for each with null `EffectiveDate`/`ExpiryDate`. Exceptions are NOT swallowed (legacy `CreateUser` did not swallow, unlike the bulk `AutoAssignUsers` loop). Clean Architecture is intact (Application → Domain interface; the EF Core implementation is authored in CP3). | **Behavior preserved (CP2 correction).** An earlier CP2 revision OMITTED this branch on the rationale that `RoleService.AutoAssignUsers` owned it; the CP2 user-auto-assignment finding showed `RoleService.AutoAssignUsers` covers ONLY the inverse role→existing-users direction (see D-023), so the new-user→existing-roles direction was a real parity gap — now closed | N |
| D-013 | User delete (`UserService.DeleteAsync`) | Deleting the portal administrator was **silently refused** (`CanDelete = deleteAdmin = False`, `UserController.vb` L209–216) | Loads the portal and **throws** `InvalidOperationException` → RFC 7807 Problem Details | The protective refusal is preserved; only its surfacing changes (silent → explicit error), consistent with the Portal last-portal and Tab child-page guards | N |
| D-014 | User delete (`UserService.DeleteAsync`) | `DeleteUser` cascaded Folder/Module/Tab permission cleanup, sent an email notification, and cleared caches (`UserController.vb` L221–251) | **Omitted** in Phase 1; `UserService` soft-deletes via the repository only | Permission cascade, mail, and cache are out of Phase 1 scope (AAP §0.2.2); the soft-delete itself is preserved | N |
| D-015 | Persistence (User) | `UserInfo.FullName` computed getter (`FirstName & " " & LastName`, `UserInfo.vb` L375) | `User.FullName` expression-bodied read-only property; `UserConfiguration` calls `Ignore(u => u.FullName)` | Computed, no backing column; mapping a get-only property would fail the EF model build | N |
| D-016 | Persistence (User) | `UserInfo.Roles As String()` denormalized role-name array (`UserInfo.vb` L261) | `User.Roles` (`string[]?`); `UserConfiguration` calls `Ignore(u => u.Roles)`; role membership modeled via the `UserRole` join entity | Denormalized, not a column; EF8 would otherwise mis-map it as a primitive/JSON collection | N |
| D-017 | Schema fidelity (User) | Physical `dbo.Users` column `AffiliateId` (lowercase `d`) | CLR property `User.AffiliateID` (all-caps `ID`) remapped via `HasColumnName("AffiliateId")` | Preserve the verbatim DNN 4.9 column name despite the C# casing convention (ADR-002) | N |
| D-018 | Persistence (User) | `UserInfo` is a flattened merge of `Users` + `aspnet_Membership` + `aspnet_Users` + `aspnet_Profile` + `UserPortals` | 12 membership/profile fields (`PortalID`, `Approved`, `CreatedDate`, `IsOnLine`, `LastActivityDate`, `LastLockoutDate`, `LastLoginDate`, `LastPasswordChangeDate`, `LockedOut`, `Password`, `PasswordAnswer`, `PasswordQuestion`) carried as scalar properties on `User` (NOT Ignored) | No physical `Users` column; mapped for Phase-1 round-trip fidelity, no schema change (ADR-002), InMemory-safe (Gate 5) | N |
| D-019 | Persistence (UserRole) | `UserRoleInfo Inherits RoleInfo`; `Subscribed` flag on the fat object | `UserRole` JOIN entity: `Subscribed` carried as a scalar (absent from the 4.9 `UserRoles` table) + explicit `HasOne(ur => ur.User)` / `HasOne(ur => ur.Role)` `.WithMany().HasForeignKey(...)` relationships keyed on the real `UserID`/`RoleID` columns | Clean relational join replacing VB inheritance; FK columns preserved verbatim; required relationships (non-nullable FK) | N |
| D-020 | User-role expiry — **SUPERSEDED** (`RoleService.AddUserRoleAsync`) | `UpdateUserRole` used `DateTime.Now` (server-local) for the membership window (`RoleController.vb` L505, L533-534) | **Removed.** This row re-expressed the self-service computed-window algorithm that an earlier CP2 revision had erroneously ported into the admin `AddUserRoleAsync`. The CP2 role-assignment-contract finding removed that algorithm entirely (the admin path is now a plain date-window upsert — see D-026); no `DateTime.UtcNow` window math remains in the service. | Corrected per CP2 review — the deviation no longer exists | N |
| D-021 | User-role expiry NRE — **SUPERSEDED** (`RoleService.AddUserRoleAsync`) | `role.TrialFrequency.ToString() <> "N"` throws `NullReferenceException` when `TrialFrequency` is null (`RoleController.vb` L521) | **Removed** together with the computed-window algorithm (see D-020/D-026); the admin upsert never evaluates `TrialFrequency`, so no NRE guard exists in the service. | Corrected per CP2 review — the deviation no longer exists | N |
| D-022 | User-role period sentinel — **SUPERSEDED** (`RoleService.AddUserRoleAsync`) | `RoleInfo.TrialPeriod`/`BillingPeriod` were non-nullable `Integer` carrying the `Null.NullInteger` (-1) sentinel (`RoleController.vb` L522, L525) | **Removed** together with the computed-window algorithm (see D-020/D-026); the admin upsert reads neither `TrialPeriod` nor `BillingPeriod`, so the `?? nullInteger` sentinel re-expression is gone from the service. | Corrected per CP2 review — the deviation no longer exists | N |
| D-023 | Auto-assign enumeration (`RoleService.AutoAssignUsersAsync`) | `AutoAssignUsers` looped `UserController.GetUsers(PortalID, False)` over all portal users (`RoleController.vb` L68-83) | Enumerates via paged `IUserRepository.GetByPortalAsync(portalId, 0, int.MaxValue)` (one max-size page); the swallow-exception loop is preserved verbatim | Repository surface is paged; a single max-size page reproduces "all users" with no behavior change | N |
| D-024 | Remove-from-role guard (`RoleService.RemoveUserRoleAsync`) | `DeleteUserRole` returned `False` silently when `CanRemoveUserFromRole` failed for the portal administrator or the registered-users role (`RoleController.vb` L330-347, L764-769) | Throws `InvalidOperationException` ("Cannot remove this user from the role"), surfaced as RFC 7807 | Guard intent preserved; surfaced explicitly instead of silently swallowed, consistent with D-010/D-013 | N |
| D-025 | Role-assignment notification (`RoleService.RemoveUserRoleAsync` / `AddUserRoleAsync`) | `SendNotification` emailed the user on add/remove via `Mail.SendMail` + `Localization` (`RoleController.vb` L577-610) | Omitted | Mail/Localization/Profile OUT OF SCOPE (AAP 0.2.2); the `IRoleService` contract carries no notify flag | N |
| D-026 | Role-assignment contract (`RoleService.AddUserRoleAsync`) | TWO distinct legacy paths: the ADMIN `AddUserRole(PortalID, UserId, RoleId, EffectiveDate, ExpiryDate)` UPSERT that persists caller-supplied dates (`RoleController.vb` L295-317; driven by the admin `SecurityRoles.ascx.vb` L528-542, where a blank date textbox becomes `Null.NullDate` → null), and the separate SELF-SERVICE `UpdateUserRole(…, Cancel)` that computes the window from trial/billing config (`RoleController.vb` L489-557) | Contract is `AddUserRoleAsync(AssignUserRoleDto) -> UserRoleAssignmentDto` implementing the **ADMIN** path verbatim: load the existing assignment (`GetUserRole`); update its `EffectiveDate`/`ExpiryDate` via `IRoleRepository.UpdateUserRoleAsync`, or create it with the admin-supplied dates via `AddUserRoleAsync`; then return the persisted join row. The write DTO `AssignUserRoleDto` carries ONLY `UserID`/`RoleID`/`EffectiveDate`/`ExpiryDate`; `IsTrialUsed`/`Subscribed` are NOT write inputs (they remain read-only on `UserRoleAssignmentDto`). The self-service computed-window path is OUT OF SCOPE. Aligned end-to-end: `RolesController.AddUserToRole` accepts the optional body, treats the route `{roleId}/{userId}` as authoritative, and returns **201 Created** with `ApiResponse.Success(assignment)`; the Angular `role.service.ts#assignUserToRole` sends the date window and returns the persisted `UserRole`; `role.model.ts#AssignUserRole` drops `isTrialUsed`/`subscribed`. | An earlier CP2 revision wrongly ported the self-service computed-window algorithm into this admin method (the CP2 role-assignment-contract finding); corrected to the admin upsert and a single consistent contract across DTO → service → repository → controller → Angular service/model | N |
| D-027 | Auth login (`AuthService.LoginAsync`) | `UserController.UserLogin(portalId, …)` was portal-scoped, ran `ValidateUser`, then `FormsAuthentication.SetAuthCookie` (`UserController.vb` L991–L1033) | `LoginRequestDto` carries no portal context → defaults to the DNN primary portal (`PortalID = 0`); password verified via BCrypt `IPasswordHasher.Verify`; a stateless JWT access+refresh pair is issued (no auth cookie, no server session); a generic `UnauthorizedAccessException` avoids user enumeration | Facet of the sanctioned auth change (see D-001/D-002); default-portal login is a Phase-1 simplification (no portal selector in scope) | **Y** |
| D-028 | Auth logout (`AuthService.LogoutAsync`) | `PortalSecurity.SignOut()` called `FormsAuthentication.SignOut()` and expired the auth/role/language cookies (`PortalSecurity.vb` L77–L95) | No-op acknowledgement returning `Task.CompletedTask` (non-`async`); JWT is stateless and the client discards its tokens; no server-side refresh-token store in Phase 1 | Facet of the sanctioned auth change (see D-001); a stateless server holds no session to clear | **Y** |
| D-029 | Auth refresh (`AuthService.RefreshAsync`) | No legacy equivalent — Forms Auth used persistent cookies/tickets, not refresh tokens (`UserController.vb` L1035–L1045) | Refresh-token validation/rotation delegated to `IJwtService` (`ValidateToken` + `GenerateRefreshToken`); subject resolved from `ClaimTypes.NameIdentifier` with a `"sub"` fallback; a fresh access+refresh pair is rotated; no server-side refresh store in Phase 1 | New capability under the sanctioned JWT model (see D-001); rotation kept stateless for horizontal scaling | **Y** |


> The three sanctioned rows (D-001…D-003) are all facets of the **single** sanctioned
> change documented in [Section 3](#3-sanctioned-behavior-change-authentication--cryptography)
> — authentication & cryptography modernization. They are listed separately only for
> traceability. The non-sanctioned rows (D-004+) are recorded for completeness and are
> **behavior-preserving** by construction.

**Portal aggregate (`PortalService.cs`) notes.** Rows D-007…D-011 capture the
`PortalController.vb` → `PortalService` port. Two further points are recorded as **behavior
parity** (not deviations): (1) the legacy `GetPortalsByName` paging sentinel is **preserved
verbatim** — `pageIndex == -1` resets `pageIndex = 0` and `pageSize = int.MaxValue` so all
matching records return on a single page (`PortalController.vb` L262–271); and (2)
`PortalService.CreateAsync` deliberately adds **no** duplicate-name or home-directory-collision
check, matching the legacy `CreatePortal`, which performed none — adding one would be a
behavioral divergence. Reads (`GetByIdAsync`, `GetAllAsync`, `GetByNameAsync`, `GetByAliasAsync`)
drop the legacy `DataCache` + `CBO` reflection hydration in favor of EF Core materialization
(see [D-004](#62-deviation-index)).

**Role aggregate (`RoleService.cs`) notes.** Rows D-020...D-026 capture the
`RoleController.vb` (+ `RoleComparer.vb`) to `RoleService` port (the most complex service).
`AddUserRoleAsync` reproduces the **admin** assignment path (`RoleController.AddUserRole(PortalID,
UserId, RoleId, EffectiveDate, ExpiryDate)`, `RoleController.vb` L295-317) verbatim — a plain UPSERT of
the admin-supplied effective/expiry window: load the existing assignment (`GetUserRole`); create it with
those dates, or update its dates; return the persisted join row. It does **not** compute a trial/billing
window and never sets `IsTrialUsed`/`Subscribed`; that is the separate self-service
`UpdateUserRole(…, Cancel)` path (`RoleController.vb` L489-557), which is OUT OF SCOPE for this admin API.
(An earlier CP2 revision wrongly ported the self-service computed-window algorithm into this admin
method; rows D-020…D-022 recorded re-expressions of that algorithm and are now **SUPERSEDED** — the
algorithm was removed per the CP2 role-assignment-contract finding; see D-026.) Three further points are
recorded as **behavior parity** (not deviations): (1) `GetByPortalAsync` reproduces the legacy `RoleComparer`
case-insensitive (CurrentCulture) ordering by `RoleName` (`RoleComparer.vb` L55-57); (2)
`CreateAsync` adds **no** duplicate-name guard, matching the legacy `AddRole`, which performed
none, and both `CreateAsync` and `UpdateAsync` invoke `AutoAssignUsers` when `AutoAssignment`
is set (`RoleController.vb` L106, L256); and (3) `GetUserRoleAssignmentsAsync` is a read-side
projection of the per-assignment membership metadata mirroring the legacy
`GetUserRoles(PortalId, UserId)` (`RoleController.vb` L392-394). Role is **hard-deleted** with a
transactional `UserRole` cascade owned by the repository (see
[6.3](#63-per-entity-delete-strategy)), and `DeleteAsync` no-ops when the role is absent,
matching the legacy `DeleteRole` (`RoleController.vb` L125-133).

**Auth aggregate (`AuthService.cs`) notes.** Rows D-027…D-029 capture the synthesis of
`UserController.UserLogin` / `GetCurrentUserInfo` and the `PortalSecurity.vb` security model
into the stateless JWT orchestration service `AuthService` — the **single sanctioned behavior
change** (see [Section 3](#3-sanctioned-behavior-change-authentication--cryptography)).
`AuthService` touches **no** cryptographic or JWT primitives directly: BCrypt verification is
delegated to `IPasswordHasher` and all token issue/validate/rotate work to `IJwtService` (both in
`DnnMigration.Infrastructure`). `GetCurrentUserAsync` replaces the legacy
`HttpContext`/`Thread.CurrentPrincipal` lookup (`UserController.vb` L381–L403) with a repository
fetch by the user id supplied from JWT claims, projected to the password-free `UserDto` via
AutoMapper. `BuildAuthResponse` and `LogoutAsync` are intentionally **non-`async`** (no awaited
work) so the warnings-as-errors build (Gate 1) stays free of CS1998.

### 6.3 Per-Entity Delete Strategy

The legacy controllers used differing delete semantics per aggregate. These semantics
are **preserved**, implemented behind a per-entity delete strategy:

| Aggregate | Delete strategy | Mechanism |
|---|---|---|
| Portal | **Hard delete** | Transactional cascade |
| Role | **Hard delete** | Transactional cascade |
| Module | **Soft delete** | `IsDeleted` flag (list queries filter it out) |
| User | **Soft delete** | `IsDeleted` flag |
| Tab | **Soft delete** | `IsDeleted` flag |

> This is a **behavior-preserving** mapping (default `N` in the
> [Deviation Index](#62-deviation-index)): the new code reproduces the legacy
> hard-vs-soft delete behavior for each aggregate rather than unifying it.

---

## 7. Dependency, Secret, and Authorization Decisions

These are **configuration- and dependency-level decisions** — not domain-behavior
changes. They are recorded here because each departs from a naive default and was made
deliberately to satisfy the AAP, ADR-002, or a CP1 review finding.

### 7.1 Pinned Framework Versions and Accepted Security Advisories

The technology stack is **frozen by the AAP**: §0.5.1 ("Dependency Inventory") pins
exact versions, and §0.7.2 states that *"Mandated versions are honored exactly
regardless of newer releases."* That same §0.7.2, however, also mandates a secure
dependency posture (*"no known vulnerable supported packages"*) as a hard validation
gate. Where a pinned package is itself the vulnerable component and no compensating
control fully removes the risk, the security gate **takes precedence** over the exact
pin and a **security-mandated version upgrade** is applied (item (b) below). Advisories
that do **not** affect this application's actual code surface are instead addressed with
**compensating controls** documented per-advisory (item (a) below). (The caret ranges
still admit in-range security patches on a clean install.)

**(a) Angular `^19.0.0`** — `frontend/package.json`. The locally resolved `@angular/*`
line carries published advisories; each is assessed against this application's actual
surface:

| Advisory (Angular) | Applies here? | Compensating control |
|---|---|---|
| `@angular/common` `formatDate` **DoS** with attacker-controlled format strings (`<= 19.2.25`) | **Yes** — `shared/pipes/date-format.pipe.ts` forwards a `format` argument to `formatDate` | **Mitigated.** The pipe clamps `format` to a curated `SAFE_FORMATS` allow-list with a 32-character cap; any unknown or over-long value falls back to the default `shortDate`. An untrusted format string can no longer reach `formatDate`. |
| Transfer-cache data leakage / state poisoning | **No** | The app is a **pure client-rendered SPA** — no SSR, no hydration, no transfer cache (AAP §0.1.1: *"all rendering is client-side"*; §0.2.2 excludes SSR). The vulnerable path is never executed. |
| Hydration DOM-clobbering / cache poisoning | **No** | Same as above — hydration is not used. |
| Compiler sanitizer-bypass **XSS** | **No** | Templates use Angular's default interpolation and built-in sanitization; **no** `bypassSecurityTrust*` API is used anywhere in `frontend/src`. |

**(b) AutoMapper — advisory GHSA-rvv3-g6hj-g44x (HIGH), REMEDIATED by upgrade.** The
AAP §0.5.1 pin was `AutoMapper.Extensions.Microsoft.DependencyInjection 12.0.1`, which
brings transitive **AutoMapper 12.0.1**. `dotnet list package --include-transitive
--vulnerable` reports a HIGH advisory (GHSA-rvv3-g6hj-g44x / CVE-2026-32933) against
AutoMapper 12.0.1: a **DoS via uncontrolled recursion** when mapping cyclic /
self-referential object graphs (stack exhaustion). Although every current CP2 profile is
a flat scalar entity↔DTO projection (no cyclic navigation maps today), the advisory flags
the **package itself**, so the secure-dependency gate (§0.7.2) is not satisfied by a
compensating control while a vulnerable supported version remains referenced.

**Resolution (security-mandated deviation from the §0.5.1 pin, authorized by §0.7.2):**

- `DnnMigration.Application.csproj`: removed
  `AutoMapper.Extensions.Microsoft.DependencyInjection 12.0.1` and now references
  **`AutoMapper 15.1.1`** directly. The patched 15.x line carries the
  GHSA-rvv3-g6hj-g44x backport (a default `MaxDepth` of 64 for self-referential types).
  From AutoMapper v13+ the `Microsoft.Extensions.DependencyInjection` integration
  (`AddAutoMapper(...)`) is part of the **core** package, so the separate extension
  package is no longer needed. Version `15.0.0` was deliberately avoided (delisted by the
  maintainer due to its breaking changes); `15.1.1` is the patched baseline on the 15.x line.
- **Breaking-change handling (`MapperConfiguration` ctor):** AutoMapper 15 changed
  `MapperConfiguration` to require an `ILoggerFactory`:
  `new MapperConfiguration(cfg => ..., ILoggerFactory)`. The three mapping unit tests
  (`PortalProfileTests`, `RoleProfileTests`, `UserProfileTests`) construct
  `MapperConfiguration` directly (no DI), so each now passes
  `Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance`.
  `services.AddAutoMapper(...)` (CP3 `Program.cs`) supplies this parameter automatically,
  so no production code change is required there.
- **Licensing note:** AutoMapper 15+ moved to a dual commercial/OSS license with an
  optional license key. Enforcement is **log-message only** (category
  `LuckyPennySoftware.AutoMapper.License`): there is no license server, no outbound HTTP
  call, and no feature degradation — a missing key does **not** affect build, tests, or
  runtime behavior. CP3 `Program.cs` may add a logging filter to mute the informational
  message; CP2 is unaffected (the `--warnaserror` Gate-1 build is a compile-time gate and
  is not touched by a runtime log message).
- **IdentityModel graph alignment (resolves the companion CP2 finding on
  `Infrastructure.csproj`):** AutoMapper 15.1.1 uses a JWT-format license key and therefore
  depends transitively on `Microsoft.IdentityModel.JsonWebTokens 8.14.0 →
  Microsoft.IdentityModel.Tokens >= 8.14.0`. Infrastructure previously pinned
  `System.IdentityModel.Tokens.Jwt` and `Microsoft.IdentityModel.Tokens` to `7.1.2`, which
  became an **NU1605 downgrade** (error under `--warnaserror`). Both Infrastructure pins are
  upgraded to **`8.14.0`**, the current supported 8.x line.
  `Microsoft.AspNetCore.Authentication.JwtBearer 8.0.11` requires
  `Microsoft.IdentityModel.* >= 7.1.2` and unifies **UP** to 8.14.0 with no downgrade;
  `JwtService.cs` (which uses `JwtSecurityTokenHandler`, `SecurityTokenDescriptor`,
  `SymmetricSecurityKey`, `SigningCredentials`, `TokenValidationParameters`) builds clean
  against 8.14.0 under `--warnaserror`. This satisfies the CP2 resolution to *"pin a
  supported/current IdentityModel line consistently"* and makes the whole solution agree on
  one IdentityModel assembly set.
- **Verification:** `dotnet list package --include-transitive --vulnerable` reports **no
  vulnerable packages** for all six projects; `DnnMigration.Application` and
  `DnnMigration.Infrastructure` build with **0 warnings / 0 errors** under `--warnaserror`;
  `DnnMigration.UnitTests` passes **191/191**.

**Forward control (binding requirement, retained):** any future profile that maps a
navigation property capable of forming a cycle **MUST** still set an explicit
`.MaxDepth(n)` on that map (defense in depth, in addition to the patched library's default
depth guard).

### 7.2 Secret Externalization

No production credential, password, or signing key is committed to source control
(CWE-798). Committed configuration contains **non-secret placeholders only**; real
values are injected from the environment / a secret manager at run time.

| Setting | Committed value (placeholder) | Runtime source |
|---|---|---|
| `ConnectionStrings:Default` (user / password) | `User Id=__DB_USER__;Password=__DB_PASSWORD__` (`appsettings.json`); `Password=__LOCAL_SQL_PASSWORD__` (`appsettings.Development.json`) | `ConnectionStrings__Default` env var |
| `Jwt:Key` | `""` (empty — **fail-closed**) | `Jwt__Key` env var, **>= 32 characters** |
| `Jwt:Issuer` / `Jwt:Audience` | `DnnMigration` / `DnnMigration` (non-secret) | committed (not secret) |

- The committed `Jwt:Key` is intentionally **empty** so that **no usable signing key is
  baked into source**. The real key must be supplied via `Jwt__Key` (>= 32 chars); the
  composition root binds `JwtSettings` and is expected to reject a missing/short key at
  startup rather than fall back to a weak default.
- The same `Jwt__Key` (>= 32 chars), `ConnectionStrings__Default`, `Jwt__Issuer`,
  `Jwt__Audience`, and `Jwt__ExpirationMinutes=60` variables are supplied by the Docker
  Compose environment for container runs.

### 7.3 Fail-Closed Authorization

The Angular `has-permission` directive (`shared/directives/has-permission/`) gates UI
affordances through `core/services/permission.service.ts`. The service's
granted-permission set (`VIEW` / `EDIT` / `DELETE` / `MANAGE_SETTINGS`) **defaults to
empty**, so the UI is **fail-closed**: nothing gated is shown until permissions are
explicitly granted after authentication. This mirrors the legacy
`HasNecessaryPermission` intent (an unknown or absent permission is **denied**) and is
the client-side companion to the server-side authorization policies (Deviation
**D-003**). Server-side checks remain authoritative; the directive only hides
affordances the user may not use.

### 7.4 `DisplaySyndicate` Scope

`ModuleInfo.DisplaySyndicate` is an **in-scope** legacy field and is preserved on the
`Module` entity/DTO and the Angular module model/form for public-contract parity.
Note that `DisplaySyndicate` is physically a `dbo.TabModules` column (not a
`dbo.Modules` column), so per the §4.2 schema-fidelity correction it is **`Ignore()`d**
in `ModuleConfiguration.cs` (not mapped onto `dbo.Modules`) and rehydrated by the
projection layer from a `TabModules` join; the property itself remains on the
entity/DTO/model for contract parity. The **Syndication / RSS provider** feature is
**explicitly excluded** by the AAP (§0.2.2). The flag is therefore carried for **data
parity only**; no RSS / syndication provider behavior is implemented, and none may be
added without an explicit scope decision.

---

## 8. References

- [`docs/technical-specifications.md`](docs/technical-specifications.md) — authoritative
  architecture, technology pins, layer responsibilities, and the source-to-target
  screen map.
- Legacy reference source (read-only, no compiled target artifacts):
  - `Library/Components/Security/PortalSecurity.vb` — Forms Auth, DES, `SecurityAccessLevel`.
  - `Library/Components/Shared/CBO.vb` — reflection hydration (`Activator.CreateInstance`, `HydrateObject`, `IHydratable.Fill`).
  - `Library/Components/Shared/Null.vb` — null sentinel constants.
  - `Library/Providers/DataProviders/SqlDataProvider/SqlDataProvider.vb` — `SqlHelper` stored-procedure layer.
  - `Library/Components/Users/Membership/UserLoginStatus.vb`, `UserCreateStatus.vb` — verbatim enum values.
  - `Library/Components/Modules/ModuleInfo.vb` — `VisibilityState` enum.
