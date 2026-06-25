namespace DnnMigration.Domain.Entities;

// MIGRATION: Converted from VB.NET DotNetNuke.Security.Permissions.FolderPermissionInfo
// (Library/Components/Security/Permissions/FolderPermission.vb). Renamed to FolderPermission;
// preserves inheritance from the base Permission. XML attributes removed; persistence-ignorant POCO.
// A folder permission scopes a Permission to a portal + folder + role/user.
// MIGRATION: Legacy declared a private dead field "_permissionKey" with no property (shadowing base PermissionKey). Dropped.
public class FolderPermission : Permission
{
    public int FolderPermissionId { get; set; }

    // MIGRATION: Legacy FolderID initialized to Null.NullInteger (-1) -> nullable int.
    public int? FolderId { get; set; }

    // MIGRATION: Multi-tenant discriminator. Legacy PortalID initialized to Null.NullInteger (-1) -> nullable int.
    public int? PortalId { get; set; }

    public string? FolderPath { get; set; }

    // MIGRATION: Legacy RoleID default Integer.Parse(glbRoleNothing) = -1 ("no role"). Sentinel preserved.
    public int RoleId { get; set; } = -1;

    public string? RoleName { get; set; }

    public bool AllowAccess { get; set; }

    // MIGRATION: Legacy UserID initialized to Null.NullInteger (-1) -> nullable int.
    public int? UserId { get; set; }

    public string? Username { get; set; }

    public string? DisplayName { get; set; }
}
