// -----------------------------------------------------------------------------
// DnnMigration - Modern DotNetNuke Migration
// Copyright (c) 2024. All rights reserved.
// Licensed under the MIT License.
// -----------------------------------------------------------------------------
// MIGRATION: VB.NET DotNetNuke.Security.Permissions.PermissionInfo → C# 12 Permission entity
// Source: Library/Components/Security/Permissions/Permission.vb
// Changes:
// - Converted from VB.NET Class to C# 12 class with file-scoped namespace
// - Converted VB Private fields (_permissionID, _permissionCode, etc.) to C# auto-properties
// - Applied nullable reference types (string? for optional strings)
// - Removed XmlElement attributes (not needed for EF Core)
// - Added XML documentation comments
// -----------------------------------------------------------------------------

namespace DnnMigration.Domain.Entities;

/// <summary>
/// Represents a base permission definition entity with permission code, key, and name.
/// </summary>
/// <remarks>
/// <para>
/// This entity maps to the Permission table and serves as the foundational entity for the
/// permission system. It defines the available permission types that can be assigned to
/// modules, tabs, or folders through the corresponding permission entities
/// (<see cref="ModulePermission"/>, <see cref="TabPermission"/>, <see cref="FolderPermission"/>).
/// </para>
/// <para>
/// MIGRATION: Converted from VB.NET DotNetNuke.Security.Permissions.PermissionInfo.
/// </para>
/// </remarks>
public class Permission
{
    /// <summary>
    /// Gets or sets the unique identifier for the permission.
    /// </summary>
    /// <value>The primary key of the permission record.</value>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET _permissionID field.
    /// </remarks>
    public int PermissionId { get; set; }

    /// <summary>
    /// Gets or sets the permission code that categorizes this permission.
    /// </summary>
    /// <value>A code string identifying the permission category (e.g., "SYSTEM_MODULE_DEFINITION"). May be null.</value>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET _permissionCode field.
    /// Permission codes are used to group related permissions together.
    /// </remarks>
    public string? PermissionCode { get; set; }

    /// <summary>
    /// Gets or sets the module definition identifier that this permission is associated with.
    /// </summary>
    /// <value>The foreign key to the ModuleDefinition entity, or -1 for system permissions.</value>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET _moduleDefID field.
    /// A value of -1 indicates this is a system-level permission not tied to a specific module.
    /// </remarks>
    public int ModuleDefId { get; set; }

    /// <summary>
    /// Gets or sets the permission key used as a unique identifier within a permission code.
    /// </summary>
    /// <value>A key string identifying the specific permission (e.g., "VIEW", "EDIT", "DELETE"). May be null.</value>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET _permissionKey field.
    /// The combination of PermissionCode and PermissionKey uniquely identifies a permission.
    /// </remarks>
    public string? PermissionKey { get; set; }

    /// <summary>
    /// Gets or sets the human-readable name of the permission.
    /// </summary>
    /// <value>The display name for the permission (e.g., "View", "Edit", "Delete"). May be null.</value>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET _PermissionName field.
    /// This is typically displayed in the permission management UI.
    /// </remarks>
    public string? PermissionName { get; set; }
}
