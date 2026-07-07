namespace DnnMigration.Domain.Entities;

/// <summary>
/// Module-scoped permission entry. Derives from <see cref="Permission"/>.
/// </summary>
// MIGRATION: Converted from VB.NET DotNetNuke.Security.Permissions.ModulePermissionInfo
// (Library/Components/Security/Permissions/ModulePermission.vb, L28), which inherited PermissionInfo.
// Inheritance is preserved (: Permission). The legacy Equals() override and the New(PermissionInfo)
// copy-constructor are DROPPED — they are collection-dedup business logic, not persisted state; a
// parameterless constructor (compiler-provided) is retained for EF Core.
public class ModulePermission : Permission
{
    // MIGRATION: legacy XML element "modulepermissionid"
    public int ModulePermissionID { get; set; }

    // MIGRATION: legacy XML element "moduleid"
    public int ModuleID { get; set; }

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
