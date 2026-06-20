namespace DnnMigration.Domain.Entities;

// MIGRATION: Ported from Library/Components/Security/Permissions/ModulePermission.vb
// (VB class ModulePermissionInfo : PermissionInfo, namespace DotNetNuke.Security.Permissions).
// VB `Inherits PermissionInfo` is preserved as `: Permission`. The legacy XML serialization
// attributes (<XmlElement(...)>), the Null/glbRoleNothing constructor sentinels, the legacy
// copy-constructor (New(ByVal permission As PermissionInfo)) and the Equals override (a
// duplicate-detection concern for ModulePermissionCollection) are intentionally dropped from the
// POCO; those behaviors move to the service layer. ModulePermissionCollection,
// CompareModulePermissions and ModulePermissionController are out of scope. EF Core column mapping
// is configured separately via Fluent API (IEntityTypeConfiguration<ModulePermission>) in the
// Infrastructure layer, honoring ADR-002 (existing DNN 4.9.0.85 schema mapped unchanged). Kept as
// a plain, attribute-free POCO with zero framework dependencies (Clean Architecture inner ring).

/// <summary>
/// Pure POCO junction entity that grants or denies a role or user access to a module — the
/// module-permission row of the legacy DotNetNuke security model.
/// </summary>
/// <remarks>
/// C# port of the legacy <c>ModulePermissionInfo</c> value object, which inherited
/// <c>PermissionInfo</c>; that inheritance is preserved here as <c>: Permission</c>, so the five
/// base permission members (<c>PermissionID</c>, <c>PermissionCode</c>, <c>ModuleDefID</c>,
/// <c>PermissionKey</c>, <c>PermissionName</c>) are inherited and are intentionally not
/// re-declared. Domain semantics and the public contract are preserved exactly; this type carries
/// no framework dependencies (no attributes, interfaces, or <c>using</c> directives) so the Domain
/// layer remains the dependency-free inner ring of the Clean Architecture solution. The
/// <c>Module</c> aggregate root references this entity through an
/// <c>ICollection&lt;ModulePermission&gt;</c> navigation.
/// </remarks>
public class ModulePermission : Permission
{
    /// <summary>
    /// Gets or sets the unique identifier of the module-permission record (primary key).
    /// </summary>
    public int ModulePermissionID { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the module the permission applies to (foreign key).
    /// </summary>
    public int ModuleID { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the role the permission is granted to or denied (foreign key).
    /// </summary>
    public int RoleID { get; set; }

    /// <summary>
    /// Gets or sets the display name of the role; optional and used for presentation only.
    /// </summary>
    public string? RoleName { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether access is granted (<c>true</c>) or denied (<c>false</c>).
    /// </summary>
    public bool AllowAccess { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the user the permission targets when it applies to a single
    /// user rather than a role (foreign key).
    /// </summary>
    public int UserID { get; set; }

    /// <summary>
    /// Gets or sets the login name of the user; optional and used for presentation only.
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Gets or sets the display name of the user; optional and used for presentation only.
    /// </summary>
    public string? DisplayName { get; set; }
}
