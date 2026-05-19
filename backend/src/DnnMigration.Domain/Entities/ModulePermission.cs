// -----------------------------------------------------------------------------
// DnnMigration - Modern DotNetNuke Migration
// Copyright (c) 2024. All rights reserved.
// Licensed under the MIT License.
// -----------------------------------------------------------------------------
// MIGRATION: VB.NET DotNetNuke.Security.Permissions.ModulePermissionInfo → C# 12 ModulePermission entity
// Source: Library/Components/Security/Permissions/ModulePermission.vb
// Changes:
// - Converted from VB.NET Class to C# 12 class with file-scoped namespace
// - No longer inherits from PermissionInfo - uses composition instead
// - Access permission definition (PermissionCode, PermissionKey, PermissionName) via Permission navigation
// - Removed XmlElement attributes (EF Core uses Fluent API)
// - Removed denormalized RoleName, Username, DisplayName (access via navigation properties)
// - Applied nullable reference types
// - Added navigation properties to Permission, Module, Role, User
// - Added XML documentation comments
// -----------------------------------------------------------------------------

namespace DnnMigration.Domain.Entities;

/// <summary>
/// Represents a module permission entity for assigning permissions at the module level.
/// </summary>
/// <remarks>
/// <para>
/// This entity maps to the ModulePermission table and enables fine-grained access control
/// for individual module instances. It links a <see cref="Permission"/> definition to a 
/// specific <see cref="Module"/> instance, granting or denying access to either a 
/// <see cref="Role"/> (for role-based permissions) or an individual <see cref="User"/> 
/// (for user-specific permissions).
/// </para>
/// <para>
/// MIGRATION: Converted from VB.NET DotNetNuke.Security.Permissions.ModulePermissionInfo.
/// The original class inherited from PermissionInfo; this has been changed to composition
/// with a navigation property to Permission. Denormalized display fields (RoleName, Username, 
/// DisplayName) have been removed - access these through the Role and User navigation properties.
/// </para>
/// <para>
/// Business Rule: Either RoleId or UserId should be set, but not both simultaneously.
/// This constraint is enforced at the application layer, not in the entity itself.
/// </para>
/// </remarks>
public class ModulePermission
{
    /// <summary>
    /// Gets or sets the unique identifier for the module permission.
    /// </summary>
    /// <value>The primary key of the module permission record.</value>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET _modulePermissionID field.
    /// Original default value was Null.NullInteger (-1).
    /// </remarks>
    public int ModulePermissionId { get; set; }

    /// <summary>
    /// Gets or sets the module identifier that this permission applies to.
    /// </summary>
    /// <value>The foreign key to the <see cref="Module"/> entity.</value>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET _moduleID field.
    /// Identifies which module instance this permission assignment is for.
    /// </remarks>
    public int ModuleId { get; set; }

    /// <summary>
    /// Gets or sets the permission identifier defining the type of permission.
    /// </summary>
    /// <value>The foreign key to the <see cref="Permission"/> entity.</value>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET inherited PermissionID property.
    /// Access PermissionCode, PermissionKey, and PermissionName through the Permission navigation property.
    /// </remarks>
    public int PermissionId { get; set; }

    /// <summary>
    /// Gets or sets the role identifier for role-based permissions.
    /// </summary>
    /// <value>
    /// The foreign key to the <see cref="Role"/> entity. 
    /// Null when this is a user-specific permission.
    /// </value>
    /// <remarks>
    /// <para>
    /// MIGRATION: Converted from VB.NET _roleID field with nullable type.
    /// </para>
    /// <para>
    /// In the legacy system, special values were used:
    /// - glbRoleNothing (-4): Indicated no specific role assigned
    /// - Null.NullInteger (-1): Indicated a null/unset value
    /// These have been consolidated to use nullable int with null representing no role.
    /// </para>
    /// <para>
    /// Note: Either RoleId or UserId should be populated, but not both.
    /// </para>
    /// </remarks>
    public int? RoleId { get; set; }

    /// <summary>
    /// Gets or sets the user identifier for user-specific permissions.
    /// </summary>
    /// <value>
    /// The foreign key to the <see cref="User"/> entity.
    /// Null when this is a role-based permission.
    /// </value>
    /// <remarks>
    /// <para>
    /// MIGRATION: Converted from VB.NET _userID field with nullable type.
    /// </para>
    /// <para>
    /// When set, this permission applies to a specific user rather than a role.
    /// Note: Either RoleId or UserId should be populated, but not both.
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
    /// MIGRATION: Converted from VB.NET _AllowAccess field.
    /// </para>
    /// <para>
    /// When <c>false</c>, this represents an explicit deny which typically takes 
    /// precedence over allow permissions from other assignments. The permission
    /// evaluation logic at the application layer determines the final access decision.
    /// </para>
    /// </remarks>
    public bool AllowAccess { get; set; }

    // Navigation properties

    /// <summary>
    /// Gets or sets the module that this permission applies to.
    /// </summary>
    /// <value>Navigation property to the <see cref="Module"/> entity.</value>
    /// <remarks>
    /// Use this navigation property to access module details including:
    /// <see cref="Module.ModuleId"/>, <see cref="Module.ModuleTitle"/>, <see cref="Module.PortalId"/>.
    /// </remarks>
    public virtual Module? Module { get; set; }

    /// <summary>
    /// Gets or sets the permission definition for this assignment.
    /// </summary>
    /// <value>Navigation property to the <see cref="Permission"/> entity.</value>
    /// <remarks>
    /// <para>
    /// MIGRATION: This navigation property replaces the inheritance from PermissionInfo.
    /// </para>
    /// <para>
    /// Use this navigation property to access permission definition details including:
    /// <see cref="Permission.PermissionId"/>, <see cref="Permission.PermissionCode"/>,
    /// <see cref="Permission.PermissionKey"/>, <see cref="Permission.PermissionName"/>,
    /// <see cref="Permission.ModuleDefId"/>.
    /// </para>
    /// </remarks>
    public virtual Permission? Permission { get; set; }

    /// <summary>
    /// Gets or sets the role that this permission applies to.
    /// </summary>
    /// <value>
    /// Navigation property to the <see cref="Role"/> entity.
    /// Null when this is a user-specific permission (UserId is set instead).
    /// </value>
    /// <remarks>
    /// <para>
    /// MIGRATION: This navigation property replaces the denormalized RoleName field.
    /// </para>
    /// <para>
    /// Use this navigation property to access role details including:
    /// <see cref="Role.RoleId"/>, <see cref="Role.RoleName"/>.
    /// </para>
    /// </remarks>
    public virtual Role? Role { get; set; }

    /// <summary>
    /// Gets or sets the user that this permission applies to.
    /// </summary>
    /// <value>
    /// Navigation property to the <see cref="User"/> entity.
    /// Null when this is a role-based permission (RoleId is set instead).
    /// </value>
    /// <remarks>
    /// <para>
    /// MIGRATION: This navigation property replaces the denormalized Username and DisplayName fields.
    /// </para>
    /// <para>
    /// Use this navigation property to access user details including:
    /// <see cref="User.UserId"/>, <see cref="User.Username"/>, <see cref="User.DisplayName"/>.
    /// </para>
    /// </remarks>
    public virtual User? User { get; set; }
}
