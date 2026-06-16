namespace DnnMigration.Domain.Entities;

// MIGRATION: Ported from PermissionInfo.vb (DotNetNuke.Security.Permissions). Base class for
// FolderPermission / ModulePermission / TabPermission. XML serialization attributes and the
// IPropertyAccess concern are intentionally dropped; EF mapping is handled by Fluent
// IEntityTypeConfiguration in the Infrastructure layer (PermissionConfiguration).
public class Permission
{
    public int PermissionID { get; set; }

    public string? PermissionCode { get; set; }

    public int ModuleDefID { get; set; }

    public string? PermissionKey { get; set; }

    public string? PermissionName { get; set; }
}
