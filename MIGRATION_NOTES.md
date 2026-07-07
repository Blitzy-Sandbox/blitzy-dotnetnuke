# Migration Notes

This document is the authoritative **decisions log** for the migration of the legacy
**DotNetNuke (DNN) 4.x** portal framework into a modern, containerized, two-tier
application. It records every *significant* decision made while rewriting the legacy
system and explains **why** the new architecture diverges structurally from the old one
while **preserving domain semantics**. It is written for maintainers, reviewers, and
future contributors, and is the companion aggregate record to the inline `// MIGRATION:`
comments found throughout the `backend/` and `frontend/` trees.

- **Legacy system:** DotNetNuke 4.x (assembly `4.9.0.85`), **VB.NET** on
  **CLR / .NET Framework 2.0**, a classic **ASP.NET Web Forms** application organized
  across two primary trees: `/Library` (core business logic, entity definitions, data
  access) and `/Website` (Web Forms presentation layer).
- **Target system:** **C# 12** on **.NET 8 LTS** (`TargetFramework: net8.0`,
  `<Nullable>enable</Nullable>`), an **ASP.NET Core 8 Web API** following the
  **Backend-for-Frontend (BFF)** pattern, an **Angular 19** standalone-component
  **Single Page Application (SPA)**, **EF Core 8** Code-First mapped to the **existing**
  database schema, and **Docker** multi-container deployment.
- **Repository layout:** New parallel trees `backend/`, `frontend/`, and `docker/` were
  added **in-place** in the same repository. The legacy `/Library` and `/Website` trees
  are retained **as reference** (they are neither deleted nor built); every target
  artifact is net-new and derives its domain semantics from a legacy source file.

> **Guiding discipline - Minimal Change Clause and Migration Discipline.**
> Although the change is architecturally total (language, framework, presentation, and
> data access are all replaced), the **domain and business logic are preserved exactly**.
> Extracted rules are **not** optimized, refactored, or "improved" during the rewrite:
> each migrated operation must produce **identical outcomes for identical inputs**
> (behavioral equivalence). Bugs discovered in the legacy code are **documented** (as
> `// MIGRATION:` comments and in this file) and are **not** fixed unless they actively
> block the migration. Where a decision *preserves* legacy behavior, it is noted as such;
> where it *diverges* (most notably authentication), the rationale and the mechanism for
> maintaining functional parity and security are explained.

## Table of Contents

