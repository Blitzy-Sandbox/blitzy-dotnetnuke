# DotNetNuke to DnnMigration - Migration Notes

This document tracks all architectural decisions, VB.NET to C# conversion patterns, legacy feature handling, and migration rationale for the DotNetNuke 4.x to DnnMigration transformation project.

## Table of Contents

1. [Migration Overview](#1-migration-overview)
2. [VB.NET to C# Conversion Patterns](#2-vbnet-to-c-conversion-patterns)
3. [Architectural Transformation Decisions](#3-architectural-transformation-decisions)
4. [Domain Model Mappings](#4-domain-model-mappings)
5. [Data Access Migration](#5-data-access-migration)
6. [Authentication Migration](#6-authentication-migration)
7. [Explicitly Excluded Components](#7-explicitly-excluded-components)
8. [Preservation Directives](#8-preservation-directives)
9. [Technology Stack](#9-technology-stack)
10. [Testing Strategy](#10-testing-strategy)

---

## 1. Migration Overview

### 1.1 Project Context

This migration transforms a legacy DotNetNuke (DNN) 4.x application from VB.NET (.NET Framework 2.0) to a modern technology stack. This is a **complete rewrite**, not an incremental migration.

### 1.2 Migration Type

| Aspect | From | To |
|--------|------|-----|
| Language | VB.NET | C# 12 |
| Framework | .NET Framework 2.0 | .NET 8 LTS |
| Architecture | ASP.NET WebForms | ASP.NET Core Web API + Angular 19 SPA |
| Data Access | ADO.NET/SqlDataProvider | Entity Framework Core 8 |
| Authentication | Forms Authentication | JWT Bearer (BFF Pattern) |
| Deployment | Windows IIS | Docker Linux Containers |

### 1.3 Migration Goals

1. **Language Modernization**: Convert all VB.NET to idiomatic C# 12 with nullable reference types
2. **API-First Architecture**: Replace WebForms postback model with REST APIs
3. **SPA Frontend**: Replace ASPX/ASCX with Angular 19 standalone components
4. **ORM Adoption**: Replace SqlDataProvider with EF Core Code-First
5. **Container-Ready**: Enable Linux deployment via Docker

---

## 2. VB.NET to C# Conversion Patterns

### 2.1 Variable Declarations

**VB.NET:**
```vb
Dim portalId As Integer
Dim portalName As String
Private _PortalID As Integer
Private _Users As Integer = Null.NullInteger
```

**C# Equivalent:**
```csharp
int portalId;
string portalName;
private int _portalId;
private int? _users = null;  // Null.NullInteger → nullable type
```

**Pattern Rules:**
- `Dim x As Type` → `Type x` or `var x` (when type is obvious)
- `Private _Field As Type` → `private Type _field` (camelCase for private fields)
- `Null.NullInteger` → `null` with nullable type (`int?`) or `default(int?)`

### 2.2 Parameter Passing

**VB.NET:**
```vb
Public Sub UpdatePortal(ByVal portalId As Integer, ByRef portal As PortalInfo)
End Sub
```

**C# Equivalent:**
```csharp
public void UpdatePortal(int portalId, ref Portal portal)
{
}
```

**Pattern Rules:**
- `ByVal` → (default, no keyword needed)
- `ByRef` → `ref` keyword

### 2.3 Null Checking

**VB.NET:**
```vb
If portal Is Nothing Then
    ' handle null
End If

If portal IsNot Nothing Then
    ' use portal
End If
```

**C# Equivalent:**
```csharp
if (portal is null)
{
    // handle null
}

if (portal is not null)
{
    // use portal
}
```

**Pattern Rules:**
- `Is Nothing` → `is null` (preferred C# 9+ pattern)
- `IsNot Nothing` → `is not null`
- Alternative: `== null` / `!= null` for older patterns

### 2.4 Logical Operators

**VB.NET:**
```vb
If isValid AndAlso hasPermission Then
    ' allow action
End If

If isAdmin OrElse isSuperUser Then
    ' grant access
End If
```

**C# Equivalent:**
```csharp
if (isValid && hasPermission)
{
    // allow action
}

if (isAdmin || isSuperUser)
{
    // grant access
}
```

**Pattern Rules:**
- `AndAlso` → `&&` (short-circuit AND)
- `OrElse` → `||` (short-circuit OR)
- `And` → `&` (bitwise AND, rarely used)
- `Or` → `|` (bitwise OR, rarely used)

### 2.5 Type Casting

**VB.NET:**
```vb
Dim portal As PortalInfo = CType(obj, PortalInfo)
Dim id As Integer = CInt(value)
Dim name As String = CStr(value)
Dim result As PortalInfo = TryCast(obj, PortalInfo)
```

**C# Equivalent:**
```csharp
Portal portal = (Portal)obj;
int id = Convert.ToInt32(value);  // or (int)value
string name = Convert.ToString(value);  // or value.ToString()
Portal? result = obj as Portal;
```

**Pattern Rules:**
- `CType(obj, Type)` → `(Type)obj` (direct cast)
- `CInt()` → `(int)` or `Convert.ToInt32()`
- `CStr()` → `.ToString()` or `Convert.ToString()`
- `TryCast()` → `as` operator (returns null on failure)
- `DirectCast()` → `(Type)` (throws on failure)

### 2.6 String Concatenation

**VB.NET:**
```vb
Dim message As String = "Portal: " & portalName & " (ID: " & portalId.ToString() & ")"
```

**C# Equivalent:**
```csharp
string message = $"Portal: {portalName} (ID: {portalId})";
// Or: string message = "Portal: " + portalName + " (ID: " + portalId + ")";
```

**Pattern Rules:**
- `&` → `+` or string interpolation `$"{value}"`
- Prefer string interpolation for readability

### 2.7 Inheritance and Interfaces

**VB.NET:**
```vb
Public Class UserController
    Inherits DataProviderBase
    Implements IUserController

End Class
```

**C# Equivalent:**
```csharp
public class UserController : DataProviderBase, IUserController
{
}
```

**Pattern Rules:**
- `Inherits` → `:` (inheritance)
- `Implements` → `:` (interface implementation)
- Both use the same syntax in C# after the class name

### 2.8 Code Regions

**VB.NET:**
```vb
#Region "Private Members"
    Private _portalId As Integer
#End Region
```

**C# Equivalent:**
```csharp
#region Private Members
private int _portalId;
#endregion
```

**Pattern Rules:**
- `#Region "Name"` → `#region Name`
- `#End Region` → `#endregion`

### 2.9 Properties

**VB.NET (Full property with backing field):**
```vb
Private _PortalID As Integer

<XmlElement("portalid")> Public Property PortalID() As Integer
    Get
        Return _PortalID
    End Get
    Set(ByVal Value As Integer)
        _PortalID = Value
    End Set
End Property
```

**C# Equivalent (Auto-property):**
```csharp
[XmlElement("portalid")]
public int PortalId { get; set; }
```

**C# Equivalent (With backing field when needed):**
```csharp
private int _portalId;

[XmlElement("portalid")]
public int PortalId
{
    get => _portalId;
    set => _portalId = value;
}
```

**Pattern Rules:**
- Simple properties → Auto-properties `{ get; set; }`
- Computed properties → Expression-bodied members
- Validation required → Full property with backing field

### 2.10 Namespace Declaration

**VB.NET:**
```vb
Namespace DotNetNuke.Entities.Portals
    Public Class PortalInfo
    End Class
End Namespace
```

**C# Equivalent (File-scoped):**
```csharp
namespace DnnMigration.Domain.Entities;

public class Portal
{
}
```

**Pattern Rules:**
- Use file-scoped namespaces (C# 10+)
- Flatten namespace from `DotNetNuke.Entities.Portals` to `DnnMigration.Domain.Entities`

---

## 3. Architectural Transformation Decisions

### 3.1 From WebForms to SPA + API

**Legacy Architecture:**
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

**Target Architecture:**
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

### 3.2 Decision Rationale

| Decision | Rationale |
|----------|-----------|
| WebForms → Angular SPA | Modern UX, client-side rendering, responsive design |
| ASPX Code-Behind → REST API | Separation of concerns, mobile-ready, testable |
| Monolith → Clean Architecture | Maintainability, testability, dependency inversion |
| Stateful Sessions → Stateless JWT | Scalability, microservices-ready, container-friendly |
| Windows IIS → Docker Linux | Cloud-native, cost reduction, consistent environments |

### 3.3 Layer Separation

| Layer | Responsibility | DNN Equivalent |
|-------|---------------|----------------|
| Domain | Entities, Interfaces, Enums | `Library/Components/*Info.vb` |
| Application | Services, DTOs, Mapping | `Library/Components/*Controller.vb` (business logic) |
| Infrastructure | EF Core, Repositories, Identity | `Library/Providers/DataProviders/` |
| API | Controllers, Middleware | `Website/admin/*.ascx.vb` (handlers) |

### 3.4 Clean Architecture Principles Applied

1. **Dependency Inversion**: Domain layer has no external dependencies
2. **Interface Segregation**: Repository and service interfaces in Domain
3. **Single Responsibility**: Each layer has one reason to change
4. **Open/Closed**: Extensible through interfaces, closed to modification

---

## 4. Domain Model Mappings

### 4.1 Entity Transformations

| Legacy Entity | Target Entity | Source File | Notes |
|--------------|---------------|-------------|-------|
| `PortalInfo` | `Portal` | `Library/Components/Portal/PortalInfo.vb` | Multi-tenant site container |
| `ModuleInfo` | `Module` | `Library/Components/Modules/ModuleInfo.vb` | Pluggable content component |
| `UserInfo` | `User` | `Library/Components/Users/UserInfo.vb` | Identity with membership |
| `RoleInfo` | `Role` | `Library/Components/Security/Roles/RoleInfo.vb` | Permission grouping |
| `TabInfo` | `Tab` | `Library/Components/Tabs/TabInfo.vb` | Navigation/page hierarchy |
| `UserMembership` | Merged into `User` | `Library/Components/Users/UserMembership.vb` | Simplified model |
| `UserProfile` | `UserProfile` | `Library/Components/Users/UserProfile.vb` | Profile properties |
| `RoleGroupInfo` | `RoleGroup` | `Library/Components/Security/Roles/RoleGroupInfo.vb` | Role grouping |
| `UserRoleInfo` | `UserRole` | `Library/Components/Users/UserRoleInfo.vb` | Many-to-many join |
| `PermissionInfo` | `Permission` | `Library/Components/Security/Permissions/PermissionInfo.vb` | Base permission |
| `ModulePermissionInfo` | `ModulePermission` | `Library/Components/Security/Permissions/ModulePermissionInfo.vb` | Module-level |
| `TabPermissionInfo` | `TabPermission` | `Library/Components/Security/Permissions/TabPermissionInfo.vb` | Tab-level |
| `FolderPermissionInfo` | `FolderPermission` | `Library/Components/Security/Permissions/FolderPermissionInfo.vb` | Folder-level |
| `PortalAliasInfo` | `PortalAlias` | `Library/Components/Portal/PortalAliasInfo.vb` | Domain alias |
| `DesktopModuleInfo` | `DesktopModule` | `Library/Components/Modules/DesktopModuleInfo.vb` | Module definition |
| `ModuleDefinitionInfo` | `ModuleDefinition` | `Library/Components/Modules/ModuleDefinitionInfo.vb` | Definition metadata |

### 4.2 Property Mapping Examples

**Portal Entity (PortalInfo → Portal):**

| VB.NET Property | C# Property | Type Change | Notes |
|-----------------|-------------|-------------|-------|
| `PortalID` | `PortalId` | `Integer` → `int` | Primary key |
| `PortalName` | `PortalName` | `String` → `string` | Required |
| `LogoFile` | `LogoFile` | `String` → `string?` | Nullable |
| `ExpiryDate` | `ExpiryDate` | `Date` → `DateTime?` | Nullable |
| `UserRegistration` | `UserRegistration` | `Integer` → `UserRegistrationType` | Enum |
| `BannerAdvertising` | `BannerAdvertising` | `Integer` → `BannerType` | Enum |
| `Users (Null.NullInteger)` | `Users` | `Integer` → `int?` | Nullable int |
| `GUID` | `Guid` | `Guid` → `Guid` | No change |

**User Entity (UserInfo → User):**

| VB.NET Property | C# Property | Type Change | Notes |
|-----------------|-------------|-------------|-------|
| `UserID` | `UserId` | `Integer` → `int` | Primary key |
| `Username` | `Username` | `String` → `string` | Required |
| `DisplayName` | `DisplayName` | `String` → `string?` | Nullable |
| `IsSuperUser` | `IsSuperUser` | `Boolean` → `bool` | No change |
| `Membership` | (merged) | Object → properties | Flattened |
| `Roles()` | `Roles` | `String()` → `ICollection<UserRole>` | Navigation property |

### 4.3 Namespace Transformations

| Legacy Namespace | Target Namespace |
|-----------------|------------------|
| `DotNetNuke.Entities.Portals` | `DnnMigration.Domain.Entities` |
| `DotNetNuke.Entities.Modules` | `DnnMigration.Domain.Entities` |
| `DotNetNuke.Entities.Users` | `DnnMigration.Domain.Entities` |
| `DotNetNuke.Security.Roles` | `DnnMigration.Domain.Entities` |
| `DotNetNuke.Entities.Tabs` | `DnnMigration.Domain.Entities` |
| `DotNetNuke.Data` | `DnnMigration.Infrastructure.Data` |
| `DotNetNuke.Services.*` | `DnnMigration.Application.Services` |
| `DotNetNuke.Common.Utilities` | `DnnMigration.Domain.Enums` / Built-in |

### 4.4 Enum Extractions

**UserRegistrationType (from PortalInfo.UserRegistration integer):**
```csharp
public enum UserRegistrationType
{
    None = 0,
    Private = 1,
    Public = 2,
    Verified = 3
}
```

**BannerType (from PortalInfo.BannerAdvertising integer):**
```csharp
public enum BannerType
{
    None = 0,
    Site = 1,
    Vendor = 2
}
```

**VisibilityState (from ModuleInfo):**
```csharp
public enum VisibilityState
{
    Maximized = 0,
    Minimized = 1,
    None = 2
}
```

---

## 5. Data Access Migration

### 5.1 SqlDataProvider to EF Core

**Legacy Pattern (SqlHelper):**
```vb
' From SqlDataProvider.vb
Public Overrides Function GetPortal(ByVal PortalId As Integer) As IDataReader
    Return SqlHelper.ExecuteReader(ConnectionString, _
        DatabaseOwner & ObjectQualifier & "GetPortal", PortalId)
End Function

Public Overrides Sub AddPortal(ByVal PortalId As Integer, ...)
    SqlHelper.ExecuteNonQuery(ConnectionString, _
        DatabaseOwner & ObjectQualifier & "AddPortal", PortalId, ...)
End Sub
```

**Target Pattern (EF Core):**
```csharp
// From PortalRepository.cs
public async Task<Portal?> GetByIdAsync(int portalId, CancellationToken cancellationToken = default)
{
    return await _context.Portals
        .AsNoTracking()
        .FirstOrDefaultAsync(p => p.PortalId == portalId, cancellationToken);
}

public async Task<Portal> AddAsync(Portal portal, CancellationToken cancellationToken = default)
{
    _context.Portals.Add(portal);
    await _context.SaveChangesAsync(cancellationToken);
    return portal;
}
```

### 5.2 Stored Procedure to LINQ Mapping

| Stored Procedure | EF Core LINQ Equivalent |
|------------------|------------------------|
| `GetPortal(@PortalId)` | `Portals.FirstOrDefault(p => p.PortalId == id)` |
| `GetPortals()` | `Portals.ToList()` |
| `AddPortal(...)` | `Portals.Add(portal); SaveChanges()` |
| `UpdatePortal(...)` | `Entry(portal).State = Modified; SaveChanges()` |
| `DeletePortal(@PortalId)` | `Portals.Remove(portal); SaveChanges()` |
| `GetModulesByTab(@TabId)` | `Modules.Where(m => m.TabId == tabId)` |
| `GetUsersByPortal(@PortalId)` | `Users.Where(u => u.PortalId == portalId)` |
| `GetRolesByPortal(@PortalId)` | `Roles.Where(r => r.PortalId == portalId)` |

### 5.3 Migration Comments for Data Access

When migrating data access code, add comments documenting the original stored procedure:

```csharp
// MIGRATION: Original stored procedure: dbo.GetPortal
// MIGRATION: Parameters: @PortalID int
// MIGRATION: SqlHelper.ExecuteReader replaced with EF Core LINQ
public async Task<Portal?> GetByIdAsync(int portalId, CancellationToken cancellationToken = default)
{
    return await _context.Portals
        .AsNoTracking()
        .FirstOrDefaultAsync(p => p.PortalId == portalId, cancellationToken);
}
```

### 5.4 Transaction Handling

**Legacy:**
```vb
Using transaction As SqlTransaction = connection.BeginTransaction()
    Try
        ' operations
        transaction.Commit()
    Catch
        transaction.Rollback()
        Throw
    End Try
End Using
```

**Target:**
```csharp
await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
try
{
    // operations
    await _context.SaveChangesAsync(cancellationToken);
    await transaction.CommitAsync(cancellationToken);
}
catch
{
    await transaction.RollbackAsync(cancellationToken);
    throw;
}
```

### 5.5 Schema Preservation

**Critical Decision:** The existing database schema is preserved during migration.

- EF Core Fluent API maps entities to existing table/column names
- No schema modifications in Phase 1
- Column names maintained exactly (e.g., `PortalID` not `PortalId`)

```csharp
// Example Fluent API configuration
builder.ToTable("Portals");  // Exact legacy table name
builder.Property(p => p.PortalId).HasColumnName("PortalID");  // Legacy column name
```

---

## 6. Authentication Migration

### 6.1 From Forms Authentication to JWT

**Legacy (Forms Authentication):**
- Cookie-based session state
- `web.config` machine key encryption
- `FormsAuthentication.SetAuthCookie()`
- Membership provider integration

**Target (JWT Bearer with BFF Pattern):**
- Stateless JWT tokens
- Short-lived access tokens (60 minutes)
- Refresh tokens for renewal
- HTTP-only cookies for security

### 6.2 BFF Pattern Implementation

The Backend-for-Frontend (BFF) pattern centralizes authentication in ASP.NET Core:

```
┌──────────────┐    ┌─────────────────┐    ┌──────────────┐
│   Angular    │    │    BFF API      │    │   Backend    │
│    SPA       │───▶│  (ASP.NET Core) │───▶│   Services   │
└──────────────┘    └─────────────────┘    └──────────────┘
                           │
                           ▼
                    ┌─────────────────┐
                    │  JWT Validation │
                    │  Session Mgmt   │
                    │  CSRF Protection│
                    └─────────────────┘
```

**Key Components:**
1. **Session Management**: Handles OIDC flow internally
2. **API Proxy**: Secure gateway for backend calls
3. **Token Storage**: Server-side, not browser localStorage

### 6.3 JWT Configuration

```csharp
// Program.cs JWT configuration
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = configuration["Jwt:Issuer"],
            ValidAudience = configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(configuration["Jwt:Secret"]!))
        };
    });
```

### 6.4 Role Mapping

DNN roles map to JWT claims for authorization:

| DNN Role | JWT Claim | Usage |
|----------|-----------|-------|
| Administrators | `role: "Administrator"` | Admin-only endpoints |
| Registered Users | `role: "RegisteredUser"` | Authenticated endpoints |
| Super Users | `role: "SuperUser"` | Host-level access |
| Custom Roles | `role: "{RoleName}"` | Role-based policies |

### 6.5 Password Hashing Migration

**Legacy (PortalSecurity.vb):**
```vb
' Hash using SHA256 or membership provider
Public Function Encrypt(ByVal Value As String) As String
```

**Target (BCrypt):**
```csharp
// Using BCrypt.Net-Next package
public string HashPassword(string password)
{
    return BCrypt.Net.BCrypt.HashPassword(password);
}

public bool VerifyPassword(string password, string hash)
{
    return BCrypt.Net.BCrypt.Verify(password, hash);
}
```

---

## 7. Explicitly Excluded Components

### 7.1 Third-Party Dependencies

| Component | Reason for Exclusion |
|-----------|---------------------|
| **Telerik RadControls** | Third-party control suite, not compatible with Angular |
| Telerik RadEditor | Replaced by modern Angular rich text editors |
| Telerik RadGrid | Replaced by Angular Material or custom data tables |
| Telerik RadMenu | Replaced by Angular navigation components |
| Telerik RadTreeView | Replaced by Angular tree components |

### 7.2 DNN Core Components

| Component | Reason for Exclusion |
|-----------|---------------------|
| **DotNetNuke.Entities.Host** namespace | Host-level functionality out of scope |
| **DotNetNuke.Common.Globals** | Static methods pattern deprecated |
| **DotNetNuke.Services.Scheduling** | Replace with `IHostedService` |
| Legacy module loader infrastructure | Simplified module architecture |
| DNN Skinning System | Angular-based theming instead |
| DNN Container System | Angular component architecture |
| DNN Module Manifest (`.dnn`) | Simplified module registration |

### 7.3 Legacy Technologies

| Component | Reason for Exclusion |
|-----------|---------------------|
| **COM interop components** | Not compatible with .NET Core |
| **VB6 dependencies** | Legacy technology |
| **ActiveX controls** | Browser security deprecated |
| Web Forms HTTP modules | ASP.NET Core middleware instead |
| Web Forms base controls | Angular components instead |
| ASMX web services | REST API instead |

### 7.4 Out-of-Scope Features

| Feature | Reason | Alternative |
|---------|--------|-------------|
| DNN Search Provider | Out of scope for initial migration | Future enhancement |
| DNN Cache Provider | Replace with ASP.NET Core caching | `IMemoryCache` |
| DNN Logging Provider | Replace with Serilog | Structured logging |
| DNN Friendly URL Provider | Angular routing | Client-side routing |
| Newsletter functionality | Out of scope | Future enhancement |
| Messaging functionality | Out of scope | Future enhancement |
| Vendors/Affiliates | Out of scope | Future enhancement |

### 7.5 Excluded File Patterns

```
# Excluded directories
Website/DesktopModules/**          # Third-party modules
Website/Providers/HtmlEditorProviders/**  # HTML editors
Library/HttpModules/**             # Web Forms HTTP modules
Library/Controls/**                # Web Forms base controls
Library/WebControls/**             # Web Forms web controls

# Excluded file types
*.aspx                             # Web Forms pages
*.ascx                             # User controls
*.master                           # Master pages
*.asmx                             # Web services
*.vbproj                           # Legacy project files
```

---

## 8. Preservation Directives

### 8.1 Domain Logic Preservation

**Critical Rule:** Extract business rules exactly as implemented in VB.NET source without optimization or "improvement" during migration.

| Directive | Description |
|-----------|-------------|
| Extract Exactly | Do not optimize business logic during migration |
| Document Bugs | Note discovered bugs as comments, do not fix |
| Preserve Validation | All validation rules must produce identical results |
| Maintain Precision | Numeric calculations must be equivalent |

### 8.2 Data Model Fidelity

| Directive | Description |
|-----------|-------------|
| Map to Existing Schema | Use Fluent API for legacy naming conventions |
| Preserve Relationships | Foreign keys unchanged |
| Maintain Null Handling | Convert `Null.NullInteger` appropriately |
| Keep Data Types | SQL Server types mapped correctly |

### 8.3 Behavioral Equivalence

| Directive | Description |
|-----------|-------------|
| Identical Outcomes | Same inputs must produce same outputs |
| Preserve Errors | Similar exceptions and error responses |
| Maintain Auditing | If logging exists, preserve it |
| Keep Security | Permission checks must be equivalent |

### 8.4 Migration Comment Standards

**Backend Comments:**
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

**Frontend Comments:**
```typescript
// MIGRATION: Derived from SiteSettings.ascx.vb cmdUpdate_Click handler
onSave(): void { }

// MIGRATION: Form fields mapped from original ASPX form controls
portalForm = new FormGroup({
  portalName: new FormControl('', Validators.required),
  // MIGRATION: txtPortalName from SiteSettings.ascx
});
```

---

## 9. Technology Stack

### 9.1 Backend Stack

| Technology | Version | Purpose |
|------------|---------|---------|
| .NET | 8.0 LTS | Runtime framework |
| C# | 12 | Programming language |
| ASP.NET Core | 8.0 | Web API framework |
| Entity Framework Core | 8.0 | ORM |
| SQL Server | 2019+ | Database |
| JWT Bearer | 8.0 | Authentication |
| AutoMapper | 12.0 | Object mapping |
| FluentValidation | 11.3 | Input validation |
| Serilog | 8.0 | Structured logging |
| Swashbuckle | 6.9 | OpenAPI/Swagger |
| BCrypt.Net-Next | 4.0 | Password hashing |

### 9.2 Frontend Stack

| Technology | Version | Purpose |
|------------|---------|---------|
| Angular | 19 | SPA framework |
| TypeScript | 5.6 | Programming language |
| RxJS | 7.8 | Reactive extensions |
| Angular Router | 19 | Client-side routing |
| Angular Forms | 19 | Reactive forms |
| Karma/Jasmine | 6.4/5.4 | Testing |

### 9.3 DevOps Stack

| Technology | Version | Purpose |
|------------|---------|---------|
| Docker | 24+ | Containerization |
| nginx | Alpine | SPA hosting |
| docker-compose | 2.x | Local orchestration |

### 9.4 Testing Stack

| Package | Purpose |
|---------|---------|
| xUnit | Unit testing framework |
| Moq | Mocking framework |
| FluentAssertions | Assertion library |
| Microsoft.AspNetCore.Mvc.Testing | Integration testing |
| Microsoft.EntityFrameworkCore.InMemory | In-memory testing |

---

## 10. Testing Strategy

### 10.1 Coverage Requirements

| Layer | Minimum Coverage | Focus Areas |
|-------|------------------|-------------|
| Domain | 90% | Entity validation, business rules |
| Application | 85% | Service logic, mapping |
| Infrastructure | 75% | Repository queries |
| API | 80% | Controller actions, validation |

### 10.2 Frontend Test Coverage

| Component Type | Minimum Coverage | Focus Areas |
|----------------|------------------|-------------|
| Services | 90% | API calls, data transformation |
| Components | 80% | User interactions, form validation |
| Guards | 95% | Route protection |

### 10.3 Integration Test Requirements

| Test Type | Description |
|-----------|-------------|
| API Integration | Test complete request/response cycle |
| Database Integration | Test EF Core queries against real schema |
| E2E (Optional) | Test complete user workflows |

### 10.4 Test Execution Commands

```bash
# Backend tests
dotnet test --configuration Release

# Frontend tests
ng test --watch=false --browsers=ChromeHeadless

# Full validation
dotnet build --configuration Release --warnaserror
ng build --configuration production
docker-compose build
```

---

## Appendix A: Source File Reference

### Core Entity Files Analyzed

| File | Lines | Purpose |
|------|-------|---------|
| `Library/Components/Portal/PortalInfo.vb` | ~250 | Portal entity with 30+ properties |
| `Library/Components/Modules/ModuleInfo.vb` | ~300 | Module instance entity |
| `Library/Components/Users/UserInfo.vb` | ~400 | User entity with profile |
| `Library/Components/Security/Roles/RoleInfo.vb` | ~150 | Role entity |

### Key Patterns Identified

- Private backing fields with `_PascalCase` naming
- Full property syntax with explicit Get/Set
- `Null.NullInteger` for nullable integers
- XML serialization attributes
- Region directives for code organization

---

## Appendix B: API Endpoint Reference

### Portal API

| Method | Endpoint | Purpose |
|--------|----------|---------|
| GET | `/api/portals` | List portals |
| GET | `/api/portals/{id}` | Get portal by ID |
| POST | `/api/portals` | Create portal |
| PUT | `/api/portals/{id}` | Update portal |
| DELETE | `/api/portals/{id}` | Delete portal |

### User API

| Method | Endpoint | Purpose |
|--------|----------|---------|
| GET | `/api/users` | List users |
| GET | `/api/users/{id}` | Get user by ID |
| POST | `/api/users` | Create user |
| PUT | `/api/users/{id}` | Update user |
| DELETE | `/api/users/{id}` | Delete user |

### Auth API

| Method | Endpoint | Purpose |
|--------|----------|---------|
| POST | `/api/auth/login` | Authenticate user |
| POST | `/api/auth/refresh` | Refresh tokens |
| POST | `/api/auth/logout` | Logout user |
| GET | `/api/auth/me` | Get current user |

---

## Document History

| Date | Version | Author | Changes |
|------|---------|--------|---------|
| 2024 | 1.0 | Migration Team | Initial migration documentation |

---

*This document is maintained as part of the DnnMigration project and should be updated as migration decisions evolve.*
