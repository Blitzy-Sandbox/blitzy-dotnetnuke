# Technical Specification

# 0. Agent Action Plan

## 0.1 Intent Clarification

Based on the prompt, the Blitzy platform understands that the refactoring objective is to perform a **complete rewrite** of a legacy DotNetNuke (DNN) 4.x application from VB.NET (.NET Framework 2.0) to a modern technology stack consisting of:

- **Backend:** C# 12 on .NET 8 LTS with ASP.NET Core 8 Web API implementing the Backend-for-Frontend (BFF) pattern
- **Frontend:** Angular 19 Single Page Application with standalone components
- **Data Access:** Entity Framework Core 8 replacing ADO.NET/SqlDataProvider
- **Deployment:** Docker containerization targeting Linux (Alpine-based images)

### 0.1.1 Core Refactoring Objective

This is not an incremental migration but a full architectural transformation. The refactoring type encompasses:

- **Language Conversion:** VB.NET → C# 12 with nullable reference types
- **Framework Migration:** .NET Framework 2.0 → .NET 8 LTS
- **Architectural Transformation:** ASP.NET WebForms → ASP.NET Core Web API + Angular 19 SPA
- **Data Access Modernization:** ADO.NET/SqlDataProvider with stored procedures → Entity Framework Core 8 with LINQ
- **Deployment Model:** Windows IIS → Linux containers (Docker)

**Target Repository:** New repository structure with separate backend and frontend projects within a single solution.

**Refactoring Goals with Enhanced Clarity:**

| Goal | Description | Technical Approach |
|------|-------------|-------------------|
| Language Modernization | Convert all VB.NET to idiomatic C# 12 | Manual rewrite with pattern matching, nullable types, records |
| API-First Architecture | Replace WebForms postback with REST APIs | ASP.NET Core 8 controllers with JSON responses |
| SPA Frontend | Replace ASPX/ASCX with Angular components | Angular 19 standalone components with reactive forms |
| ORM Adoption | Replace SqlDataProvider with EF Core | Code-First with Fluent API mapping to existing schema |
| Container-Ready | Enable Linux deployment | Docker multi-stage builds with Alpine images |

**Implicit Requirements Surfaced:**

- Maintain functional parity for all Portal, Module, and User CRUD operations
- Preserve multi-tenant portal isolation semantics
- Map existing stored procedures to EF Core LINQ queries or raw SQL where necessary
- Implement JWT-based authentication replacing Forms Authentication
- Ensure API backward compatibility with existing data contracts

### 0.1.2 Special Instructions and Constraints

**Critical Preservation Directives:**

- **Domain Logic Preservation:** Extract business rules exactly as implemented in VB.NET source without optimization or "improvement" during migration
- **Data Model Fidelity:** Map EF Core entities to match existing database schema using Fluent API for legacy naming conventions
- **Behavioral Equivalence:** Each migrated operation must produce identical outcomes for identical inputs
- **UI Functional Parity:** Angular components must support all user workflows from original ASPX pages

**Explicit Exclusions (Do Not Migrate):**

- Telerik RadControls and all Telerik dependencies
- DNN 4.x authentication providers (replace with JWT)
- `DotNetNuke.Entities.Host` namespace
- `DotNetNuke.Common.Globals` static methods
- Legacy module loader infrastructure
- `DotNetNuke.Services.Scheduling` (replace with `IHostedService`)
- COM interop components and VB6 dependencies
- Web Forms postback infrastructure

**Success Criteria:**

| Gate | Metric | Validation |
|------|--------|------------|
| API Compilation | 0 errors, 0 warnings | `dotnet build --configuration Release --warnaserror` |
| API Unit Tests | 100% pass rate | `dotnet test --configuration Release` |
| Angular Build | 0 errors, 0 warnings | `ng build --configuration production` |
| Angular Unit Tests | 100% pass rate | `ng test --watch=false --browsers=ChromeHeadless` |
| Container Build | Both images build | `docker-compose build` |
| Health Checks | HTTP 200 responses | `curl -f http://localhost:8080/health` |

### 0.1.3 Technical Interpretation

This refactoring translates to the following technical transformation strategy:

**Current Architecture (As-Is):**
```
┌─────────────────────────────────────────────────────┐
│            DotNetNuke 4.x Monolith                  │
│  ┌─────────────────┐  ┌─────────────────┐          │
│  │   Website/      │  │   Library/      │          │
│  │   (ASPX/ASCX)   │←─│   (VB.NET DLL)  │          │
│  │   WebForms UI   │  │   Business Logic│          │
│  └────────┬────────┘  └────────┬────────┘          │
│           │                    │                    │
│           └────────┬───────────┘                    │
│                    ▼                                │
│        ┌───────────────────────┐                   │
│        │   SqlDataProvider     │                   │
│        │   (ADO.NET/SqlHelper) │                   │
│        └───────────┬───────────┘                   │
│                    ▼                                │
│           [Stored Procedures]                       │
└─────────────────────────────────────────────────────┘
```

**Target Architecture (To-Be):**
```
┌─────────────────────────────────────────────────────┐
│                 Client Browser                       │
└───────────────────────┬─────────────────────────────┘
                        ▼
┌─────────────────────────────────────────────────────┐
│              Angular 19 SPA (nginx)                  │
│   Standalone Components | Signals | Reactive Forms   │
└───────────────────────┬─────────────────────────────┘
                        │ /api/*
                        ▼
┌─────────────────────────────────────────────────────┐
│           ASP.NET Core 8 BFF API (Kestrel)          │
│   Controllers → Services → Repositories → EF Core   │
└───────────────────────┬─────────────────────────────┘
                        ▼
┌─────────────────────────────────────────────────────┐
│            SQL Server Database                       │
│         (Existing Schema Preserved)                  │
└─────────────────────────────────────────────────────┘
```

**Key Domain Model Transformations:**

| Legacy Entity | Target Entity | Notes |
|--------------|---------------|-------|
| `PortalInfo` | `Portal` | Multi-tenant site container |
| `ModuleInfo` | `Module` | Pluggable content component |
| `UserInfo` | `User` | Identity with membership |
| `RoleInfo` | `Role` | Permission grouping |
| `TabInfo` | `Tab`/`Page` | Navigation hierarchy |


## 0.2 Source Analysis

### 0.2.1 Comprehensive Source File Discovery

The legacy DotNetNuke 4.x codebase consists of two primary directories that will be analyzed for migration:

**Repository Root Structure:**
```
DotNetNuke/
├── DotNetNuke.sln                    # VS 2005 Solution
├── DotNetNuke_VS2008.sln             # VS 2008 Solution
├── Library/                          # Core Framework (→ DotNetNuke.dll)
│   ├── Components/                   # Business Logic & Entities
│   │   ├── Portal/                   # Portal domain
│   │   ├── Modules/                  # Module system
│   │   ├── Users/                    # User management
│   │   ├── Security/                 # Roles & Permissions
│   │   ├── Tabs/                     # Navigation/Pages
│   │   ├── Providers/                # Provider abstractions
│   │   └── Shared/                   # Common utilities
│   ├── Providers/                    # Provider implementations
│   │   ├── DataProviders/            # Data access
│   │   ├── CachingProviders/         # Caching
│   │   ├── LoggingProviders/         # Logging
│   │   └── MembershipProviders/      # Authentication
│   ├── Controls/                     # UI Base Controls
│   ├── WebControls/                  # Web Control Library
│   └── HttpModules/                  # HTTP Pipeline
└── Website/                          # Web Application
    ├── Default.aspx.vb               # Application entry
    ├── admin/                         # Admin UI modules
    │   ├── Portal/                   # Portal management
    │   ├── Users/                    # User management
    │   ├── Modules/                  # Module management
    │   ├── Security/                 # Security management
    │   ├── Host/                     # Host-level settings
    │   └── Files/                    # File management
    ├── DesktopModules/               # Installed modules
    ├── Providers/                    # Provider configs & scripts
    └── App_Code/                     # Shared code files
```

### 0.2.2 Core Domain Components to Migrate

**Portal Domain (`Library/Components/Portal/`):**

| Source File | Purpose | Lines | Migration Priority |
|-------------|---------|-------|-------------------|
| `PortalInfo.vb` | Portal entity with 30+ properties | ~250 | Critical |
| `PortalController.vb` | Portal CRUD operations | ~800 | Critical |
| `PortalSettings.vb` | Portal configuration wrapper | ~400 | Critical |
| `PortalAliasController.vb` | Domain/alias management | ~200 | High |
| `PortalAliasInfo.vb` | Alias entity | ~50 | High |

**Key PortalInfo Properties Identified:**
- `PortalID`, `PortalName`, `LogoFile`, `FooterText`
- `ExpiryDate`, `UserRegistration`, `BannerAdvertising`
- `AdministratorId`, `Currency`, `HostFee`, `HostSpace`
- `PageQuota`, `UserQuota`, `AdministratorRoleId`, `RegisteredRoleId`
- `Description`, `KeyWords`, `BackgroundFile`, `GUID`
- `HomeTabId`, `LoginTabId`, `UserTabId`, `SplashTabId`
- `DefaultLanguage`, `TimeZoneOffset`, `HomeDirectory`

**Module Domain (`Library/Components/Modules/`):**