1. [Overview](#1-overview)
2. [Architecture Decisions](#2-architecture-decisions)
3. [Language and Framework Conversion (VB.NET 2.0 to C# 12)](#3-language-and-framework-conversion-vbnet-20-to-c-12)
4. [Data Access Migration (ADO.NET / SqlDataProvider to EF Core 8)](#4-data-access-migration-adonet--sqldataprovider-to-ef-core-8)
5. [Presentation Re-architecture (Web Forms to API + Angular)](#5-presentation-re-architecture-web-forms-to-api--angular)
6. [Authentication and Security](#6-authentication-and-security)
7. [Configuration and Secret Handling](#7-configuration-and-secret-handling)
8. [Dependency and Tooling Changes](#8-dependency-and-tooling-changes)
9. [Conventions, Risks, and Validation](#9-conventions-risks-and-validation)

---

## 1. Overview

The migration is simultaneously **four** transformations, executed together in a single
phase rather than incrementally:

| # | Transformation | From | To |
|---|----------------|------|----|
| 1 | Language conversion | VB.NET | idiomatic **C# 12** with nullable reference types |
| 2 | Framework migration | .NET Framework 2.0 | **.NET 8 LTS** (`net8.0`) |
| 3 | Presentation re-architecture | ASP.NET Web Forms | **ASP.NET Core 8 Web API (BFF)** + **Angular 19 SPA** |
| 4 | Data-access modernization | ADO.NET / `SqlDataProvider` | **EF Core 8** Code-First to the existing schema |

The overarching rules that govern every file-level change:

- **Preserve domain semantics, replace mechanics.** Entity shape, relationships, and
  business rules are preserved; the language, framework, data-access mechanism, and
  presentation model are replaced wholesale.
- **DTO boundary.** EF Core entities are never returned from controllers; responses are
  projected to DTOs (via AutoMapper) and wrapped in a consistent response envelope.
- **Async-only.** All data access and I/O use `async`/`await` with zero synchronous
  blocking calls.
- **Namespace remap.** Legacy `DotNetNuke.*` namespaces map to
  `DnnMigration.{Domain|Application|Infrastructure|Api}.*`.

The end-to-end target request flow realized by this migration:

```text
Client Browser
  -> Angular 19 SPA (standalone components + Signals)
     -> nginx:alpine reverse proxy   (serves the SPA, proxies /api/*)
        -> ASP.NET Core 8 BFF API (Controllers)
           -> Application Services (+ DTOs / AutoMapper)
              -> Repositories (IRepository<T>)
                 -> EF Core 8 (DnnDbContext)
                    -> Existing SQL Server schema (aspnet_* + DNN tables)
```

---

## 2. Architecture Decisions

### 2.1 Clean / Onion architecture

The backend is a layered **Clean / Onion** solution (`DnnMigration.sln`, root namespace
`DnnMigration.*`). Dependencies point **inward**, and the Domain layer has **no external
dependencies**.

```text
Api  -->  Application  -->  Domain
                              ^
Infrastructure  ------------- +   (implements Domain interfaces; depends on Application/Domain)
```

| Layer | Project | Responsibility | Legacy origin |
|-------|---------|----------------|---------------|
| Domain | `DnnMigration.Domain` | Entities, repository interfaces, enums; no external deps | `*Info.vb` entity classes, `DataProvider.vb` contract |
| Application | `DnnMigration.Application` | Services (business rules), DTOs, AutoMapper profiles, FluentValidation validators | business-logic portions of `*Controller.vb`, `*Validator.vb` |
| Infrastructure | `DnnMigration.Infrastructure` | `DnnDbContext`, EF configurations, repositories, identity (JWT / BCrypt) | `SqlDataProvider.vb`, `PortalSecurity.vb` |
| Api | `DnnMigration.Api` | Controllers, middleware, `Program.cs` host | `Global.asax`, `Default.aspx.vb`, `Website/admin/**` |

**Namespace remap.** VB `Imports X` becomes C# `using X;` (with file-scoped namespaces),
and the DNN namespaces are remapped as follows:

| Legacy namespace | Target namespace |
|------------------|------------------|
| `DotNetNuke.Entities.Portals` / `.Modules` / `.Users` | `DnnMigration.Domain.Entities` |
| `DotNetNuke.Security.Roles` / `.Security.Permissions` | `DnnMigration.Domain.Entities` |
| business logic in `DotNetNuke.*Controller` | `DnnMigration.Application.Services` |
| `DotNetNuke.Data` (`SqlDataProvider`) | `DnnMigration.Infrastructure.Data` / `.Repositories` |

### 2.2 Decomposing the "Info / Controller / Collection" triad

The legacy DNN pattern pairs an `*Info` entity, a `*Controller` that **co-mingles
business logic and ADO.NET data access**, and a `*Collection`. Across the six domain
controllers this co-mingled code totals roughly **6,725 lines** (`PortalController`
~1,632, `ModuleController` ~1,456, `UserController` ~1,372, `TabController` ~1,302,
`RoleController` ~892, `PermissionController` ~71).

Each legacy `*Controller.vb` is split along a clean boundary into a **triad**:

- **Repository** (`DnnMigration.Infrastructure.Repositories`) - data access only, over
  `DnnDbContext`.
- **Service** (`DnnMigration.Application.Services`) - business rules only, extracted
  verbatim from the legacy controller.
- **API Controller** (`DnnMigration.Api.Controllers`) - HTTP concerns only.

Two hard rules follow from this decomposition and are enforced everywhere:

- **No business logic in API controllers.**
- **All data access goes through repository interfaces** (`IRepository<T>` and the
  per-entity interfaces defined in the Domain layer).

### 2.3 Target API surface

All resource endpoints are RESTful and live under `/api`:

| Resource | Endpoints |
|----------|-----------|
| Portals | `GET /api/portals`, `POST /api/portals`, `GET/PUT/DELETE /api/portals/{id}` |
| Modules | `GET /api/modules`, `POST /api/modules`, `GET/PUT/DELETE /api/modules/{id}` |
| Users | `GET /api/users`, `POST /api/users`, `GET/PUT/DELETE /api/users/{id}` |
| Roles | `GET /api/roles`, `POST /api/roles`, `GET/PUT/DELETE /api/roles/{id}` |
| Tabs | `GET /api/tabs`, `POST /api/tabs`, `GET/PUT/DELETE /api/tabs/{id}` |
| Auth | `POST /api/auth/login`, `POST /api/auth/refresh`, `POST /api/auth/logout`, `GET /api/auth/me` |
| Health | `GET /health` (`[AllowAnonymous]`) |

The health endpoint is anonymous and returns a fixed payload:

```json
{ "status": "Healthy", "version": "1.0.0.0" }
```

### 2.4 Response contract

Successful responses use a consistent envelope; errors use **RFC 7807 Problem Details**.
EF Core entities are **never** returned from controllers - every response is projected to
a DTO via **AutoMapper**, and all I/O is **async-only**.

Success envelope:

```json
{ "data": { }, "meta": { } }
```

Error (RFC 7807 Problem Details):

```json
{ "type": "...", "title": "...", "status": 400, "detail": "...", "errors": { } }
```

---

## 3. Language and Framework Conversion (VB.NET 2.0 to C# 12)

The conversion applies a fixed per-construct mapping so that behavior is preserved while
syntax and typing modernize. Direct transliteration is avoided in favor of idiomatic
C# 12.

| VB.NET 2.0 construct | C# 12 equivalent |
|----------------------|------------------|
| `Imports X` | `using X;` |
| `Namespace N ... End Namespace` | file-scoped `namespace N;` |
| `Inherits B` | `: B` |
| `Implements I` | `: I` (comma-joined with a base class) |
| `MustInherit` / `MustOverride` | `abstract` |
| `Overridable` / `Overrides` | `virtual` / `override` |
| `NotInheritable` | `sealed` |
| `Public Shared` (static) controller members | **dependency-injected instance methods** |
| `ByRef` | `ref` / `out` |
| `Optional p As T = v` | default parameters / overloads |
| `With obj ... End With` | object initializers |
| `Is Nothing` / `IsNot Nothing` | `is null` / `is not null` |
| `Null.NullInteger` | `null` / `default(int?)` |
| `CType(x, T)` / `DirectCast(x, T)` | `(T)x` / `x as T` |
| `AndAlso` / `OrElse` | `&&` / `\|\|` |
| property `Get` / `Set` blocks | C# auto-properties |
| `Option Strict On` / `Option Explicit On` | `<Nullable>enable</Nullable>` + treat-warnings-as-errors |

**Static-to-DI conversion (called out explicitly).** Legacy `Public Shared` controller
members are the single largest structural change in this section. For example,
`UserController.GetUser` and `UserController.DeleteUser` (static `Shared` methods in the
legacy code) become instance methods on DI-registered services and repositories. No
statics remain in application code; every collaborator is constructor-injected. This is
what makes the built-in ASP.NET Core DI container the composition root for the system.

---

## 4. Data Access Migration (ADO.NET / SqlDataProvider to EF Core 8)

The legacy data tier is an abstract `DataProvider` contract (with `MustOverride`
`ExecuteNonQuery` / `ExecuteReader` / `ExecuteScalar` / `ExecuteDataSet`) and a concrete
`SqlDataProvider` implementation that calls stored procedures through `SqlHelper`
(`{databaseOwner}{objectQualifier}<Proc>`). This entire tier is replaced by a single
**`DnnDbContext`** plus one **`IEntityTypeConfiguration<T>` per entity** using the EF Core
**Fluent API**.

> **Schema fidelity is mandatory (called out explicitly).**
> EF Core entities are mapped to the **existing** database schema **without altering table
> structures**. The Fluent API configuration preserves legacy **table names, column names,
> and foreign-key constraint names verbatim** using `ToTable(...)`, `HasKey(...)`,
> `HasColumnName(...)`, and `HasConstraintName(...)`. The legacy database remains the
> system of record; **no production schema migration is performed**. EF Core migrations
> exist **only** for greenfield / test databases (for example the in-memory database used
> by integration tests), never to reshape the production schema.

Fluent mapping precedence is **Fluent API > Data Annotations > conventions**. The legacy
schema is defined by the install scripts `InstallCommon.sql`, `InstallMembership.sql`,
`InstallRoles.sql`, and `InstallProfile.sql`. Mapped (not recreated) tables include the
ASP.NET membership tables and the DNN tables:

| Existing table | Mapped by |
|----------------|-----------|
| `aspnet_Applications` | application/portal configuration |
| `aspnet_Users` | user identity |
| `aspnet_Membership` | credential / salted-hash columns (see Section 6) |
| `aspnet_Roles` | role definitions |
| `aspnet_UsersInRoles` | user-role association |
| `aspnet_Profile` | user profile values |
| `aspnet_SchemaVersions` | schema versioning |
| DNN tables (`Portals`, `Tabs`, `Modules`, permissions, ...) | domain entities |

**Data-access conventions applied everywhere:**

- Configure each relationship from **one** side only.
- Set an **explicit `DeleteBehavior`** on each relationship (do not rely on convention).
- Use **`AsNoTracking()`** on all read paths for performance and to avoid accidental
  change tracking.
- **Project to DTOs**; never return EF entities from a repository through to a controller.

**Stored procedures are not ported.** The **91 `.SqlDataProvider` version scripts** that
carry stored-procedure logic are **not** migrated. The **schema is the contract**; the
behavior previously expressed in stored procedures is re-expressed as **LINQ / repository
methods** that produce equivalent results.

---

## 5. Presentation Re-architecture (Web Forms to API + Angular)

**There is no in-place migration from ASP.NET Web Forms to ASP.NET Core** (per Microsoft
guidance, Web Forms functionality must be rewritten, favoring Web APIs). Accordingly, the
Web Forms presentation layer is rewritten as a **stateless JSON API** plus an **Angular
19 SPA**. The following legacy mechanics are **eliminated, not ported**:

- **ViewState**, `IPostBackEventHandler`, and `IClientAPICallbackEventHandler`
  (implemented by `Website/Default.aspx.vb`).
- `.aspx` / `.ascx` / `.master` markup and all **server-side HTML rendering**. The API
  emits JSON only (via `System.Text.Json`); there are no Razor views.

### 5.1 Web Forms to API + Angular mapping strategy

| Web Forms artifact | API endpoint | Angular component |
|--------------------|--------------|-------------------|
| List / Grid page | `GET /api/{entity}` | List component with table/grid |
| Detail / View page | `GET /api/{entity}/{id}` | Detail component |
| Create page | `POST /api/{entity}` | Form component (create mode) |
| Edit page | `PUT /api/{entity}/{id}` | Form component (edit mode) |
| Delete action | `DELETE /api/{entity}/{id}` | Confirmation dialog + service call |
| Search / Filter | `GET /api/{entity}?query=...` | List component with filter controls |

At the code level: a `Page_Load` data fetch becomes `ngOnInit` + an `ApiService` GET
call, and a postback button handler becomes a POST / PUT / DELETE call. Each legacy admin
editor screen (for example `Website/admin/Portal/SiteSettings.ascx.vb`,
`Website/admin/Users/User.ascx.vb`, `Website/admin/Security/EditRoles.ascx.vb`) becomes an
Angular feature component plus a backing API action.

### 5.2 Angular 19 conventions

- **Standalone components** only (no `NgModule`s).
- **`inject()`** for dependency injection.
- **Signals** / `computed` for component and derived state.
- **`@if` / `@for` / `@switch`** built-in control flow.
- **`loadComponent`** lazy routes with a preloading strategy for anticipated routes.
- **OnPush** change detection; **virtual scrolling** for large lists.
- **Typed reactive forms** (`FormGroup`) whose validation rules **mirror the original
  ASPX validators**, preserving UI functional parity (form validation, error messages,
  and user feedback are equivalent to the legacy screens).

The `Global.asax` lifecycle and the `Library/HttpModules/**` pipeline become the
`Program.cs` service registration plus ASP.NET Core middleware (see Sections 6-7).

---

## 6. Authentication and Security

Authentication is the **most significant behavioral divergence** in the migration.
DNN's Forms Authentication and provider-based membership are **out of scope** and are
replaced by a **JWT bearer** scheme plus **BCrypt** password hashing, while the
user / role / permission security model is preserved. The relevant legacy logic lives in
`Library/Components/Security/PortalSecurity.vb`.

| Legacy construct (`PortalSecurity`) | Target replacement |
|-------------------------------------|--------------------|
| `SecurityAccessLevel` enum (`Anonymous` / `View` / `Edit` / `Admin` / `Host`) | `[Authorize]` / `[AllowAnonymous]` attributes + claims/role policies |
| `Encrypt` / `Decrypt` (DES via `DESCryptoServiceProvider` + `CryptoStream`, Base64) and legacy hashing | `BCrypt.Net-Next` `PasswordHasher` |
| `SignOut` (`FormsAuthentication.SignOut` + cookie expiry) | stateless JWT logout (`POST /api/auth/logout`) |
| `UserLogin` (delegated to `UserController.UserLogin`) | `AuthService` login that issues tokens (`POST /api/auth/login`) |
| static `Shared` `IsInRole` / `IsInRoles` / `HasNecessaryPermission` overloads | DI service methods + policy evaluation |

> **Forward-hash-on-login strategy (called out explicitly).**
> The legacy `aspnet_Membership` **salted-hash** columns are **read for verification** at
> login time so existing credentials continue to work. On a **successful** login the
> submitted password is **re-hashed with BCrypt and persisted forward** (the BCrypt hash
> replaces / augments the legacy hash for that user). Over time, active users are
> transparently migrated to BCrypt with no password reset required. This decision is
> mandatory and is documented here as the single most important auth-migration behavior.

### 6.1 BFF token flow

- Tokens are **issued by the ASP.NET Core API** and **attached by the Angular
  `auth.interceptor.ts`** to outgoing requests as a bearer token.
- A **`401`** response triggers a **silent refresh** via `POST /api/auth/refresh`.
- **Short-lived access tokens** with **refresh-token rotation**.
- **CORS** is restricted to the **Angular origin only**.
- **Rate limiting** is applied to the authentication endpoints.
- **HTTPS** is enforced.

### 6.2 Provider collapse

The DNN provider abstraction (Caching / Data / Logging / Membership / Navigation /
Scheduling / Search) collapses so that only three concerns remain on the core migration
path:

| DNN provider | Fate on the core path |
|--------------|-----------------------|
| Data | `-> ` EF Core (`DnnDbContext`) |
| Membership | `-> ` Identity / JWT (`AuthService`, `JwtTokenService`) |
| Logging | `-> ` Serilog |
| Caching / Navigation / Search / HtmlEditor | not on the core path (dropped / out of scope) |
| Scheduling (`DNNScheduler`) | `IHostedService` / `BackgroundService` **only if** a job is surfaced by Portal / Module / User parity (none currently required) |

### 6.3 `AuthService` credential persistence, refresh-token store, and portal scoping

Three interlocking decisions make the `AuthService` login / refresh / logout / current-user
flow behave correctly against the preserved DNN schema. Each was driven by a concrete
defect in the first-pass implementation and was fixed **at its root cause**, spanning the
Application and Infrastructure layers while keeping the Clean/Onion dependency direction
intact (the new ports live in **Application**; their adapters live in **Infrastructure**).

**(a) Membership is an EF Core _owned type_ of `User`, not a standalone entity.**
`UserService.CreateAsync` hashes the password into `user.Membership.Password`, and
`AuthService.LoginAsync` verifies that same value. But the first-pass
`UserConfiguration` did `builder.Ignore(e => e.Membership)`, so the credential was
**never persisted or loaded** and every real login failed. Membership is now mapped with
`builder.OwnsOne(e => e.Membership, ...)` to the legacy `aspnet_Membership` table, carrying
the legacy column-name divergences verbatim (`Approved -> IsApproved`,
`CreatedDate -> CreateDate`, `LastPasswordChangeDate -> LastPasswordChangedDate`,
`LockedOut -> IsLockedOut`) for **data-model fidelity**. An owned type is auto-loaded with
its owner (even under `AsNoTracking`) and saved in the same `SaveChanges`, so the existing
`UserRepository` read paths and `UserService` write paths work unchanged. Consequently the
matching `DbSet<UserMembership>` was **removed** from `DnnDbContext` — EF Core forbids an
owned type from also being an aggregate-root set.
  - **Scope decision:** only **Membership** was converted to an owned type in this pass.
    `UserProfile` is deliberately **left as the pre-existing standalone entity** (the
    authentication flow never touches Profile), so the `aspnet_Profile` name/value-blob
    remodel remains a distinct, out-of-boundary concern.
  - **Known caveat (deferred):** as an owned type, `aspnet_Membership` is keyed by the
    owner's **int `User.UserID`**, whereas the physical DNN table keys on a **`Guid`
    `UserId`** correlated through `aspnet_Users`. Full Guid-key / `UserPortals`-junction
    fidelity is a broader `UserConfiguration` concern tracked under that boundary's own
    findings; it does not affect credential round-tripping through the modern stack or the
    EF Core InMemory integration path.

**(b) Refresh tokens are opaque server-side state validated by lookup, with single-use
rotation and real revocation.**
`IJwtTokenService.GenerateRefreshToken()` returns an **opaque cryptographically-random**
value, **not a JWT**; the first-pass `RefreshAsync` tried to `ValidateToken` it as a JWT,
so **every refresh failed**, and `LogoutAsync` was a **no-op** despite the `IAuthService`
contract promising revocation. A new **`IRefreshTokenStore`** port (Application) with an
**`InMemoryRefreshTokenStore`** adapter (Infrastructure) now owns refresh-token lifecycle:
  - `LoginAsync` / `RefreshAsync` **persist** every issued refresh token; `RefreshAsync`
    **validates by store lookup** (never by JWT parsing) and performs **single-use
    rotation** — the presented token is **revoked before** a new pair is issued, so a
    replayed or stolen refresh token fails after its first legitimate use.
  - `LogoutAsync` now **revokes all** of the caller's refresh tokens, honoring the
    contract; short-lived access tokens still expire naturally.
  - Tokens are stored **hashed (SHA-256)**, never in plaintext, keyed for O(1) lookup;
    expiry is bound from `JwtSettings.RefreshTokenExpirationDays` and expired entries are
    pruned opportunistically. No new database table is introduced (**schema fidelity**,
    §4 / AAP §0.6.2).
  - **Distributed-cache seam:** the in-memory store is registered as a **singleton** and
    is correct for a single instance. For multi-instance / production deployment the same
    `IRefreshTokenStore` port should be re-implemented over a shared backing store (e.g.
    `IDistributedCache` / Redis) — no `AuthService` change is required, only a different
    Infrastructure registration.

**(c) The effective portal is derived from a _trusted_ ambient context, never from the
client alone.**
The first-pass login trusted `dto.PortalId ?? 0` — a **client-supplied** value — to scope
the user lookup, and the current-user flow performed **no claim/resource matching**. A new
**`IPortalContextAccessor`** port (Application) supplies the **trusted** portal derived from
the request host / alias:
  - `LoginAsync` uses the ambient portal when present; a client-supplied `PortalId` may only
    **agree** with it and can **never override** it (a disagreement is rejected). When no
    ambient portal is available the flow falls back to the request-supplied id (default
    portal `0`), preserving legacy behavior until the host-aware accessor is wired.
  - `GetCurrentUserAsync` enforces **claim/resource scoping**: a non-super user may only
    resolve `/me` for the portal stamped into their token's `portalId` claim; a mismatch
    (a tampered or stale token, or a cross-portal attempt) yields `null`. **Super users**
    are host-level and intentionally span portals, so they are exempt.
  - **Host-alias resolution deferred to the API layer (CP4):** the Infrastructure default
    `PortalContextAccessor` is a **null-object** (`GetPortalId() => null`), which safely
    degrades to the request-supplied portal. The **authoritative** host/alias-aware
    accessor — resolving the portal from the incoming `Host` header via the `PortalAlias`
    table — belongs in the API host and is registered there as a **scoped** service when
    the HTTP pipeline (`Program.cs`) is introduced at the next checkpoint.

**Forward-hash-on-login interaction and the legacy-SHA1 limitation.** With Membership now
persisted, credentials created through the modern stack (BCrypt) verify correctly.
Verifying **legacy `aspnet_Membership` SHA1 salted hashes** additionally requires the
`PasswordSalt` / `PasswordFormat` columns, which are **not modeled on `UserMembership`**;
the forward-hash-on-login step for pre-existing accounts (see the boxed strategy in §6)
therefore remains a **deferred** item outside this boundary until those columns are added.

---

## 7. Configuration and Secret Handling

Legacy configuration lives in `Website/development.config` and `Website/release.config`.
**There is no `web.config` in the source.** Their values - including the `SiteSqlServer`
connection string (the `<connectionStrings>` and `<appSettings>` entries; the commented
SQL Server 2000/2005 example points at `Database=DotNetNuke`) - move to
`appsettings.json` + `appsettings.Development.json` + **environment variables**, bound
through the ASP.NET Core Options pattern.

> **DES-encrypted secrets are not portable (called out explicitly).**
> The legacy stack stores DES-encrypted secrets that the new stack **cannot decrypt**
> (the DES key material and algorithm are intentionally not carried across). Therefore
> connection strings and host secrets are **re-supplied** through **environment variables
> or user-secrets** and are **never committed** to source control. Representative keys:
> `ConnectionStrings__Default` (the SQL Server connection to the existing DNN schema) and
> `Jwt__Secret` (the JWT signing key, alongside issuer/audience). Placeholders only appear
> in committed configuration; real values are injected at deploy time.

**Classic HttpModules become middleware (or are dropped).** The `system.webServer`
`<modules>` section registers the DNN modules **Compression**, **RequestFilter**,
**UrlRewrite**, **Exception**, **UsersOnline**, **DNNMembership**, and **Personalization**.
These become ASP.NET Core middleware where they are **still relevant** - exception
handling, correlation-id propagation, and authentication - and are **dropped** where they
are not (for example the ASP.NET AJAX `ScriptModule` and Web Forms-era personalization).

---

## 8. Dependency and Tooling Changes

### 8.1 Retired legacy dependencies

These are retired outright, not ported:

| Retired legacy dependency | Replacement / disposition |
|---------------------------|---------------------------|
| .NET Framework 2.0 / CLR 2.0 | `net8.0` |
| `Microsoft.VisualBasic` runtime | not required in C# |
| `System.Web` / `System.Web.Extensions` (ASP.NET AJAX, `v1.0.61025.0`) | ASP.NET Core |
| `DotNetNuke.WebControls` | Angular components |
| `SharpZipLib` | `System.IO.Compression` (if archiving is needed) |
| `Microsoft.ApplicationBlocks.Data` (`SqlHelper`) | EF Core |
| Telerik RadControls | excluded (precautionary; negligible footprint) |
| 34 prebuilt `.dll` binaries in `Website/bin/`, 20 `.vbproj` projects, dual `.sln` files | retired (not converted) |

### 8.2 New backend dependencies (NuGet, `net8.0`)

All `Microsoft.EntityFrameworkCore.*` and ASP.NET Core packages share the **8.0.11**
servicing line.

| Package | Version | Purpose |
|---------|---------|---------|
| Microsoft.AspNetCore.Authentication.JwtBearer | 8.0.11 | JWT bearer authentication |
| Microsoft.AspNetCore.OpenApi | 8.0.11 | OpenAPI metadata |
| Microsoft.EntityFrameworkCore | 8.0.11 | ORM core |
| Microsoft.EntityFrameworkCore.SqlServer | 8.0.11 | SQL Server provider |
| Microsoft.EntityFrameworkCore.Design | 8.0.11 | Design-time migrations |
| Microsoft.EntityFrameworkCore.Tools | 8.0.11 | EF Core CLI / PMC tooling |
| Swashbuckle.AspNetCore | 6.9.0 | Swagger UI / OpenAPI generation |
| AutoMapper.Extensions.Microsoft.DependencyInjection | 12.0.1 | Entity to DTO mapping |
| FluentValidation.AspNetCore | 11.3.0 | Request validation |
| Serilog.AspNetCore | 8.0.3 | Structured logging |
| Serilog.Sinks.Console | 6.0.0 | Console log sink |
| BCrypt.Net-Next | 4.0.3 | Password hashing |

### 8.3 Backend test dependencies (NuGet)

| Package | Version | Purpose |
|---------|---------|---------|
| xunit | 2.9.2 | Unit test framework |
| xunit.runner.visualstudio | 2.8.2 | Test runner integration |
| Moq | 4.20.72 | Mocking |
| FluentAssertions | 6.12.2 | Assertions |
| Microsoft.AspNetCore.Mvc.Testing | 8.0.11 | Integration host (`WebApplicationFactory`) |
| Microsoft.EntityFrameworkCore.InMemory | 8.0.11 | In-memory DB for integration tests |

> **Test-runner note.** `dotnet test` also requires `Microsoft.NET.Test.Sdk` (for
> example `17.11.1`) in each test project. It is a runner prerequisite rather than a
> functional dependency and is not listed among the application packages above.

### 8.4 Frontend dependencies (npm) and container images

- **Angular 19:** `@angular/{core,common,compiler,forms,router,platform-browser,
  platform-browser-dynamic,animations}` `^19.0.0`; `@angular/{cli,compiler-cli}`
  `^19.0.0`.
- **Runtime / language:** `rxjs ^7.8.1`, `zone.js ^0.15.0`, `tslib ^2.8.0`,
  `typescript ^5.6.0`.
- **Test stack:** `karma ^6.4.4`, `karma-jasmine ^5.1.0`, `jasmine-core ^5.4.0`
  (headless Chrome).
- **Container base images:** `mcr.microsoft.com/dotnet/sdk:8.0-alpine` (build) and
  `mcr.microsoft.com/dotnet/aspnet:8.0-alpine` (API runtime); `node:20-alpine` (frontend
  build) and `nginx:alpine` (frontend runtime). The frontend build runtime is Node.js
  20.x LTS + npm 10.x.

---

## 9. Conventions, Risks, and Validation

### 9.1 Migration comment convention

> **`// MIGRATION: [explanation]` convention (called out explicitly).**
> Every non-obvious conversion is marked inline with a `// MIGRATION: [explanation]`
> comment in **both** the backend (`backend/**/*.cs`) and the frontend
> (`frontend/**/*.ts`). This makes divergences from the legacy code discoverable at the
> point of change. **This `MIGRATION_NOTES.md` file is the companion aggregate record**
> that collects the significant decisions those inline comments reference.

### 9.2 Cross-cutting risks

- **Controller concern-splitting.** The six large legacy controllers (~6,725 combined
  lines) must each be split into Repository (data) + Service (business) + Controller
  (HTTP) without changing behavior; incorrect extraction is the primary regression risk.
- **Exact name preservation.** Table, column, and foreign-key constraint names must be
  retained exactly so the legacy database stays authoritative. No production schema
  migration is performed (see Section 4).
- **DES secret non-portability.** Legacy DES-encrypted secrets cannot be decrypted by the
  new stack; connection strings and host secrets must be re-supplied out-of-band (see
  Section 7).

### 9.3 Validation gates (success criteria)

All gates must pass:

| Gate | Command / check | Pass condition |
|------|-----------------|----------------|
| 1 - API compilation | `dotnet build --configuration Release --warnaserror` | exit 0; 0 errors / 0 warnings (excluding `CS8618` nullable) |
| 2 - API unit tests | `dotnet test --configuration Release` | exit 0; 100% pass |
| 3 - Angular build | `ng build --configuration production` | exit 0; 0 errors / 0 warnings |
| 4 - Angular unit tests | `ng test --watch=false --browsers=ChromeHeadless --code-coverage` | exit 0; 100% pass |
| 5 - API integration tests | `PortalApiTests`, `ModuleApiTests`, `UserApiTests` full CRUD | POST -> 201, GET -> 200, PUT -> 200, DELETE -> 204 |
| 6 - Container build | `docker-compose build` | exit 0; both images created |
| 7 - Container startup | `curl -f http://localhost:8080/health` and `curl -f http://localhost:4200` | both return HTTP 200 |

> **Environment note.** Gates 6 and 7 require a Linux Docker environment
> (Alpine-based images). On a Windows-only host without a Docker daemon these two gates
> are executed in a Linux Docker CI environment; Gates 1-5 run locally and use
> `EFCore.InMemory` for integration tests, so they need no external SQL Server.

### 9.4 Git workflow directive

Per the project setup instruction: always run `git fetch origin <branch>` **immediately
before** `git push --force-with-lease` to prevent stale tracking-ref rejections.
History-altering commands (rebase / reset / force without lease) are not used.
