namespace DnnMigration.Domain.Entities;

// MIGRATION: Ported from PermissionInfo.vb (DotNetNuke.Security.Permissions, lines 28-88).
// This is the FOUNDATIONAL base permission entity; FolderPermission, ModulePermission, and
// TabPermission all inherit from it (": Permission"). The legacy VB "Property Get/Set"
// backing-field style is converted to C# auto-properties. The legacy XML serialization
// attributes (<XmlElement("permissioncode")>, <XmlIgnore()>) and the IPropertyAccess /
// IHydratable concerns are intentionally dropped: relational mapping is handled by a Fluent
// IEntityTypeConfiguration<Permission> in the Infrastructure layer (PermissionConfiguration),
// honoring ADR-002 (the existing DNN 4.9.0.85 schema is mapped unchanged). Kept as a plain,
// attribute-free POCO with zero framework dependencies (Clean Architecture inner ring).
public class Permission
{
    // VB: <XmlElement("permissionid")> Public Property PermissionID() As Integer
    // Identity scalar; non-nullable (no Null sentinel default in the legacy constructor).
    public int PermissionID { get; set; }

    // VB: <XmlElement("permissioncode")> Public Property PermissionCode() As String
    // Nullable because legacy permission rows can carry a null code.
    public string? PermissionCode { get; set; }

    // VB: <XmlIgnore()> Public Property ModuleDefID() As Integer
    // Foreign-key style scalar; non-nullable (in-memory only in the legacy model).
    public int ModuleDefID { get; set; }

    // VB: <XmlElement("permissionkey")> Public Property PermissionKey() As String
    public string? PermissionKey { get; set; }

    // VB: <XmlIgnore()> Public Property PermissionName() As String
    public string? PermissionName { get; set; }
}