| Source File | Purpose | Lines | Migration Priority |
|-------------|---------|-------|-------------------|
| `ModuleInfo.vb` | Module instance entity | ~300 | Critical |
| `ModuleController.vb` | Module CRUD & lifecycle | ~1200 | Critical |
| `PortalModuleBase.vb` | Base class for modules | ~500 | Critical |
| `DesktopModuleController.vb` | Module definition management | ~400 | High |
| `ModuleDefinitionController.vb` | Definition CRUD | ~200 | High |

**User Domain (`Library/Components/Users/`):**

| Source File | Purpose | Lines | Migration Priority |
|-------------|---------|-------|-------------------|
| `UserInfo.vb` | User entity with profile | ~400 | Critical |
| `UserController.vb` | User CRUD operations | ~1000 | Critical |
| `UserMembership.vb` | Membership wrapper | ~150 | Critical |
| `UserRoleInfo.vb` | User-Role association | ~80 | Critical |
| `UserModuleBase.vb` | User module base | ~200 | Medium |

**Key UserInfo Properties Identified:**
- `UserID`, `Username`, `DisplayName`, `FullName`, `Email`
- `PortalID`, `IsSuperUser`, `AffiliateID`
- `Membership` (UserMembership object)
- `Profile` (UserProfile object)
- `Roles` (String array)

**Security Domain (`Library/Components/Security/`):**

| Source File | Purpose | Lines | Migration Priority |
|-------------|---------|-------|-------------------|
| `PortalSecurity.vb` | Security utilities | ~400 | High |
| `Roles/RoleController.vb` | Role management | ~500 | Critical |
| `Roles/RoleInfo.vb` | Role entity | ~150 | Critical |
| `Roles/RoleGroupInfo.vb` | Role grouping | ~80 | Medium |
| `Permissions/PermissionController.vb` | Permission management | ~400 | Critical |
| `Permissions/ModulePermission*.vb` | Module permissions | ~200 | Critical |
| `Permissions/TabPermission*.vb` | Tab permissions | ~200 | High |
| `Permissions/FolderPermission*.vb` | Folder permissions | ~200 | Medium |

**Tabs/Navigation Domain (`Library/Components/Tabs/`):**

| Source File | Purpose | Lines | Migration Priority |
|-------------|---------|-------|-------------------|
| `TabInfo.vb` | Page/Tab entity | ~350 | High |
| `TabController.vb` | Tab CRUD & hierarchy | ~900 | High |
| `Navigation.vb` | Navigation utilities | ~300 | Medium |

### 0.2.3 Data Access Layer Analysis

**SqlDataProvider (`Library/Providers/DataProviders/SqlDataProvider/`):**

| Source File | Purpose | Migration Approach |
|-------------|---------|-------------------|
| `SqlDataProvider.vb` | Main DAL implementation | Replace with EF Core DbContext |

**Key Data Access Patterns Identified:**

```vb
' Pattern 1: Stored Procedure Execution via SqlHelper
SqlHelper.ExecuteNonQuery(ConnectionString, DatabaseOwner & ObjectQualifier & ProcedureName, commandParameters)
SqlHelper.ExecuteReader(ConnectionString, DatabaseOwner & ObjectQualifier & ProcedureName, commandParameters)
SqlHelper.ExecuteScalar(ConnectionString, DatabaseOwner & ObjectQualifier & ProcedureName, commandParameters)

' Pattern 2: Dynamic SQL Execution
SqlHelper.ExecuteReader(ConnectionString, CommandType.Text, SQL, sqlCommandParameters)

' Pattern 3: Transaction Management
transaction.Commit()
transaction.Connection.Close()
```

**Stored Procedure Categories (from `Website/Providers/DataProviders/`):**

- Portal: `GetPortal`, `AddPortal`, `UpdatePortal`, `DeletePortal`, `GetPortals`
- Module: `GetModule`, `AddModule`, `UpdateModule`, `DeleteModule`, `GetModules`
- User: `GetUser`, `AddUser`, `UpdateUser`, `DeleteUser`, `GetUsers`
- Role: `GetRole`, `AddRole`, `UpdateRole`, `DeleteRole`, `GetRoles`
- Tab: `GetTab`, `AddTab`, `UpdateTab`, `DeleteTab`, `GetTabs`

### 0.2.4 Admin UI Components Analysis

**Portal Administration (`Website/admin/Portal/`):**

| Source File | Current Function | Target Angular Component |
|-------------|-----------------|-------------------------|
| `SiteSettings.ascx.vb` | Portal configuration | `PortalSettingsComponent` |
| `Portals.ascx.vb` | Portal list/management | `PortalListComponent` |
| `Signup.ascx.vb` | Portal creation wizard | `PortalCreateComponent` |

**User Administration (`Website/admin/Users/`):**

| Source File | Current Function | Target Angular Component |
|-------------|-----------------|-------------------------|
| `ManageUsers.ascx.vb` | User list with filtering | `UserListComponent` |
| `User.ascx.vb` | User detail/edit | `UserFormComponent` |
| `Membership.ascx.vb` | Membership settings | `MembershipSettingsComponent` |

**Module Administration (`Website/admin/Modules/`):**

| Source File | Current Function | Target Angular Component |
|-------------|-----------------|-------------------------|
| `ModuleSettings.ascx.vb` | Module configuration | `ModuleSettingsComponent` |
| `Export.ascx.vb` | Module export | `ModuleExportComponent` |
| `Import.ascx.vb` | Module import | `ModuleImportComponent` |

**Security Administration (`Website/admin/Security/`):**

| Source File | Current Function | Target Angular Component |
|-------------|-----------------|-------------------------|
| `Roles.ascx.vb` | Role list | `RoleListComponent` |
| `SecurityRoles.ascx.vb` | Role assignments | `RoleAssignmentComponent` |
| `EditRoles.ascx.vb` | Role edit form | `RoleFormComponent` |

### 0.2.5 VB.NET Constructs Identified for Conversion

Based on codebase analysis, the following VB.NET patterns require C# conversion:

| VB.NET Construct | Frequency | C# Equivalent |
|-----------------|-----------|---------------|
| `Dim x As Type` | High | `Type x` or `var x` |
| `Private _field As Type` | High | `private Type _field` |
| `Public Property X() As Type` | High | `public Type X { get; set; }` |
| `ByVal`/`ByRef` | High | (default)/`ref` |
| `Null.NullInteger` | High | `null` or `default(int?)` |
| `CType(obj, Type)` | Medium | `(Type)obj` or `as Type` |
| `Is Nothing` | High | `== null` or `is null` |
| `IsNot Nothing` | High | `!= null` or `is not null` |
| `AndAlso`/`OrElse` | Medium | `&&`/`||` |
| `&` (concatenation) | High | `+` or string interpolation |
| `Inherits` | Medium | `:` (inheritance) |
| `Implements` | Medium | `:` (interface) |
| `Namespace...End Namespace` | High | `namespace { }` |
| `#Region...#End Region` | High | `#region...#endregion` |


## 0.3 Target Design

### 0.3.1 Refactored Solution Structure

The target solution follows Clean Architecture principles with separate backend and frontend projects:

