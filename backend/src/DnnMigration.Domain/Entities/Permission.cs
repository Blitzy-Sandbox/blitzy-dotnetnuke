namespace DnnMigration.Domain.Entities;

/// <summary>
/// Base permission entity. Base class for the <c>ModulePermission</c>,
/// <c>TabPermission</c>, and <c>FolderPermission</c> entities.
/// </summary>
// NOTE: The derived permission entities are referenced as inline code (&lt;c&gt;...&lt;/c&gt;)
// rather than &lt;see cref&gt; links. The dependency-free Domain layer is compiled with
// GenerateDocumentationFile under the Gate 1 warnings-as-errors build, so unresolved crefs
// (CS1574) would fail the build while those sibling entities are not yet part of this
// compilation (they arrive in a later checkpoint). This mirrors the UserProfile.cs convention.
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
