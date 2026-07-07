namespace DnnMigration.Domain.Entities;

/// <summary>
/// Folder-scoped permission entry. Derives from <see cref="Permission"/>.
/// </summary>
// MIGRATION: Converted from VB.NET DotNetNuke.Security.Permissions.FolderPermissionInfo
// (Library/Components/Security/Permissions/FolderPermission.vb, L29), which inherited PermissionInfo.
// Inheritance is preserved (: Permission). Only data/state properties are retained.
public class FolderPermission : Permission
{
    // MIGRATION: legacy XmlIgnore
    public int FolderPermissionID { get; set; }

    // MIGRATION: legacy XmlIgnore
    public int FolderID { get; set; }

    // MIGRATION: legacy XmlIgnore
    public int PortalID { get; set; }

    // MIGRATION: legacy XML element "folderpath"
    public string FolderPath { get; set; } = string.Empty;

    // MIGRATION: legacy XmlIgnore
    public int RoleID { get; set; }

    // MIGRATION: legacy XML element "rolename"
    public string RoleName { get; set; } = string.Empty;

    // MIGRATION: legacy XML element "allowaccess"
    public bool AllowAccess { get; set; }

    // MIGRATION: legacy XML element "userid"
    public int UserID { get; set; }

    // MIGRATION: legacy XML element "username"
    public string Username { get; set; } = string.Empty;

    // MIGRATION: legacy XML element "displayname"
    public string DisplayName { get; set; } = string.Empty;
}
