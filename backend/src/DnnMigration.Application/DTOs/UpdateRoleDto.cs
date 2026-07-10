namespace DnnMigration.Application.DTOs;

/// <summary>
/// Request payload to update an existing role. Bound from the JSON body of
/// <c>PUT /api/roles/{id}</c> and validated by the sibling FluentValidation
/// validator; it carries only the editable role attributes.
/// </summary>
/// <remarks>
/// MIGRATION: This DTO is the editable subset of the legacy
/// <c>RoleInfo</c> entity (Library/Components/Security/Roles/RoleInfo.vb).
/// The role identifier is taken from the route ({id}), so <c>RoleID</c> is
/// intentionally omitted from the body, and <c>PortalID</c> is immutable and
/// therefore also excluded. VB types are mirrored to idiomatic C#: legacy
/// <c>Single</c> fee fields map to <see cref="float"/>, <c>Integer</c> fields
/// to <see cref="int"/>, <c>Boolean</c> flags to <see cref="bool"/>, and the
/// optional VB <c>String</c> fields to nullable <see cref="string"/>. The
/// field set corresponds to the editable inputs on the legacy
/// Website/admin/Security/editroles.ascx screen.
/// </remarks>
public record UpdateRoleDto
{
    /// <summary>The display name of the role.</summary>
    public string RoleName { get; init; } = string.Empty;

    /// <summary>An optional human-readable description of the role.</summary>
    public string? Description { get; init; }

    /// <summary>Identifier of the role group this role belongs to.</summary>
    public int RoleGroupID { get; init; }

    /// <summary>Whether the role is publicly visible for self-subscription.</summary>
    public bool IsPublic { get; init; }

    /// <summary>Whether new users are automatically assigned to this role.</summary>
    public bool AutoAssignment { get; init; }

    /// <summary>The recurring service fee charged for the role.</summary>
    public float ServiceFee { get; init; }

    /// <summary>
    /// The billing frequency code for the service fee (for example N, O, D, W, M, Y).
    /// </summary>
    public string? BillingFrequency { get; init; }

    /// <summary>The length of the billing period, expressed in billing-frequency units.</summary>
    public int BillingPeriod { get; init; }

    /// <summary>The trial fee charged during the trial period.</summary>
    public float TrialFee { get; init; }

    /// <summary>The length of the trial period, expressed in trial-frequency units.</summary>
    public int TrialPeriod { get; init; }

    /// <summary>
    /// The trial frequency code for the trial period (for example N, O, D, W, M, Y).
    /// </summary>
    public string? TrialFrequency { get; init; }

    /// <summary>An optional RSVP code used to control self-subscription to the role.</summary>
    public string? RSVPCode { get; init; }

    /// <summary>An optional path to the icon file associated with the role.</summary>
    public string? IconFile { get; init; }
}
