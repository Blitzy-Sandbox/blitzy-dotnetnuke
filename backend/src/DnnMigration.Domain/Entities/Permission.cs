namespace DnnMigration.Domain.Entities;

// MIGRATION: Converted from VB.NET DotNetNuke.Security.Permissions.PermissionInfo
// (Library/Components/Security/Permissions/Permission.vb). Renamed PermissionInfo -> Permission
// (dropped the legacy "Info" suffix). XML-serialization attributes removed; persistence-ignorant POCO.
// This is the base class of the permission hierarchy (ModulePermission/TabPermission/FolderPermission inherit it).
public class Permission
{
    // MIGRATION: Legacy <XmlElement("permissionid")> Integer PermissionID. DB column PermissionID mapped via Fluent API.
    public int PermissionId { get; set; }

    // MIGRATION: Legacy <XmlElement("permissioncode")> String PermissionCode.
    public string? PermissionCode { get; set; }

    // MIGRATION: Legacy <XmlIgnore()> Integer ModuleDefID.
    public int ModuleDefId { get; set; }

    // MIGRATION: Legacy <XmlElement("permissionkey")> String PermissionKey.
    public string? PermissionKey { get; set; }

    // MIGRATION: Legacy <XmlIgnore()> String PermissionName.
    public string? PermissionName { get; set; }
}
