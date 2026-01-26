// -----------------------------------------------------------------------------
// DnnMigration - Modern DotNetNuke Migration
// Copyright (c) 2024. All rights reserved.
// Licensed under the MIT License.
// -----------------------------------------------------------------------------
// MIGRATION: VB.NET DotNetNuke.Security.Roles.RoleInfo → C# 12 Role entity
// Source: Library/Components/Security/Roles/RoleInfo.vb
// Changes:
// - Converted from VB.NET Class to C# 12 class with file-scoped namespace
// - Converted Private fields to C# auto-properties
// - Removed XmlRoot and XmlElement attributes (EF Core uses Fluent API)
// - Converted VB Single type to C# decimal for ServiceFee and TrialFee (better currency precision)
// - Applied nullable reference types (string? for Description, BillingFrequency, etc.)
// - Added navigation properties for Portal, RoleGroup, and UserRoles collection
// - Added XML documentation comments
// -----------------------------------------------------------------------------

namespace DnnMigration.Domain.Entities;

/// <summary>
/// Represents a role for permission grouping within a portal.
/// </summary>
/// <remarks>
/// <para>
/// This entity maps to the Roles table and represents security roles that can be assigned
/// to users. Roles are used to group users for permission assignments. Each role belongs
/// to a specific portal and optionally to a <see cref="RoleGroup"/>.
/// </para>
/// <para>
/// MIGRATION: Converted from VB.NET DotNetNuke.Security.Roles.RoleInfo.
/// ServiceFee and TrialFee converted from Single to decimal for better currency precision.
/// </para>
/// </remarks>
public class Role
{
    /// <summary>
    /// Gets or sets the unique identifier for the role.
    /// </summary>
    /// <value>The primary key of the role record.</value>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET _RoleID field.
    /// </remarks>
    public int RoleId { get; set; }

    /// <summary>
    /// Gets or sets the portal identifier that this role belongs to.
    /// </summary>
    /// <value>The foreign key to the Portal entity.</value>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET _PortalID field.
    /// </remarks>
    public int PortalId { get; set; }

    /// <summary>
    /// Gets or sets the role group identifier that this role belongs to.
    /// </summary>
    /// <value>The foreign key to the RoleGroup entity, or null if not in a group.</value>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET _RoleGroupID field.
    /// A value of -1 or null indicates the role is not in a role group.
    /// </remarks>
    public int? RoleGroupId { get; set; }

    /// <summary>
    /// Gets or sets the name of the role.
    /// </summary>
    /// <value>The display name for the role.</value>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET _RoleName field.
    /// </remarks>
    public string RoleName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of the role.
    /// </summary>
    /// <value>A description of the role's purpose. May be null.</value>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET _Description field.
    /// </remarks>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the service fee for the role subscription.
    /// </summary>
    /// <value>The fee amount for subscribing to this role.</value>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET _ServiceFee field (Single → decimal for currency precision).
    /// </remarks>
    public decimal ServiceFee { get; set; }

    /// <summary>
    /// Gets or sets the billing frequency for the role.
    /// </summary>
    /// <value>
    /// A string representing the billing frequency:
    /// N - None, O - One time fee, D - Daily, W - Weekly, M - Monthly, Y - Yearly.
    /// May be null.
    /// </value>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET _BillingFrequency field.
    /// </remarks>
    public string? BillingFrequency { get; set; }

    /// <summary>
    /// Gets or sets the length of the billing period.
    /// </summary>
    /// <value>An integer representing the number of billing frequency units.</value>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET _BillingPeriod field.
    /// </remarks>
    public int BillingPeriod { get; set; }

    /// <summary>
    /// Gets or sets the trial fee for the role.
    /// </summary>
    /// <value>The trial fee amount for this role.</value>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET _TrialFee field (Single → decimal for currency precision).
    /// </remarks>
    public decimal TrialFee { get; set; }

    /// <summary>
    /// Gets or sets the trial frequency for the role.
    /// </summary>
    /// <value>
    /// A string representing the trial frequency:
    /// N - None, O - One time fee, D - Daily, W - Weekly, M - Monthly, Y - Yearly.
    /// May be null.
    /// </value>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET _TrialFrequency field.
    /// </remarks>
    public string? TrialFrequency { get; set; }

    /// <summary>
    /// Gets or sets the length of the trial period.
    /// </summary>
    /// <value>An integer representing the number of trial frequency units.</value>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET _TrialPeriod field.
    /// </remarks>
    public int TrialPeriod { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this role is public.
    /// </summary>
    /// <value><c>true</c> if users can subscribe to this role; otherwise, <c>false</c>.</value>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET _IsPublic field.
    /// </remarks>
    public bool IsPublic { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether users are automatically assigned to this role.
    /// </summary>
    /// <value><c>true</c> if new users are auto-assigned; otherwise, <c>false</c>.</value>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET _AutoAssignment field.
    /// </remarks>
    public bool AutoAssignment { get; set; }

    /// <summary>
    /// Gets or sets the RSVP code for the role.
    /// </summary>
    /// <value>A code that users can use to subscribe to the role. May be null.</value>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET _RSVPCode field.
    /// </remarks>
    public string? RSVPCode { get; set; }

    /// <summary>
    /// Gets or sets the icon file path for the role.
    /// </summary>
    /// <value>The relative path to the icon file. May be null.</value>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET _IconFile field.
    /// </remarks>
    public string? IconFile { get; set; }

    /// <summary>
    /// Gets or sets the portal that this role belongs to.
    /// </summary>
    /// <value>Navigation property to the <see cref="Portal"/> entity.</value>
    public virtual Portal? Portal { get; set; }

    /// <summary>
    /// Gets or sets the role group that this role belongs to.
    /// </summary>
    /// <value>Navigation property to the <see cref="RoleGroup"/> entity. May be null if role is not grouped.</value>
    public virtual RoleGroup? RoleGroup { get; set; }

    /// <summary>
    /// Gets or sets the collection of user-role assignments for this role.
    /// </summary>
    /// <value>A collection of <see cref="UserRole"/> entities representing users assigned to this role.</value>
    public virtual ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}
