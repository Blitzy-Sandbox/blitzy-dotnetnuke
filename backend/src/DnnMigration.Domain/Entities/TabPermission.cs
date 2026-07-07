namespace DnnMigration.Domain.Entities;

/// <summary>
/// Tab (page)-scoped permission entry. Derives from <see cref="Permission"/>.
/// </summary>
// MIGRATION: Converted from VB.NET DotNetNuke.Security.Permissions.TabPermissionInfo
// (Library/Components/Security/Permissions/TabPermission.vb, L29), which inherited PermissionInfo.
// Inheritance is preserved (: Permission). Only data/state properties are retained.
public class TabPermission : Permission
{
    // MIGRATION: legacy XML element "tabpermissionid"
    public int TabPermissionID { get; set; }

    // MIGRATION: legacy XML element "tabid"
    public int TabID { get; set; }

    // MIGRATION: legacy XML element "roleid"
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
