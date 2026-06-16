namespace DnnMigration.Domain.Entities;

/// <summary>
/// Role aggregate-root entity for the DotNetNuke security subsystem.
/// Represents a security role within a portal, including its membership-fee and
/// trial-billing configuration and self-registration (RSVP / auto-assignment) options.
/// Pure POCO: no behavior, no framework dependencies - persistence mapping is supplied
/// by an EF Core <c>IEntityTypeConfiguration&lt;Role&gt;</c> in the Infrastructure layer.
/// </summary>
// MIGRATION: Ported from RoleInfo.vb (DotNetNuke.Security.Roles). XML serialization attributes
// (XmlRoot/XmlElement/XmlIgnore) dropped; VB Single fees (ServiceFee, TrialFee) -> float;
// VB String members -> nullable string?. Domain semantics preserved verbatim (AAP section 0.7.1),
// including the RSVPCode casing. RoleController/RoleComparer are out of scope, and UserRole does
// NOT inherit from Role (legacy "UserRoleInfo Inherits RoleInfo" is modelled as a JOIN entity).
public class Role
{
    /// <summary>Primary key - unique identifier of the role.</summary>
    public int RoleID { get; set; }

    /// <summary>Foreign key - identifier of the portal that owns this role.</summary>
    public int PortalID { get; set; }

    // MIGRATION: schema column [RoleGroupID] int NULL -> nullable int? (Null.NullInteger -> null);
    // a role that belongs to no role group is null, never coerced to 0.
    /// <summary>Foreign key - identifier of the role group this role belongs to; <c>null</c> when ungrouped.</summary>
    public int? RoleGroupID { get; set; }

    /// <summary>Display name of the role.</summary>
    public string? RoleName { get; set; }

    /// <summary>Human-readable description of the role.</summary>
    public string? Description { get; set; }

    /// <summary>
    /// Billing frequency code for the role's service fee.
    /// Legacy codes: N = None, O = One-time fee, D = Daily, W = Weekly, M = Monthly, Y = Yearly.
    /// </summary>
    public string? BillingFrequency { get; set; }

    // MIGRATION: schema column [ServiceFee] money NULL -> nullable float? (no fee configured -> null).
    /// <summary>Recurring service fee charged for membership in the role; <c>null</c> when no fee applies.</summary>
    public float? ServiceFee { get; set; }

    /// <summary>
    /// Trial billing frequency code.
    /// Legacy codes: N = None, O = One-time fee, D = Daily, W = Weekly, M = Monthly, Y = Yearly.
    /// </summary>
    public string? TrialFrequency { get; set; }

    // MIGRATION: schema column [TrialPeriod] int NULL -> nullable int? (no trial configured -> null).
    /// <summary>Length of the trial period, expressed in units of <see cref="TrialFrequency"/>; <c>null</c> when no trial applies.</summary>
    public int? TrialPeriod { get; set; }

    // MIGRATION: schema column [BillingPeriod] int NULL -> nullable int? (no recurring billing -> null).
    /// <summary>Length of the billing period, expressed in units of <see cref="BillingFrequency"/>; <c>null</c> when no recurring billing applies.</summary>
    public int? BillingPeriod { get; set; }

    // MIGRATION: schema column [TrialFee] money NULL -> nullable float? (no trial fee configured -> null).
    /// <summary>Fee charged for the trial period; <c>null</c> when no trial fee applies.</summary>
    public float? TrialFee { get; set; }

    /// <summary>Indicates whether the role is public (users may subscribe to it themselves).</summary>
    public bool IsPublic { get; set; }

    /// <summary>Indicates whether new users are automatically assigned to this role.</summary>
    public bool AutoAssignment { get; set; }

    /// <summary>RSVP code used for self-service enrollment into the role.</summary>
    public string? RSVPCode { get; set; }

    /// <summary>Path to the icon file associated with the role.</summary>
    public string? IconFile { get; set; }
}
