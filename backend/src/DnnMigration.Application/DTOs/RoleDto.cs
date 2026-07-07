namespace DnnMigration.Application.DTOs;

/// <summary>
/// Read-only projection of a security role returned by the Roles API
/// (<c>GET /api/roles</c> and <c>GET /api/roles/{id}</c>).
/// </summary>
/// <remarks>
/// MIGRATION: mapped from the legacy VB.NET entity <c>RoleInfo</c>
/// (Library/Components/Security/Roles/RoleInfo.vb). This is an outbound
/// Data Transfer Object at the Application boundary: it is projected from the
/// <c>Role</c> domain entity via AutoMapper and never carries business logic
/// or data-access concerns. Property names and CLR types intentionally mirror
/// the source entity one-for-one so AutoMapper requires no custom value
/// converters. The legacy <c>Single</c> fee columns map to <see cref="float"/>
/// and the legacy <c>Integer</c> columns map to <see cref="int"/>.
/// </remarks>
public record RoleDto
{
    /// <summary>
    /// Unique identifier of the role. MIGRATION: from <c>RoleInfo.RoleID</c> (VB <c>Integer</c>).
    /// </summary>
    public int RoleID { get; init; }

    /// <summary>
    /// Identifier of the portal that owns the role. MIGRATION: from <c>RoleInfo.PortalID</c> (VB <c>Integer</c>).
    /// </summary>
    public int PortalID { get; init; }

    /// <summary>
    /// Identifier of the role group the role belongs to. MIGRATION: from <c>RoleInfo.RoleGroupID</c> (VB <c>Integer</c>).
    /// </summary>
    public int RoleGroupID { get; init; }

    /// <summary>
    /// Display name of the role. MIGRATION: from <c>RoleInfo.RoleName</c>.
    /// </summary>
    public string RoleName { get; init; } = string.Empty;

    /// <summary>
    /// Human-readable description of the role. MIGRATION: from <c>RoleInfo.Description</c>.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Indicates whether the role is public (self-selectable by users).
    /// MIGRATION: from <c>RoleInfo.IsPublic</c>.
    /// </summary>
    public bool IsPublic { get; init; }

    /// <summary>
    /// Indicates whether users are automatically assigned to the role.
    /// MIGRATION: from <c>RoleInfo.AutoAssignment</c>.
    /// </summary>
    public bool AutoAssignment { get; init; }

    /// <summary>
    /// Recurring fee charged for the role. MIGRATION: from <c>RoleInfo.ServiceFee</c> (VB <c>Single</c>).
    /// </summary>
    public float ServiceFee { get; init; }

    /// <summary>
    /// Billing frequency code for the service fee (N=None, O=One-time, D=Daily, W=Weekly, M=Monthly, Y=Yearly).
    /// MIGRATION: from <c>RoleInfo.BillingFrequency</c>.
    /// </summary>
    public string BillingFrequency { get; init; } = string.Empty;

    /// <summary>
    /// Length of the billing period, expressed in units of <see cref="BillingFrequency"/>.
    /// MIGRATION: from <c>RoleInfo.BillingPeriod</c> (VB <c>Integer</c>).
    /// </summary>
    public int BillingPeriod { get; init; }

    /// <summary>
    /// Fee charged for the trial period. MIGRATION: from <c>RoleInfo.TrialFee</c> (VB <c>Single</c>).
    /// </summary>
    public float TrialFee { get; init; }

    /// <summary>
    /// Length of the trial period, expressed in units of <see cref="TrialFrequency"/>.
    /// MIGRATION: from <c>RoleInfo.TrialPeriod</c> (VB <c>Integer</c>).
    /// </summary>
    public int TrialPeriod { get; init; }

    /// <summary>
    /// Trial frequency code (N=None, O=One-time, D=Daily, W=Weekly, M=Monthly, Y=Yearly).
    /// MIGRATION: from <c>RoleInfo.TrialFrequency</c>.
    /// </summary>
    public string TrialFrequency { get; init; } = string.Empty;

    /// <summary>
    /// RSVP code used to grant role membership by invitation. MIGRATION: from <c>RoleInfo.RSVPCode</c>.
    /// </summary>
    public string RSVPCode { get; init; } = string.Empty;

    /// <summary>
    /// Relative path to the icon file associated with the role. MIGRATION: from <c>RoleInfo.IconFile</c>.
    /// </summary>
    public string IconFile { get; init; } = string.Empty;
}
