namespace DnnMigration.Domain.Entities;

/// <summary>
/// Folder-permission junction entity for the DotNetNuke security subsystem.
/// Associates a security role or an individual user with a folder (identified by
/// <see cref="FolderID"/> / <see cref="FolderPath"/>) and records whether access is
/// granted, alongside the permission definition inherited from <see cref="Permission"/>.
/// Pure POCO: no behavior and no framework dependencies - persistence mapping is supplied
/// by an EF Core <c>IEntityTypeConfiguration&lt;FolderPermission&gt;</c> in the
/// Infrastructure layer.
/// </summary>
// MIGRATION: Ported from FolderPermissionInfo.vb (DotNetNuke.Security.Permissions). VB
// `Inherits PermissionInfo` is preserved as `: Permission`. XML serialization attributes
// (XmlIgnore / XmlElement) and the Null/glbRoleNothing sentinel constructor defaults are
// dropped; optional display strings are modelled as nullable string?. Identity and
// foreign-key integers (FolderPermissionID, FolderID, PortalID, RoleID, UserID) remain
// non-nullable because EF treats them as required keys. The inherited Permission members
// (PermissionID, PermissionCode, ModuleDefID, PermissionKey, PermissionName) are NOT
// re-declared. FolderPermissionCollection / CompareFolderPermissions / FolderPermissionController
// are out of scope (collections become navigation properties; controller logic moves to the
// Application and Infrastructure layers).
public class FolderPermission : Permission
{
    /// <summary>Primary key - unique identifier of the folder-permission assignment.</summary>
    public int FolderPermissionID { get; set; }

    /// <summary>Foreign key - identifier of the folder this permission applies to.</summary>
    public int FolderID { get; set; }

    /// <summary>Foreign key - identifier of the portal that owns the folder.</summary>
    public int PortalID { get; set; }

    /// <summary>Path of the folder this permission applies to.</summary>
    public string? FolderPath { get; set; }

    /// <summary>Foreign key - identifier of the role granted (or denied) access.</summary>
    public int RoleID { get; set; }

    /// <summary>Display name of the role granted (or denied) access.</summary>
    public string? RoleName { get; set; }

    /// <summary>Indicates whether access is allowed (true) or explicitly denied (false).</summary>
    public bool AllowAccess { get; set; }

    /// <summary>Foreign key - identifier of the user granted (or denied) access.</summary>
    public int UserID { get; set; }

    /// <summary>Username of the user granted (or denied) access.</summary>
    public string? Username { get; set; }

    /// <summary>Display name of the user granted (or denied) access.</summary>
    public string? DisplayName { get; set; }
}
