// -----------------------------------------------------------------------------
// DnnMigration - Modern .NET 8 Migration of DotNetNuke
// Copyright (c) DnnMigration Project. All rights reserved.
// Licensed under the MIT License.
// -----------------------------------------------------------------------------
// MIGRATION: Derived from Website/admin/Security/EditRoles.ascx.vb cmdUpdate_Click handler
// MIGRATION: Form fields mapped from original ASPX form controls and RoleInfo.vb entity properties
// MIGRATION: All fields nullable to support partial updates (PATCH-style semantics)
// -----------------------------------------------------------------------------

using System.ComponentModel.DataAnnotations;

namespace DnnMigration.Application.DTOs.Role;

/// <summary>
/// Request DTO for updating an existing security role in the system.
/// Contains all updatable form fields from the legacy EditRoles.ascx.vb form.
/// All fields are nullable to support partial updates via PUT /api/roles/{id} endpoint.
/// </summary>
/// <remarks>
/// <para>
/// MIGRATION: This DTO captures updatable fields from the legacy DotNetNuke 4.x EditRoles.ascx.vb form controls:
/// </para>
/// <list type="bullet">
/// <item><description>txtRoleName - Role name (optional for update)</description></item>
/// <item><description>txtDescription - Role description</description></item>
/// <item><description>cboRoleGroups - Role group dropdown</description></item>
/// <item><description>chkIsPublic - Public visibility checkbox</description></item>
/// <item><description>chkAutoAssignment - Auto-assignment checkbox</description></item>
/// <item><description>txtServiceFee, txtBillingPeriod, cboBillingFrequency - Billing configuration</description></item>
/// <item><description>txtTrialFee, txtTrialPeriod, cboTrialFrequency - Trial configuration</description></item>
/// <item><description>txtRSVPCode - RSVP invitation code</description></item>
/// <item><description>ctlIcon - Icon file selector</description></item>
/// </list>
/// <para>
/// Unlike <see cref="CreateRoleRequest"/>, all properties are nullable to allow partial updates
/// where only the specified fields are modified.
/// </para>
/// </remarks>
public record UpdateRoleRequest
{
    /// <summary>
    /// Gets the updated name of the role.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Mapped from txtRoleName control in EditRoles.ascx.vb (line 237: objRoleInfo.RoleName = txtRoleName.Text).
    /// Maximum length of 50 characters matches legacy database schema constraint.
    /// Null value indicates no change to the role name.
    /// </remarks>
    /// <example>Premium Subscribers</example>
    [StringLength(50, ErrorMessage = "Role name cannot exceed 50 characters.")]
    public string? RoleName { get; init; }

    /// <summary>
    /// Gets the updated description for the role.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Mapped from txtDescription control in EditRoles.ascx.vb (line 238: objRoleInfo.Description = txtDescription.Text).
    /// Maximum length of 1000 characters provides ample space for detailed role descriptions.
    /// Null value indicates no change to the description.
    /// </remarks>
    /// <example>Members with premium access to all content.</example>
    [StringLength(1000, ErrorMessage = "Description cannot exceed 1000 characters.")]
    public string? Description { get; init; }

    /// <summary>
    /// Gets the updated role group identifier for categorizing the role.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Mapped from cboRoleGroups dropdown in EditRoles.ascx.vb (line 236: Integer.Parse(cboRoleGroups.SelectedValue)).
    /// A value of -1 indicates the role should belong to Global Roles (no specific group).
    /// Null value indicates no change to the role group assignment.
    /// </remarks>
    /// <example>3</example>
    public int? RoleGroupId { get; init; }

    /// <summary>
    /// Gets the updated value indicating whether the role is publicly visible.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Mapped from chkIsPublic checkbox in EditRoles.ascx.vb (line 245: objRoleInfo.IsPublic = chkIsPublic.Checked).
    /// Public roles are visible to users for self-enrollment; non-public roles are admin-assigned only.
    /// Null value indicates no change to public visibility setting.
    /// </remarks>
    /// <example>true</example>
    public bool? IsPublic { get; init; }

