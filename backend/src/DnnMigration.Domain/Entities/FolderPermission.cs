// -----------------------------------------------------------------------------
// DnnMigration - Modern DotNetNuke Migration
// Copyright (c) 2024. All rights reserved.
// Licensed under the MIT License.
// -----------------------------------------------------------------------------
// MIGRATION: VB.NET DotNetNuke.Security.Permissions.FolderPermissionInfo → C# 12 FolderPermission entity
// Source: Library/Components/Security/Permissions/FolderPermission.vb
// Changes:
// - Converted from VB.NET Class to C# 12 class with file-scoped namespace 'DnnMigration.Domain.Entities'
// - No longer inherits from PermissionInfo (composition over inheritance via Permission navigation property)
// - Removed inherited permission properties (PermissionCode, PermissionKey, PermissionName) - access via Permission navigation
// - Removed denormalized fields (PortalID, FolderPath, RoleName, Username, DisplayName) - access via navigation properties
// - Removed XmlElement/XmlIgnore attributes (EF Core uses Fluent API for configuration)
// - Applied nullable reference types for optional foreign keys (RoleId?, UserId?)
// - Added navigation properties to Permission, Role (optional), User (optional)
// - Added comprehensive XML documentation comments
// - Note: Either RoleId or UserId should be set (not both) - business rule enforced at application layer
// -----------------------------------------------------------------------------

namespace DnnMigration.Domain.Entities;

/// <summary>
/// Represents a folder permission entity that assigns a specific permission to a file system folder
/// for either a role or an individual user.
/// </summary>
/// <remarks>
/// <para>
/// This entity maps to the FolderPermission table and enables fine-grained access control for 
/// file management operations within portal folders. Each folder permission links a 
/// <see cref="Permission"/> definition to a specific folder (identified by <see cref="FolderId"/>),
/// granting or denying access to a <see cref="Role"/> or individual <see cref="User"/>.
/// </para>
/// <para>
/// <strong>Permission Assignment Model:</strong>
/// Each folder permission should target either a role OR a user, but not both simultaneously.
/// This is a business rule enforced at the application layer:
/// <list type="bullet">
///   <item><description>Role-based permission: <see cref="RoleId"/> is set, <see cref="UserId"/> is null</description></item>
///   <item><description>User-specific permission: <see cref="UserId"/> is set, <see cref="RoleId"/> is null</description></item>
/// </list>
/// </para>
/// <para>
/// <strong>Permission Access:</strong>
/// Permission details (PermissionCode, PermissionKey, PermissionName) are accessed through the
/// <see cref="Permission"/> navigation property rather than being duplicated on this entity.
/// This follows composition over inheritance pattern, replacing the original VB.NET inheritance
/// from PermissionInfo base class.
/// </para>
/// <para>
/// MIGRATION: Converted from VB.NET DotNetNuke.Security.Permissions.FolderPermissionInfo.
/// The original class inherited from PermissionInfo; this has been changed to composition.
/// Denormalized display fields (PortalID, FolderPath, RoleName, Username, DisplayName) removed -
/// use navigation properties instead.
/// </para>
/// </remarks>
/// <example>
/// Creating a role-based folder permission:
/// <code>
/// var permission = new FolderPermission
/// {
///     FolderId = 42,
///     PermissionId = 1,  // e.g., "READ" permission
///     RoleId = 5,        // Assign to a specific role
///     UserId = null,     // Not user-specific
///     AllowAccess = true
/// };
/// </code>
/// Creating a user-specific folder permission:
/// <code>
/// var permission = new FolderPermission
/// {
///     FolderId = 42,
///     PermissionId = 2,  // e.g., "WRITE" permission
///     RoleId = null,     // Not role-based
///     UserId = 100,      // Assign to specific user
///     AllowAccess = true
/// };
/// </code>
/// </example>
public class FolderPermission
{
    /// <summary>
    /// Gets or sets the unique identifier for the folder permission.
    /// </summary>
    /// <value>The primary key of the folder permission record.</value>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET _folderPermissionID field.
    /// Original VB.NET initialized to Null.NullInteger (-1).
    /// </remarks>
    public int FolderPermissionId { get; set; }

    /// <summary>
    /// Gets or sets the folder identifier that this permission applies to.
    /// </summary>
    /// <value>
    /// The foreign key referencing the folder in the folder management system.
    /// </value>
    /// <remarks>
    /// <para>
    /// MIGRATION: Converted from VB.NET _folderID field.
    /// Original VB.NET initialized to Null.NullInteger (-1).
    /// </para>
    /// <para>
    /// Note: FolderId references the folder management system. There is no explicit Folder entity
    /// in this migration scope as folders are handled separately by the file management subsystem.
    /// </para>
    /// </remarks>
    public int FolderId { get; set; }

    /// <summary>
    /// Gets or sets the permission identifier defining the type of permission being assigned.
    /// </summary>
    /// <value>The foreign key to the <see cref="Entities.Permission"/> entity.</value>
    /// <remarks>
    /// <para>
    /// MIGRATION: This replaces the inheritance relationship from PermissionInfo.
    /// The original VB.NET class inherited PermissionID, PermissionCode, PermissionKey, and
    /// PermissionName from the base class. In the C# 12 version, these are accessed through
    /// the <see cref="Permission"/> navigation property.
    /// </para>
    /// <para>
    /// Common permission types include:
    /// <list type="bullet">
    ///   <item><description>READ - View folder contents</description></item>
    ///   <item><description>WRITE - Create/modify files in folder</description></item>
    ///   <item><description>DELETE - Remove files from folder</description></item>
    ///   <item><description>BROWSE - Browse folder structure</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public int PermissionId { get; set; }

