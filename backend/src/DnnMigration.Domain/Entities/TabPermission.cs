// -----------------------------------------------------------------------------
// DnnMigration - Modern DotNetNuke Migration
// Copyright (c) 2024. All rights reserved.
// Licensed under the MIT License.
// -----------------------------------------------------------------------------
// MIGRATION: VB.NET DotNetNuke.Security.Permissions.TabPermissionInfo → C# 12 TabPermission entity
// Source: Library/Components/Security/Permissions/TabPermission.vb
// Changes:
// - Converted from VB.NET Class to C# 12 class with file-scoped namespace
//   'DnnMigration.Domain.Entities'
// - Do NOT inherit from PermissionInfo - use composition instead
//   (Permission navigation property replaces inheritance)
// - Removed inherited permission properties (PermissionCode, PermissionKey, PermissionName)
//   - Access these via Permission navigation property
// - Removed denormalized display fields (RoleName, Username, DisplayName)
//   - Access these via Role and User navigation properties respectively
// - Removed XmlElement attributes (EF Core uses Fluent API for mapping)
// - Applied nullable reference types for RoleId and UserId
// - Added navigation properties for Tab, Permission, Role (optional), User (optional)
// - Business Rule: Either RoleId or UserId should be set (but not both)
//   - This is enforced at the application layer, not domain entity
// - Added comprehensive XML documentation comments
// -----------------------------------------------------------------------------

namespace DnnMigration.Domain.Entities;

/// <summary>
/// Represents a permission assignment at the tab (page) level, granting or denying access
/// to a specific <see cref="Role"/> or individual <see cref="User"/>.
/// </summary>
/// <remarks>
/// <para>
/// This entity maps to the TabPermission table in the database and implements fine-grained
/// access control for individual pages in the navigation hierarchy. Each tab permission
/// links a <see cref="Permission"/> definition to a specific <see cref="Tab"/>, specifying
/// whether access is allowed or denied for the assigned role or user.
/// </para>
/// <para>
/// <b>Permission Assignment Types:</b>
/// <list type="bullet">
/// <item>
/// <term>Role-based Permission</term>
/// <description>
/// When <see cref="RoleId"/> is set and <see cref="UserId"/> is null, the permission
/// applies to all users in the specified role.
/// </description>
/// </item>
/// <item>
/// <term>User-specific Permission</term>
/// <description>
/// When <see cref="UserId"/> is set and <see cref="RoleId"/> is null, the permission
/// applies only to the specified individual user.
/// </description>
/// </item>
/// </list>
/// </para>
/// <para>
/// <b>Business Rule:</b> Either <see cref="RoleId"/> or <see cref="UserId"/> should be set,
/// but not both simultaneously. This constraint is enforced at the application layer
/// (service/validation) rather than the domain entity.
/// </para>
/// <para>
/// <b>Access Evaluation:</b> When <see cref="AllowAccess"/> is <c>false</c>, this represents
/// an explicit deny that typically takes precedence over other allow permissions in the
/// permission evaluation logic.
/// </para>
/// <para>
/// MIGRATION: Converted from VB.NET DotNetNuke.Security.Permissions.TabPermissionInfo.
/// The original class inherited from PermissionInfo to access PermissionCode, PermissionKey,
/// and PermissionName properties. This has been changed to composition - access permission
/// details through the <see cref="Permission"/> navigation property. Denormalized display
/// fields (RoleName, Username, DisplayName) have been removed; access these through
/// <see cref="Role"/> and <see cref="User"/> navigation properties.
/// </para>
/// </remarks>
/// <seealso cref="Permission"/>
/// <seealso cref="Tab"/>
/// <seealso cref="ModulePermission"/>
/// <seealso cref="FolderPermission"/>
public class TabPermission
{
    /// <summary>
    /// Gets or sets the unique identifier for the tab permission.
    /// </summary>
    /// <value>The primary key of the tab permission record.</value>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET _TabPermissionID Private field.
    /// Original VB.NET initialized this to Null.NullInteger (-1) in the constructor.
    /// </remarks>
    public int TabPermissionId { get; set; }

    /// <summary>
    /// Gets or sets the tab identifier that this permission applies to.
    /// </summary>
    /// <value>The foreign key to the <see cref="Tab"/> entity.</value>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET _TabID Private field.
    /// Original VB.NET initialized this to Null.NullInteger (-1) in the constructor.
    /// This identifies which page/tab in the navigation hierarchy the permission controls.
    /// </remarks>
    public int TabId { get; set; }

    /// <summary>
    /// Gets or sets the permission identifier defining the type of permission.
    /// </summary>
    /// <value>The foreign key to the <see cref="Permission"/> entity.</value>
    /// <remarks>
    /// MIGRATION: The original VB.NET inherited PermissionID from PermissionInfo base class.
    /// This property replaces the inherited relationship with a direct foreign key.
    /// Access permission details (PermissionCode, PermissionKey, PermissionName) through
    /// the <see cref="Permission"/> navigation property.
    /// </remarks>
    public int PermissionId { get; set; }

