// MIGRATION: Converted from VB.NET RoleInfo.vb (Library/Components/Security/Roles/RoleInfo.vb)
// Original namespace: DotNetNuke.Security.Roles
// Target namespace: DnnMigration.Application.DTOs.Role

namespace DnnMigration.Application.DTOs.Role;

/// <summary>
/// Data Transfer Object representing a security role entity in API responses.
/// </summary>
/// <remarks>
/// <para>
/// MIGRATION: This record is converted from the legacy VB.NET RoleInfo class.
/// All properties are mapped from the original entity with the following conversions:
/// </para>
/// <list type="bullet">
/// <item>VB.NET Private fields with Property accessors → C# auto-properties</item>
/// <item>VB.NET Single type → C# decimal for monetary precision (ServiceFee, TrialFee)</item>
/// <item>XmlIgnore attributes removed (using JSON serialization instead)</item>
/// <item>Null.NullInteger sentinel values → nullable types (int?)</item>
/// </list>
/// <para>
/// Used by GET /api/roles and GET /api/roles/{id} endpoints.
/// </para>
/// </remarks>
public record RoleDto
{
    /// <summary>
    /// Gets the unique identifier for the role.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From _RoleID field (RoleInfo.vb line 43).
    /// Originally marked with XmlIgnore in VB.NET; included in API response for identification.
    /// </remarks>
    public int RoleId { get; init; }

    /// <summary>
    /// Gets the identifier of the portal this role belongs to.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From _PortalID field (RoleInfo.vb line 44).
    /// Originally marked with XmlIgnore in VB.NET; included for multi-tenant context.
    /// </remarks>
    public int PortalId { get; init; }

    /// <summary>
    /// Gets the identifier of the role group this role belongs to, or null for global roles.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From _RoleGroupID field (RoleInfo.vb line 45).
    /// Originally marked with XmlIgnore in VB.NET.
    /// Value of -1 (Null.NullInteger) in legacy code is represented as null in this DTO.
    /// </remarks>
    public int? RoleGroupId { get; init; }

    /// <summary>
    /// Gets the display name of the role.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From _RoleName field (RoleInfo.vb line 46).
    /// Originally marked with XmlElement("rolename").
    /// </remarks>
    public required string RoleName { get; init; }

    /// <summary>
    /// Gets the description of the role, or null if not specified.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From _Description field (RoleInfo.vb line 47).
    /// Originally marked with XmlElement("description").
    /// </remarks>
    public string? Description { get; init; }

    /// <summary>
    /// Gets the service fee amount for the role subscription.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From _ServiceFee field (RoleInfo.vb line 48).
    /// Type converted from VB.NET Single to C# decimal for improved monetary precision.
    /// Originally marked with XmlElement("servicefee").
    /// </remarks>
    public decimal ServiceFee { get; init; }

    /// <summary>
    /// Gets the billing frequency code for the role subscription.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From _BillingFrequency field (RoleInfo.vb line 49).
    /// Originally marked with XmlElement("billingfrequency").
    /// <para>Valid values:</para>
    /// <list type="bullet">
    /// <item><term>N</term><description>None</description></item>
    /// <item><term>O</term><description>One time fee</description></item>
    /// <item><term>D</term><description>Daily</description></item>
    /// <item><term>W</term><description>Weekly</description></item>
    /// <item><term>M</term><description>Monthly</description></item>
    /// <item><term>Y</term><description>Yearly</description></item>
    /// </list>
    /// </remarks>
    public string? BillingFrequency { get; init; }

    /// <summary>
    /// Gets the billing period (number of units based on BillingFrequency).
    /// </summary>
    /// <remarks>
    /// MIGRATION: From _BillingPeriod field (RoleInfo.vb line 52).
    /// Originally marked with XmlElement("billingperiod").
    /// </remarks>
    public int BillingPeriod { get; init; }

    /// <summary>
    /// Gets the trial fee amount for the role trial period.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From _TrialFee field (RoleInfo.vb line 53).
    /// Type converted from VB.NET Single to C# decimal for improved monetary precision.
    /// Originally marked with XmlElement("trialfee").
    /// </remarks>
    public decimal TrialFee { get; init; }

    /// <summary>
    /// Gets the trial frequency code for the role trial period.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From _TrialFrequency field (RoleInfo.vb line 51).
    /// Originally marked with XmlElement("trialfrequency").
    /// <para>Valid values:</para>
    /// <list type="bullet">
    /// <item><term>N</term><description>None</description></item>
    /// <item><term>O</term><description>One time fee</description></item>
    /// <item><term>D</term><description>Daily</description></item>
    /// <item><term>W</term><description>Weekly</description></item>
    /// <item><term>M</term><description>Monthly</description></item>
    /// <item><term>Y</term><description>Yearly</description></item>
    /// </list>
    /// </remarks>
    public string? TrialFrequency { get; init; }

    /// <summary>
    /// Gets the trial period (number of units based on TrialFrequency).
    /// </summary>
    /// <remarks>
    /// MIGRATION: From _TrialPeriod field (RoleInfo.vb line 50).
    /// Originally marked with XmlElement("trialperiod").
    /// </remarks>
    public int TrialPeriod { get; init; }

    /// <summary>
    /// Gets a value indicating whether the role is publicly visible and can be requested by users.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From _IsPublic field (RoleInfo.vb line 54).
    /// Originally marked with XmlElement("ispublic").
    /// </remarks>
    public bool IsPublic { get; init; }

    /// <summary>
    /// Gets a value indicating whether users are automatically assigned to this role upon registration.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From _AutoAssignment field (RoleInfo.vb line 55).
    /// Originally marked with XmlElement("autoassignment").
    /// </remarks>
    public bool AutoAssignment { get; init; }

    /// <summary>
    /// Gets the RSVP code for invitation-based role signup, or null if not configured.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From _RSVPCode field (RoleInfo.vb line 56).
    /// Originally marked with XmlElement("rsvpcode").
    /// Users can enter this code to request membership in the role.
    /// </remarks>
    public string? RSVPCode { get; init; }

    /// <summary>
    /// Gets the path to the icon file associated with the role, or null if not configured.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From _IconFile field (RoleInfo.vb line 57).
    /// Originally marked with XmlElement("iconfile").
    /// </remarks>
    public string? IconFile { get; init; }
}
