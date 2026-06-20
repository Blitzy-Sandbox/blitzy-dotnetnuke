namespace DnnMigration.Domain.Entities;

// MIGRATION: Ported from TabPermissionInfo.vb (DotNetNuke.Security.Permissions).
// VB `Inherits PermissionInfo` is preserved as `: Permission`, so the inherited base
// members (PermissionID, PermissionCode, ModuleDefID, PermissionKey, PermissionName) are
// NOT re-declared here. This tab/page-permission junction entity associates a Tab (page)
// with a Role and/or User and an access flag.
//
// Behavioral notes (Minimal Change Clause):
//  * The legacy XML serialization attributes (<XmlElement("tabpermissionid")>, etc.) are
//    intentionally dropped; relational mapping is handled by a Fluent
//    IEntityTypeConfiguration<TabPermission> in the Infrastructure layer, honoring ADR-002
//    (the existing DNN 4.9.0.85 schema is mapped unchanged).
//  * The legacy constructor sentinels (Null.NullInteger / Null.NullString and the
//    Integer.Parse(glbRoleNothing) RoleID seed) are dropped; identity/FK integer keys default
//    to their CLR default and optional display strings are modeled as nullable reference types.
// Kept as a plain, attribute-free POCO with zero framework dependencies (Clean Architecture
// inner ring); no IPropertyAccess / IHydratable concerns survive the migration.
public class TabPermission : Permission
{
    // VB: <XmlElement("tabpermissionid")> Public Property TabPermissionID() As Integer
    // Identity scalar for the TabPermission row; non-nullable.
    public int TabPermissionID { get; set; }

    // VB: <XmlElement("tabid")> Public Property TabID() As Integer
    // Foreign key to the owning Tab (page); non-nullable.
    public int TabID { get; set; }

    // VB: <XmlElement("roleid")> Public Property RoleID() As Integer
    // Foreign key to the granted Role; non-nullable (legacy glbRoleNothing seed dropped).
    public int RoleID { get; set; }

    // VB: <XmlElement("rolename")> Public Property RoleName() As String
    // Optional display-only role name; nullable.
    public string? RoleName { get; set; }

    // VB: <XmlElement("allowaccess")> Public Property AllowAccess() As Boolean
    // Grant (true) vs. deny (false) flag for this permission entry.
    public bool AllowAccess { get; set; }

    // VB: <XmlElement("userid")> Public Property UserID() As Integer
    // Foreign key to the granted User; non-nullable.
    public int UserID { get; set; }

    // VB: <XmlElement("username")> Public Property Username() As String
    // Optional display-only username; nullable.
    public string? Username { get; set; }

    // VB: <XmlElement("displayname")> Public Property DisplayName() As String
    // Optional display-only full name; nullable.
    public string? DisplayName { get; set; }
}
