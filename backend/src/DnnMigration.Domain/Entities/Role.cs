namespace DnnMigration.Domain.Entities;

// MIGRATION: Ported from Library/Components/Security/Roles/RoleInfo.vb (DotNetNuke.Security.Roles).
// XML serialization attributes (<XmlRoot>/<XmlElement>/<XmlIgnore>) are dropped to honor the
// zero-framework-dependency Domain rule; the legacy copyright header and all Imports are dropped.
// VB Single fees (ServiceFee, TrialFee) -> C# float. The legacy private backing fields + Property
// Get/Set are converted to C# auto-properties, preserving names and casing verbatim (e.g. RSVPCode).
// RoleController/RoleComparer are out of scope, and UserRole is modeled as a JOIN entity rather than
// inheriting from Role (legacy UserRoleInfo inherited RoleInfo). EF Core persistence/column mapping
// is configured separately via Fluent API (IEntityTypeConfiguration<Role>) in the Infrastructure layer.
//
// MIGRATION (CP1 schema-fidelity correction, ADR-002): the physical [Roles] table (authoritative
// DotNetNuke.Schema.SqlDataProvider) declares ServiceFee (money), TrialFee (money), TrialPeriod (int),
// BillingPeriod (int) and RoleGroupID (int) as NULL-able columns. They are therefore modeled as nullable
// CLR types (float?/int?) so EF Core materialization preserves the legacy Null.NullInteger/no-value
// semantics rather than coercing a missing DB value to 0 (a 0 RoleGroupID would falsely denote "role
// group 0"; a 0 fee would falsely denote "free" instead of "unset"). Recorded in MIGRATION_NOTES.md.

/// <summary>
/// Pure POCO entity representing the Role aggregate root — the Entity Layer Role object.
/// </summary>
/// <remarks>
/// C# port of the legacy <c>RoleInfo</c> value object. Domain semantics and the public contract are
/// preserved exactly; this type intentionally carries no framework dependencies (no attributes,
/// interfaces, or <c>using</c> directives) so the Domain layer remains the dependency-free inner ring
/// of the Clean Architecture solution.
/// </remarks>
public class Role
{
    /// <summary>
    /// Gets or sets the unique identifier of the role (primary key).
    /// </summary>
    public int RoleID { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the portal that owns the role (foreign key).
    /// </summary>
    public int PortalID { get; set; }

    // MIGRATION: physical [Roles].RoleGroupID is NULL-able -> nullable int (legacy Null.NullInteger
    // "no role group" sentinel; null != 0, so an ungrouped role is not silently coerced to group 0).
    /// <summary>
    /// Gets or sets the identifier of the role group the role belongs to (foreign key); null when ungrouped.
    /// </summary>
    public int? RoleGroupID { get; set; }

    /// <summary>
    /// Gets or sets the name of the role.
    /// </summary>
    public string? RoleName { get; set; }

    /// <summary>
    /// Gets or sets the description of the role.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the billing frequency for the role.
    /// </summary>
    /// <remarks>
    /// Legacy single-character codes are preserved verbatim:
    /// N = None, O = One time fee, D = Daily, W = Weekly, M = Monthly, Y = Yearly.
    /// </remarks>
    public string? BillingFrequency { get; set; }

    // MIGRATION: physical [Roles].ServiceFee is a NULL-able money column -> nullable float (null = unset,
    // distinct from 0 = explicitly free; preserves legacy no-value semantics).
    /// <summary>
    /// Gets or sets the recurring service fee charged for the role; null when unset.
    /// </summary>
    public float? ServiceFee { get; set; }

    /// <summary>
    /// Gets or sets the trial frequency for the role.
    /// </summary>
    /// <remarks>
    /// Legacy single-character codes are preserved verbatim:
    /// N = None, O = One time fee, D = Daily, W = Weekly, M = Monthly, Y = Yearly.
    /// </remarks>
    public string? TrialFrequency { get; set; }

    // MIGRATION: physical [Roles].TrialPeriod is NULL-able -> nullable int (null = unset).
    /// <summary>
    /// Gets or sets the length of the trial period; null when unset.
    /// </summary>
    public int? TrialPeriod { get; set; }

    // MIGRATION: physical [Roles].BillingPeriod is NULL-able -> nullable int (null = unset).
    /// <summary>
    /// Gets or sets the length of the billing period; null when unset.
    /// </summary>
    public int? BillingPeriod { get; set; }

    // MIGRATION: physical [Roles].TrialFee is a NULL-able money column -> nullable float (null = unset).
    /// <summary>
    /// Gets or sets the trial fee charged for the role; null when unset.
    /// </summary>
    public float? TrialFee { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the role is public.
    /// </summary>
    public bool IsPublic { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether users are automatically assigned to the role.
    /// </summary>
    public bool AutoAssignment { get; set; }

    /// <summary>
    /// Gets or sets the RSVP code used to subscribe to the role.
    /// </summary>
    public string? RSVPCode { get; set; }

    /// <summary>
    /// Gets or sets the icon file associated with the role.
    /// </summary>
    public string? IconFile { get; set; }
}