    /// <summary>
    /// Gets the updated value indicating whether users are automatically assigned to this role upon registration.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Mapped from chkAutoAssignment checkbox in EditRoles.ascx.vb (line 246: objRoleInfo.AutoAssignment = chkAutoAssignment.Checked).
    /// When true, all new users are automatically added to this role.
    /// Null value indicates no change to auto-assignment setting.
    /// </remarks>
    /// <example>false</example>
    public bool? AutoAssignment { get; init; }

    /// <summary>
    /// Gets the updated service fee for paid role membership.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Mapped from txtServiceFee control in EditRoles.ascx.vb (line 217: Single.Parse(txtServiceFee.Text)).
    /// VB.NET Single type converted to C# decimal for financial precision.
    /// A value of 0 indicates a free role; null indicates no change.
    /// </remarks>
    /// <example>19.99</example>
    public decimal? ServiceFee { get; init; }

    /// <summary>
    /// Gets the updated billing period duration (number of frequency units).
    /// </summary>
    /// <remarks>
    /// MIGRATION: Mapped from txtBillingPeriod control in EditRoles.ascx.vb (line 218: Integer.Parse(txtBillingPeriod.Text)).
    /// Used in conjunction with BillingFrequency to determine billing cycle (e.g., 1 Month, 3 Months).
    /// Null value indicates no change to the billing period.
    /// </remarks>
    /// <example>1</example>
    public int? BillingPeriod { get; init; }

    /// <summary>
    /// Gets the updated billing frequency code for recurring billing.
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
    /// <para>Null value indicates no change to the billing frequency.</para>
    /// </remarks>
    /// <example>Y</example>
    [StringLength(1, ErrorMessage = "Billing frequency must be a single character code.")]
    public string? BillingFrequency { get; init; }

    /// <summary>
    /// Gets the updated trial fee for the trial period.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Mapped from txtTrialFee control in EditRoles.ascx.vb (line 227: Single.Parse(txtTrialFee.Text)).
    /// VB.NET Single type converted to C# decimal for financial precision.
    /// Trial is only applicable when ServiceFee is greater than 0.
    /// Null value indicates no change to the trial fee.
    /// </remarks>
    /// <example>0.00</example>
    public decimal? TrialFee { get; init; }

    /// <summary>
    /// Gets the updated trial period duration (number of frequency units).
    /// </summary>
    /// <remarks>
    /// MIGRATION: Mapped from txtTrialPeriod control in EditRoles.ascx.vb (line 228: Integer.Parse(txtTrialPeriod.Text)).
    /// Used in conjunction with TrialFrequency to determine trial duration (e.g., 7 Days, 1 Month).
    /// Null value indicates no change to the trial period.
    /// </remarks>
    /// <example>14</example>
    public int? TrialPeriod { get; init; }

    /// <summary>
    /// Gets the updated trial frequency code.
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
    /// <para>Null value indicates no change to the trial frequency.</para>
    /// </remarks>
    /// <example>D</example>
    [StringLength(1, ErrorMessage = "Trial frequency must be a single character code.")]
    public string? TrialFrequency { get; init; }

    /// <summary>
    /// Gets the updated RSVP code for invitation-based role signup.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Mapped from txtRSVPCode control in EditRoles.ascx.vb (line 247: objRoleInfo.RSVPCode = txtRSVPCode.Text).
    /// When set, users can join the role by navigating to the portal with ?rsvp={code} parameter.
    /// Null value indicates no change to the RSVP code.
    /// Empty string can be used to clear the existing RSVP code.
    /// </remarks>
    /// <example>PREMIUM2024</example>
    [StringLength(50, ErrorMessage = "RSVP code cannot exceed 50 characters.")]
    public string? RSVPCode { get; init; }

    /// <summary>
    /// Gets the updated icon file path or URL for the role.
    /// </summary>
    /// <remarks>
    /// MIGRATION: Mapped from ctlIcon.Url control in EditRoles.ascx.vb (line 248: objRoleInfo.IconFile = ctlIcon.Url).
    /// Can be a relative path within the portal or an external URL.
    /// Null value indicates no change to the icon file.
    /// Empty string can be used to remove the existing icon.
    /// </remarks>
    /// <example>~/images/roles/premium.png</example>
    [StringLength(500, ErrorMessage = "Icon file path cannot exceed 500 characters.")]
    public string? IconFile { get; init; }
}