```
DnnMigration/
├── backend/
│   ├── src/
│   │   ├── DnnMigration.Domain/                 # Domain Layer
│   │   │   ├── Entities/
│   │   │   │   ├── Portal.cs
│   │   │   │   ├── Module.cs
│   │   │   │   ├── User.cs
│   │   │   │   ├── Role.cs
│   │   │   │   ├── Tab.cs
│   │   │   │   ├── Permission.cs
│   │   │   │   └── ...
│   │   │   ├── Interfaces/
│   │   │   │   ├── IPortalRepository.cs
│   │   │   │   ├── IModuleRepository.cs
│   │   │   │   ├── IUserRepository.cs
│   │   │   │   └── ...
│   │   │   ├── Enums/
│   │   │   │   ├── UserRegistrationType.cs
│   │   │   │   └── ...
│   │   │   └── DnnMigration.Domain.csproj
│   │   │
│   │   ├── DnnMigration.Application/            # Application Layer
│   │   │   ├── Services/
│   │   │   │   ├── PortalService.cs
│   │   │   │   ├── ModuleService.cs
│   │   │   │   ├── UserService.cs
│   │   │   │   └── ...
│   │   │   ├── DTOs/
│   │   │   │   ├── Portal/
│   │   │   │   │   ├── PortalDto.cs
│   │   │   │   │   ├── CreatePortalRequest.cs
│   │   │   │   │   └── UpdatePortalRequest.cs
│   │   │   │   ├── Module/
│   │   │   │   ├── User/
│   │   │   │   └── ...
│   │   │   ├── Interfaces/
│   │   │   │   ├── IPortalService.cs
│   │   │   │   └── ...
│   │   │   ├── Mapping/
│   │   │   │   └── MappingProfile.cs
│   │   │   └── DnnMigration.Application.csproj
│   │   │
│   │   ├── DnnMigration.Infrastructure/         # Infrastructure Layer
│   │   │   ├── Data/
│   │   │   │   ├── DnnDbContext.cs
│   │   │   │   ├── Configurations/
│   │   │   │   │   ├── PortalConfiguration.cs
│   │   │   │   │   ├── ModuleConfiguration.cs
│   │   │   │   │   ├── UserConfiguration.cs
│   │   │   │   │   └── ...
│   │   │   │   └── Migrations/
│   │   │   ├── Repositories/
│   │   │   │   ├── PortalRepository.cs
│   │   │   │   ├── ModuleRepository.cs
│   │   │   │   ├── UserRepository.cs
│   │   │   │   └── ...
│   │   │   ├── Identity/
│   │   │   │   └── JwtService.cs
│   │   │   └── DnnMigration.Infrastructure.csproj
│   │   │
│   │   └── DnnMigration.Api/                    # API Layer (BFF)
│   │       ├── Controllers/
│   │       │   ├── PortalsController.cs
│   │       │   ├── ModulesController.cs
│   │       │   ├── UsersController.cs
│   │       │   ├── RolesController.cs
│   │       │   └── AuthController.cs
│   │       ├── Middleware/
│   │       │   └── ExceptionHandlingMiddleware.cs
│   │       ├── appsettings.json
│   │       ├── appsettings.Development.json
│   │       ├── Program.cs
│   │       └── DnnMigration.Api.csproj
│   │
│   ├── tests/
│   │   ├── DnnMigration.UnitTests/
│   │   │   ├── Services/
│   │   │   └── DnnMigration.UnitTests.csproj
│   │   └── DnnMigration.IntegrationTests/
│   │       ├── ApiTests/
│   │       └── DnnMigration.IntegrationTests.csproj
│   │
│   └── DnnMigration.sln
│
├── frontend/
│   ├── src/
│   │   ├── app/
│   │   │   ├── core/                            # Core Module
│   │   │   │   ├── auth/
│   │   │   │   │   ├── auth.service.ts
│   │   │   │   │   ├── auth.guard.ts
│   │   │   │   │   └── auth.interceptor.ts
│   │   │   │   ├── services/
│   │   │   │   │   └── api.service.ts
│   │   │   │   └── models/
│   │   │   │       └── user.model.ts
│   │   │   │
│   │   │   ├── shared/                          # Shared Module
│   │   │   │   ├── components/
│   │   │   │   │   ├── data-table/
│   │   │   │   │   ├── form-controls/
│   │   │   │   │   ├── confirmation-dialog/
│   │   │   │   │   └── loading-spinner/
│   │   │   │   ├── pipes/
│   │   │   │   └── directives/
│   │   │   │
│   │   │   ├── features/                        # Feature Modules
│   │   │   │   ├── portal/
│   │   │   │   │   ├── components/
│   │   │   │   │   │   ├── portal-list/
│   │   │   │   │   │   ├── portal-form/
│   │   │   │   │   │   └── portal-settings/
│   │   │   │   │   ├── services/
│   │   │   │   │   │   └── portal.service.ts
│   │   │   │   │   ├── models/
│   │   │   │   │   │   └── portal.model.ts
│   │   │   │   │   └── portal.routes.ts
│   │   │   │   │
│   │   │   │   ├── module/
│   │   │   │   │   ├── components/
│   │   │   │   │   │   ├── module-list/
│   │   │   │   │   │   ├── module-form/
│   │   │   │   │   │   └── module-settings/
│   │   │   │   │   ├── services/
│   │   │   │   │   ├── models/
│   │   │   │   │   └── module.routes.ts
│   │   │   │   │
│   │   │   │   ├── user/
│   │   │   │   │   ├── components/
│   │   │   │   │   │   ├── user-list/
│   │   │   │   │   │   ├── user-form/
│   │   │   │   │   │   └── user-profile/
│   │   │   │   │   ├── services/
│   │   │   │   │   ├── models/
│   │   │   │   │   └── user.routes.ts
│   │   │   │   │
│   │   │   │   ├── role/
│   │   │   │   │   ├── components/
│   │   │   │   │   ├── services/
│   │   │   │   │   ├── models/
│   │   │   │   │   └── role.routes.ts
│   │   │   │   │
│   │   │   │   └── auth/
│   │   │   │       ├── login/
│   │   │   │       └── auth.routes.ts
│   │   │   │
│   │   │   ├── layout/
│   │   │   │   ├── header/
│   │   │   │   ├── sidebar/
│   │   │   │   └── footer/
│   │   │   │
│   │   │   ├── app.component.ts
│   │   │   ├── app.config.ts
│   │   │   └── app.routes.ts
│   │   │
│   │   ├── assets/
│   │   ├── environments/
│   │   │   ├── environment.ts
│   │   │   └── environment.prod.ts
│   │   ├── index.html
│   │   ├── main.ts
│   │   └── styles.scss
│   │
│   ├── angular.json
│   ├── package.json
│   ├── tsconfig.json
│   └── karma.conf.js
│
├── docker/
│   ├── api.Dockerfile
│   ├── frontend.Dockerfile
│   ├── nginx.conf
│   └── docker-compose.yml
│
├── MIGRATION_NOTES.md
└── README.md
```

### 0.3.2 Web Search Research Conducted

Research was conducted to inform best practices for this migration:

**VB.NET to C# Migration Best Practices:**
- Microsoft has confirmed VB.NET will receive only security updates, with no new language features
- For WebForms to modern stack, manual rewrite is required as no automated tools exist
- Converting to C# enables full .NET Core/8 capabilities including Linux deployment and cross-platform support
- Semantic analysis tools can assist with complex conversions like parameterized properties and loop iteration patterns

**Angular 19 Standalone Components:**
- Angular 19 makes `standalone: true` the default for all components, directives, and pipes
- Benefits include simplified structure, improved maintainability, and reduced boilerplate
- Best practices: organize by feature, use barrel files, minimize imports, leverage lazy loading with `loadComponent`
- Use the `inject()` function for dependency injection instead of constructor injection
- Apply control flow syntax (`@if`, `@for`, `@switch`) in templates

**Entity Framework Core 8 Migration:**
- Code-First with Fluent API recommended for mapping to existing schema
- Use `AsNoTracking()` for read-only queries to improve performance
- Implement repository pattern for separation of concerns
- Manually review migrations before production deployment
- DTOs recommended for API projections instead of exposing domain entities

**BFF Pattern Implementation:**
- The BFF pattern is recognized as the current OAuth working group best practice
- Key components: Session Management (handles OIDC flow) and API Proxy (secure gateway)
- Eliminates browser token storage, uses HTTP-only cookies
- Centralizes authentication in ASP.NET Core
- Protects against CSRF with proper anti-forgery implementation

### 0.3.3 Design Pattern Applications

**Repository Pattern:**
```csharp
public interface IPortalRepository
{
    Task<Portal?> GetByIdAsync(int id);
    Task<IEnumerable<Portal>> GetAllAsync();
    Task<Portal> AddAsync(Portal portal);
    Task UpdateAsync(Portal portal);
    Task DeleteAsync(int id);
}
```

**Service Layer Pattern:**
```csharp
public interface IPortalService
{
    Task<PortalDto?> GetPortalAsync(int id);
    Task<PagedResult<PortalDto>> GetPortalsAsync(int pageIndex);
    Task<PortalDto> CreatePortalAsync(CreatePortalRequest request);
    Task<PortalDto> UpdatePortalAsync(int id, UpdatePortalRequest request);
    Task DeletePortalAsync(int id);
}
```

**Dependency Injection:**
```csharp
// Program.cs
builder.Services.AddScoped<IPortalRepository, PortalRepository>();
builder.Services.AddScoped<IPortalService, PortalService>();
builder.Services.AddDbContext<DnnDbContext>(options =>
    options.UseSqlServer(connectionString));
```

### 0.3.4 API Endpoint Design

**Portal API Endpoints:**

| Method | Endpoint | Request Body | Response | Purpose |
|--------|----------|--------------|----------|---------|
| GET | `/api/portals` | - | `PagedResult<PortalDto>` | List portals |
| GET | `/api/portals/{id}` | - | `PortalDto` | Get portal by ID |
| POST | `/api/portals` | `CreatePortalRequest` | `PortalDto` (201) | Create portal |
| PUT | `/api/portals/{id}` | `UpdatePortalRequest` | `PortalDto` | Update portal |
| DELETE | `/api/portals/{id}` | - | 204 No Content | Delete portal |

**Module API Endpoints:**

| Method | Endpoint | Request Body | Response | Purpose |
|--------|----------|--------------|----------|---------|
| GET | `/api/modules` | - | `PagedResult<ModuleDto>` | List modules |
| GET | `/api/modules/{id}` | - | `ModuleDto` | Get module by ID |
| POST | `/api/modules` | `CreateModuleRequest` | `ModuleDto` (201) | Create module |
| PUT | `/api/modules/{id}` | `UpdateModuleRequest` | `ModuleDto` | Update module |
| DELETE | `/api/modules/{id}` | - | 204 No Content | Delete module |

**User API Endpoints:**

| Method | Endpoint | Request Body | Response | Purpose |
|--------|----------|--------------|----------|---------|
| GET | `/api/users` | - | `PagedResult<UserDto>` | List users |
| GET | `/api/users/{id}` | - | `UserDto` | Get user by ID |
| POST | `/api/users` | `CreateUserRequest` | `UserDto` (201) | Create user |
| PUT | `/api/users/{id}` | `UpdateUserRequest` | `UserDto` | Update user |
| DELETE | `/api/users/{id}` | - | 204 No Content | Delete user |

**Authentication API Endpoints:**

| Method | Endpoint | Request Body | Response | Purpose |
|--------|----------|--------------|----------|---------|
| POST | `/api/auth/login` | `LoginRequest` | `AuthResponse` | Authenticate user |
| POST | `/api/auth/refresh` | `RefreshRequest` | `AuthResponse` | Refresh tokens |
| POST | `/api/auth/logout` | - | 204 No Content | Logout user |
| GET | `/api/auth/me` | - | `UserDto` | Get current user |

### 0.3.5 Entity Framework Core Model Design

