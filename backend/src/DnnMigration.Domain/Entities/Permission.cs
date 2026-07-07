namespace DnnMigration.Domain.Entities;

/// <summary>
/// Base permission entity. Base class for <see cref="ModulePermission"/>,
/// <see cref="TabPermission"/>, and <see cref="FolderPermission"/>.
/// </summary>
// MIGRATION: Converted from VB.NET DotNetNuke.Security.Permissions.PermissionInfo
// (Library/Components/Security/Permissions/Permission.vb, L28). XML-serialization attributes
// (IPropertyAccess/IHydratable were NOT implemented here) are dropped; only data/state
// properties are retained. EF Core mapping to the existing schema is configured downstream in
// DnnMigration.Infrastructure via IEntityTypeConfiguration<Permission>.
public class Permission
{
    // MIGRATION: legacy XML element "permissionid"
    public int PermissionID { get; set; }

    // MIGRATION: legacy XML element "permissioncode"
    public string PermissionCode { get; set; } = string.Empty;

    // MIGRATION: legacy XmlIgnore
    public int ModuleDefID { get; set; }

    // MIGRATION: legacy XML element "permissionkey"
    public string PermissionKey { get; set; } = string.Empty;

    // MIGRATION: legacy XmlIgnore
    public string PermissionName { get; set; } = string.Empty;
}
