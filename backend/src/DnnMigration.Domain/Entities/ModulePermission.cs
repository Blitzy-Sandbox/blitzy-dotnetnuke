namespace DnnMigration.Domain.Entities;

// MIGRATION: Ported from ModulePermissionInfo.vb (DotNetNuke.Security.Permissions). VB
// `Inherits PermissionInfo` is preserved as `: Permission`. The legacy XML serialization
// attributes (<XmlElement>), the Null/glbRoleNothing constructor sentinels, the copy-constructor
// (New(ByVal permission As PermissionInfo)) and the Equals override (duplicate-detection concern
// for ModulePermissionCollection) are intentionally dropped from this POCO; those behaviors move
// to the service layer. EF mapping is handled by Fluent IEntityTypeConfiguration in the
// Infrastructure layer (ModulePermissionConfiguration). This entity is referenced by Module.cs
// as an ICollection<ModulePermission> navigation, so it stays a clean, attribute-free POCO.
public class ModulePermission : Permission
{
    public int ModulePermissionID { get; set; }

    public int ModuleID { get; set; }

    public int RoleID { get; set; }

    public string? RoleName { get; set; }

    public bool AllowAccess { get; set; }

    public int UserID { get; set; }

    public string? Username { get; set; }

    public string? DisplayName { get; set; }
}
