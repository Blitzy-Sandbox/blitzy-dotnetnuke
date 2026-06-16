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
7. [References](#7-references)

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
- **New.** Stateless **JWT Bearer** tokens: a **60-minute** access token plus
  **refresh-token rotation**. The server retains **no session state**, which enables
  horizontal scaling (BFF pattern).
- **Target code.** `DnnMigration.Infrastructure/Identity/JwtService.cs` (issue /
  validate / rotate) and `DnnMigration.Application/Services/AuthService.cs`
  (orchestration), exposed via `/api/auth/{login,refresh,logout,me}`.

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

## 7. References

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