    /// <summary>
    /// Gets or sets the role identifier that this permission applies to.
    /// </summary>
    /// <value>
    /// The foreign key to the <see cref="Role"/> entity.
    /// <c>null</c> indicates a user-specific permission.
    /// </value>
    /// <remarks>
    /// <para>
    /// MIGRATION: Converted from VB.NET _roleID Private field.
    /// Original VB.NET initialized this to Integer.Parse(glbRoleNothing) which equals -1,
    /// indicating no specific role assignment. The nullable type better represents this semantic.
    /// </para>
    /// <para>
    /// When this property has a value and <see cref="UserId"/> is null, this permission
    /// applies to all users assigned to the specified role.
    /// </para>
    /// </remarks>
    public int? RoleId { get; set; }

    /// <summary>
    /// Gets or sets the user identifier for user-specific permissions.
    /// </summary>
    /// <value>
    /// The foreign key to the <see cref="User"/> entity.
    /// <c>null</c> indicates a role-based permission.
    /// </value>
    /// <remarks>
    /// <para>
    /// MIGRATION: Converted from VB.NET _userID Private field.
    /// Original VB.NET initialized this to Null.NullInteger (-1) indicating no specific user.
    /// The nullable type better represents this semantic.
    /// </para>
    /// <para>
    /// When this property has a value and <see cref="RoleId"/> is null, this permission
    /// applies only to the specific individual user, providing user-level access control
    /// that overrides or supplements role-based permissions.
    /// </para>
    /// </remarks>
    public int? UserId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether access is allowed or denied.
    /// </summary>
    /// <value>
    /// <c>true</c> if access is granted; <c>false</c> if access is explicitly denied.
    /// </value>
    /// <remarks>
    /// <para>
    /// MIGRATION: Converted from VB.NET _AllowAccess Private field.
    /// Original VB.NET initialized this to <c>False</c> in the constructor.
    /// </para>
    /// <para>
    /// When <c>false</c>, this represents an explicit deny which typically takes precedence
    /// over other allow permissions during security evaluation. This enables administrators
    /// to create exceptions to role-based permissions.
    /// </para>
    /// </remarks>
    public bool AllowAccess { get; set; }

    // -------------------------------------------------------------------------
    // Navigation Properties
    // -------------------------------------------------------------------------
    // These navigation properties enable EF Core to load related entities
    // and replace the denormalized properties from the original VB.NET implementation.
    // -------------------------------------------------------------------------

    /// <summary>
    /// Gets or sets the tab that this permission applies to.
    /// </summary>
    /// <value>Navigation property to the <see cref="Tab"/> entity.</value>
    /// <remarks>
    /// <para>
    /// This navigation property enables access to the tab (page) details including
    /// TabName, PortalId, and other tab properties.
    /// </para>
    /// <para>
    /// The relationship is defined by the <see cref="TabId"/> foreign key.
    /// </para>
    /// </remarks>
    public virtual Tab? Tab { get; set; }

    /// <summary>
    /// Gets or sets the permission definition.
    /// </summary>
    /// <value>Navigation property to the <see cref="Permission"/> entity.</value>
    /// <remarks>
    /// <para>
    /// MIGRATION: This navigation property replaces the inheritance relationship from PermissionInfo.
    /// Use this property to access:
    /// <list type="bullet">
    /// <item><description><see cref="Permission.PermissionId"/> - The permission identifier</description></item>
    /// <item><description><see cref="Permission.PermissionCode"/> - The permission category code</description></item>
    /// <item><description><see cref="Permission.PermissionKey"/> - The permission key (e.g., "VIEW", "EDIT")</description></item>
    /// <item><description><see cref="Permission.PermissionName"/> - The human-readable permission name</description></item>
    /// <item><description><see cref="Permission.ModuleDefId"/> - The associated module definition ID</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// The relationship is defined by the <see cref="PermissionId"/> foreign key.
    /// </para>
    /// </remarks>
    public virtual Permission? Permission { get; set; }

    /// <summary>
    /// Gets or sets the role that this permission applies to.
    /// </summary>
    /// <value>
    /// Navigation property to the <see cref="Role"/> entity.
    /// <c>null</c> when this is a user-specific permission.
    /// </value>
    /// <remarks>
    /// <para>
    /// MIGRATION: This navigation property replaces the denormalized _RoleName property
    /// from the original VB.NET implementation. Access RoleName through <c>Role.RoleName</c>.
    /// </para>
    /// <para>
    /// The relationship is defined by the optional <see cref="RoleId"/> foreign key.
    /// This property will be null when the permission is user-specific rather than role-based.
    /// </para>
    /// </remarks>
    public virtual Role? Role { get; set; }

    /// <summary>
    /// Gets or sets the user that this permission applies to.
    /// </summary>
    /// <value>
    /// Navigation property to the <see cref="User"/> entity.
    /// <c>null</c> when this is a role-based permission.
    /// </value>
    /// <remarks>
    /// <para>
    /// MIGRATION: This navigation property replaces the denormalized _Username and _DisplayName
    /// properties from the original VB.NET implementation. Access these through:
    /// <list type="bullet">
    /// <item><description><c>User.Username</c> - The user's login name</description></item>
    /// <item><description><c>User.DisplayName</c> - The user's display name</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// The relationship is defined by the optional <see cref="UserId"/> foreign key.
    /// This property will be null when the permission is role-based rather than user-specific.
    /// </para>
    /// </remarks>
    public virtual User? User { get; set; }
}