**Portal Entity Configuration:**
```csharp
public class PortalConfiguration : IEntityTypeConfiguration<Portal>
{
    public void Configure(EntityTypeBuilder<Portal> builder)
    {
        builder.ToTable("Portals");
        builder.HasKey(p => p.PortalId);
        
        builder.Property(p => p.PortalName)
            .HasMaxLength(128).IsRequired();
        
        builder.HasOne(p => p.Administrator)
            .WithMany().HasForeignKey(p => p.AdministratorId);
        
        builder.HasMany(p => p.Users)
            .WithOne(u => u.Portal)
            .HasForeignKey(u => u.PortalId);
    }
}
```

### 0.3.6 Angular Component Architecture

**Standalone Component Pattern:**
```typescript
@Component({
  selector: 'app-portal-list',
  standalone: true,
  imports: [CommonModule, RouterModule, DataTableComponent],
  template: `
    @if (loading()) {
      <app-loading-spinner />
    } @else {
      <app-data-table 
        [data]="portals()" 
        [columns]="columns"
        (rowClick)="onRowClick($event)" />
    }
  `
})
export class PortalListComponent {
  private portalService = inject(PortalService);
  
  portals = signal<Portal[]>([]);
  loading = signal(true);
}
```

**Lazy Loading Route Configuration:**
```typescript
export const routes: Routes = [
  { path: '', redirectTo: 'portals', pathMatch: 'full' },
  {
    path: 'portals',
    loadComponent: () => import('./features/portal/components/portal-list')
      .then(m => m.PortalListComponent)
  },
  {
    path: 'portals/:id',
    loadComponent: () => import('./features/portal/components/portal-form')
      .then(m => m.PortalFormComponent)
  }
];
```


## 0.4 Transformation Mapping

### 0.4.1 File-by-File Transformation Plan

This section provides exhaustive source-to-target file mappings for the complete migration. The transformation modes are:

- **UPDATE** - Modify an existing file
- **CREATE** - Create a new file from scratch
- **REFERENCE** - Use source file as reference for patterns and structure

**Backend Domain Layer (`DnnMigration.Domain/`):**

| Target File | Transformation | Source File | Key Changes |
|------------|----------------|-------------|-------------|
| `Entities/Portal.cs` | CREATE | `Library/Components/Portal/PortalInfo.vb` | VB→C#, properties to auto-properties, nullable types |
| `Entities/PortalAlias.cs` | CREATE | `Library/Components/Portal/PortalAliasInfo.vb` | VB→C#, entity configuration |
| `Entities/Module.cs` | CREATE | `Library/Components/Modules/ModuleInfo.vb` | VB→C#, remove Web Forms dependencies |
| `Entities/ModuleDefinition.cs` | CREATE | `Library/Components/Modules/ModuleDefinitionInfo.vb` | VB→C#, simplify for CRUD |
| `Entities/DesktopModule.cs` | CREATE | `Library/Components/Modules/DesktopModuleInfo.vb` | VB→C#, flat structure |
| `Entities/User.cs` | CREATE | `Library/Components/Users/UserInfo.vb` | VB→C#, integrate membership |
| `Entities/UserProfile.cs` | CREATE | `Library/Components/Users/UserProfile.vb` | VB→C#, profile properties |
| `Entities/Role.cs` | CREATE | `Library/Components/Security/Roles/RoleInfo.vb` | VB→C#, role entity |
| `Entities/RoleGroup.cs` | CREATE | `Library/Components/Security/Roles/RoleGroupInfo.vb` | VB→C#, role grouping |
| `Entities/UserRole.cs` | CREATE | `Library/Components/Users/UserRoleInfo.vb` | VB→C#, many-to-many |
| `Entities/Tab.cs` | CREATE | `Library/Components/Tabs/TabInfo.vb` | VB→C#, page hierarchy |
| `Entities/Permission.cs` | CREATE | `Library/Components/Security/Permissions/PermissionInfo.vb` | VB→C#, permission entity |
| `Entities/ModulePermission.cs` | CREATE | `Library/Components/Security/Permissions/ModulePermissionInfo.vb` | VB→C#, module-level permissions |
| `Entities/TabPermission.cs` | CREATE | `Library/Components/Security/Permissions/TabPermissionInfo.vb` | VB→C#, tab-level permissions |
| `Entities/FolderPermission.cs` | CREATE | `Library/Components/Security/Permissions/FolderPermissionInfo.vb` | VB→C#, folder-level permissions |
| `Interfaces/IPortalRepository.cs` | CREATE | `Library/Components/Portal/PortalController.vb` | Extract interface from controller |
| `Interfaces/IModuleRepository.cs` | CREATE | `Library/Components/Modules/ModuleController.vb` | Extract interface from controller |
| `Interfaces/IUserRepository.cs` | CREATE | `Library/Components/Users/UserController.vb` | Extract interface from controller |
| `Interfaces/IRoleRepository.cs` | CREATE | `Library/Components/Security/Roles/RoleController.vb` | Extract interface from controller |
| `Interfaces/ITabRepository.cs` | CREATE | `Library/Components/Tabs/TabController.vb` | Extract interface from controller |
| `Enums/UserRegistrationType.cs` | CREATE | `Library/Components/Portal/PortalInfo.vb` | Extract registration enum |
| `Enums/BannerType.cs` | CREATE | `Library/Components/Portal/PortalInfo.vb` | Extract banner enum |
| `DnnMigration.Domain.csproj` | CREATE | N/A | New SDK-style project file |

**Backend Application Layer (`DnnMigration.Application/`):**

| Target File | Transformation | Source File | Key Changes |
|------------|----------------|-------------|-------------|
| `Services/PortalService.cs` | CREATE | `Library/Components/Portal/PortalController.vb` | Extract business logic, async/await |
| `Services/ModuleService.cs` | CREATE | `Library/Components/Modules/ModuleController.vb` | Extract business logic, async/await |
| `Services/UserService.cs` | CREATE | `Library/Components/Users/UserController.vb` | Extract business logic, async/await |
| `Services/RoleService.cs` | CREATE | `Library/Components/Security/Roles/RoleController.vb` | Extract business logic, async/await |
| `Services/TabService.cs` | CREATE | `Library/Components/Tabs/TabController.vb` | Extract business logic, async/await |
| `Services/PermissionService.cs` | CREATE | `Library/Components/Security/Permissions/PermissionController.vb` | Extract business logic |
| `DTOs/Portal/PortalDto.cs` | CREATE | `Library/Components/Portal/PortalInfo.vb` | API response DTO |
| `DTOs/Portal/CreatePortalRequest.cs` | CREATE | `Website/admin/Portal/Signup.ascx.vb` | Extract form fields |
| `DTOs/Portal/UpdatePortalRequest.cs` | CREATE | `Website/admin/Portal/SiteSettings.ascx.vb` | Extract update fields |
| `DTOs/Module/ModuleDto.cs` | CREATE | `Library/Components/Modules/ModuleInfo.vb` | API response DTO |
| `DTOs/Module/CreateModuleRequest.cs` | CREATE | N/A | New module creation |
| `DTOs/Module/UpdateModuleRequest.cs` | CREATE | `Website/admin/Modules/ModuleSettings.ascx.vb` | Extract settings |
| `DTOs/User/UserDto.cs` | CREATE | `Library/Components/Users/UserInfo.vb` | API response DTO |
| `DTOs/User/CreateUserRequest.cs` | CREATE | `Website/admin/Users/User.ascx.vb` | Extract form fields |
| `DTOs/User/UpdateUserRequest.cs` | CREATE | `Website/admin/Users/User.ascx.vb` | Extract update fields |
| `DTOs/Role/RoleDto.cs` | CREATE | `Library/Components/Security/Roles/RoleInfo.vb` | API response DTO |
| `DTOs/Role/CreateRoleRequest.cs` | CREATE | `Website/admin/Security/EditRoles.ascx.vb` | Extract form fields |
| `DTOs/Auth/LoginRequest.cs` | CREATE | N/A | JWT authentication |
| `DTOs/Auth/AuthResponse.cs` | CREATE | N/A | JWT response |
| `DTOs/Common/PagedResult.cs` | CREATE | N/A | Pagination wrapper |
| `Interfaces/IPortalService.cs` | CREATE | N/A | Service interface |
| `Interfaces/IModuleService.cs` | CREATE | N/A | Service interface |
| `Interfaces/IUserService.cs` | CREATE | N/A | Service interface |
| `Interfaces/IRoleService.cs` | CREATE | N/A | Service interface |
| `Mapping/MappingProfile.cs` | CREATE | N/A | AutoMapper configuration |
| `DnnMigration.Application.csproj` | CREATE | N/A | New SDK-style project file |

**Backend Infrastructure Layer (`DnnMigration.Infrastructure/`):**