    /// <summary>
    /// Gets or sets the role identifier for role-based permissions.
    /// </summary>
    /// <value>
    /// The foreign key to the <see cref="Entities.Role"/> entity, or <c>null</c> for user-specific permissions.
    /// </value>
    /// <remarks>
    /// <para>
    /// MIGRATION: Converted from VB.NET _roleID field.
    /// Original VB.NET initialized to glbRoleNothing (typically "-1").
    /// Converted to nullable int to properly represent the absence of a role assignment.
    /// </para>
    /// <para>
    /// <strong>Business Rule:</strong> Either RoleId or UserId should be set, but not both.
    /// When RoleId is set, all users with that role receive the permission.
    /// </para>
    /// <para>
    /// Note: The denormalized RoleName field from the original VB.NET class has been removed.
    /// Access role details through the <see cref="Role"/> navigation property.
    /// </para>
    /// </remarks>
    public int? RoleId { get; set; }

    /// <summary>
    /// Gets or sets the user identifier for user-specific permissions.
    /// </summary>
    /// <value>
    /// The foreign key to the <see cref="Entities.User"/> entity, or <c>null</c> for role-based permissions.
    /// </value>
    /// <remarks>
    /// <para>
    /// MIGRATION: Converted from VB.NET _userID field.
    /// Original VB.NET initialized to Null.NullInteger (-1).
    /// Converted to nullable int to properly represent the absence of a user assignment.
    /// </para>
    /// <para>
    /// <strong>Business Rule:</strong> Either RoleId or UserId should be set, but not both.
    /// When UserId is set, only that specific user receives the permission.
    /// </para>
    /// <para>
    /// Note: The denormalized Username and DisplayName fields from the original VB.NET class
    /// have been removed. Access user details through the <see cref="User"/> navigation property.
    /// </para>
    /// </remarks>
    public int? UserId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether access is allowed or denied.
    /// </summary>
    /// <value>
    /// <c>true</c> if access is granted (allow); <c>false</c> if access is explicitly denied.
    /// </value>
    /// <remarks>
    /// <para>
    /// MIGRATION: Converted from VB.NET _AllowAccess field.
    /// Original VB.NET initialized to False.
    /// </para>
    /// <para>
    /// <strong>Permission Evaluation:</strong>
    /// When evaluating permissions, explicit denies (AllowAccess = false) typically take
    /// precedence over allows. This enables administrators to grant broad permissions to
    /// a role while explicitly denying access to specific users within that role.
    /// </para>
    /// </remarks>
    public bool AllowAccess { get; set; }

    // -------------------------------------------------------------------------
    // Navigation Properties
    // -------------------------------------------------------------------------

    /// <summary>
    /// Gets or sets the permission definition for this folder permission.
    /// </summary>
    /// <value>
    /// Navigation property to the <see cref="Entities.Permission"/> entity containing
    /// the permission code, key, and name.
    /// </value>
    /// <remarks>
    /// <para>
    /// MIGRATION: This navigation property replaces the inheritance relationship from PermissionInfo.
    /// Access permission details (PermissionCode, PermissionKey, PermissionName, ModuleDefId)
    /// through this property instead of inherited properties.
    /// </para>
    /// <example>
    /// Accessing permission details:
    /// <code>
    /// var permissionKey = folderPermission.Permission?.PermissionKey;
    /// var permissionName = folderPermission.Permission?.PermissionName;
    /// </code>
    /// </example>
    /// </remarks>
    public virtual Permission? Permission { get; set; }

    /// <summary>
    /// Gets or sets the role that this permission applies to.
    /// </summary>
    /// <value>
    /// Navigation property to the <see cref="Entities.Role"/> entity, or <c>null</c>
    /// if this is a user-specific permission (UserId is set instead of RoleId).
    /// </value>
    /// <remarks>
    /// <para>
    /// MIGRATION: This navigation property replaces the denormalized RoleName field
    /// from the original VB.NET class. Access role details through this property.
    /// </para>
    /// <example>
    /// Accessing role details:
    /// <code>
    /// var roleName = folderPermission.Role?.RoleName;
    /// </code>
    /// </example>
    /// </remarks>
    public virtual Role? Role { get; set; }

    /// <summary>
    /// Gets or sets the user that this permission applies to.
    /// </summary>
    /// <value>
    /// Navigation property to the <see cref="Entities.User"/> entity, or <c>null</c>
    /// if this is a role-based permission (RoleId is set instead of UserId).
    /// </value>
    /// <remarks>
    /// <para>
    /// MIGRATION: This navigation property replaces the denormalized Username and DisplayName
    /// fields from the original VB.NET class. Access user details through this property.
    /// </para>
    /// <example>
    /// Accessing user details:
    /// <code>
    /// var username = folderPermission.User?.Username;
    /// var displayName = folderPermission.User?.DisplayName;
    /// </code>
    /// </example>
    /// </remarks>
    public virtual User? User { get; set; }
}
