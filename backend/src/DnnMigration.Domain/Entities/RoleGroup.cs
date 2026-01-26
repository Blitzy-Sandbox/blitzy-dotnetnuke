// -----------------------------------------------------------------------------
// DnnMigration - Modern DotNetNuke Migration
// Copyright (c) 2024. All rights reserved.
// Licensed under the MIT License.
// -----------------------------------------------------------------------------
// MIGRATION: VB.NET DotNetNuke.Security.Roles.RoleGroupInfo → C# 12 RoleGroup entity
// Source: Library/Components/Security/Roles/RoleGroupInfo.vb
// Changes:
// - Converted from VB.NET Class to C# 12 class with file-scoped namespace
// - Converted Private fields (_RoleGroupID, _PortalID, etc.) to C# auto-properties
// - Applied nullable reference types (string? for Description)
// - Added navigation property for Portal relationship
// - Added collection navigation property for Roles in this group
// - Added XML documentation comments
// -----------------------------------------------------------------------------

namespace DnnMigration.Domain.Entities;

/// <summary>
/// Represents a role group for organizing roles into logical groupings within a portal.
/// </summary>
/// <remarks>
/// <para>
/// This entity maps to the RoleGroups table and provides a way to categorize and organize
/// roles within a portal. Each role group belongs to a specific portal and can contain
/// multiple <see cref="Role"/> entities.
/// </para>
/// <para>
/// MIGRATION: Converted from VB.NET DotNetNuke.Security.Roles.RoleGroupInfo.
/// </para>
/// </remarks>
public class RoleGroup
{
    /// <summary>
    /// Gets or sets the unique identifier for the role group.
    /// </summary>
    /// <value>The primary key of the role group record.</value>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET _RoleGroupID field.
    /// </remarks>
    public int RoleGroupId { get; set; }

    /// <summary>
    /// Gets or sets the portal identifier that this role group belongs to.
    /// </summary>
    /// <value>The foreign key to the Portal entity.</value>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET _PortalID field.
    /// </remarks>
    public int PortalId { get; set; }

    /// <summary>
    /// Gets or sets the name of the role group.
    /// </summary>
    /// <value>The display name for the role group.</value>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET _RoleGroupName field.
    /// </remarks>
    public string RoleGroupName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of the role group.
    /// </summary>
    /// <value>A description of the role group's purpose. May be null.</value>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET _Description field.
    /// </remarks>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the portal that this role group belongs to.
    /// </summary>
    /// <value>Navigation property to the <see cref="Portal"/> entity.</value>
    /// <remarks>
    /// This navigation property allows access to the parent portal from a role group instance.
    /// </remarks>
    public virtual Portal? Portal { get; set; }

    /// <summary>
    /// Gets or sets the collection of roles in this role group.
    /// </summary>
    /// <value>
    /// A collection of <see cref="Role"/> entities that belong to this role group.
    /// </value>
    /// <remarks>
    /// Roles can optionally belong to a role group for organizational purposes.
    /// </remarks>
    public virtual ICollection<Role> Roles { get; set; } = new List<Role>();
}