| Target File | Transformation | Source File | Key Changes |
|------------|----------------|-------------|-------------|
| `Data/DnnDbContext.cs` | CREATE | `Library/Providers/DataProviders/SqlDataProvider/SqlDataProvider.vb` | EF Core DbContext replacing SqlHelper |
| `Data/Configurations/PortalConfiguration.cs` | CREATE | N/A | Fluent API for Portal table |
| `Data/Configurations/ModuleConfiguration.cs` | CREATE | N/A | Fluent API for Module tables |
| `Data/Configurations/UserConfiguration.cs` | CREATE | N/A | Fluent API for User table |
| `Data/Configurations/RoleConfiguration.cs` | CREATE | N/A | Fluent API for Role table |
| `Data/Configurations/TabConfiguration.cs` | CREATE | N/A | Fluent API for Tab table |
| `Data/Configurations/PermissionConfiguration.cs` | CREATE | N/A | Fluent API for Permissions |
| `Repositories/PortalRepository.cs` | CREATE | `Library/Components/Portal/PortalController.vb` | Replace SqlHelper with EF Core |
| `Repositories/ModuleRepository.cs` | CREATE | `Library/Components/Modules/ModuleController.vb` | Replace SqlHelper with EF Core |
| `Repositories/UserRepository.cs` | CREATE | `Library/Components/Users/UserController.vb` | Replace SqlHelper with EF Core |
| `Repositories/RoleRepository.cs` | CREATE | `Library/Components/Security/Roles/RoleController.vb` | Replace SqlHelper with EF Core |
| `Repositories/TabRepository.cs` | CREATE | `Library/Components/Tabs/TabController.vb` | Replace SqlHelper with EF Core |
| `Identity/JwtService.cs` | CREATE | N/A | JWT token generation |
| `Identity/PasswordHasher.cs` | CREATE | `Library/Components/Security/PortalSecurity.vb` | Password hashing logic |
| `DnnMigration.Infrastructure.csproj` | CREATE | N/A | New SDK-style project file |

**Backend API Layer (`DnnMigration.Api/`):**

| Target File | Transformation | Source File | Key Changes |
|------------|----------------|-------------|-------------|
| `Controllers/PortalsController.cs` | CREATE | `Website/admin/Portal/SiteSettings.ascx.vb` | REST API endpoints |
| `Controllers/ModulesController.cs` | CREATE | `Website/admin/Modules/ModuleSettings.ascx.vb` | REST API endpoints |
| `Controllers/UsersController.cs` | CREATE | `Website/admin/Users/ManageUsers.ascx.vb` | REST API endpoints |
| `Controllers/RolesController.cs` | CREATE | `Website/admin/Security/Roles.ascx.vb` | REST API endpoints |
| `Controllers/TabsController.cs` | CREATE | N/A | REST API endpoints |
| `Controllers/AuthController.cs` | CREATE | N/A | Authentication endpoints |
| `Controllers/HealthController.cs` | CREATE | N/A | Health check endpoint |
| `Middleware/ExceptionHandlingMiddleware.cs` | CREATE | N/A | Global error handling |
| `Middleware/RequestLoggingMiddleware.cs` | CREATE | N/A | Request logging |
| `Program.cs` | CREATE | N/A | Application entry point |
| `appsettings.json` | CREATE | `Website/web.config` | Configuration migration |
| `appsettings.Development.json` | CREATE | N/A | Dev configuration |
| `DnnMigration.Api.csproj` | CREATE | N/A | New SDK-style project file |

### 0.4.2 Frontend Transformation Mapping

**Angular Core (`frontend/src/app/core/`):**

| Target File | Transformation | Source File | Key Changes |
|------------|----------------|-------------|-------------|
| `auth/auth.service.ts` | CREATE | N/A | JWT authentication service |
| `auth/auth.guard.ts` | CREATE | N/A | Route protection |
| `auth/auth.interceptor.ts` | CREATE | N/A | Token injection |
| `services/api.service.ts` | CREATE | N/A | Base HTTP service |
| `models/user.model.ts` | CREATE | `Library/Components/Users/UserInfo.vb` | TypeScript interface |
| `models/auth.model.ts` | CREATE | N/A | Auth models |

**Angular Shared (`frontend/src/app/shared/`):**

| Target File | Transformation | Source File | Key Changes |
|------------|----------------|-------------|-------------|
| `components/data-table/data-table.component.ts` | CREATE | N/A | Reusable table component |
| `components/form-controls/form-controls.component.ts` | CREATE | N/A | Form input components |
| `components/confirmation-dialog/confirmation-dialog.component.ts` | CREATE | N/A | Delete confirmation |
| `components/loading-spinner/loading-spinner.component.ts` | CREATE | N/A | Loading indicator |
| `pipes/date-format.pipe.ts` | CREATE | N/A | Date formatting |

**Angular Portal Feature (`frontend/src/app/features/portal/`):**

| Target File | Transformation | Source File | Key Changes |
|------------|----------------|-------------|-------------|
| `components/portal-list/portal-list.component.ts` | CREATE | `Website/admin/Portal/Portals.ascx.vb` | Portal grid display |
| `components/portal-form/portal-form.component.ts` | CREATE | `Website/admin/Portal/SiteSettings.ascx.vb` | Portal edit form |
| `components/portal-settings/portal-settings.component.ts` | CREATE | `Website/admin/Portal/SiteSettings.ascx.vb` | Portal configuration |
| `services/portal.service.ts` | CREATE | N/A | Portal API service |
| `models/portal.model.ts` | CREATE | `Library/Components/Portal/PortalInfo.vb` | TypeScript interface |
| `portal.routes.ts` | CREATE | N/A | Portal routing |

**Angular Module Feature (`frontend/src/app/features/module/`):**

| Target File | Transformation | Source File | Key Changes |
|------------|----------------|-------------|-------------|
| `components/module-list/module-list.component.ts` | CREATE | `Website/admin/Modules/` | Module grid |
| `components/module-form/module-form.component.ts` | CREATE | `Website/admin/Modules/ModuleSettings.ascx.vb` | Module form |
| `components/module-settings/module-settings.component.ts` | CREATE | `Website/admin/Modules/ModuleSettings.ascx.vb` | Module config |
| `services/module.service.ts` | CREATE | N/A | Module API service |
| `models/module.model.ts` | CREATE | `Library/Components/Modules/ModuleInfo.vb` | TypeScript interface |
| `module.routes.ts` | CREATE | N/A | Module routing |

**Angular User Feature (`frontend/src/app/features/user/`):**

| Target File | Transformation | Source File | Key Changes |
|------------|----------------|-------------|-------------|
| `components/user-list/user-list.component.ts` | CREATE | `Website/admin/Users/ManageUsers.ascx.vb` | User grid with filters |
| `components/user-form/user-form.component.ts` | CREATE | `Website/admin/Users/User.ascx.vb` | User edit form |
| `components/user-profile/user-profile.component.ts` | CREATE | `Website/admin/Users/Membership.ascx.vb` | Profile management |
| `services/user.service.ts` | CREATE | N/A | User API service |
| `models/user.model.ts` | CREATE | `Library/Components/Users/UserInfo.vb` | TypeScript interface |
| `user.routes.ts` | CREATE | N/A | User routing |

**Angular Role Feature (`frontend/src/app/features/role/`):**

| Target File | Transformation | Source File | Key Changes |
|------------|----------------|-------------|-------------|
| `components/role-list/role-list.component.ts` | CREATE | `Website/admin/Security/Roles.ascx.vb` | Role grid |
| `components/role-form/role-form.component.ts` | CREATE | `Website/admin/Security/EditRoles.ascx.vb` | Role form |
| `components/role-assignment/role-assignment.component.ts` | CREATE | `Website/admin/Security/SecurityRoles.ascx.vb` | User-role mapping |
| `services/role.service.ts` | CREATE | N/A | Role API service |
| `models/role.model.ts` | CREATE | `Library/Components/Security/Roles/RoleInfo.vb` | TypeScript interface |
| `role.routes.ts` | CREATE | N/A | Role routing |

**Angular Auth Feature (`frontend/src/app/features/auth/`):**

| Target File | Transformation | Source File | Key Changes |
|------------|----------------|-------------|-------------|
| `login/login.component.ts` | CREATE | N/A | Login page |
| `auth.routes.ts` | CREATE | N/A | Auth routing |

### 0.4.3 Infrastructure and Configuration Files

**Docker Configuration (`docker/`):**

| Target File | Transformation | Source File | Key Changes |
|------------|----------------|-------------|-------------|
| `api.Dockerfile` | CREATE | N/A | Multi-stage .NET 8 build |
| `frontend.Dockerfile` | CREATE | N/A | Multi-stage Angular build |
| `nginx.conf` | CREATE | N/A | SPA routing + API proxy |
| `docker-compose.yml` | CREATE | N/A | Container orchestration |

**Root Configuration:**

| Target File | Transformation | Source File | Key Changes |
|------------|----------------|-------------|-------------|
| `README.md` | CREATE | N/A | Project documentation |
| `MIGRATION_NOTES.md` | CREATE | N/A | Migration decisions log |

### 0.4.4 Cross-File Dependencies and Import Updates

**Entity Relationship Imports:**

| Entity | Related Entities | Import Pattern |
|--------|-----------------|----------------|
| Portal | User, Role, Tab, Module | Navigation properties with lazy loading |
| Module | Tab, ModulePermission | Foreign key relationships |
| User | Portal, Role, UserRole | Many-to-many through UserRole |
| Role | Portal, Permission, UserRole | Many-to-many through UserRole |
| Tab | Portal, Module, TabPermission | Hierarchical parent-child |

**Angular Service Imports:**

```typescript
// features/portal/services/portal.service.ts
import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Portal, CreatePortalRequest, UpdatePortalRequest } from '../models';
```

### 0.4.5 One-Phase Execution

The entire migration will be executed by Blitzy in **ONE phase**. All files listed above are included in this single execution phase. There are no multi-phase dependencies or sequential ordering requirements beyond standard compilation order.


## 0.5 Dependency Inventory

### 0.5.1 Key Private and Public Packages

**Backend NuGet Packages (.NET 8):**

