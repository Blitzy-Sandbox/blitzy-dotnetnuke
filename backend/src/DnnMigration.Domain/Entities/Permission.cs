namespace DnnMigration.Domain.Entities;

// MIGRATION: Converted from VB.NET DotNetNuke.Security.Permissions.PermissionInfo
// (Library/Components/Security/Permissions/Permission.vb). Renamed PermissionInfo -> Permission
// (dropped the legacy "Info" suffix). XML-serialization attributes removed; persistence-ignorant POCO.
//
// MIGRATION (QA-4 #4): this is the standalone [Permission] CATALOG entity (5 columns: PermissionID,
// PermissionCode, ModuleDefID, PermissionKey, PermissionName). It is NO LONGER the base of an inheritance
// hierarchy â€” ModulePermission/TabPermission/FolderPermission now relate to it by COMPOSITION (each carries a
// PermissionID FK), matching the four physically-separate legacy tables. See PermissionConfiguration.
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
