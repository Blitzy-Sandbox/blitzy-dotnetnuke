// -----------------------------------------------------------------------------
// DnnMigration - Modern DotNetNuke Migration
// Copyright (c) 2024. All rights reserved.
// Licensed under the MIT License.
// -----------------------------------------------------------------------------
// MIGRATION: VB.NET DotNetNuke.Security.Permissions.FolderPermissionInfo → C# 12 FolderPermission entity
// Source: Library/Components/Security/Permissions/FolderPermission.vb
// Changes:
// - Converted from VB.NET Class to C# 12 class with file-scoped namespace
// - No longer inherits from PermissionInfo (composition over inheritance)
// - Removed XmlElement attributes (EF Core uses Fluent API)
// - Removed denormalized RoleName, Username, DisplayName (use navigation properties)
// - Applied nullable reference types
// - Added navigation properties to Permission, Role, User
// - Added XML documentation comments
// -----------------------------------------------------------------------------

namespace DnnMigration.Domain.Entities;

/// <summary>
/// Represents a permission assignment for a folder to a specific role or user.
/// </summary>
/// <remarks>
/// <para>
/// This entity maps to the FolderPermission table and links a <see cref="Permission"/>
/// definition to a specific folder (identified by FolderID), granting or denying access
/// to a <see cref="Role"/> or individual <see cref="User"/>.
/// </para>
/// <para>
/// MIGRATION: Converted from VB.NET DotNetNuke.Security.Permissions.FolderPermissionInfo.
/// The original class inherited from PermissionInfo; this has been changed to composition.
/// Denormalized display fields (RoleName, Username, DisplayName) removed.
/// </para>
/// </remarks>
public class FolderPermission
{
    /// <summary>
    /// Gets or sets the unique identifier for the folder permission.
    /// </summary>
    /// <value>The primary key of the folder permission record.</value>
    public int FolderPermissionId { get; set; }

    /// <summary>
    /// Gets or sets the folder identifier that this permission applies to.
    /// </summary>
    /// <value>The foreign key to the Folder entity (or FolderID in the Folders table).</value>
    public int FolderId { get; set; }

    /// <summary>
    /// Gets or sets the permission identifier defining the type of permission.
    /// </summary>
    /// <value>The foreign key to the Permission entity.</value>
    public int PermissionId { get; set; }

    /// <summary>
    /// Gets or sets the role identifier that this permission applies to.
    /// </summary>
    /// <value>The foreign key to the Role entity. May be null for user-specific permissions.</value>
    /// <remarks>
    /// A value of -1 in the legacy system indicated "All Users" or no specific role.
    /// </remarks>
    public int? RoleId { get; set; }

    /// <summary>
    /// Gets or sets the user identifier for user-specific permissions.
    /// </summary>
    /// <value>The foreign key to the User entity. May be null for role-based permissions.</value>
    public int? UserId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether access is allowed.
    /// </summary>
    /// <value><c>true</c> if access is granted; <c>false</c> if access is denied.</value>
    /// <remarks>
    /// When false, this represents an explicit deny which takes precedence over other allows.
    /// </remarks>
    public bool AllowAccess { get; set; }

    /// <summary>
    /// Gets or sets the date when this permission was created.
    /// </summary>
    /// <value>The creation date of the permission record.</value>
    public DateTime CreatedDate { get; set; }

    // Navigation properties

    /// <summary>
    /// Gets or sets the permission definition.
    /// </summary>
    /// <value>Navigation property to the <see cref="Permission"/> entity.</value>
    public virtual Permission? Permission { get; set; }

    /// <summary>
    /// Gets or sets the role that this permission applies to.
    /// </summary>
    /// <value>Navigation property to the <see cref="Role"/> entity. May be null for user-specific permissions.</value>
    public virtual Role? Role { get; set; }

    /// <summary>
    /// Gets or sets the user that this permission applies to.
    /// </summary>
    /// <value>Navigation property to the <see cref="User"/> entity. May be null for role-based permissions.</value>
    public virtual User? User { get; set; }
}