| Package | Registry | Version | Purpose |
|---------|----------|---------|---------|
| `Microsoft.AspNetCore.Authentication.JwtBearer` | NuGet | 8.0.11 | JWT Bearer authentication |
| `Microsoft.EntityFrameworkCore` | NuGet | 8.0.11 | ORM framework |
| `Microsoft.EntityFrameworkCore.SqlServer` | NuGet | 8.0.11 | SQL Server provider |
| `Microsoft.EntityFrameworkCore.Design` | NuGet | 8.0.11 | Design-time tools |
| `Microsoft.EntityFrameworkCore.Tools` | NuGet | 8.0.11 | CLI tools |
| `Swashbuckle.AspNetCore` | NuGet | 6.9.0 | OpenAPI/Swagger |
| `AutoMapper.Extensions.Microsoft.DependencyInjection` | NuGet | 12.0.1 | Object mapping |
| `FluentValidation.AspNetCore` | NuGet | 11.3.0 | Input validation |
| `Serilog.AspNetCore` | NuGet | 8.0.3 | Structured logging |
| `Serilog.Sinks.Console` | NuGet | 6.0.0 | Console logging |
| `Microsoft.AspNetCore.OpenApi` | NuGet | 8.0.11 | OpenAPI integration |
| `BCrypt.Net-Next` | NuGet | 4.0.3 | Password hashing |
| `xunit` | NuGet | 2.9.2 | Unit testing |
| `xunit.runner.visualstudio` | NuGet | 2.8.2 | Test runner |
| `Moq` | NuGet | 4.20.72 | Mocking framework |
| `FluentAssertions` | NuGet | 6.12.2 | Test assertions |
| `Microsoft.AspNetCore.Mvc.Testing` | NuGet | 8.0.11 | Integration testing |
| `Microsoft.EntityFrameworkCore.InMemory` | NuGet | 8.0.11 | In-memory testing |

**Frontend npm Packages (Angular 19):**

| Package | Registry | Version | Purpose |
|---------|----------|---------|---------|
| `@angular/core` | npm | ^19.0.0 | Angular framework |
| `@angular/common` | npm | ^19.0.0 | Common utilities |
| `@angular/compiler` | npm | ^19.0.0 | Template compiler |
| `@angular/platform-browser` | npm | ^19.0.0 | Browser platform |
| `@angular/platform-browser-dynamic` | npm | ^19.0.0 | Dynamic compilation |
| `@angular/router` | npm | ^19.0.0 | Client-side routing |
| `@angular/forms` | npm | ^19.0.0 | Reactive forms |
| `@angular/animations` | npm | ^19.0.0 | Animation support |
| `rxjs` | npm | ^7.8.1 | Reactive extensions |
| `zone.js` | npm | ^0.15.0 | Change detection |
| `tslib` | npm | ^2.8.0 | TypeScript helpers |

**Frontend Dev Dependencies:**

| Package | Registry | Version | Purpose |
|---------|----------|---------|---------|
| `@angular/cli` | npm | ^19.0.0 | CLI tools |
| `@angular/compiler-cli` | npm | ^19.0.0 | AOT compilation |
| `@angular-devkit/build-angular` | npm | ^19.0.0 | Build system |
| `typescript` | npm | ^5.6.0 | TypeScript compiler |
| `karma` | npm | ^6.4.4 | Test runner |
| `karma-chrome-launcher` | npm | ^3.2.0 | Chrome launcher |
| `karma-coverage` | npm | ^2.2.1 | Code coverage |
| `karma-jasmine` | npm | ^5.1.0 | Jasmine integration |
| `karma-jasmine-html-reporter` | npm | ^2.1.0 | HTML reporter |
| `jasmine-core` | npm | ^5.4.0 | Testing framework |
| `@types/jasmine` | npm | ^5.1.4 | Jasmine types |

### 0.5.2 Legacy Dependencies Being Removed

The following legacy dependencies from the source codebase will NOT be migrated:

| Legacy Dependency | Replacement | Reason |
|-------------------|-------------|--------|
| `Microsoft.ApplicationBlocks.Data` (SqlHelper) | Entity Framework Core | Modern ORM with LINQ |
| `System.Data.SqlClient` | `Microsoft.Data.SqlClient` | Updated SQL client |
| `System.Web` | `Microsoft.AspNetCore.*` | Modern web framework |
| `System.Web.UI` | Angular components | SPA architecture |
| `System.Web.Security` | ASP.NET Core Identity | Modern auth |
| Telerik RadControls | N/A | Not migrated (excluded) |
| DNN Module Framework | Custom implementation | Simplified module system |

### 0.5.3 Import Refactoring

**Backend Namespace Transformations:**

| Legacy Namespace | Target Namespace |
|-----------------|------------------|
| `DotNetNuke.Entities.Portals` | `DnnMigration.Domain.Entities` |
| `DotNetNuke.Entities.Modules` | `DnnMigration.Domain.Entities` |
| `DotNetNuke.Entities.Users` | `DnnMigration.Domain.Entities` |
| `DotNetNuke.Security.Roles` | `DnnMigration.Domain.Entities` |
| `DotNetNuke.Entities.Tabs` | `DnnMigration.Domain.Entities` |
| `DotNetNuke.Data` | `DnnMigration.Infrastructure.Data` |
| `DotNetNuke.Common.Utilities` | `DnnMigration.Domain.Enums` / Built-in |
| `DotNetNuke.Services.*` | `DnnMigration.Application.Services` |

**Files Requiring Import Updates:**

All files in the target solution will use new namespace conventions. The import transformation affects:

- `backend/src/**/*.cs` - All C# files use new namespaces
- `frontend/src/**/*.ts` - All TypeScript files use Angular imports

### 0.5.4 Configuration File Transformations

**Legacy `web.config` → `appsettings.json`:**

| Legacy Setting | Target Setting | Location |
|----------------|---------------|----------|
| `<connectionStrings>` | `ConnectionStrings:Default` | `appsettings.json` |
| `<appSettings>` | Custom configuration sections | `appsettings.json` |
| `<authentication mode="Forms">` | JWT Bearer configuration | `Program.cs` |
| `<machineKey>` | Data Protection keys | `Program.cs` |
| Provider configuration | DI registration | `Program.cs` |

**Target `appsettings.json` Structure:**

```json
{
  "ConnectionStrings": {
    "Default": "Server=...;Database=DotNetNuke;..."
  },
  "Jwt": {
    "Secret": "...",
    "Issuer": "DnnMigration",
    "Audience": "DnnMigration",
    "ExpirationMinutes": 60
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  }
}
```

### 0.5.5 External Reference Updates

**Documentation Files:**

| File Pattern | Update Required |
|--------------|-----------------|
| `README.md` | New project documentation |
| `MIGRATION_NOTES.md` | Migration decisions and patterns |
| `docs/**/*.md` | API documentation (auto-generated) |

**Build Files:**

| File | Purpose |
|------|---------|
| `backend/DnnMigration.sln` | .NET solution file |
| `backend/**/*.csproj` | SDK-style project files |
| `frontend/angular.json` | Angular CLI configuration |
| `frontend/package.json` | npm dependencies |
| `frontend/tsconfig.json` | TypeScript configuration |

**CI/CD Files:**

| File Pattern | Purpose |
|--------------|---------|
| `.github/workflows/*.yml` | GitHub Actions (if used) |
| `.gitlab-ci.yml` | GitLab CI (if used) |
| `docker-compose.yml` | Local development |
| `docker/*.Dockerfile` | Container builds |


## 0.6 Scope Boundaries

### 0.6.1 Exhaustively In Scope

**Backend Source Transformations:**

| Pattern | Description | Priority |
|---------|-------------|----------|
| `Library/Components/Portal/**/*.vb` | Portal domain entities and logic | Critical |
| `Library/Components/Modules/**/*.vb` | Module system entities and logic | Critical |
| `Library/Components/Users/**/*.vb` | User management entities and logic | Critical |
| `Library/Components/Security/**/*.vb` | Security, roles, permissions | Critical |
| `Library/Components/Tabs/**/*.vb` | Navigation/page entities | High |
| `Library/Components/Shared/**/*.vb` | Shared utilities (selective) | Medium |
| `Library/Providers/DataProviders/SqlDataProvider/*.vb` | Data access patterns reference | Critical |

**Admin UI Components to Migrate:**

| Pattern | Description | Priority |
|---------|-------------|----------|
| `Website/admin/Portal/**/*.ascx.vb` | Portal admin UI code-behind | Critical |
| `Website/admin/Users/**/*.ascx.vb` | User admin UI code-behind | Critical |
| `Website/admin/Modules/**/*.ascx.vb` | Module admin UI code-behind | Critical |
| `Website/admin/Security/**/*.ascx.vb` | Security admin UI code-behind | Critical |

**Test Coverage:**

| Pattern | Description | Priority |
|---------|-------------|----------|
| `backend/tests/**/*.cs` | All unit and integration tests | Critical |
| `frontend/**/*.spec.ts` | All Angular component tests | Critical |

**Configuration Updates:**

| Pattern | Description | Priority |
|---------|-------------|----------|
| `backend/src/**/appsettings*.json` | Application configuration | Critical |
| `frontend/src/environments/*.ts` | Angular environment config | Critical |
| `docker/*.yml` | Docker configuration | Critical |
| `docker/*.Dockerfile` | Container definitions | Critical |

**Documentation Updates:**

| Pattern | Description | Priority |
|---------|-------------|----------|
| `README.md` | Project documentation | High |
| `MIGRATION_NOTES.md` | Migration decision log | High |
| `backend/**/*.xml` | XML documentation comments | Medium |

