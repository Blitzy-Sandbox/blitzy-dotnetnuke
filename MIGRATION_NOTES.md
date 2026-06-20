# Migration Notes — DotNetNuke 4.x → .NET 8 + Angular 19

> **Authority:** This document is the root-level **Minimal Change Clause deviation log** mandated by the Agent Action Plan (AAP §0.2.1 *Documentation creations*, §0.4.1, and §0.7.2 *Special Instructions and Constraints*). It is the human-readable counterpart to the in-code `// MIGRATION:` comment convention and the single, authoritative index of **every deviation** from the legacy DotNetNuke (DNN) `4.9.0.85` behavior, the rationale for each, and **every ported (intentionally reproduced) bug**.

This migration is a **full, ground-up rewrite** — not an incremental, in-place modernization — of the legacy DNN 4.x codebase (VB.NET / ASP.NET Web Forms on .NET Framework 2.0) into two independently deployable applications:

- a **C# 12 / .NET 8 LTS** ASP.NET Core 8 Web API following the Backend-for-Frontend (BFF) pattern, and
- an **Angular 19** standalone-component single-page application (SPA).

The legacy `Library/` and `Website/` VB.NET trees are retained strictly as **source/reference material**; they emit no compiled target artifacts. Unless a section below states otherwise, **all domain logic is preserved exactly** and the only sanctioned behavior change is the authentication/cryptography modernization documented in [§3](#3-sanctioned-behavior-change--authentication--cryptography).

---

## Table of Contents

1. [Minimal Change Clause & Scope](#1-minimal-change-clause--scope)
2. [The `// MIGRATION:` Annotation Convention](#2-the--migration-annotation-convention)
3. [Sanctioned Behavior Change — Authentication & Cryptography](#3-sanctioned-behavior-change--authentication--cryptography)
4. [Behavior-Preserving Conversions](#4-behavior-preserving-conversions)
   - [4.1 Data Access: ADO.NET / `SqlHelper` / `CBO` → EF Core 8](#41-data-access-adonet--sqlhelper--cbo--ef-core-8)
   - [4.2 Schema Preservation (ADR-002)](#42-schema-preservation-adr-002)
   - [4.3 Null Sentinels → C# Nullable Types](#43-null-sentinels--c-nullable-types)
   - [4.4 VB.NET → C# 12 Construct Catalog](#44-vbnet--c-12-construct-catalog)
   - [4.5 Enum Verbatim Preservation](#45-enum-verbatim-preservation)
5. [Presentation Re-platforming](#5-presentation-re-platforming)
6. [Ported Bugs & Deviation Index](#6-ported-bugs--deviation-index)
   - [6.1 Ported Bugs](#61-ported-bugs)
   - [6.2 Deviation Index](#62-deviation-index)
   - [6.3 Per-Entity Delete Strategy](#63-per-entity-delete-strategy)
7. [References](#7-references)

---

## 1. Minimal Change Clause & Scope

**This is a full rewrite, not an incremental change.** The transformation spans five orthogonal dimensions simultaneously — language (VB.NET → C# 12), runtime (.NET Framework 2.0 → .NET 8 LTS), presentation (ASP.NET Web Forms → REST API + Angular SPA), data access (ADO.NET `SqlHelper` / `SqlDataProvider` → EF Core 8), and deployment (IIS/Windows → Docker on Linux). Despite the breadth of the re-platforming, the **domain semantics are held constant**.

**Preserved public contracts and domain semantics.** The behavior and semantics of `PortalInfo`, `ModuleInfo`, `UserInfo`, `RoleInfo`, and `TabInfo` are preserved exactly. Enum values are carried over **verbatim** (see [§4.5](#45-enum-verbatim-preservation)) so that any persisted or compared integer values remain valid.

**Behavioral equivalence is required.** The rewritten system must produce **identical outputs for identical inputs**. Business logic is ported as-is; it is **not** "improved," refactored for elegance, or re-architected beyond the mechanical VB→C# conversion and the layering described in this log.

**UI functional parity is required.** Every in-scope legacy administrative workflow (Portal, Module, User, Role, and Tab/Page management) is reproduced in Angular, **including its field-level validation rules and error messages**.

**Do not optimize or improve business logic.** Pre-existing bugs are **reproduced and documented** (see [§6.1](#61-ported-bugs)) rather than silently fixed. A fix is applied **only** when a pre-existing defect blocks compilation or a mandated validation gate; any such fix is itself recorded as a deviation in [§6.2](#62-deviation-index).

### Scope at a glance

| Aspect | Legacy (DNN 4.9.0.85) | Target |
|---|---|---|
| Language | VB.NET (`Option Strict On`, `Option Explicit On`) | C# 12 (nullable reference types on) |
| Runtime | .NET Framework 2.0 | .NET 8 LTS |
| Presentation | ASP.NET Web Forms (`.aspx`/`.ascx`, postback, ViewState) | REST API (`/api/v1/...`) + Angular 19 SPA |
| Data access | ADO.NET via `SqlHelper` + `SqlDataProvider` stored procedures; `CBO` reflection hydration | EF Core 8 (Code-First, Fluent API), entity materialization |
| Authentication | Forms Authentication + DES (see [§3](#3-sanctioned-behavior-change--authentication--cryptography)) | JWT Bearer + BCrypt **(only sanctioned change)** |
| Deployment | IIS / Windows | Docker multi-container on Linux |

> **Scope note.** Only the five in-scope aggregates and their shared utilities are migrated. Excluded components (Telerik RadControls, `DotNetNuke.Entities.Host`, `DotNetNuke.Common.Globals`, the Scheduling/Skinning/Search/Cache/Logging providers, `Website/DesktopModules/**`, `Website/Install/**`, COM/VB6 interop, and the Syndication/RSS subsystem) are neither migrated nor used as target-generation references, per AAP §0.2.2.

---

## 2. The `// MIGRATION:` Annotation Convention

Every deviation from legacy behavior, and every intentionally reproduced (ported) bug, is annotated **at the point of change** in the C# or TypeScript source with a `// MIGRATION:` comment. This document is the **central index** of those annotations: each entry in the [Deviation Index](#62-deviation-index) and the [Ported Bugs](#61-ported-bugs) table is intended to be traceable to one or more `// MIGRATION:` comments in code, and vice versa.

**Comment grammar.** A `// MIGRATION:` comment briefly states *what* changed (or *what bug is preserved*), *why*, and — where applicable — the originating legacy file/line. Keep it terse; the full rationale lives here.

The following examples are **illustrative only**. They document the comment style; they are not compilable units of this file and are not required to build.

```csharp
// MIGRATION: Forms Authentication (PortalSecurity.vb:L79 FormsAuthentication.SignOut)
// replaced by stateless JWT logout (refresh-token revocation). Sanctioned change DEV-001.
public Task LogoutAsync(string refreshToken) => _jwtService.RevokeAsync(refreshToken);
```

```csharp
// MIGRATION: legacy Null.NullInteger sentinel (-1) -> nullable int?.
// A persisted -1 retains its legacy "unset" meaning; see MIGRATION_NOTES §4.3.
public int? ParentId { get; set; }
```

```csharp
// MIGRATION (PORTED BUG BUG-XXX): reproduces legacy off-by-one in <legacy file>:<line>.
// Preserved for behavioral equivalence; do NOT "fix". Tracked in MIGRATION_NOTES §6.1.
```

```typescript
// MIGRATION: legacy ViewState postback grid -> stateless GET /api/v1/portals.
// Letter-filter + paging semantics preserved from Portals.ascx.vb.
```

---

## 3. Sanctioned Behavior Change — Authentication & Cryptography

This is the **single sanctioned behavior change** in the entire migration. It is an explicit **security upgrade**, deliberately chosen over preserve-as-is, and is the one place where the rewritten system is *permitted* to behave differently from DNN 4.9.0.85. All sub-changes below carry deviation ID **DEV-001** in the [Deviation Index](#62-deviation-index).

The legacy security model lives in `Library/Components/Security/PortalSecurity.vb` (≈650 lines), which is the only sanctioned site of behavioral change.

### 3.1 Session management: Forms Authentication → stateless JWT Bearer

- **Legacy.** Session management uses ASP.NET **Forms Authentication** — e.g. `System.Web.Security.FormsAuthentication.SignOut()` (`PortalSecurity.vb` ≈ line 79), with identity carried in server-managed auth cookies.
- **Target.** Stateless **JWT Bearer** tokens: a **60-minute access token** plus **refresh-token rotation**. The server retains **no session state**, which enables horizontal scaling. (JWT `Issuer` = `DnnMigration`, `Audience` = `DnnMigration`, `ExpirationMinutes` = `60`.)
- **Where.** `DnnMigration.Infrastructure/Identity/JwtService.cs` (token issue/validate/rotate) + `DnnMigration.Application/Services/AuthService.cs` (login orchestration), surfaced through `/api/auth/{login,refresh,logout,me}`. Auth endpoints are rate-limited; CORS is restricted to the Angular origin.

### 3.2 Password cryptography: DES → BCrypt

- **Legacy.** Symmetric encryption uses the weak **56-bit DES** algorithm via `DESCryptoServiceProvider` + `CryptoStream`, inside the `Encrypt`/`Decrypt` methods (`PortalSecurity.vb` ≈ lines 138–211). The legacy key is padded with `X` (or truncated) to **16 characters**, then split into an **8-byte key** (`Left(strKey, 8)`) and an **8-byte initialization vector** (`Right(strKey, 8)`); output is Base64-encoded.
- **Target.** Adaptive **BCrypt** password hashing via **BCrypt.Net-Next 4.0.3** in `DnnMigration.Infrastructure/Identity/PasswordHasher.cs`. Passwords are hashed, never reversibly encrypted; the DES `Encrypt`/`Decrypt` round-trip is retired entirely.

### 3.3 Authorization: `SecurityAccessLevel` / `HasNecessaryPermission` → policies + directive

- **Legacy.** Authorization is expressed through the `SecurityAccessLevel` enum and a set of `HasNecessaryPermission` overloads (`PortalSecurity.vb` ≈ lines 469, 494, 517) that `Select Case` on that level. The verbatim legacy enum values are recorded in [§4.5](#45-enum-verbatim-preservation).
- **Target.** `HasNecessaryPermission` maps to **ASP.NET Core authorization policies/handlers** on the server, and to the Angular **`has-permission` directive** for UI gating. Permission keys `VIEW`, `EDIT`, `DELETE`, and `MANAGE_SETTINGS` are honored; an **unknown permission key yields a `403`** (`ForbiddenException`).

> **Reminder.** Everything in §3 is the *only* deviation that changes observable behavior. All conversions in [§4](#4-behavior-preserving-conversions) and [§5](#5-presentation-re-platforming) are **behavior-preserving** by construction.

---

## 4. Behavior-Preserving Conversions

The conversions in this section change *how* the system is built but **not** *what it does*. None of them is a sanctioned behavior change; each is a mechanical or structural re-platforming that preserves observable behavior. Accordingly, every item here defaults to **Sanctioned? = N** in the [Deviation Index](#62-deviation-index).

### 4.1 Data Access: ADO.NET / `SqlHelper` / `CBO` → EF Core 8

The legacy data layer is built on Microsoft.ApplicationBlocks.Data `SqlHelper` with a stored-procedure-per-operation convention, and hydrates results into objects by reflection.

- **Legacy stored-procedure layer.** `Library/Providers/DataProviders/SqlDataProvider/SqlDataProvider.vb` contains **255 stored-procedure call sites**. Procedure names are composed at runtime as `DatabaseOwner & ObjectQualifier & "<ProcName>"` (e.g. `GetPortal`, `GetPortalByAlias`, `GetPortals`, `GetPortalCount`). The `ObjectQualifier` and `DatabaseOwner` read-only properties (≈ lines 96 and 102) supply the multi-tenant prefix; the connection string is resolved via `Config.GetConnectionString()` (≈ line 61).
- **Legacy hydration.** Returned `IDataReader` results are mapped to objects by `Library/Components/Shared/CBO.vb` using reflection — `Activator.CreateInstance` followed by `HydrateObject` (PropertyInfo name-matching against reader columns, ≈ line 136) or, when the type implements `IHydratable`, its `Fill` method (≈ lines 50–90). The collection variants are `FillCollection`/`FillObject`.

**Target approach.** The `SqlHelper` + `CBO` reflection pipeline is replaced **wholesale** by **EF Core 8 entity materialization**. Repositories issue LINQ queries against POCO entities mapped — through `IEntityTypeConfiguration<T>` Fluent API — onto the existing tables. **No manual `IDataReader`-to-object code survives.**

- Reads use `AsNoTracking()`; writes use change tracking with `SaveChangesAsync()`.
- The legacy `ObjectQualifier` / `DatabaseOwner` prefix is expressed in the EF Core `ToTable()` mapping rather than string-concatenated at every call site.
- **Retained-procedure fallback.** Where a stored procedure encapsulates complex set-based logic that is risky to re-express in LINQ, the procedure is **preserved** and invoked via `FromSqlRaw` / `ExecuteSqlRawAsync`, keeping behavioral equivalence.

### 4.2 Schema Preservation (ADR-002)

The existing DNN `4.9.0.85` database schema is mapped **UNCHANGED**:

- **No table or column changes.**
- **No EF Core migrations** are generated in Phase 1.
- **No data migration** is performed in Phase 1.

Authoritative table and column names come from the install scripts — `InstallCommon.sql`, `InstallRoles.sql`, `InstallProfile.sql`, `InstallMembership.sql` (under `Website/Providers/DataProviders/SqlDataProvider/`) — together with `SqlDataProvider.vb`. EF Core's `IEntityTypeConfiguration<T>` Fluent API maps the POCO entities onto those exact names; the tenant prefix is applied in `ToTable()`.

### 4.3 Null Sentinels → C# Nullable Types

Legacy DNN encodes "no value" with sentinel constants from `Library/Components/Shared/Null.vb` rather than database `NULL`. These map to C# **nullable types** (`int?`, `bool?`, …) or `default`. Preserving the *meaning* of "unset" is behavior-preserving; the representation changes from a magic value to a true nullable.

| Legacy sentinel (`Null.vb`) | Legacy value | Target C# representation |
|---|---|---|
| `Null.NullShort` | `-1` (`Short`) | `short?` (`null` = unset) |
| `Null.NullInteger` | `-1` (`Integer`) | `int?` (`null` = unset) |
| `Null.NullByte` | `255` (`Byte`) | `byte?` (`null` = unset) |
| `Null.NullSingle` | `Single.MinValue` | `float?` (`null` = unset) |
| `Null.NullDouble` | `Double.MinValue` | `double?` (`null` = unset) |
| `Null.NullDecimal` | `Decimal.MinValue` | `decimal?` (`null` = unset) |
| `Null.NullDate` | `Date.MinValue` | `DateTime?` (`null` = unset) |
| `Null.NullString` | `""` (empty string) | `string?` (`null` / empty per call-site semantics) |
| `Null.NullBoolean` | `False` | `bool?` (`null` = unset) |
| `Null.NullGuid` | `Guid.Empty` | `Guid?` (`null` = unset) |

> **Note.** Where legacy code compares against a sentinel (e.g. `Null.IsNull(value)` or `value = Null.NullInteger`), the C# equivalent compares against `null` (or the documented sentinel where a persisted value must remain bit-compatible). Any place where the sentinel value itself is persisted to an unchanged column is annotated with a `// MIGRATION:` comment.

### 4.4 VB.NET → C# 12 Construct Catalog

The legacy projects compile with `Option Explicit On` and `Option Strict On`. The target enables **nullable reference types** and a zero-warning **`--warnaserror`** policy (excluding `CS8618` nullable-field warnings, per the build configuration). The following catalog governs the mechanical conversion.

| Legacy VB.NET construct | Target C# 12 |
|---|---|
| `Implements IInterface` | `: IInterface` |
| Private backing field + `Property Get`/`Set` | Auto-property `{ get; set; }` |
| `Null.NullInteger` / `Null.NullBoolean` sentinels | Nullable types (`int?`, `bool?`) / `default` (see [§4.3](#43-null-sentinels--c-nullable-types)) |
| `CType(obj, Type)` | `(Type)obj` or `as Type` |
| `Is Nothing` | `is null` / `== null` |
| `AndAlso` / `OrElse` | `&&` / `\|\|` |
| `Inherits BaseClass` | `: BaseClass` |
| `Shared` member | `static` member |
| `ReadOnly Property` | get-only property |
| Module-level `Imports` | File-scoped namespace + global usings |
| `ByRef` / `ParamArray` | `ref` / `params` |
| VB attributes (e.g. `<Browsable(False)>`, `<Required(True), MaxLength(128)>`) | DataAnnotations / FluentValidation rules |

### 4.5 Enum Verbatim Preservation

Enum values are carried over **EXACTLY** so that any persisted or compared integer remains valid. The values below were read directly from the legacy source.

**`UserLoginStatus`** — from `Library/Components/Users/Membership/UserLoginStatus.vb`:

| Member | Value |
|---|---|
| `LOGIN_FAILURE` | `0` |
| `LOGIN_SUCCESS` | `1` |
| `LOGIN_SUPERUSER` | `2` |
| `LOGIN_USERLOCKEDOUT` | `3` |
| `LOGIN_USERNOTAPPROVED` | `4` |
| `LOGIN_INSECUREADMINPASSWORD` | `5` |
| `LOGIN_INSECUREHOSTPASSWORD` | `6` |

**`UserCreateStatus`** — from `Library/Components/Users/Membership/UserCreateStatus.vb` (preserved verbatim; values `0`–`17`):

| Member | Value | Member | Value |
|---|---|---|---|
| `AddUser` | `0` | `InvalidQuestion` | `10` |
| `UsernameAlreadyExists` | `1` | `InvalidUserName` | `11` |
| `UserAlreadyRegistered` | `2` | `ProviderError` | `12` |
| `DuplicateEmail` | `3` | `Success` | `13` |
| `DuplicateProviderUserKey` | `4` | `UnexpectedError` | `14` |
| `DuplicateUserName` | `5` | `UserRejected` | `15` |
| `InvalidAnswer` | `6` | `PasswordMismatch` | `16` |
| `InvalidEmail` | `7` | `AddUserToPortal` | `17` |
| `InvalidPassword` | `8` | | |
| `InvalidProviderUserKey` | `9` | | |

**`VisibilityState`** — from `Library/Components/Modules/ModuleInfo.vb`. The enum has no explicit values, so VB assigns them by **declaration order** starting at `0`. The verbatim source order must be preserved:

| Member | Implicit value (declaration order) |
|---|---|
| `Maximized` | `0` |
| `Minimized` | `1` |
| `None` | `2` |

> **Accuracy note.** AAP §0.3.1 / §0.6.3 informally lists this enum as "None/Minimized/Maximized." That ordering would invert the persisted integers. The **legacy source declaration order is `Maximized, Minimized, None`**, yielding `Maximized = 0`, `Minimized = 1`, `None = 2`. The C# enum preserves the source order so that stored/compared values stay valid.

**`SecurityAccessLevel`** — from `Library/Components/Security/PortalSecurity.vb` (≈ lines 45–53). Recorded verbatim (AAP §0.6.2 abbreviated this list to `Anonymous`/`View`/`Edit`/`Admin`/`Host`; the full legacy set includes the two negative control values):

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

The ASP.NET Web Forms presentation tier is **fully replaced**. The postback / ViewState / server-control / code-behind event model of `.aspx` and `.ascx` controls is retired in favor of a **stateless REST API** (`/api/v1/...`) consumed by a **client-rendered Angular 19 SPA**. There is no server-side HTML rendering and no SSR.

- **Standard mapping pattern.** List/Grid → `GET /api/v1/{entity}` → list component; Detail → `GET /api/v1/{entity}/{id}` → detail component; Create → `POST /api/v1/{entity}` → create form; Edit → `PUT /api/v1/{entity}/{id}` → edit form; Delete → `DELETE /api/v1/{entity}/{id}` → confirmation dialog; Search/Filter → `GET /api/v1/{entity}?query=…` → filtered list.
- **Login is new.** The Angular login component has no legacy equivalent; it replaces Forms Authentication (see [§3](#3-sanctioned-behavior-change--authentication--cryptography)).
- **Error handling.** `Website/ErrorPage.aspx.vb` is **superseded by** `ExceptionHandlingMiddleware` (RFC 7807 Problem Details) rather than ported as a page.

### Uniform API envelope

- **Success** responses carry a `{ data, meta }` envelope.
- **Errors** follow **RFC 7807 Problem Details**: `{ type, title, status, detail, errors }`.
- All resource endpoints are URL-path versioned under `/api/v1/`.

> The authoritative source-to-target screen map (each Angular feature component traced to its originating `.ascx.vb` control) is enumerated in AAP §0.4.1; it is referenced here rather than duplicated.

---

## 6. Ported Bugs & Deviation Index

This is the **living** part of the log. As the rewrite proceeds, every intentionally reproduced bug and every recorded deviation is added below and cross-referenced with its `// MIGRATION:` comment in code.

### 6.1 Ported Bugs

Pre-existing defects in the legacy code are **reproduced as-is** to preserve behavioral equivalence; they are **not** silently fixed. Each ported bug is logged here and annotated in code as `// MIGRATION (PORTED BUG <ID>): …`.

| ID | Location (legacy file) | Legacy Behavior | Why Preserved | `// MIGRATION:` ref |
|---|---|---|---|---|
| _BUG-001_ | _(none recorded yet)_ | _—_ | _—_ | _—_ |

> _No ported bugs have been recorded yet. Entries are added as defects are encountered during the rewrite. A defect is fixed (rather than ported) **only** if it blocks compilation or a mandated validation gate, in which case the fix is logged as a deviation in [§6.2](#62-deviation-index)._

### 6.2 Deviation Index

The authentication/cryptography modernization (**DEV-001**) is the **only** sanctioned behavior change. All other deviations are structural/mechanical and default to **Sanctioned? = N** (behavior-preserving).

| ID | Area | Legacy | New | Rationale | Sanctioned? (Y/N) |
|---|---|---|---|---|---|
| DEV-001 | Authentication & cryptography | Forms Authentication + 56-bit DES (`PortalSecurity.vb`) | JWT Bearer (60-min access + refresh rotation) + BCrypt.Net-Next 4.0.3 | Explicit security upgrade; stateless tokens enable horizontal scaling (see [§3](#3-sanctioned-behavior-change--authentication--cryptography)) | **Y** |
| DEV-002 | Data access | ADO.NET `SqlHelper` + `CBO` reflection hydration + 255 stored procedures | EF Core 8 entity materialization (`AsNoTracking`/`SaveChangesAsync`; `FromSqlRaw` fallback) | Re-platforming; observable behavior unchanged (see [§4.1](#41-data-access-adonet--sqlhelper--cbo--ef-core-8)) | N |
| DEV-003 | Null handling | `Null.*` sentinel constants | C# nullable types / `default` | Idiomatic representation of "unset"; meaning preserved (see [§4.3](#43-null-sentinels--c-nullable-types)) | N |
| DEV-004 | Presentation | ASP.NET Web Forms (postback/ViewState) | REST `/api/v1/...` + Angular 19 SPA | Tech-stack migration; UI functional parity preserved (see [§5](#5-presentation-re-platforming)) | N |
| DEV-005 | Error handling | `Website/ErrorPage.aspx.vb` | `ExceptionHandlingMiddleware` (RFC 7807) | Stateless API error contract; same error semantics (see [§5](#5-presentation-re-platforming)) | N |

> Add new deviations with the next sequential `DEV-NNN` ID. Anything that is **not** DEV-001 must be behavior-preserving (Sanctioned? = N); if a change would alter observable behavior, it must be justified as unblocking a compilation or validation gate and called out explicitly.

### 6.3 Per-Entity Delete Strategy

The delete strategy intentionally differs per aggregate, mirroring the legacy semantics (AAP §0.3.3, Factory/Strategy pattern). This is **behavior-preserving**: each entity keeps the delete semantics it had in DNN.

| Entity | Delete strategy | Mechanism |
|---|---|---|
| Portal | **Hard delete** | Transactional cascade |
| Role | **Hard delete** | Transactional cascade |
| Module | **Soft delete** | `IsDeleted` flag; list queries filter it out |
| User | **Soft delete** | `IsDeleted` flag; list queries filter it out |
| Tab | **Soft delete** | `IsDeleted` flag; list queries filter it out |

---

## 7. References

- [`./docs/technical-specifications.md`](./docs/technical-specifications.md) — the project technical specification (architecture, API surface, configuration, screen map).
- [`./README.md`](./README.md) — installation and usage instructions for the new backend and frontend applications.
- Legacy source of truth (reference only, not compiled): `Library/Components/Security/PortalSecurity.vb`, `Library/Components/Shared/CBO.vb`, `Library/Components/Shared/Null.vb`, `Library/Providers/DataProviders/SqlDataProvider/SqlDataProvider.vb`, `Library/Components/Users/UserInfo.vb` (and the `Membership/` enum files), `Library/Components/Modules/ModuleInfo.vb`.

---

_This document is maintained alongside the migration. Keep every factual claim consistent with the AAP and the cited legacy source files, and keep each entry traceable to its `// MIGRATION:` annotation in code._
