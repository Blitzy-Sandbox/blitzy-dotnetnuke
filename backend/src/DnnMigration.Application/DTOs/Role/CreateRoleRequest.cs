// -----------------------------------------------------------------------------
// DnnMigration - Modern .NET 8 Migration of DotNetNuke
// Copyright (c) DnnMigration Project. All rights reserved.
// Licensed under the MIT License.
// -----------------------------------------------------------------------------
// MIGRATION: Derived from Website/admin/Security/EditRoles.ascx.vb cmdUpdate_Click handler (lines 208-274)
// MIGRATION: Form fields mapped from original ASPX form controls and RoleInfo.vb entity properties
// -----------------------------------------------------------------------------

using System.ComponentModel.DataAnnotations;

namespace DnnMigration.Application.DTOs.Role;

/// <summary>
/// Request DTO for creating a new security role in the system.
/// Contains all form fields from the legacy EditRoles.ascx.vb form for role creation via POST /api/roles endpoint.
/// </summary>
/// <remarks>
/// <para>
/// MIGRATION: This DTO captures fields from the legacy DotNetNuke 4.x EditRoles.ascx.vb form controls:
/// </para>
/// <list type="bullet">
/// <item><description>txtRoleName - Role name (required)</description></item>
/// <item><description>txtDescription - Role description</description></item>
/// <item><description>cboRoleGroups - Role group dropdown</description></item>
/// <item><description>chkIsPublic - Public visibility checkbox</description></item>
/// <item><description>chkAutoAssignment - Auto-assignment checkbox</description></item>
/// <item><description>txtServiceFee, txtBillingPeriod, cboBillingFrequency - Billing configuration</description></item>
/// <item><description>txtTrialFee, txtTrialPeriod, cboTrialFrequency - Trial configuration</description></item>
/// <item><description>txtRSVPCode - RSVP invitation code</description></item>
/// <item><description>ctlIcon - Icon file selector</description></item>
/// </list>
/// </remarks>
public record CreateRoleRequest
{
    /// <summary>
    /// Gets the name of the role being created.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Mapped from txtRoleName control in EditRoles.ascx.vb (line 237: objRoleInfo.RoleName = txtRoleName.Text).
    /// Maximum length of 50 characters matches legacy database schema constraint.
    /// </remarks>
    /// <example>Subscribers</example>
    [Required(ErrorMessage = "Role name is required.")]
    [StringLength(50, ErrorMessage = "Role name cannot exceed 50 characters.")]
    public required string RoleName { get; init; }

    /// <summary>
    /// Gets the optional description for the role.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Mapped from txtDescription control in EditRoles.ascx.vb (line 238: objRoleInfo.Description = txtDescription.Text).
    /// Maximum length of 1000 characters provides ample space for detailed role descriptions.
    /// </remarks>
    /// <example>Members who have subscribed to premium content access.</example>
    [StringLength(1000, ErrorMessage = "Description cannot exceed 1000 characters.")]
    public string? Description { get; init; }

    /// <summary>
    /// Gets the optional role group identifier for categorizing the role.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Mapped from cboRoleGroups dropdown in EditRoles.ascx.vb (line 236: Integer.Parse(cboRoleGroups.SelectedValue)).
    /// A value of -1 or null indicates the role belongs to Global Roles (no specific group).
    /// </remarks>
    /// <example>2</example>
    public int? RoleGroupId { get; init; }

    /// <summary>
    /// Gets a value indicating whether the role is publicly visible.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Mapped from chkIsPublic checkbox in EditRoles.ascx.vb (line 245: objRoleInfo.IsPublic = chkIsPublic.Checked).
    /// Public roles are visible to users for self-enrollment; non-public roles are admin-assigned only.
    /// </remarks>
    /// <example>true</example>
    public bool IsPublic { get; init; }

    /// <summary>
    /// Gets a value indicating whether users are automatically assigned to this role upon registration.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Mapped from chkAutoAssignment checkbox in EditRoles.ascx.vb (line 246: objRoleInfo.AutoAssignment = chkAutoAssignment.Checked).
    /// When true, all new users are automatically added to this role.
    /// </remarks>
    /// <example>false</example>
    public bool AutoAssignment { get; init; }

    /// <summary>
    /// Gets the optional service fee for paid role membership.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Mapped from txtServiceFee control in EditRoles.ascx.vb (line 217: Single.Parse(txtServiceFee.Text)).
    /// VB.NET Single type converted to C# decimal for financial precision.
    /// A value of null or 0 indicates a free role.
    /// </remarks>
    /// <example>9.99</example>
    public decimal? ServiceFee { get; init; }