### 0.6.2 Explicitly Out of Scope

**Excluded Components (Per User Requirements):**

| Component | Reason |
|-----------|--------|
| Telerik RadControls | Third-party control suite not migrated |
| `DotNetNuke.Entities.Host` namespace | Host-level functionality excluded |
| `DotNetNuke.Common.Globals` static methods | Legacy globals pattern deprecated |
| `DotNetNuke.Services.Scheduling` | Replace with `IHostedService` |
| Legacy module loader infrastructure | Simplified module architecture |
| COM interop components | Not compatible with .NET Core |
| VB6 dependencies and ActiveX controls | Legacy technology |

**Excluded Directories:**

| Directory | Reason |
|-----------|--------|
| `Website/DesktopModules/**` | Third-party modules not included |
| `Website/Providers/HtmlEditorProviders/**` | HTML editor providers excluded |
| `Library/HttpModules/**` | Web Forms HTTP modules |
| `Library/Controls/**` | Web Forms base controls |
| `Library/WebControls/**` | Web Forms web controls |

**Excluded Files:**

| File Pattern | Reason |
|--------------|--------|
| `*.aspx` | Web Forms pages (replaced by Angular) |
| `*.ascx` | User controls (replaced by Angular components) |
| `*.master` | Master pages (replaced by Angular layout) |
| `*.asmx` | Web services (replaced by REST API) |
| `*.vbproj` | Legacy VB project files |
| `*.sln` (legacy) | Legacy solution files |

**Not Migrated Features:**

| Feature | Reason |
|---------|--------|
| DNN Skinning System | Angular-based theming instead |
| DNN Container System | Angular component architecture |
| DNN Module Manifest (`.dnn`) | Simplified module registration |
| DNN Search Provider | Out of scope for initial migration |
| DNN Cache Provider | Using ASP.NET Core caching |
| DNN Logging Provider | Using Serilog |
| DNN Friendly URL Provider | Using Angular routing |
| Newsletter functionality | Out of scope |
| Messaging functionality | Out of scope |
| Vendors/Affiliates | Out of scope |

### 0.6.3 Boundary Decisions

**Database Schema:**

| Decision | Rationale |
|----------|-----------|
| Preserve existing schema | Minimize migration risk |
| Use EF Core Fluent API mapping | Map to existing table/column names |
| No schema modifications in Phase 1 | Data integrity preservation |
| Code-First for new entities only | If extending the model |

**Authentication Boundary:**

| Legacy | Target |
|--------|--------|
| Forms Authentication | JWT Bearer tokens |
| DNN Membership Provider | ASP.NET Core Identity concepts |
| Cookie-based sessions | Stateless JWT (BFF pattern) |

**API Boundary:**

| Decision | Rationale |
|----------|-----------|
| REST JSON API only | Modern API standards |
| No SOAP/XML services | Legacy protocol deprecated |
| OpenAPI documentation | Swagger/Swashbuckle |
| Versioning via URL path | `/api/v1/...` pattern |

### 0.6.4 Scope Validation Checklist

**Pre-Migration Validation:**

- [ ] All Portal CRUD operations identified
- [ ] All Module CRUD operations identified
- [ ] All User CRUD operations identified
- [ ] All Role CRUD operations identified
- [ ] All Permission operations identified
- [ ] Database schema analyzed and documented
- [ ] Stored procedures cataloged

**Post-Migration Validation:**

- [ ] API compilation succeeds (0 errors, 0 warnings)
- [ ] API unit tests pass (100%)
- [ ] Angular build succeeds (0 errors, 0 warnings)
- [ ] Angular unit tests pass (100%)
- [ ] Portal CRUD works end-to-end
- [ ] Module CRUD works end-to-end
- [ ] User CRUD works end-to-end
- [ ] Docker containers build and start
- [ ] Health endpoints return HTTP 200


## 0.7 Refactoring Rules

### 0.7.1 Refactoring-Specific Rules

The following rules are explicitly emphasized for this migration to ensure consistency, quality, and functional parity:

**Domain Logic Preservation:**

| Rule | Description |
|------|-------------|
| Extract business rules exactly | Do not optimize, refactor, or "improve" business logic during migration |
| Document discovered bugs as comments | Do not fix unless blocking migration |
| Preserve validation logic | All validation rules must produce identical results |
| Maintain calculation precision | Numeric calculations must be equivalent |

**Data Model Fidelity:**

| Rule | Description |
|------|-------------|
| Map EF Core entities to existing schema | Use Fluent API for legacy naming conventions |
| Preserve all relationships | Foreign keys, one-to-many, many-to-many unchanged |
| Maintain null handling semantics | Convert `Null.NullInteger` patterns appropriately |
| Keep data types compatible | SQL Server types mapped correctly |

**Behavioral Equivalence:**

| Rule | Description |
|------|-------------|
| Identical outcomes for identical inputs | Each migrated operation must match original behavior |
| Preserve error handling | Similar exceptions and error responses |
| Maintain audit trails | If logging exists, preserve it |
| Keep security boundaries | Permission checks must be equivalent |

### 0.7.2 Code Quality Standards

**C# 12 Coding Standards:**

| Standard | Implementation |
|----------|---------------|
| Nullable reference types | Enable `<Nullable>enable</Nullable>` in all projects |
| File-scoped namespaces | Use `namespace DnnMigration.Domain.Entities;` |
| Primary constructors | Use where appropriate for DTOs and services |
| Collection expressions | Use `[]` syntax for array/list initialization |
| Pattern matching | Prefer `is null` over `== null` |
| Target-typed new | Use `new()` where type is obvious |

**Async/Await Patterns:**

| Pattern | Rule |
|---------|------|
| All I/O operations | Must be async (database, HTTP) |
| No blocking calls | No `.Result` or `.Wait()` |
| Cancellation support | Include `CancellationToken` parameters |
| ConfigureAwait | Use `ConfigureAwait(false)` in library code |

**Example:**
```csharp
public async Task<Portal?> GetByIdAsync(
    int id, 
    CancellationToken cancellationToken = default)
{
    return await _context.Portals
        .AsNoTracking()
        .FirstOrDefaultAsync(p => p.PortalId == id, cancellationToken)
        .ConfigureAwait(false);
}
```

### 0.7.3 Angular 19 Coding Standards

**Standalone Component Rules:**

| Standard | Implementation |
|----------|---------------|
| All components standalone | No NgModules for feature components |
| Use `inject()` function | Prefer over constructor injection |
| Signals for state | Use `signal()` and `computed()` |
| Control flow syntax | Use `@if`, `@for`, `@switch` in templates |
| Typed reactive forms | Use `FormGroup<T>` with typed controls |

**Example:**
```typescript
@Component({
  selector: 'app-portal-list',
  standalone: true,
  imports: [CommonModule, RouterModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `...`
})
export class PortalListComponent {
  private portalService = inject(PortalService);
  portals = signal<Portal[]>([]);
  loading = signal(true);
}
```

### 0.7.4 Migration Comment Standards

**Backend Migration Comments:**

Mark non-obvious conversions with standardized comments:

```csharp
// MIGRATION: VB ByRef parameter converted to ref keyword
public void UpdatePortal(ref Portal portal) { }

// MIGRATION: VB Nothing check converted to null pattern
if (portal is null) { }

// MIGRATION: SqlHelper.ExecuteReader replaced with EF Core LINQ
var portals = await _context.Portals.ToListAsync();

// MIGRATION: Original stored procedure: dbo.GetPortal
// MIGRATION: Parameters: @PortalID int
```

**Frontend Migration Comments:**

```typescript
// MIGRATION: Derived from SiteSettings.ascx.vb cmdUpdate_Click handler
onSave(): void { }

// MIGRATION: Form fields mapped from original ASPX form controls
portalForm = new FormGroup({
  portalName: new FormControl('', Validators.required),
  // MIGRATION: txtPortalName from SiteSettings.ascx
});
```

### 0.7.5 Testing Requirements

**Backend Test Coverage:**

| Layer | Minimum Coverage | Focus Areas |
|-------|------------------|-------------|
| Domain | 90% | Entity validation, business rules |
| Application | 85% | Service logic, mapping |
| Infrastructure | 75% | Repository queries |
| API | 80% | Controller actions, validation |

**Frontend Test Coverage:**

| Component Type | Minimum Coverage | Focus Areas |
|----------------|------------------|-------------|
| Services | 90% | API calls, data transformation |
| Components | 80% | User interactions, form validation |
| Guards | 95% | Route protection |

**Integration Test Requirements:**

| Test Type | Description |
|-----------|-------------|
| API Integration | Test complete request/response cycle |
| Database Integration | Test EF Core queries against real schema |
| E2E (Optional) | Test complete user workflows |

### 0.7.6 Error Handling Standards

**API Error Responses (RFC 7807):**

```json
{
  "type": "https://dnnmigration.com/errors/validation",
  "title": "Validation Error",
  "status": 400,
  "detail": "One or more validation errors occurred.",
  "errors": {
    "portalName": ["Portal name is required."]
  }
}
```

**Exception Handling Rules:**

| Exception Type | HTTP Status | Action |
|----------------|-------------|--------|
| `ValidationException` | 400 Bad Request | Return validation errors |
| `NotFoundException` | 404 Not Found | Return entity not found |
| `UnauthorizedException` | 401 Unauthorized | Return auth required |
| `ForbiddenException` | 403 Forbidden | Return access denied |
| `Exception` (unhandled) | 500 Internal Error | Log and return generic |

