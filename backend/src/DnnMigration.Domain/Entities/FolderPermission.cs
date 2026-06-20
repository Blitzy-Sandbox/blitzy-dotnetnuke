namespace DnnMigration.Domain.Entities;

// MIGRATION: Ported from FolderPermissionInfo.vb (DotNetNuke.Security.Permissions). The VB
// `Inherits PermissionInfo` is preserved as `: Permission` (the base entity in this same
// namespace, so no using directive is required). The legacy XML serialization attributes
// (<XmlIgnore()> / <XmlElement(...)>) and the Null.NullInteger / Null.NullString / glbRoleNothing
// sentinel defaults from the parameterless constructor are intentionally dropped; identity and
// foreign-key integers stay non-nullable while optional display strings become nullable. Relational
// mapping (table/column names, keys) is applied by a Fluent IEntityTypeConfiguration<FolderPermission>
// in the Infrastructure layer, honoring ADR-002 (the existing DNN 4.9.0.85 schema is mapped
// unchanged). The result is a plain, attribute-free POCO with zero framework dependencies, in keeping
// with the Clean Architecture inner ring.

/// <summary>
/// Folder-permission junction entity that associates a portal folder with the role or user granted
/// (or denied) access to it. Faithful C# port of the legacy <c>FolderPermissionInfo</c> value object
/// from DotNetNuke <c>4.9.0.85</c>; the shared permission descriptor (<c>PermissionID</c>,
/// <c>PermissionCode</c>, <c>ModuleDefID</c>, <c>PermissionKey</c>, <c>PermissionName</c>) is
/// inherited from <see cref="Permission"/> and therefore not re-declared here.
/// </summary>
public class FolderPermission : Permission
{
    /// <summary>Primary key identifier for this folder-permission record (legacy <c>FolderPermissionID</c>, VB.NET <c>Integer</c>).</summary>
    public int FolderPermissionID { get; set; }

    /// <summary>Foreign key to the folder this permission applies to (legacy <c>FolderID</c>, VB.NET <c>Integer</c>).</summary>
    public int FolderID { get; set; }

    /// <summary>Foreign key to the owning portal (legacy <c>PortalID</c>, VB.NET <c>Integer</c>).</summary>
    public int PortalID { get; set; }

    /// <summary>Path of the folder the permission targets (legacy <c>FolderPath</c>, VB.NET <c>String</c>); optional display value, hence nullable.</summary>
    public string? FolderPath { get; set; }

    /// <summary>Foreign key to the role granted (or denied) access (legacy <c>RoleID</c>, VB.NET <c>Integer</c>).</summary>
    public int RoleID { get; set; }

    /// <summary>Display name of the associated role (legacy <c>RoleName</c>, VB.NET <c>String</c>); optional, hence nullable.</summary>
    public string? RoleName { get; set; }

    /// <summary>Indicates whether access is allowed (<c>true</c>) or denied (<c>false</c>) (legacy <c>AllowAccess</c>, VB.NET <c>Boolean</c>).</summary>
    public bool AllowAccess { get; set; }

    /// <summary>Foreign key to the user granted (or denied) access (legacy <c>UserID</c>, VB.NET <c>Integer</c>).</summary>
    public int UserID { get; set; }

    /// <summary>Login name of the associated user (legacy <c>Username</c>, VB.NET <c>String</c>); optional, hence nullable.</summary>
    public string? Username { get; set; }

    /// <summary>Display name of the associated user (legacy <c>DisplayName</c>, VB.NET <c>String</c>); optional, hence nullable.</summary>
    public string? DisplayName { get; set; }
}
