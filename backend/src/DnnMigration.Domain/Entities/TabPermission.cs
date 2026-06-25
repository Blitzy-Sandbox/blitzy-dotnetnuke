namespace DnnMigration.Domain.Entities;

// MIGRATION: Converted from VB.NET DotNetNuke.Security.Permissions.TabPermissionInfo
// (Library/Components/Security/Permissions/TabPermission.vb). Renamed to TabPermission;
// preserves inheritance from the base Permission. XML attributes removed; persistence-ignorant POCO.
// MIGRATION: Legacy declared a private dead field "_permissionKey" with no property (shadowing the base
// PermissionKey). Dropped — base Permission.PermissionKey is authoritative. (Documented, not "fixed".)
public class TabPermission : Permission
{
    public int TabPermissionId { get; set; }

    // MIGRATION: Legacy TabID initialized to Null.NullInteger (-1) -> nullable int. Scopes the permission to a tab (page).
    public int? TabId { get; set; }

    // MIGRATION: Legacy RoleID default Integer.Parse(glbRoleNothing) = -1 ("no role"/user-level grant). Sentinel preserved.
    public int RoleId { get; set; } = -1;

    public string? RoleName { get; set; }

    public bool AllowAccess { get; set; }

    // MIGRATION: Legacy UserID initialized to Null.NullInteger (-1) -> nullable int.
    public int? UserId { get; set; }

    public string? Username { get; set; }

    public string? DisplayName { get; set; }
}