### 0.7.7 Security Rules

**Authentication:**

| Rule | Implementation |
|------|---------------|
| JWT Bearer tokens | Short-lived access tokens (60 min) |
| Refresh tokens | Longer-lived for token renewal |
| HTTPS enforcement | All production traffic encrypted |
| CORS configuration | Restrict to Angular origin |

**Authorization:**

| Rule | Implementation |
|------|---------------|
| Role-based access | Map DNN roles to claims |
| Resource authorization | Per-entity permission checks |
| Admin-only endpoints | Require Administrator role |

**Input Validation:**

| Rule | Implementation |
|------|---------------|
| Model validation | FluentValidation for all requests |
| Sanitization | Encode output, validate input |
| SQL injection prevention | EF Core parameterized queries |
| XSS prevention | Angular sanitization |

### 0.7.8 Performance Rules

**Backend Performance:**

| Rule | Implementation |
|------|---------------|
| Use `AsNoTracking()` | For read-only queries |
| Pagination | All list endpoints paginated |
| Selective loading | Project DTOs, not full entities |
| Connection pooling | EF Core default pooling |

**Frontend Performance:**

| Rule | Implementation |
|------|---------------|
| Lazy loading | All feature modules lazy-loaded |
| OnPush change detection | All components use OnPush |
| Virtual scrolling | For large data lists |
| Bundle optimization | Production build with tree-shaking |


## 0.8 References

### 0.8.1 Codebase Files Analyzed

The following files and folders were systematically searched and analyzed to derive the conclusions in this Agent Action Plan:

**Root Directory:**
- `DotNetNuke.sln` - Visual Studio 2005 solution file
- `DotNetNuke_VS2008.sln` - Visual Studio 2008 solution file

**Library (Core Framework):**

| Path | Purpose |
|------|---------|
| `Library/Components/Portal/` | Portal domain entities and controllers |
| `Library/Components/Portal/PortalInfo.vb` | Portal entity (~250 lines, 30+ properties) |
| `Library/Components/Portal/PortalController.vb` | Portal CRUD operations |
| `Library/Components/Portal/PortalSettings.vb` | Portal configuration wrapper |
| `Library/Components/Portal/PortalAliasController.vb` | Domain alias management |
| `Library/Components/Modules/` | Module system entities and controllers |
| `Library/Components/Modules/ModuleInfo.vb` | Module instance entity |
| `Library/Components/Modules/ModuleController.vb` | Module lifecycle management |
| `Library/Components/Modules/PortalModuleBase.vb` | Module base class |
| `Library/Components/Modules/DesktopModuleController.vb` | Module definition management |
| `Library/Components/Users/` | User management entities |
| `Library/Components/Users/UserInfo.vb` | User entity with profile |
| `Library/Components/Users/UserController.vb` | User CRUD operations |
| `Library/Components/Users/UserMembership.vb` | Membership wrapper |
| `Library/Components/Users/UserRoleInfo.vb` | User-role association |
| `Library/Components/Security/` | Security domain |
| `Library/Components/Security/PortalSecurity.vb` | Security utilities |
| `Library/Components/Security/Roles/` | Role management |
| `Library/Components/Security/Roles/RoleController.vb` | Role CRUD |
| `Library/Components/Security/Roles/RoleInfo.vb` | Role entity |
| `Library/Components/Security/Roles/RoleGroupInfo.vb` | Role grouping |
| `Library/Components/Security/Permissions/` | Permission system |
| `Library/Components/Security/Permissions/PermissionController.vb` | Permission management |
| `Library/Components/Security/Permissions/ModulePermission*.vb` | Module permissions |
| `Library/Components/Security/Permissions/TabPermission*.vb` | Tab permissions |
| `Library/Components/Security/Permissions/FolderPermission*.vb` | Folder permissions |
| `Library/Components/Tabs/` | Navigation/page domain |
| `Library/Components/Tabs/TabInfo.vb` | Tab/page entity |
| `Library/Components/Tabs/TabController.vb` | Tab hierarchy management |
| `Library/Providers/DataProviders/SqlDataProvider/` | Data access layer |
| `Library/Providers/DataProviders/SqlDataProvider/SqlDataProvider.vb` | SqlHelper-based DAL |

**Website (Admin UI):**

| Path | Purpose |
|------|---------|
| `Website/admin/Portal/` | Portal administration UI |
| `Website/admin/Portal/SiteSettings.ascx.vb` | Portal configuration form |
| `Website/admin/Portal/Portals.ascx.vb` | Portal list management |
| `Website/admin/Portal/Signup.ascx.vb` | Portal creation wizard |
| `Website/admin/Users/` | User administration UI |
| `Website/admin/Users/ManageUsers.ascx.vb` | User list with filtering |
| `Website/admin/Users/User.ascx.vb` | User detail/edit form |
| `Website/admin/Users/Membership.ascx.vb` | Membership settings |
| `Website/admin/Modules/` | Module administration UI |
| `Website/admin/Modules/ModuleSettings.ascx.vb` | Module configuration |
| `Website/admin/Modules/Export.ascx.vb` | Module export |
| `Website/admin/Modules/Import.ascx.vb` | Module import |
| `Website/admin/Security/` | Security administration UI |
| `Website/admin/Security/Roles.ascx.vb` | Role list |
| `Website/admin/Security/SecurityRoles.ascx.vb` | Role assignments |
| `Website/admin/Security/EditRoles.ascx.vb` | Role edit form |
| `Website/Providers/` | Provider configurations and SQL scripts |

### 0.8.2 External Research Sources

**VB.NET to C# Migration:**
- TYMIQ: "When to migrate from VB.NET to C#" - VB.NET maintenance mode, C# future-proof choice
- Microsoft Q&A: Large-scale WebForms migration guidance - Manual rewrite required, lock requirements
- GAPVelocity AI: VB.NET to C# migration tooling - Semantic analysis for complex conversions

**Angular 19 Best Practices:**
- Syncfusion Blogs: "Angular 19 Standalone Components" - Feature-based organization, barrel files
- Angular Blog: "The future is standalone!" - Standalone true by default, no NgModules needed
- Angular University: Standalone Components Guide - Lazy loading, tree-shaking benefits

**Entity Framework Core 8:**
- Fahri Goktuna (Medium): ADO.NET to EF Core Migration - DB-First scaffolding, structured approach
- Devart Blog: ADO.NET vs EF Core - ORM benefits, migration considerations
- Samuel Getachew (Medium): EF Core Best Practices .NET 8 - AsNoTracking, DTOs, validation

**BFF Pattern:**
- Tore Nestenius: "Implementing BFF Pattern in ASP.NET Core" - Session management, API proxy
- Duende Software: BFF Security Framework - OAuth best practices, token security
- Auth0: Backend For Frontend Pattern - OIDC integration, secure authentication

### 0.8.3 User-Provided Attachments

No attachments were provided for this project.

### 0.8.4 User-Provided URLs

No Figma screens or external URLs were provided for this project.

### 0.8.5 Technology Documentation References

**Official Documentation:**

| Technology | Reference |
|------------|-----------|
| .NET 8 | https://learn.microsoft.com/dotnet/core/whats-new/dotnet-8 |
| ASP.NET Core 8 | https://learn.microsoft.com/aspnet/core/release-notes/aspnetcore-8.0 |
| Entity Framework Core 8 | https://learn.microsoft.com/ef/core/what-is-new/ef-core-8.0 |
| Angular 19 | https://angular.dev |
| C# 12 | https://learn.microsoft.com/dotnet/csharp/whats-new/csharp-12 |

**Package Documentation:**

| Package | Reference |
|---------|-----------|
| Microsoft.AspNetCore.Authentication.JwtBearer | https://www.nuget.org/packages/Microsoft.AspNetCore.Authentication.JwtBearer |
| Swashbuckle.AspNetCore | https://github.com/domaindrivendev/Swashbuckle.AspNetCore |
| AutoMapper | https://automapper.org |
| FluentValidation | https://docs.fluentvalidation.net |
| Serilog | https://serilog.net |

### 0.8.6 Search Tracking Summary

| Search # | Tool | Target | Purpose | Result |
|----------|------|--------|---------|--------|
| 1 | bash | `.blitzyignore` | Check exclusion patterns | None found |
| 2 | get_source_folder_contents | Root (`""`) | Identify solution structure | Found Library/, Website/ |
| 3 | get_source_folder_contents | `Library` | Map core framework | Found Components/, Providers/ |
| 4 | get_source_folder_contents | `Library/Components` | Map domain entities | Found Portal/, Modules/, Users/, Security/, Tabs/ |
| 5 | get_source_folder_contents | `Website` | Map web application | Found admin/, DesktopModules/ |
| 6 | get_source_folder_contents | `Website/admin` | Map admin UI | Found Portal/, Users/, Modules/, Security/ |
| 7-10 | get_source_folder_contents | Portal, Module, User domains | Deep component analysis | Entity and controller files |
| 11-13 | get_source_folder_contents | Security subfolders | Map roles and permissions | Role and permission files |
| 14-16 | get_source_folder_contents | SqlDataProvider | Map data access | SqlHelper patterns |
| 17-20 | get_source_folder_contents | Admin UI folders | Map ASPX code-behind | Form and list patterns |
| 21-24 | read_file | Key entity files | Understand VB.NET patterns | Property declarations, namespaces |
| 25-28 | web_search | Migration best practices | Research guidance | VB→C#, Angular 19, EF Core, BFF |


