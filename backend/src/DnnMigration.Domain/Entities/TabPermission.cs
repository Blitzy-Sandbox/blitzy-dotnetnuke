namespace DnnMigration.Domain.Entities;

// MIGRATION: Ported from TabPermissionInfo.vb (DotNetNuke.Security.Permissions). VB
// `Inherits PermissionInfo` preserved as `: Permission`. XML serialization attributes and the
// Null.NullInteger / Null.NullString / glbRoleNothing constructor sentinels are intentionally
// dropped; optional display fields are nullable. EF mapping is handled by Fluent
// IEntityTypeConfiguration in the Infrastructure layer (TabPermissionConfiguration). Tab.cs holds
// the owning ICollection<TabPermission> navigation. Structurally identical to ModulePermission
// except the owning key is TabID.
public class TabPermission : Permission
{
    public int TabPermissionID { get; set; }

    public int TabID { get; set; }

    public int RoleID { get; set; }

    public string? RoleName { get; set; }

    public bool AllowAccess { get; set; }

    public int UserID { get; set; }

    public string? Username { get; set; }

    public string? DisplayName { get; set; }
}
