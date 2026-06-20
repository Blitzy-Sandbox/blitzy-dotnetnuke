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
   - [4.6 User & UserRole EF Mapping (`UserConfiguration`)](#46-user--userrole-ef-mapping-userconfiguration)
- [4.7 Tab & TabPermission EF Mapping (`TabConfiguration`)](#47-tab--tabpermission-ef-mapping-tabconfiguration)
- [4.8 Module / DesktopModule / ModuleDefinition EF Mapping (`ModuleConfiguration`)](#48-module--desktopmodule--moduledefinition-ef-mapping-moduleconfiguration)
5. [Presentation Re-platforming](#5-presentation-re-platforming)
6. [Ported Bugs & Deviation Index](#6-ported-bugs--deviation-index)
   - [6.1 Ported Bugs](#61-ported-bugs)
   - [6.2 Deviation Index](#62-deviation-index)
   - [6.3 Per-Entity Delete Strategy](#63-per-entity-delete-strategy)
   - [6.4 CP1 Foundation-Layer Remediation & Risk Index](#64-cp1-foundation-layer-remediation--risk-index)
   - [6.5 Accepted Dependency-Vulnerability Risks (AAP-Pinned)](#65-accepted-dependency-vulnerability-risks-aap-pinned)
   - [6.6 CP1 Carry-Forward Open Items](#66-cp1-carry-forward-open-items)
   - [6.7 Portal Service (`PortalService.cs`) — Service-Level Deviations](#67-portal-service-portalservicecs--service-level-deviations)
   - [6.8 Role Service (`RoleService.cs`) — Service-Level Deviations](#68-role-service-roleservicecs--service-level-deviations)
   - [6.9 Auth Service (`AuthService.cs`) — Service-Level Deviations](#69-auth-service-authservicecs--service-level-deviations)
   - [6.10 Module Service (`ModuleService.cs`) — Service-Level Deviations](#610-module-service-moduleservicecs--service-level-deviations)
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
- **Client.** `frontend/src/app/core/auth/auth.interceptor.ts` — a functional `HttpInterceptorFn` (`authInterceptor`, registered via `provideHttpClient(withInterceptors([authInterceptor]))`) — replaces the legacy *implicit* Forms Authentication cookie (managed by the browser, cleared by `PortalSecurity.SignOut()`, `PortalSecurity.vb` L77-95) with an **explicit** `Authorization: Bearer <accessToken>` header read from `AuthService.accessToken()`. On a `401`, it performs a single transparent `refresh()` → retry of the original request with the rotated token; a failed refresh triggers `logout()` and rethrows. The `/auth/login` and `/auth/refresh` endpoints are skipped (no header, no refresh) to prevent infinite refresh recursion, since `refresh()` re-enters the interceptor through `HttpClient`.

### 3.2 Password cryptography: DES → BCrypt

- **Legacy.** Symmetric encryption uses the weak **56-bit DES** algorithm via `DESCryptoServiceProvider` + `CryptoStream`, inside the `Encrypt`/`Decrypt` methods (`PortalSecurity.vb` ≈ lines 138–211). The legacy key is padded with `X` (or truncated) to **16 characters**, then split into an **8-byte key** (`Left(strKey, 8)`) and an **8-byte initialization vector** (`Right(strKey, 8)`); output is Base64-encoded.
- **Target.** Adaptive **BCrypt** password hashing via **BCrypt.Net-Next 4.0.3** in `DnnMigration.Infrastructure/Identity/PasswordHasher.cs`. Passwords are hashed, never reversibly encrypted; the DES `Encrypt`/`Decrypt` round-trip is retired entirely.

### 3.3 Authorization: `SecurityAccessLevel` / `HasNecessaryPermission` → policies + directive

- **Legacy.** Authorization is expressed through the `SecurityAccessLevel` enum and a set of `HasNecessaryPermission` overloads (`PortalSecurity.vb` ≈ lines 469, 494, 517) that `Select Case` on that level. The verbatim legacy enum values are recorded in [§4.5](#45-enum-verbatim-preservation).
- **Target.** `HasNecessaryPermission` maps to **ASP.NET Core authorization policies/handlers** on the server, and to the Angular **`has-permission` directive** for UI gating. Permission keys `VIEW`, `EDIT`, `DELETE`, and `MANAGE_SETTINGS` are honored; an **unknown permission key yields a `403`** (`ForbiddenException`).

### 3.4 Request gating: server-side Forms-Auth check → client-side functional route guard

- **Legacy.** Access to a protected administrative page was gated **server-side, per request**: ASP.NET Forms Authentication challenged unauthenticated requests (`HttpContext.Current.Request.IsAuthenticated`) and `PortalSecurity` role checks (e.g. `IsInRole` / `IsInRoles`, `PortalSecurity.vb` ≈ lines 97–136) ran before the `.aspx`/`.ascx` was served, redirecting unauthenticated users to the login page via the Forms-Auth `loginUrl`.
- **Target.** Because all rendering moves client-side (Angular SPA), this gating is reproduced as a **functional `CanActivateFn`** — `frontend/src/app/core/auth/auth.guard.ts`, exported as `authGuard` — applied via `canActivate: [authGuard]` on the `portals`, `modules`, `users`, and `roles` routes in `app.routes.ts`. It reads the JWT session through `AuthService.isAuthenticated()` (a `computed` signal); an unauthenticated user is redirected to `/auth/login` by **returning a `UrlTree`** (which both cancels the in-flight navigation and redirects in one step). This is a presentation-tier relocation of the *enforcement point*, not a relaxation of it: authoritative authorization remains **server-side** on the JWT-secured API (see [§3.1](#31-session-management-forms-authentication--stateless-jwt-bearer) and [§3.3](#33-authorization-securityaccesslevel--hasnecessarypermission--policies--directive)). The guard only gates client navigation and never grants real access on its own.

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

### 4.6 User & UserRole EF Mapping (`UserConfiguration`)

`backend/src/DnnMigration.Infrastructure/Persistence/Configurations/UserConfiguration.cs` is the single home that maps the `User` and `UserRole` POCO entities onto the unchanged `dbo.Users` and `dbo.UserRoles` tables (ADR-002). It implements `IEntityTypeConfiguration<User>` and `IEntityTypeConfiguration<UserRole>` and is auto-discovered through `ApplyConfigurationsFromAssembly`. The following decisions are behavior-preserving (Sanctioned? = N) and each is annotated with a `// MIGRATION:` comment in the source:

- **`User.FullName` is `Ignore`d.** Legacy `UserInfo.FullName` is a computed, read-only value (`FirstName & " " & LastName`) with no backing column; mapping it would fail the model build, so `builder.Ignore(u => u.FullName)`.
- **`User.Roles` is `Ignore`d.** Legacy `UserInfo.Roles As String()` is a denormalized, auto-hydrated array of role names — not a physical column. It is excluded with `builder.Ignore(u => u.Roles)`; relational role membership is modeled exclusively through the `UserRole` join entity.
- **`AffiliateID` casing remap.** The entity property is `AffiliateID` (capital `D`) but the physical `Users` column is `AffiliateId` (lowercase `d`, per the `DotNetNuke.Schema.SqlDataProvider` DDL). Mapped verbatim with `HasColumnName("AffiliateId")`.
- **Membership/profile flatten ([DEV-031](#62-deviation-index) — CP2 schema-fidelity correction).** `UserInfo` is a flattened merge of `Users` + `aspnet_Membership` + `aspnet_Users` + `aspnet_Profile` + `UserPortals`. Only **nine** properties are real `Users` columns (`UserID`, `Username`, `FirstName`, `LastName`, `IsSuperUser`, `AffiliateId`, `Email`, `DisplayName`, `UpdatePassword`). The remaining membership/profile/portal properties (`PortalID`, `Approved`, `CreatedDate`, `IsOnLine`, `LastActivityDate`, `LastLockoutDate`, `LastLoginDate`, `LastPasswordChangeDate`, `LockedOut`, `Password`, `PasswordAnswer`, `PasswordQuestion`) have **no column on `Users`**; per ADR-002 they are **`Ignore`d** (`builder.Ignore(...)`) — **not** mapped as scalar columns — so EF never queries or inserts a nonexistent `Users` column. The CLR properties remain on the `User` entity for DTO/AutoMapper projection; their values are populated from the membership/profile/`UserPortals` source tables (joins/projections, a keyless query type, or a preserved stored-procedure projection) by the repository/service layer in a later checkpoint. `Password`/`PasswordAnswer`/`PasswordQuestion` are ignored here; BCrypt hashing lives in the Identity layer (`PasswordHasher`/`AuthService`), see [§3.2](#32-password-cryptography-des--bcrypt).
- **`UserRole.Subscribed` ignored ([DEV-031](#62-deviation-index)).** `Subscribed` has no column in the 4.9 `UserRoles` baseline; per ADR-002 it is **`Ignore`d** (**not** carried as a scalar). The six real `UserRoles` columns are `UserRoleID`, `UserID`, `RoleID`, `ExpiryDate`, `IsTrialUsed`, `EffectiveDate`.
- **`UserRole` relationships.** `UserRole -> User` (`HasForeignKey(ur => ur.UserID)`) and `UserRole -> Role` (`HasForeignKey(ur => ur.RoleID)`) are configured with `WithMany()` (no inverse collection on `User`/`Role`). Both are **required** — the FK columns are non-nullable `int` — even though the CLR navigations (`User?`/`Role?`) are nullable, so `IsRequired(false)` is deliberately **not** used. `OnDelete(DeleteBehavior.NoAction)` avoids SQL-Server multiple-cascade-path warnings under the `--warnaserror` gate; the in-memory provider ignores delete behavior. `Role`'s table/key are owned by `RoleConfiguration`; EF merges configurations across the Infrastructure assembly. Key generation is left at the EF `ValueGeneratedOnAdd` convention (no `ValueGeneratedNever`), and no `HasDefaultValueSql`/`HasComputedColumnSql`/raw SQL is used, keeping the model in-memory-provider-safe.

### 4.7 Tab & TabPermission EF Mapping (`TabConfiguration`)

`backend/src/DnnMigration.Infrastructure/Persistence/Configurations/TabConfiguration.cs` is the single home that maps the `Tab` POCO entity (in DNN a "Tab" == a site Page) onto the unchanged `dbo.Tabs` table (ADR-002). It implements `IEntityTypeConfiguration<Tab>` and is auto-discovered through `ApplyConfigurationsFromAssembly`. The authoritative column set is the `dbo.Tabs` CREATE TABLE DDL in `DotNetNuke.Schema.SqlDataProvider` (PK `[TabID]` CLUSTERED, `IDENTITY(0,1)`) plus the `[IsSecure]` column added by `04.05.04.SqlDataProvider` and confirmed live by `04.09.00.SqlDataProvider` (`update Tabs set IsSecure = 1`) — **22 physical columns** total. Each decision below is behavior-preserving (Sanctioned? = N) and is annotated with a `// MIGRATION:` comment in the source:

- **`Tab.TabType` is `Ignore`d.** Legacy `TabInfo.TabType` (`TabInfo.vb` L406, `<XmlIgnore()> ReadOnly Property TabType`) is a computed, read-only enum derived from `Url` at runtime with no setter and no backing column; mapping it would fail the EF model build, so `builder.Ignore(t => t.TabType)`. This is the only `Ignore` on the entity.
- **`IsDeleted` soft-delete PRESERVED.** `[IsDeleted]` (`bit NOT NULL`) is a real column and the soft-delete flag that `TabService`/`TabRepository` filter on; it is MAPPED (never `Ignore`d), consistent with the Tab = soft-delete strategy in [§6.3](#63-per-entity-delete-strategy).
- **`ParentId` nullable self-FK as a scalar.** `[ParentId]` (`int NULL`) references `Tabs.TabID`, but the `Tab` entity exposes no `Parent`/`Children` navigation, so it is mapped as a plain nullable `int?` scalar with **no** EF self-relationship (faithful to the legacy `TabInfo` value object).
- **Three non-physical properties `Ignore`d ([DEV-031](#62-deviation-index) — CP2 schema-fidelity correction).** `HasChildren` / `AuthorizedRoles` / `AdministratorRoles` were computed / permission-derived at runtime in legacy (they appear only as stored-procedure `@parameters`, never as `Tabs` columns). Per ADR-002 they are **`Ignore`d** (`builder.Ignore(t => t.HasChildren)`, etc.) — **not** carried as mapped scalars — so EF never references a nonexistent `Tabs` column. The CLR properties remain for projection and are populated from the tab-hierarchy (`HasChildren`) and tab-permission (`AuthorizedRoles`/`AdministratorRoles`) projections by the repository/service layer in a later checkpoint.
- **`PortalID` nullability nuance.** `[PortalID]` is physically `int NULL`, but the entity models it as a non-nullable `int` (host/super tabs are handled at the service layer, not by nulling the FK). Per ADR-002 it is mapped AS-IS; `.IsRequired(false)` is **not** applied — the same precedent as the non-nullable `RoleID`/`UserID` junction columns in `PermissionConfiguration` and the `UserRole` FKs in [§4.6](#46-user--userrole-ef-mapping-userconfiguration).
- **`Tab → TabPermission` relationship.** `Tab` (principal) → `TabPermissions` (dependent) is declared `builder.HasMany(t => t.TabPermissions).WithOne().HasForeignKey(tp => tp.TabID)`. `TabPermission`'s own table/key/columns are owned by `PermissionConfiguration` (which detaches it from the `Permission` EF hierarchy via `HasBaseType((Type?)null)`); EF merges the two configurations across the Infrastructure assembly. `WithOne()` = no inverse navigation on `TabPermission` (it exposes no `Tab` navigation); `[TabID]` is the FK. `OnDelete(DeleteBehavior.NoAction)` is used because `Tab` is soft-deleted (a hard cascade is not the legacy behavior) and it also avoids a SQL-Server multiple-cascade-path warning under the `--warnaserror` gate; the in-memory provider ignores delete behavior (Gate 5). Key generation is left at the EF `ValueGeneratedOnAdd` convention (no `ValueGeneratedNever`), and no `HasDefaultValueSql`/`HasComputedColumnSql`/`HasColumnType`/raw SQL is used, keeping the model in-memory-provider-safe.

### 4.8 Module / DesktopModule / ModuleDefinition EF Mapping (`ModuleConfiguration`)

`backend/src/DnnMigration.Infrastructure/Persistence/Configurations/ModuleConfiguration.cs` is the single home that maps the `Module`, `DesktopModule` and `ModuleDefinition` POCO entities onto the unchanged `dbo.Modules`, `dbo.DesktopModules` and `dbo.ModuleDefinitions` tables (ADR-002). One `sealed` class implements `IEntityTypeConfiguration<Module>`, `IEntityTypeConfiguration<DesktopModule>` and `IEntityTypeConfiguration<ModuleDefinition>`, auto-discovered through `ApplyConfigurationsFromAssembly`. Each decision below is behavior-preserving (Sanctioned? = N) and is annotated with a `// MIGRATION:` comment in the source:

- **Denormalized `Module` members `Ignore`d ([DEV-031](#62-deviation-index) — CP2 schema-fidelity correction).** Legacy `ModuleInfo` is a fat object hydrated from a JOIN across `Modules` + `TabModules` + `ModuleControls` + `DesktopModules` + `ModuleDefinitions`. Only **eleven** properties are real `Modules` columns (`ModuleID`, `ModuleDefID`, `ModuleTitle`, `AllTabs`, `IsDeleted`, `InheritViewPermissions`, `Header`, `Footer`, `StartDate`, `EndDate`, `PortalID`). The remaining join-sourced properties have no physical column on `Modules` and per ADR-002 are **`Ignore`d** (`builder.Ignore(...)`) — **not** mapped as scalar columns — so EF never references a nonexistent `Modules` column: the `TabModules` group (`TabModuleID`, `TabID`, `PaneName`, `ModuleOrder`, `CacheTime`, `Alignment`, `Color`, `Border`, `IconFile`, `Visibility`, `ContainerSrc`, `DisplayTitle`, `DisplayPrint`, `DisplaySyndicate`); the `ModuleControls` group (`ModuleControlId`, `ControlSrc`, `ControlType`, `ControlTitle`, `HelpUrl`, `SupportsPartialRendering`); and the `DesktopModules`/`ModuleDefinitions` group (`DesktopModuleID`, `FriendlyName`, `FolderName`, `Description`, `Version`, `IsPremium`, `IsAdmin`, `BusinessControllerClass`, `ModuleName`, `SupportedFeatures`). The CLR properties remain for projection and are populated from the `TabModules`/`ModuleControls`/`DesktopModules`/`ModuleDefinitions` source tables (joins/projections or keyless query types) by the repository/service layer in a later checkpoint. This mirrors the §4.6 `User` flatten and the `PermissionConfiguration` Deviation-4 `Ignore` of display/join-only junction fields — all four are tracked under the single [DEV-031](#62-deviation-index) CP2 schema-fidelity correction.
- **`Module.IsDeleted` preserved.** `IsDeleted` is a real `bit NOT NULL` column and is mapped (**not** `Ignore`d); the module list query filters on it rather than physically deleting (soft delete, see section 6.3).
- **`Module.Visibility` `Ignore`d ([DEV-031](#62-deviation-index)).** `Visibility` (the `VisibilityState` enum: `Maximized=0`, `Minimized=1`, `None=2`, see §4.5) is a denormalized `[TabModules]` field, **not** a physical `[Modules]` column, so it is part of the `TabModules` group `Ignore`d above; the prior explicit `HasConversion<int>()` scalar mapping is **removed** with it. The enum↔int conversion is applied at the future `[TabModules]` projection site; the `VisibilityState` enum values remain preserved verbatim in the Domain.
- **`Module -> ModulePermissions` relationship.** Configured on the principal side: `HasMany(m => m.ModulePermissions).WithOne().HasForeignKey(mp => mp.ModuleID)`. `WithOne()` declares no inverse navigation (`ModulePermission` exposes none). `ModulePermission`'s table/PK are owned by `PermissionConfiguration`, which detaches it from the `Permission` EF hierarchy via `HasBaseType((Type?)null)`; EF merges the two configurations across the Infrastructure assembly. `OnDelete(DeleteBehavior.Cascade)` mirrors the legacy `ModuleController` transactional hard-delete that removes a module's permission rows with it (the in-memory provider ignores delete behavior).
- **`DesktopModule` derived members `Ignore`d ([DEV-031](#62-deviation-index)).** Eleven real `DesktopModules` columns are mapped (`DesktopModuleID`, `FriendlyName`, `Description`, `Version`, `IsPremium`, `IsAdmin`, `BusinessControllerClass`, `FolderName`, `ModuleName`, `SupportedFeatures`, `CompatibleVersions`). `IsUpgradeable`/`IsPortable`/`IsSearchable` (legacy-derived at runtime from the `SupportedFeatures` bitmask) and `Dependencies`/`Permissions` (absent from the 4.9 baseline schema) have no physical `DesktopModules` column and per ADR-002 are **`Ignore`d** (**not** carried as scalars). The CLR properties remain for projection; the three booleans are computed from `SupportedFeatures` and `Dependencies`/`Permissions` are populated by the service layer.
- **`ModuleDefinition.TempModuleID` `Ignore`d ([DEV-031](#62-deviation-index)).** Four real `ModuleDefinitions` columns are mapped (`ModuleDefID`, `FriendlyName`, `DesktopModuleID`, `DefaultCacheTime`). `DesktopModuleID` is a plain scalar FK - no `ModuleDefinition -> DesktopModule` navigation exists, so no relationship is configured. The runtime-only `TempModuleID` has no physical column and per ADR-002 is **`Ignore`d** (**not** carried as a scalar); the CLR property remains for transient runtime use.
- **Provider safety.** Integer identity PKs are left at the EF `ValueGeneratedOnAdd` convention (no `ValueGeneratedNever`); no `HasDefaultValueSql`/`HasComputedColumnSql`/`HasColumnType`/raw SQL is used, keeping the model fully compatible with the `Microsoft.EntityFrameworkCore.InMemory` provider (Gate 5).

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
| BUG-001 | `Website/admin/Tabs/ManageTabs.ascx` (+ `.ascx.vb` L298-299) | The page **Refresh Interval** field has **no lower-bound validator** — `txtRefreshInterval` carries no `RangeValidator`/`CompareValidator`, and the code-behind only persists the value `If txtRefreshInterval.Text.Length > 0 AndAlso IsNumeric(...)`. A **negative** interval is therefore accepted and stored (a meta-refresh interval that browsers ignore). | Reproduced as-is for behavioral equivalence: the Tab validators add **no** range rule for `RefreshInterval`. The DTO's `int?` typing still enforces "numeric integer"; nullability preserves the optional/unset case. Adding a `>= 0` guard would *improve* (over-constrain) legacy behavior, which the Minimal Change Clause forbids. | `CreateTabValidator.cs` / `UpdateTabValidator.cs` — see the `// MIGRATION: RefreshInterval is intentionally NOT range-validated` annotation |
| BUG-002 | `Library/Components/Security/Roles/RoleController.vb` (`UpdateUserRole`, L540-547) | The `Select Case Frequency` block has **no `Case Else`** — when the trial/billing frequency code is unmatched (e.g. an empty string), `ExpiryDate` is left at its pre-switch value (the reset `Now`/existing expiry) instead of being recomputed or cleared. | Reproduced as-is for behavioral equivalence: `RoleService.AddUserRoleAsync` keeps the `switch (frequency)` with **no `default`** branch. Adding a default would *change* the ported behavior, which the Minimal Change Clause forbids. | `RoleService.cs` — see the `// MIGRATION (PORTED BUG BUG-002):` annotation on the `switch (frequency)` |

> A defect is fixed (rather than ported) **only** if it blocks compilation or a mandated validation gate, in which case the fix is logged as a deviation in [§6.2](#62-deviation-index). BUG-001 above documents a deliberately **preserved** legacy weakness.

### 6.2 Deviation Index

The authentication/cryptography modernization (**DEV-001**) is the **only** sanctioned behavior change. All other deviations are structural/mechanical and default to **Sanctioned? = N** (behavior-preserving).

| ID | Area | Legacy | New | Rationale | Sanctioned? (Y/N) |
|---|---|---|---|---|---|
| DEV-001 | Authentication & cryptography | Forms Authentication + 56-bit DES (`PortalSecurity.vb`) | JWT Bearer (60-min access + refresh rotation) + BCrypt.Net-Next 4.0.3 | Explicit security upgrade; stateless tokens enable horizontal scaling (see [§3](#3-sanctioned-behavior-change--authentication--cryptography)) | **Y** |
| DEV-002 | Data access | ADO.NET `SqlHelper` + `CBO` reflection hydration + 255 stored procedures | EF Core 8 entity materialization (`AsNoTracking`/`SaveChangesAsync`; `FromSqlRaw` fallback) | Re-platforming; observable behavior unchanged (see [§4.1](#41-data-access-adonet--sqlhelper--cbo--ef-core-8)) | N |
| DEV-003 | Null handling | `Null.*` sentinel constants | C# nullable types / `default` | Idiomatic representation of "unset"; meaning preserved (see [§4.3](#43-null-sentinels--c-nullable-types)) | N |
| DEV-004 | Presentation | ASP.NET Web Forms (postback/ViewState) | REST `/api/v1/...` + Angular 19 SPA | Tech-stack migration; UI functional parity preserved (see [§5](#5-presentation-re-platforming)) | N |
| DEV-005 | Error handling | `Website/ErrorPage.aspx.vb` | `ExceptionHandlingMiddleware` (RFC 7807) | Stateless API error contract; same error semantics (see [§5](#5-presentation-re-platforming)) | N |
| DEV-006 | Secret read-projection | `PortalInfo.ProcessorPassword` surfaced through the legacy SiteSettings admin UI | Excluded from **read** projections (`PortalDto`, `portal.model.ts`); retained **write-only** on `CreatePortalDto`/`UpdatePortalDto` + the Angular write requests | The new REST/BFF read contract (DEV-004) never serializes a payment-processor credential; the **domain field and write path are preserved**, so no domain behavior changes. Security-by-design property of the new contract (resolves CP1 CRITICAL `ProcessorPassword` exposure). | N |
| DEV-007 | Identity ownership | Portal `GUID` (`uniqueidentifier`, DB `DEFAULT (newid())`) | Removed from the client-writable `CreatePortalDto`/`UpdatePortalDto` surface; `PortalProfile` `.Ignore()`s `GUID` on create+update; server/DB retains/generates it | Reproduces legacy ownership: the column was DB-generated and not a client-set field. Removing it from the writable surface **preserves** that semantic and closes a CP1 integrity gap. | N |
| DEV-008 | JSON field-casing contract | Web Forms had no JSON wire contract | `System.Text.Json` default camelCase serializes the verbatim-preserved PascalCase IDs (`UserID`→`userID`, `PortalID`→`portalID`, `AffiliateID`→`affiliateID`); the Angular `user.model.ts` is aligned to those exact wire names | Mechanical serialization-contract alignment; the C# domain casing is preserved verbatim (per the public-contract rule), and the SPA model is matched to the actual wire shape. No domain behavior change. | N |
| DEV-009 | User aggregate mapping | `UserInfo` flattened across `Users` + `aspnet_Membership`/`aspnet_Profile`/`UserPortals`; computed `FullName`; `Roles As String()` array; `AffiliateId` column casing | `UserConfiguration` maps the 9 real `Users` columns, carries membership/profile fields as scalars, `Ignore`s `FullName`/`Roles`, remaps `AffiliateID`->`AffiliateId`; `UserRole` join carries `Subscribed` and wires required `User`/`Role` FKs | Schema mapped unchanged (ADR-002); data shape and meaning preserved (see [§4.6](#46-user--userrole-ef-mapping-userconfiguration)) | N |
| DEV-010 | User create — role auto-assignment | `CreateUser` auto-assigned every non-superuser to all `AutoAssignment` portal roles (`UserController.vb:L166-180`) | `UserService.CreateAsync` **reproduces** the auto-assignment: for a newly-created non-superuser it loads the portal roles via `IRoleRepository.GetByPortalAsync` and INSERTs a `UserRole` with NULL effective/expiry dates for each `AutoAssignment` role (see [DEV-033](#62-deviation-index)) | **Parity restored (CP2 review M4).** Faithfully matches the legacy direct-add (`Null.NullDate`/`Null.NullDate`) control flow with no `Try/Catch` — this is no longer a divergence. Implemented via a cross-aggregate `IRoleRepository` dependency (not `IRoleService`), preserving Clean-Architecture dependency direction and DI acyclicity (`RoleService` depends on `IUserRepository`, never `IUserService`). | N |
| DEV-011 | User delete — administrator guard | `DeleteUser` set `CanDelete = deleteAdmin` (False for the single-arg delete) when `UserID == Portal.AdministratorId`, **silently** refusing (`UserController.vb:L209-216`) | `UserService.DeleteAsync` loads the portal and **throws** `InvalidOperationException` | The refusal-to-delete-the-administrator semantic is preserved; only the *signaling* changes from a silent `False` to an exception surfaced as RFC 7807 by `ExceptionHandlingMiddleware` (DEV-005), consistent with the Portal last-portal and Tab child guards. | N |
| DEV-012 | User delete — side effects | `DeleteUser` cascaded Folder/Module/Tab permission cleanup, logged an event, sent a Mail notification, and cleared portal/user caches (`UserController.vb:L221-250`) | `UserService.DeleteAsync` performs the **hard delete** (see DEV-039) via the repository only; the cascade, mail, event log, and cache clear are **omitted in Phase 1** | Permission-cascade, mail, event-log, and caching are out-of-scope cross-cutting subsystems for Phase 1 (AAP §0.2.2); the core delete is preserved. | N |
| DEV-013 | User missing on delete | `DeleteUser` was wrapped in `Try/Catch` → returned `CanDelete = False` on any error, including a missing user | `UserService.DeleteAsync` treats a missing user as an idempotent no-op (returns without error) | Idempotent DELETE is the REST norm; a missing user is indistinguishable from an already-deleted one, matching the legacy "could not delete → False" outcome without surfacing an error. | N |
| DEV-030 | Tab & TabPermission EF mapping | `TabInfo` exposed a computed `TabType`, a `ParentId` self-FK, runtime-computed `HasChildren`/`AuthorizedRoles`/`AdministratorRoles`, and a `TabPermissionCollection` | `TabConfiguration` maps the 22 physical `Tabs` columns, `Ignore`s the computed `TabType`, `Ignore`s the three non-physical properties (`HasChildren`/`AuthorizedRoles`/`AdministratorRoles` — see [DEV-031](#62-deviation-index)), maps `ParentId` as a nullable scalar (no navigation), preserves the `IsDeleted` soft-delete flag, and declares the principal-side `Tab → TabPermission` relationship (`HasForeignKey(tp => tp.TabID)`, `OnDelete(NoAction)`) | Schema mapped unchanged (ADR-002); data shape and meaning preserved (see [§4.7](#47-tab--tabpermission-ef-mapping-tabconfiguration)) | N |
| DEV-031 | EF schema fidelity — computed/join/denormalized properties `Ignore`d (CP2 review C1–C4) | The CP2 `UserConfiguration`, `PermissionConfiguration`, `ModuleConfiguration` and `TabConfiguration` initially **mapped** non-physical properties (flattened membership/profile/`UserPortals` fields and `UserRole.Subscribed`; permission display/join-only fields `PortalID`/`FolderPath`/`RoleName`/`Username`/`DisplayName`; denormalized `Modules` fields sourced from `TabModules`/`ModuleControls`/`DesktopModules`/`ModuleDefinitions` incl. `Visibility`, the `DesktopModule` derived flags `IsUpgradeable`/`IsPortable`/`IsSearchable` + `Dependencies`/`Permissions`, and `ModuleDefinition.TempModuleID`; and `Tab.HasChildren`/`AuthorizedRoles`/`AdministratorRoles`) as scalar table columns | All such non-physical members are now `builder.Ignore(...)`d so each table mapping is limited to its real physical columns (Users=9, UserRoles=6, permission junctions=ID+parentFK+`PermissionID`+`RoleID`+`AllowAccess`+`UserID`, Modules=11, DesktopModules=11, ModuleDefinitions=4, Tabs=22). The CLR properties remain on the entities for DTO/AutoMapper projection and are populated from the related/source tables (joins, projections, keyless query types, or preserved stored-procedure projections) by the repository/service layer in a later checkpoint | Mapping nonexistent columns violated ADR-002 and would fail every query/insert against SQL Server (the prior scalar mapping was an InMemory-only convenience). `Ignore()` affects only the EF model build, never the CLR contract, so the preserved public domain surface is unchanged; behavior-preserving (see [§4.6](#46-user--userrole-ef-mapping-userconfiguration)/[§4.7](#47-tab--tabpermission-ef-mapping-tabconfiguration)/[§4.8](#48-module--desktopmodule--moduledefinition-ef-mapping-moduleconfiguration)) | N |

| DEV-032 | Refresh-token contract — signed JWT (CP2 review C5/C6) | `JwtService.GenerateRefreshToken()` returned an **opaque** cryptographically-random Base64 string, while `JwtService.ValidateToken()` only validates **signed JWTs**. `AuthService.RefreshAsync` therefore passed an opaque token to `ValidateToken`, which **always failed** — `/api/auth/refresh` and the frontend 401-recovery flow were permanently broken | `GenerateRefreshToken(User user)` now issues a **signed JWT** (same HMAC-SHA256 key/issuer/audience as the access token) carrying `sub` + `ClaimTypes.NameIdentifier` (the user id), a unique `jti`, and a `token_use=refresh` claim, with a `RefreshTokenExpirationDays` lifetime; `GenerateAccessToken` now stamps `token_use=access`; `AuthService.RefreshAsync` validates the token via `ValidateToken` **and** asserts `token_use==refresh` (rejecting an access token replayed at the refresh endpoint — token-type confusion) before resolving the subject and rotating a fresh access+refresh pair | Part of the single sanctioned auth change (DEV-001 / [§3.1](#31-session-management-forms-authentication--stateless-jwt-bearer)); makes the AAP-mandated **stateless** refresh rotation actually function. Validity is established cryptographically because Phase 1 retains **no** server-side refresh-token store (AAP §0.6.2). Forms Auth had no refresh concept, so there is no legacy behavior to diverge from | N |
| DEV-035 | JWT signing-key fail-fast validation (CP2 review M1) | `JwtService` built `SymmetricSecurityKey` from `_settings.Key` with no length enforcement; `JwtSettings` only **documented** the ">= 32 characters" rule and base `appsettings.json` ships an empty placeholder, so a missing/short key failed at the first login with an opaque `IDX10653` | `JwtService`'s constructor now **fail-fasts**: a null/empty/whitespace key or a key shorter than 32 characters (256 bits) throws a clear `InvalidOperationException` naming `Jwt:Key`/`Jwt__Key`. (The composition root `Program.cs` will additionally register `AddOptions<JwtSettings>().Validate(...)` when it enters scope; this constructor guard is the authoritative enforcement that does not depend on the DI wiring) | Configuration hardening; surfaces a deployment misconfiguration immediately and clearly instead of as an opaque crypto failure. Behavior-preserving for any correctly-configured deployment (the Development/test/compose settings all supply a >= 32-char key) | N |
| DEV-033 | User create — auto-assignment implemented (CP2 review M4) | `UserService.CreateAsync` had **omitted** the legacy auto-assignment of a new non-superuser to `AutoAssignment` portal roles (the prior DEV-010 deviation), leaving net membership divergent from DNN with no replacement orchestration in CP2 | `CreateAsync` now injects `IRoleRepository` and, after a successful add, for a non-superuser loads `GetByPortalAsync(PortalID)` and calls the **insert-only** `AddUserRoleAsync(new UserRole { UserID, RoleID, EffectiveDate = null, ExpiryDate = null })` for each role whose `AutoAssignment` is true — a **direct add with NULL dates**, NOT the expiry-computing `UpdateUserRole`/`RoleService.AddUserRoleAsync` path; superusers are skipped and no `Try/Catch` wraps the loop, matching `UserController.vb:L166-180` verbatim | Restores behavioral parity (closes DEV-010). The cross-aggregate dependency is the Domain `IRoleRepository` interface (not `IRoleService`), so Clean-Architecture dependency direction and DI acyclicity hold. Verified by an ad-hoc Moq test (auto-assignment hits only `AutoAssignment` roles with NULL dates; superuser path makes zero membership calls) | N |
| DEV-034 | User-role assignment — add-vs-update split (CP2 review M2/M3) | `RoleService.AddUserRoleAsync` routed an **existing** assignment through `IRoleRepository.AddUserRoleAsync` (the insert path), even though legacy `RoleController.UpdateUserRole` took an `UpdateUserRole` branch for `UserRoleId <> -1`; `IRoleRepository` exposed only an add method, risking a duplicate join row or reliance on undocumented upsert behavior | `IRoleRepository` now declares an explicit **`UpdateUserRoleAsync`** alongside the **insert-only** `AddUserRoleAsync` (both XML-documented). `RoleService.AddUserRoleAsync` routes an **existing** assignment to `UpdateUserRoleAsync` (retaining its loaded `EffectiveDate`/`IsTrialUsed`, recomputing only `ExpiryDate`) and a **new** assignment to `AddUserRoleAsync`, mirroring the legacy add-vs-update split | Restores the legacy update semantics for existing assignments and removes the duplicate-row/undocumented-upsert risk. The EF Core implementation of `UpdateUserRoleAsync` is delivered in a later checkpoint (no repository implementations exist yet); declaring the contract now lets the Application layer route correctly. Verified by an ad-hoc Moq test (existing → `UpdateUserRoleAsync`; new → `AddUserRoleAsync`) | N |
| DEV-036 | IdentityModel pin bump 7.1.2 → 7.7.0 (CP2 review M13) | `DnnMigration.Infrastructure.csproj` explicitly pinned `System.IdentityModel.Tokens.Jwt` and `Microsoft.IdentityModel.Tokens` to **7.1.2**, which `dotnet list package` reports as **Legacy** (and the transitive `Microsoft.IdentityModel.Protocols.OpenIdConnect` 7.1.2 as `Legacy,CriticalBugs`); `JwtService` depends on these APIs | Both explicit pins raised to **7.7.0** — the final maintained release of the SAME major (7.x) as the AAP-pinned `Microsoft.AspNetCore.Authentication.JwtBearer` 8.0.11. This moves off the specifically-flagged 7.1.2 patch, carries the 7.x security/bug fixes, and unifies the `Microsoft.IdentityModel.*` token family (JsonWebTokens/Logging/Abstractions/Tokens) **up** to 7.7.0 with no NU1605/NU1608 and no cross-major ABI risk; the 192 unit tests still pass | Maintenance/security-posture improvement within the AAP envelope. The residual major-line **Legacy** metadata flag persists on all of IdentityModel 7.x (Microsoft steers to 8.x); clearing it fully requires IdentityModel 8.x, which is a **cross-major mismatch** with the AAP-pinned JwtBearer 8.0.11 (risks a runtime `MissingMethodException`) and is deferred to a CP3/CP4 cleanup tied to a future ASP.NET Core 9 upgrade — exactly the carry-forward the CP2 review anticipated. The AAP does not list these two packages in §0.5.1, so the bump does not violate the frozen inventory | N |
| DEV-037 | API error contract — bare responses → RFC 7807 ProblemDetails (CP2 review M5–M9) | The CP2 resource/auth controllers returned **bare** `NotFound()` / `Unauthorized()` (no body) for application-level not-found and authentication failures: `PortalsController` get-by-id; `UsersController` get-by-username / get-by-email / get-by-id; `RolesController` get-by-id; `ModulesController` get-by-id; `AuthController` login + me. These bare results bypassed the AAP RFC 7807 error contract and handed the Angular SPA no structured error body | All five controllers now return `Problem(statusCode: StatusCodes.Status404NotFound` / `Status401Unauthorized, title, detail)`, so ASP.NET Core emits `application/problem+json` with the RFC 7807 `{ type, title, status, detail }` shape; this matches the existing `Problem(statusCode: 400, …)` validation-failure convention already present in those same controllers and the `ProblemDetails` model the frontend `ApiService` already parses (incl. `errors`) | Aligns the controllers with the AAP API response standard (RFC 7807 Problem Details, [§5](#5-presentation-re-platforming)) and the future `ExceptionHandlingMiddleware` (DEV-005), which will translate typed exceptions into the identical envelope. Behavior-preserving: the HTTP **status codes are unchanged** (404 stays 404, 401 stays 401); only the response **body** gains the structured Problem Details envelope. The logout XML-doc accuracy fix (CP2 review m1) is tracked under DEV-028 | N |
| DEV-038 | Frontend CP2 core remediation — API wire-contract alignment, auth refresh-failure teardown, and the shared form-control template (CP2 review C7/M10/M11) | (1) `features/user/models/user.model.ts` `CreateUserDto`/`UpdateUserDto` diverged from the backend wire contracts — missing `portalID`/`isSuperUser`/`approved` and the update body `userID`, using `authorize` instead of `approved`, and carrying form-only fields (`confirmPassword`, `randomPassword`, `question`, `answer`, `notify`, `affiliateId`) the API rejects or ignores. (2) `auth.interceptor.ts` called `authService.logout()` on refresh failure; `logout()` POSTs `/api/auth/logout`, which is intentionally NOT on the interceptor skip list, so its own 401 could re-enter the refresh handler — a refresh/logout recursion loop. (3) `form-controls.component.ts` declared a `templateUrl` whose `.html` file was missing, so the shared label/input/error wrapper could not compile or render its accessible UI | (1) `CreateUserDto`/`UpdateUserDto` now match the backend `DnnMigration.Application.DTOs.User.CreateUserDto`/`UpdateUserDto` 1:1 (including the body `userID` the `UsersController` route-id check requires, and DEV-008 acronym casing `portalID`/`userID`); the legacy form-only inputs relocate to a new `CreateUserForm` UI superset that is mapped and stripped down to the wire DTO before submission. (2) a local-only `AuthService.clearSession()` (clears the token/user signals plus their localStorage mirror and redirects to login, with NO server call) is added, and the interceptor calls it on refresh failure — breaking the loop while a normal user-initiated `logout()` still notifies the server. (3) `form-controls.component.html` is created with Angular built-in control flow (`@let`/`@if`/`@for`; no `CommonModule`) rendering the associated `<label for>`, the `[formControl]` input with `appValidationHighlight` + `[appAutofocus]` + `aria-invalid` + `aria-describedby`, and an `aria-live` error region that merges client-side validator messages and RFC 7807 `ProblemDetails.errors`; a spec covers label association, server errors, and ARIA wiring | Behavior-preserving frontend remediation. (1) Aligns the SPA to the actual REST contract so user create/update no longer 400 or send ignored data, preserving the public contract and DEV-008 casing while retaining legacy form parity on `CreateUserForm`. (2) Completes the stateless-JWT auth flow (DEV-001/DEV-032 family) without changing the contract — a normal logout still notifies the server best-effort. (3) Restores the intended accessible field UI; no legacy semantics change. The `api.service.spec.ts` envelope fixtures were also corrected to `portalID` (DEV-008). Verified by `ng test` (70 specs SUCCESS, including the new form-control specs) and a targeted strict `tsc` of the interceptor and the user model | N |
| DEV-039 | User delete — HARD delete (not soft) | AAP §0.3.3/§6.3 grouped `User` with Module/Tab under a soft-delete strategy | `UserRepository.DeleteAsync` performs a **HARD delete** (`_context.Users.Remove` + `SaveChangesAsync`); a missing `userId` is an idempotent no-op | The DNN 4.9 `Users` table has **no `IsDeleted` column** (verified against the install schema; `UserConfiguration` maps none), so a soft delete is impossible without a schema change, which ADR-002 forbids. The legacy `UserController.DeleteUser` likewise hard-deleted via the ASP.NET membership provider (`UserController.vb:L231`, `memberProvider.DeleteUser`). Behavior-preserving w.r.t. the legacy hard delete; the `// MIGRATION:` annotation in `UserRepository.DeleteAsync` is the durable in-code counterpart. | N |

> Add new deviations with the next sequential `DEV-NNN` ID. Anything that is **not** DEV-001 must be behavior-preserving (Sanctioned? = N); if a change would alter observable behavior, it must be justified as unblocking a compilation or validation gate and called out explicitly. Dependency-vulnerability **risk-acceptances** (which change no behavior) are tracked separately in [§6.5](#65-accepted-dependency-vulnerability-risks-aap-pinned).

### 6.3 Per-Entity Delete Strategy

The delete strategy intentionally differs per aggregate, mirroring the legacy semantics (AAP §0.3.3, Factory/Strategy pattern). This is **behavior-preserving**: each entity keeps the delete semantics it had in DNN.

| Entity | Delete strategy | Mechanism |
|---|---|---|
| Portal | **Hard delete** | Transactional cascade |
| Role | **Hard delete** | Transactional cascade |
| Module | **Soft delete** | `IsDeleted` flag; list queries filter it out |
| User | **Hard delete** | No `IsDeleted` column exists on the DNN 4.9 `Users` table (ADR-002 forbids adding one); physical row removal via `UserRepository.DeleteAsync` — see DEV-039 |
| Tab | **Soft delete** | `IsDeleted` flag; list queries filter it out |

---

### 6.4 CP1 Foundation-Layer Remediation & Risk Index

This subsection indexes **every** deviation/correction applied during the CP1 foundation-layer code-review remediation, cross-referenced to the affected file(s) and the originating review finding ID. Each `// MIGRATION:` annotation in the cited file is the durable in-code counterpart. Unless a row maps to a `DEV-NNN` (a recorded contract/security decision in [§6.2](#62-deviation-index)) or `BUG-001` ([§6.1](#61-ported-bugs)), the change is a **behavior-preserving schema/contract correction** that brings the initial CP1 implementation into compliance with ADR-002 (schema fidelity), the public-contract rule, or legacy UI validation parity — it does **not** alter legacy domain behavior.

**Schema fidelity & nullability (ADR-002; behavior-preserving; relate to [DEV-003](#62-deviation-index)):**

| Finding | File(s) | Correction | Disposition |
|---|---|---|---|
| C2 / C8 | `Domain/Entities/Portal.cs`, `Infrastructure/.../Configurations/PortalConfiguration.cs` | The 10 physically-nullable `[Portals]` columns (`ExpiryDate`; and `AdministratorId`, `AdministratorRoleId`, `RegisteredRoleId`, `SiteLogHistory`, `HomeTabId`, `LoginTabId`, `UserTabId`, `AdminTabId`, `SplashTabId`) are modeled as `DateTime?` / `int?` so EF materialization preserves DB-null (e.g., `AdministratorId` null must not coerce to `0`, which would falsely denote the host superuser) | ADR-002 schema fidelity |
| C7 | `PortalConfiguration.cs` | The 7 non-physical aggregate/runtime properties (`Email`, `SuperTabId`, `Users`, `Pages`, `AdministratorRoleName`, `RegisteredRoleName`, `Version`) are `.Ignore()`d — they are not `[Portals]` columns and were previously mapped, which would raise invalid-column errors | ADR-002 schema fidelity |
| M16 | `PortalConfiguration.cs` | Provider-neutral `HasMaxLength(...)`/`IsRequired(...)` configured for physical columns (e.g., `PortalName` 128, `LogoFile` 50, `Description` 500, `HomeDirectory` 100) — InMemory-safe (no `HasColumnType`/`HasDefaultValueSql`) | ADR-002 schema fidelity |
| C3 / C9 | `Domain/Entities/Role.cs`, `Configurations/RoleConfiguration.cs` | The 5 physically-nullable `[Roles]` columns (`ServiceFee`, `TrialFee` money; `TrialPeriod`, `BillingPeriod`, `RoleGroupID` int) are modeled `float?`/`int?` so legacy "no value" semantics survive materialization | ADR-002 schema fidelity |
| C6 | `Application/Mapping/RoleProfile.cs` | Removed the silent `RoleGroupID int? -> int` null-to-`0` coercion by making `Role.RoleGroupID` nullable and keeping DTO/profile nullability aligned (a `0` role-group is no longer fabricated from a DB null) | ADR-002 / mapping semantics |
| M17 | `RoleConfiguration.cs` | Provider-neutral `HasMaxLength(...)`/`IsRequired(...)` for `[Roles]` and `[RoleGroups]` (e.g., `RoleName` 50, `Description` 1000, `BillingFrequency` char(1), `RoleGroupName` 50) | ADR-002 schema fidelity |

**API / contract surfaces:**

| Finding | File(s) | Correction | Disposition |
|---|---|---|---|
| C4 | `Application/DTOs/Portal/PortalDto.cs` | `ProcessorPassword` removed from the read DTO | [DEV-006](#62-deviation-index) |
| C12 | `frontend/.../features/portal/models/portal.model.ts` | `processorPassword` removed from the read interface; `guid` removed from the write requests (kept `processorPassword` write-only) | [DEV-006](#62-deviation-index) / [DEV-007](#62-deviation-index) |
| M5 / M6 | `Application/DTOs/Portal/CreatePortalDto.cs`, `UpdatePortalDto.cs` | Client-writable `GUID` removed | [DEV-007](#62-deviation-index) |
| M15 | `Application/Mapping/PortalProfile.cs` | `.ForMember(d => d.GUID, o => o.Ignore())` on the create and update maps; server retains/generates `GUID` | [DEV-007](#62-deviation-index) |
| C5 / C10 | `Application/DTOs/User/UserDto.cs` (unchanged), `frontend/.../core/models/user.model.ts` | The SPA model is aligned to the actual camelCase wire names (`userID`/`portalID`/`affiliateID`); the C# DTO keeps verbatim PascalCase | [DEV-008](#62-deviation-index) |

**Validator UI parity (M7-M14; behavior-preserving exact reproduction of legacy validators):**

| Finding | File(s) | Correction | Disposition |
|---|---|---|---|
| M11 / M12 | `Application/Validators/CreateRoleValidator.cs`, `UpdateRoleValidator.cs` | `BillingPeriod` and `TrialPeriod` corrected to `GreaterThan(0)` to match the legacy `EditRoles.ascx` `Operator="GreaterThan"` (fees stay `>= 0`, matching `Operator="GreaterThanEqual"`); messages reproduce the `EditRoles.ascx.resx` displayed text verbatim. There is **no** "free-trial relaxation" — an earlier comment claiming one was inaccurate and has been removed | Legacy validation parity |
| M13 / M14 | `Application/Validators/CreateTabValidator.cs`, `UpdateTabValidator.cs` | Page-name message corrected to `Page Name Is Required` (the `ManageTabs.ascx.resx` `valTabName.ErrorMessage` overrides the markup's inline `Tab Name Is Required`); the non-legacy `RefreshInterval >= 0` rule was removed (see [BUG-001](#61-ported-bugs)) | Legacy validation parity |
| M9 / M10 | `Application/Validators/CreateUserValidator.cs`, `UpdateUserValidator.cs` | Required/`MaxLength` rules verified against `UserInfo.vb` attributes and the schema (`DisplayName` 128, `Email` 256, `FirstName`/`LastName` 50, `Username` required with no length attribute); `Display Name Is Required.` casing aligned to the legacy convention | Legacy validation parity |
| M7 / M8 | `Application/Validators/CreatePortalValidator.cs`, `UpdatePortalValidator.cs` | `Portal Name Is Required.` is a verbatim match to `Signup.ascx.resx` `valPortalName.ErrorMessage`; physical-column lengths are enforced at the EF layer (`HasMaxLength`) rather than re-declared at the validation tier, because the legacy SiteSettings/Signup tier used only a `RequiredFieldValidator` on the name | Legacy validation parity |

**Security & configuration:**

| Finding | File(s) | Correction | Disposition |
|---|---|---|---|
| C1 | `Api/appsettings.json` | Removed the hardcoded SQL Server password and JWT signing key from base config; bound from environment (`ConnectionStrings__Default`, `Jwt__Key`) with empty/placeholder defaults | Security (secrets management) |
| M4 | `Api/appsettings.Development.json` | Added a development `Jwt` section (`Issuer`/`Audience` = `DnnMigration`, key `>= 32` chars, access 60 min, refresh 7 days) and a `Cors` section (`http://localhost:4200` only) | Config completeness |

**Frontend Angular build/integration:**

| Finding | File(s) | Correction | Disposition |
|---|---|---|---|
| C11 | `frontend/.../shared/components/confirmation-dialog/confirmation-dialog.component.html` (created) | Created the missing template the component's `templateUrl` referenced (build break); accessible dialog markup (`role="dialog"`, `aria-modal`, `aria-labelledby`/`aria-describedby`, `#dialog`, keydown/backdrop/confirm/cancel, rendered under `@if (open())`) | Build / accessibility |
| M18 | `frontend/.../features/module/components/module-settings/module-settings.component.html` | The two `*appHasPermission` references are gated behind `// MIGRATION:` comments — the `has-permission` directive is a CP2 deliverable (see [§6.6](#66-cp1-carry-forward-open-items)); strict-template compilation no longer fails | Template integration |
| m7 | `module-settings.component.html` | The empty permissions grid is hidden behind `@if (permissions().length > 0)` until the permission API/model lands (CP2) | UI completeness |
| m1-m6, m8-m11 | `loading-spinner`, `form-controls`, `sidebar`, `portal-form`, `module-settings`, `role-list`, `role-form`, `login` SCSS; `header.scss` | Hardcoded colors replaced with app-owned `var(--color-*)` tokens (derived shades via `color-mix`); `header` and `sidebar` given responsive breakpoints; the non-existent `--color-on-primary` reference corrected to `--color-primary-contrast` | UI token consistency / responsive |

**Dependency closure:**

| Finding | File(s) | Correction | Disposition |
|---|---|---|---|
| M2 / M3 | `frontend/package.json`, `frontend/package-lock.json` | `@angular/cdk@^19.0.0` was declared but uninstalled (`npm ls` reported `UNMET`); installed and synchronized into the lockfile (`npm ls --depth=0` clean). npm re-alphabetized the dependency blocks (all version pins unchanged) | Dependency closure |

### 6.5 Accepted Dependency-Vulnerability Risks (AAP-Pinned)

The AAP freezes the dependency versions (AAP §0.5.1) and directs that "mandated versions are honored exactly regardless of newer releases" (AAP §0.7.2). Where a published advisory's only fix requires violating a frozen pin (a major-version bump), the pin is honored and the residual risk is **accepted and mitigated** here rather than silently upgraded. These are **risk-acceptances that change no behavior** (no `DEV-NNN` is assigned).

| Risk | Package (pin) | Severity | Why no in-range fix | Mitigation |
|---|---|---|---|---|
| Angular production advisories (8 total: 7 high + 1 moderate) — `@angular/common` (DoS via OOM in `DatePipe`; weak 32-bit cache-key hashing in `HttpTransferCache`), `@angular/compiler` (two-way property-binding sanitization bypass), `@angular/core` (client-hydration DOM clobbering and response-cache poisoning) | `@angular/*` pinned `^19.0.0` (resolves 19.x) | High/Moderate | Every `npm audit --omit=dev` fix points to a **major** bump (Angular 20.x/21.x); no patched release exists within `^19.0.0` | This SPA uses **no SSR/hydration** (the hydration DOM-clobbering and response-cache-poisoning vectors are not exercised); `DatePipe` format strings are author-controlled, not attacker-supplied; Angular's built-in template sanitization is retained; a strict CSP is enforced by nginx ([`docker/nginx.conf`](./docker/nginx.conf)). Revisit when the AAP permits an Angular major upgrade. |
| `AutoMapper` transitive advisory `GHSA-rvv3-g6hj-g44x` (re-affirmed by CP2 review M12) | `AutoMapper` 12.0.1 (pinned transitively by `AutoMapper.Extensions.Microsoft.DependencyInjection` 12.0.1, which constrains `AutoMapper [12.0.1, 13.0.0)`) | High | The fix ships in `AutoMapper` 13.x+; reaching it requires abandoning the AAP-pinned 12.0.1 DI package — a breaking change (the DI extension folded into AutoMapper core in 13.x, removing `AddAutoMapper(Assembly)`) that also shifts toward the commercial-license line, and that is outside the frozen inventory (AAP §0.5.1, honored verbatim per §0.7.2) | AutoMapper is used only for **static, compile-time** entity↔DTO profiles over trusted internal types; there is no dynamic or attacker-controlled mapping configuration, so the advisory vector is not reachable by the application surface. The advisory is **transitive** and the .NET SDK default `NuGetAuditMode` is `direct`, so the zero-warning `warnaserror` Gate 1 build does **not** emit `NU1903` (verified); the advisory surfaces only under a `dotnet list package` vulnerable + include-transitive query. A matching `// MIGRATION (M12)` risk-acceptance note is recorded in `DnnMigration.Application.csproj`. Revisit when the AAP permits the AutoMapper 13.x line. |

### 6.6 CP1 Carry-Forward Open Items

These are not deviations but **tracked obligations** surfaced at CP1 that later checkpoints must honor:

- **`User.Roles` EF mapping (CP2).** `User.Roles` is intentionally `string[]?` — a preserved legacy convenience contract, **not** a physical `[Users]` column. The CP2 User EF configuration **must** `.Ignore()` it so EF does not attempt to map a non-existent column.
- **`has-permission` directive (CP2).** The structural directive backing `*appHasPermission` requires the CP2 authentication/authorization infrastructure (`core/auth/*`). Until it exists, the `module-settings.component.html` references are gated with `// MIGRATION:` comments (see M18 in [§6.4](#64-cp1-foundation-layer-remediation--risk-index)); the directive and its re-enablement are CP2 work.
- **JWT key tri-point (later checkpoints).** The `>= 32`-character JWT signing-key requirement is now **enforced fail-fast** in `JwtService`'s constructor (see [DEV-035](#62-deviation-index)), not merely documented in `JwtSettings.cs`. It must still be satisfied consistently in **all three** configuration sites so the guard never trips a correctly-deployed environment: `appsettings`, the docker-compose `Jwt__Key` environment variable, and the integration-test settings.

---

### 6.7 Portal Service (`PortalService.cs`) — Service-Level Deviations

`DnnMigration.Application/Services/PortalService.cs` ports the business logic of the legacy `Library/Components/Portal/PortalController.vb` (1632 lines), decoupled from data access via `IPortalRepository` (EF Core), with entity↔DTO translation by AutoMapper and inbound validation by FluentValidation. All items below are **behavior-preserving** with respect to the in-scope database semantics; each is annotated with a `// MIGRATION:` comment in the service. None alters in-scope observable behavior beyond surfacing previously-implicit conditions through the standard RFC 7807 error contract (cf. [DEV-005](#62-deviation-index)), so all default to **Sanctioned? = N**.

| ID | Method | Legacy behavior | New behavior | Rationale |
|---|---|---|---|---|
| DEV-014 | `CreateAsync` | `CreatePortal` (L326–377) seeded `ExpiryDate`, `HostFee`, `HostSpace`, `PageQuota`, `UserQuota`, `SiteLogHistory`, and `Currency` from `Common.Globals.HostSettings(...)` | Host-derived defaults omitted; the values are supplied by the inbound `CreatePortalDto` | `DotNetNuke.Common.Globals` / the Host namespace are OUT OF SCOPE (AAP §0.2.2) |
| DEV-015 | `UpdateAsync` | `UpdatePortalInfo` (L1524–1575) called `DataCache.ClearPortalCache(PortalId, True)` after the data-provider update | Cache-clear omitted | Cache Provider is OUT OF SCOPE (AAP §0.2.2); the stateless API holds no portal cache to invalidate |
| DEV-016 | `DeleteAsync` | `DeletePortal` (L162–204) deleted custom `.resx` files, child-portal folders, the upload directory, and `HomeDirectoryMapPath` before removing DB references | Filesystem cleanup omitted; DB removal only (HARD-delete, transactional cascade in `IPortalRepository.DeleteAsync`) | FileSystem subsystem is OUT OF SCOPE (AAP §0.2.2); see [§6.3](#63-per-entity-delete-strategy) (Portal = hard delete) |
| DEV-017 | `DeleteAsync` | When `GetPortalCount() <= 1`, set `strMessage = "LastPortal"` and **silently skipped** deletion (returned the message string to the caller) | Throws `InvalidOperationException("Cannot delete the last remaining portal.")`, surfaced as an RFC 7807 response by `ExceptionHandlingMiddleware` | Same guard (the last portal cannot be deleted); the delivery channel changes from a return-string to the stateless API error contract (cf. DEV-005) |
| DEV-018 | `UpdateAsync` | `UpdatePortalInfo` passed the supplied values straight to the data provider — a **no-op** when the row was absent | Loads the entity first; a missing portal throws `KeyNotFoundException` → `404` RFC 7807 | The DTO + repository pattern requires loading the tracked entity to map onto; not-found is surfaced per the API error contract (cf. DEV-005) |

**Faithfully preserved quirk (not a deviation).** `GetByNameAsync` reproduces the legacy `GetPortalsByName` (L262–271) **-1 paging sentinel** verbatim: a `pageIndex` of `-1` is normalized to `pageIndex = 0`, `pageSize = int.MaxValue` (return all matching records on a single page) **before** the repository call. This is behavioral equivalence; it is annotated `// MIGRATION:` only to flag the non-obvious sentinel.

**Not added (behavioral equivalence).** `CreateAsync` intentionally adds **no** duplicate-name or home-directory-collision check, because legacy `CreatePortal` performed none; adding one would diverge from the ported behavior.

### 6.8 Role Service (`RoleService.cs`) — Service-Level Deviations

`DnnMigration.Application/Services/RoleService.cs` ports the business logic of the legacy `Library/Components/Security/Roles/RoleController.vb` (+ `RoleComparer.vb`), decoupled from data access via `IRoleRepository` (EF Core), with entity↔DTO translation by AutoMapper and inbound validation by FluentValidation. The Role aggregate is **HARD-deleted** with a transactional `UserRole` cascade performed in `IRoleRepository.DeleteAsync` (see [§6.3](#63-per-entity-delete-strategy)). The user-role expiry schedule in `AddUserRoleAsync` is the most behavior-critical port and reproduces the legacy `UpdateUserRole` (non-`Cancel` branch, L489–557) step-for-step — the order of the `< now` resets, the `period == -1` (`Null.NullInteger`) short-circuit, and the no-default `switch` are all preserved. Each item below is annotated with a `// MIGRATION:` comment in the service; all are behavior-preserving with respect to in-scope semantics, so default to **Sanctioned? = N**.

| ID | Method | Legacy behavior | New behavior | Rationale |
|---|---|---|---|---|
| DEV-019 | `RemoveUserRoleAsync` | `DeleteUserRole` (L330–347) returned `False` **silently** when `CanRemoveUserFromRole` (L741–746/L764–769) disallowed removal (a protected Administrator or Registered-Users assignment) | Throws `InvalidOperationException("Cannot remove this user from the role.")`, surfaced as an RFC 7807 response by `ExceptionHandlingMiddleware` | Same guard; the delivery channel changes from a return-`False` to the stateless API error contract (cf. [DEV-005](#62-deviation-index)) |
| DEV-020 | `RemoveUserRoleAsync` | On a successful removal, `DeleteUserRole` (L714–723) optionally sent a `SendNotification` "remove" email (L577–610) | Email notification omitted | Mail / Localization / Profile subsystems are OUT OF SCOPE (AAP §0.2.2); the `IRoleService` contract carries no notify flag |
| DEV-021 | `AddUserRoleAsync` | `UpdateUserRole` seeded `ExpiryDate = Now` and compared `EffectiveDate`/`ExpiryDate` against `Now` (server-local `DateTime.Now`) (L505, L530–534) | Uses `DateTime.UtcNow` throughout | Container / timezone consistency; the stateless API runs in UTC |
| DEV-022 | `AddUserRoleAsync` | `If IsTrialUsed = False And role.TrialFrequency.ToString <> "N"` (L521) **NREs** when `TrialFrequency` is null | Guards `!string.IsNullOrEmpty(TrialFrequency)` and treats a null/empty frequency as billing (the trial branch is not taken) | Forced by the nullable `string?` entity field; reproduces the legacy intent (a role with no trial frequency cannot enter the trial branch) while removing the latent null-dereference |
| DEV-023 | `AddUserRoleAsync` | `Period As Integer` read the non-nullable `TrialPeriod`/`BillingPeriod`, whose `Null.NullInteger` (`-1`) value drove the `If Period = Null.NullInteger Then ExpiryDate = Null.NullDate` short-circuit (L522, L525, L537–538) | `Role.TrialPeriod`/`Role.BillingPeriod` are nullable (`int?`); each is coalesced `?? -1` before the `period == nullInteger` check | The rewritten entity models "unset" as `null` (= legacy `Null.NullInteger`); coalescing to `-1` (not `0`) keeps the no-expiry short-circuit faithful — coalescing to `0` would wrongly fall through to the frequency switch and compute a real expiry |
| DEV-024 | `AutoAssignUsersAsync` | `AutoAssignUsers` (L68–83) looped **all** portal users via `UserController.GetUsers(PortalID, False)` | Enumerates one max-size page: `IUserRepository.GetByPortalAsync(portalId, 0, int.MaxValue)` | The repository surface is paged; a single `int.MaxValue` page is equivalent to "all users" |
| DEV-025 | `RemoveUserRoleAsync` (guard) | `CanRemoveUserFromRole` compared the **non-null** `AdministratorId`/`AdministratorRoleId`/`RegisteredRoleId` (L745/L768) | The same comparison runs against the nullable (`int?`) `Portal` fields; a `null` field yields `false` in the lifted `==`, so an unset Administrator/Registered role does not protect the assignment (it stays removable) | Safe superset of the legacy non-null semantics; the boolean result is identical for every populated portal |
| DEV-034 | `AddUserRoleAsync` (add-vs-update split — CP2 review M2/M3) | `UpdateUserRole` (L500–557) took an **add-vs-update** split: `UserRoleId <> -1 → provider.UpdateUserRole(userId, roleId, ExpiryDate)` for an existing assignment, else `provider.AddUserRole(...)` for a new one | The existing-assignment branch had been collapsed onto the insert-only `_roleRepository.AddUserRoleAsync`. It now routes an **existing** assignment to the new `_roleRepository.UpdateUserRoleAsync` (retaining the loaded `EffectiveDate`/`IsTrialUsed`, recomputing only `ExpiryDate`) and a **new** assignment to `AddUserRoleAsync`; `IRoleRepository` gained an explicit `UpdateUserRoleAsync` and documents `AddUserRoleAsync` as insert-only (see [DEV-034 in §6.2](#62-deviation-index)) | Preserves the legacy update semantics for an existing assignment and eliminates the duplicate-join-row / undocumented-upsert risk; the EF implementation of `UpdateUserRoleAsync` lands in a later checkpoint (interface-only in CP2) |

**Ported bug (reproduced, not fixed).** `AddUserRoleAsync` keeps the legacy `Select Case Frequency` **no-`Case Else`** switch (L540–547): an unmatched frequency code leaves `ExpiryDate` at its pre-switch value rather than recomputing or clearing it. Reproduced verbatim and tracked as [BUG-002](#61-ported-bugs).

**Faithfully preserved quirk (not a deviation).** `GetByPortalAsync` reproduces the legacy `RoleComparer` (`RoleComparer.vb`, L55–57) ordering — a case-insensitive (`CurrentCulture`) sort by `RoleName` — via `OrderBy(r => r.RoleName ?? string.Empty, StringComparer.CurrentCultureIgnoreCase)`.

**Not added (behavioral equivalence).** `CreateAsync` intentionally adds **no** duplicate-role-name check, because legacy `AddRole` (L100–112) performed none. `CreateAsync`/`UpdateAsync` invoke `AutoAssignUsersAsync` only when `AutoAssignment` is enabled, matching the legacy guard that wrapped the `AutoAssignUsers` loop body.

### 6.9 Auth Service (`AuthService.cs`) — Service-Level Deviations

`DnnMigration.Application/Services/AuthService.cs` is the **single sanctioned behavior change** of the migration (see [DEV-001](#62-deviation-index) and [§3](#3-sanctioned-behavior-change--authentication--cryptography)). It does **not** port a legacy class 1:1; it *synthesizes* the legacy `UserController.UserLogin` (`Library/Components/Users/UserController.vb` L991–L1008) + `GetCurrentUserInfo` (L381–L403) flow and the `PortalSecurity.vb` security model into a stateless JWT design. The service depends only on the Application identity ports `IPasswordHasher` and `IJwtService` (the DES→BCrypt and Forms-Auth→JWT primitives live in `DnnMigration.Infrastructure/Identity/`), never touching cryptographic or token primitives directly. Each item below is therefore a facet of the sanctioned authentication change (DEV-001), annotated with a `// MIGRATION:` comment in the service.

| ID | Method | Legacy behavior | New behavior | Rationale |
|---|---|---|---|---|
| DEV-026 | `LoginAsync` | `UserLogin` (L991–L1008) took an explicit `portalId` argument (multi-portal) and, on success, called `FormsAuthentication.SetAuthCookie` (L1033), optionally creating a persistent cookie ticket | `LoginRequestDto` carries no portal context, so login defaults to the DNN **primary portal** (`PortalID = 0`, the `DefaultPortalId` constant); **no cookie** is issued — a stateless JWT access + refresh pair is returned instead | Sanctioned auth change (DEV-001 / [§3.1](#31-session-management-forms-authentication--stateless-jwt-bearer)); JWT is stateless so there is no cookie; a multi-portal login flow is out of Phase-1 scope |
| DEV-027 | `LoginAsync` | `ValidateUser` performed DES-based credential checking and returned granular `UserLoginStatus` codes (e.g. `LOGIN_USERLOCKEDOUT`, `LOGIN_USERNOTAPPROVED`) by reference | BCrypt verification via `IPasswordHasher.Verify`; any invalid credential (missing user, missing stored hash, or hash mismatch) surfaces a **single generic** `UnauthorizedAccessException("Invalid username or password.")` | DES→BCrypt sanctioned change (DEV-001 / [§3.2](#32-password-cryptography-des--bcrypt)); the generic message prevents username enumeration; granular lockout/approval login-status reporting is not part of the Phase-1 auth surface |
| DEV-028 | `LogoutAsync` | `PortalSecurity.SignOut()` (`PortalSecurity.vb` L79) called `FormsAuthentication.SignOut()` and expired the `language`/`authentication`/`portalaliasid` cookies | Stateless **no-op** returning `Task.CompletedTask`; the server holds no session and the client simply discards its tokens | JWT is stateless and there is **no server-side refresh-token store** in Phase-1 scope, so nothing is revoked server-side. (The `RevokeAsync` snippet in [§2](#2-the--migration-annotation-convention) is an *illustrative* convention example, not the Phase-1 contract; `LogoutAsync(int userId)` is the no-op acknowledgement actually implemented.) |
| DEV-029 | `RefreshAsync` | Forms Authentication had **no** refresh-token concept | Refresh-token validation and rotation are delegated to `IJwtService`: the refresh token is a **signed JWT** (see [DEV-032](#62-deviation-index)) validated by `ValidateToken`, the `token_use==refresh` claim is asserted (rejecting an access token replayed here), the subject is resolved from `ClaimTypes.NameIdentifier` (falling back to the `sub` claim), the user is reloaded via `IUserRepository.GetByIdAsync`, and a **fresh access + refresh pair** is issued | Stateless rotation aligned with [§3.1](#31-session-management-forms-authentication--stateless-jwt-bearer); a persistent server-side refresh-token store is out of Phase-1 scope, so validity is established cryptographically by `IJwtService` |

**No password material ever leaves the service (security invariant).** Every successful response is built by the private `BuildAuthResponse(User)` helper, which projects the entity to `UserDto` via AutoMapper; `UserDto` deliberately omits `Password`/`PasswordAnswer`/`PasswordQuestion`, so tokens and the safe user projection are all that cross the API boundary.

**Roles are populated upstream (not a deviation).** `AuthService` passes the loaded `User` to `IJwtService.GenerateAccessToken` as-is; per the `IJwtService` contract, role claims are emitted from `User.Roles`, which the repository/persistence layer is responsible for hydrating. The service intentionally adds no separate role-loading step, preserving the uniform repository-backed flow.

### 6.10 Module Service (`ModuleService.cs`) — Service-Level Deviations

`DnnMigration.Application/Services/ModuleService.cs` ports the business logic of the legacy `Library/Components/Modules/ModuleController.vb` (1456 lines), decoupled from data access via `IModuleRepository` (EF Core), with entity↔DTO translation by AutoMapper and inbound validation by FluentValidation. The Module aggregate is **soft-deleted** via the `IsDeleted` flag (see [§6.3](#63-per-entity-delete-strategy)): `DeleteAsync` delegates the `IsDeleted = true` flip to `IModuleRepository.DeleteAsync`, and the list reads (`GetByTabAsync`, `GetByPortalAsync`) return only non-deleted modules because the repository applies the `IsDeleted == false` predicate (in the legacy stack this filtering lived in the stored procedures). Each item below is annotated with a `// MIGRATION:` comment in the service; all are behavior-preserving with respect to in-scope database semantics, so default to **Sanctioned? = N**.

| ID | Method | Legacy behavior | New behavior | Rationale |
|---|---|---|---|---|
| DEV-040 | `CreateAsync` | `AddModule` (L645–682) looped `objModule.ModulePermissions` and called `ModulePermissionController.AddModulePermission(..., TabID)` to seed module permission rows | Permission seeding omitted; only the module entity is persisted via `IModuleRepository.AddAsync` | ModulePermission management is OUT OF SCOPE (AAP §0.2.2); the `CreateModuleDto` carries no permission collection |
| DEV-041 | `CreateAsync` | `AddModule` inserted the denormalized `TabModule` link (`AddTabModule`), positioned the module within its pane (`ModuleOrder = -1` ⇒ bottom of pane via `UpdateModuleOrder`/`UpdateTabModuleOrder`), then called `ClearCache(TabID)` | Tab-module linking, pane ordering, and cache-clear omitted | Tab-module ordering is delegated to the repository/persistence layer; the Cache Provider is OUT OF SCOPE (AAP §0.2.2); the stateless API holds no module cache |
| DEV-042 | `UpdateAsync` | `UpdateModule` (L1095–1148) diffed the current `ModulePermissionCollection` against the supplied one (`CompareTo`) and, on a difference, deleted then re-added permission rows (honoring `InheritViewPermissions`/`AllowAccess`) | Permission diff/replace omitted | ModulePermission management is OUT OF SCOPE (AAP §0.2.2) |
| DEV-043 | `UpdateAsync` | `UpdateModule` synchronized the `TabModule` row (`UpdateTabModule` + `UpdateModuleOrder`), persisted the `IsDefaultModule` site settings (`PortalSettings.UpdateSiteSetting "defaultmoduleid"/"defaulttabid"`), and propagated container/visibility settings to every non-admin tab when `AllModules` was set, then `ClearCache(TabID)` | Tab-module sync/ordering, `IsDefaultModule` site-settings, `AllModules` cross-tab propagation, and cache-clear all omitted | Tab-module sync/ordering, Host/site-settings, the `AllModules` propagation, and the Cache Provider are OUT OF SCOPE (AAP §0.2.2) |
| DEV-044 | `UpdateAsync` | `UpdateModule` passed the supplied values straight to the data provider — a **no-op** when the row was absent | Loads the entity first; a missing module throws `KeyNotFoundException` → `404` RFC 7807 | The DTO + repository pattern requires loading the tracked entity to map onto; not-found is surfaced per the API error contract (cf. [DEV-005](#62-deviation-index)) |
| DEV-045 | `DeleteAsync` | `DeleteModule` (L819–826) called `DataProvider.DeleteModule(ModuleId)` then `DataProvider.DeleteSearchItems(ModuleId)` | Soft-delete delegated to `IModuleRepository.DeleteAsync` (sets `IsDeleted = true`); the Search Provider cleanup, tab-module reordering, and cache-clear are omitted | Module = soft delete ([§6.3](#63-per-entity-delete-strategy)); Search Provider and Cache Provider are OUT OF SCOPE (AAP §0.2.2) |

**Faithfully preserved (behavioral equivalence).** The list reads (`GetByTabAsync`, `GetByPortalAsync`) return only non-deleted modules; the legacy stored procedures filtered `IsDeleted` server-side, and that responsibility now lives in `IModuleRepository` (`IsDeleted == false`). `ModuleService` never re-introduces deleted rows.

**Preserved tolerance (not a deviation).** `DeleteAsync` performs no pre-load existence check, so a non-existent module id is a silent no-op — matching legacy `DeleteModule`, whose stored procedure simply affected no rows for an unknown id. (This differs from `UpdateAsync`, where the DTO + repository mapping pattern necessarily loads the entity and therefore surfaces a missing row as `KeyNotFoundException` per DEV-044.)

---

## 7. References

- [`./docs/technical-specifications.md`](./docs/technical-specifications.md) — the project technical specification (architecture, API surface, configuration, screen map).
- [`./README.md`](./README.md) — installation and usage instructions for the new backend and frontend applications.
- Legacy source of truth (reference only, not compiled): `Library/Components/Security/PortalSecurity.vb`, `Library/Components/Shared/CBO.vb`, `Library/Components/Shared/Null.vb`, `Library/Providers/DataProviders/SqlDataProvider/SqlDataProvider.vb`, `Library/Components/Users/UserInfo.vb` (and the `Membership/` enum files), `Library/Components/Modules/ModuleInfo.vb`.

---

_This document is maintained alongside the migration. Keep every factual claim consistent with the AAP and the cited legacy source files, and keep each entry traceable to its `// MIGRATION:` annotation in code._