    /// <summary>
    /// Gets the optional billing period duration (number of frequency units).
    /// </summary>
    /// <remarks>
    /// MIGRATION: Mapped from txtBillingPeriod control in EditRoles.ascx.vb (line 218: Integer.Parse(txtBillingPeriod.Text)).
    /// Used in conjunction with BillingFrequency to determine billing cycle (e.g., 1 Month, 3 Months).
    /// </remarks>
    /// <example>1</example>
    public int? BillingPeriod { get; init; }

    /// <summary>
    /// Gets the optional billing frequency code for recurring billing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// MIGRATION: Mapped from cboBillingFrequency dropdown in EditRoles.ascx.vb (line 219: cboBillingFrequency.SelectedItem.Value).
    /// </para>
    /// <para>Frequency codes (from legacy RoleInfo.vb):</para>
    /// <list type="bullet">
    /// <item><description>N - None (no recurring billing)</description></item>
    /// <item><description>O - One time fee</description></item>
    /// <item><description>D - Daily</description></item>
    /// <item><description>W - Weekly</description></item>
    /// <item><description>M - Monthly</description></item>
    /// <item><description>Y - Yearly</description></item>
    /// </list>
    /// </remarks>
    /// <example>M</example>
    [StringLength(1, ErrorMessage = "Billing frequency must be a single character code.")]
    public string? BillingFrequency { get; init; }

    /// <summary>
    /// Gets the optional trial fee for the trial period.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Mapped from txtTrialFee control in EditRoles.ascx.vb (line 227: Single.Parse(txtTrialFee.Text)).
    /// VB.NET Single type converted to C# decimal for financial precision.
    /// Trial is only applicable when ServiceFee is greater than 0.
    /// </remarks>
    /// <example>0.00</example>
    public decimal? TrialFee { get; init; }

    /// <summary>
    /// Gets the optional trial period duration (number of frequency units).
    /// </summary>
    /// <remarks>
    /// MIGRATION: Mapped from txtTrialPeriod control in EditRoles.ascx.vb (line 228: Integer.Parse(txtTrialPeriod.Text)).
    /// Used in conjunction with TrialFrequency to determine trial duration (e.g., 7 Days, 1 Month).
    /// </remarks>
    /// <example>7</example>
    public int? TrialPeriod { get; init; }

    /// <summary>
    /// Gets the optional trial frequency code.
    /// </summary>
    /// <remarks>
    /// <para>
    /// MIGRATION: Mapped from cboTrialFrequency dropdown in EditRoles.ascx.vb (line 229: cboTrialFrequency.SelectedItem.Value).
    /// </para>
    /// <para>Frequency codes (from legacy RoleInfo.vb):</para>
    /// <list type="bullet">
    /// <item><description>N - None (no trial period)</description></item>
    /// <item><description>O - One time</description></item>
    /// <item><description>D - Daily</description></item>
    /// <item><description>W - Weekly</description></item>
    /// <item><description>M - Monthly</description></item>
    /// <item><description>Y - Yearly</description></item>
    /// </list>
    /// </remarks>
    /// <example>D</example>
    [StringLength(1, ErrorMessage = "Trial frequency must be a single character code.")]
    public string? TrialFrequency { get; init; }

    /// <summary>
    /// Gets the optional RSVP code for invitation-based role signup.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Mapped from txtRSVPCode control in EditRoles.ascx.vb (line 247: objRoleInfo.RSVPCode = txtRSVPCode.Text).
    /// When set, users can join the role by navigating to the portal with ?rsvp={code} parameter.
    /// </remarks>
    /// <example>PREMIUM2024</example>
    [StringLength(50, ErrorMessage = "RSVP code cannot exceed 50 characters.")]
    public string? RSVPCode { get; init; }

    /// <summary>
    /// Gets the optional icon file path or URL for the role.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Mapped from ctlIcon.Url control in EditRoles.ascx.vb (line 248: objRoleInfo.IconFile = ctlIcon.Url).
    /// Can be a relative path within the portal or an external URL.
    /// </remarks>
    /// <example>~/images/roles/subscribers.png</example>
    [StringLength(500, ErrorMessage = "Icon file path cannot exceed 500 characters.")]
    public string? IconFile { get; init; }
}
