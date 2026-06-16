namespace DnnMigration.Domain.Entities;

/// <summary>
/// Association (JOIN) entity that links a <see cref="User"/> to a <see cref="Role"/>
/// within the DotNetNuke security subsystem, together with the per-assignment data
/// that governs the lifetime and billing state of that membership (effective/expiry
/// window, trial usage, and subscription flag).
/// </summary>
/// <remarks>
/// Pure POCO data carrier: no behavior, no framework dependencies. Persistence mapping
/// onto the existing (unchanged) DotNetNuke 4.9.0.85 database schema — including table
/// and column names and the user&lt;-&gt;role foreign keys — is supplied by an EF Core
/// Fluent <c>IEntityTypeConfiguration&lt;UserRole&gt;</c> in the Infrastructure layer
/// rather than by attributes on this type.
///
/// MIGRATION: Ported from <c>UserRoleInfo.vb</c> (<c>Library/Components/Users/UserRoleInfo.vb</c>,
/// namespace <c>DotNetNuke.Entities.Users</c>) of DotNetNuke 4.9.0.85. The legacy class declared
/// <c>UserRoleInfo Inherits RoleInfo</c>; that inheritance is intentionally NOT reproduced. Per the
/// Agent Action Plan intent this is modeled as a clean relational JOIN entity (user&lt;-&gt;role) with
/// explicit foreign keys (<see cref="UserID"/>, <see cref="RoleID"/>) and navigation properties
/// (<see cref="User"/>, <see cref="Role"/>), rather than by inheriting <see cref="Role"/>. Because the
/// legacy type obtained its <c>RoleID</c> via inheritance from <c>RoleInfo</c>, an explicit
/// <see cref="RoleID"/> foreign key is declared here. The legacy denormalized display fields
/// <c>FullName</c> and <c>Email</c> are dropped: those values are reached through the
/// <see cref="User"/> navigation (<c>userRole.User?.FullName</c> / <c>userRole.User?.Email</c>),
/// per the folder specification's "prefer navigation to User/Role" guidance. The legacy private
/// backing field / <c>Property Get/Set</c> pattern is converted to C# auto-properties, VB
/// <c>Date</c> members become <see cref="System.DateTime"/>, and the legacy <c>Null</c> date
/// sentinels become nullable <c>DateTime?</c> values for the optional effective/expiry window.
/// </remarks>
public class UserRole
{
    /// <summary>
    /// Gets or sets the unique identifier (primary key) of this user-role assignment.
    /// Legacy VB equivalent: <c>UserRoleID As Integer</c>.
    /// </summary>
    public int UserRoleID { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the assigned <see cref="User"/>.
    /// Acts as the foreign key on the user side of the join.
    /// Legacy VB equivalent: <c>UserID As Integer</c>.
    /// </summary>
    public int UserID { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the assigned <see cref="Role"/>.
    /// Acts as the foreign key on the role side of the join.
    /// </summary>
    // MIGRATION: explicit FK — legacy UserRoleInfo inherited RoleID via RoleInfo (Inherits RoleInfo);
    // since inheritance is flattened into a JOIN entity, RoleID is declared explicitly here.
    public int RoleID { get; set; }

    /// <summary>
    /// Gets or sets the date on which this role assignment becomes effective.
    /// Optional membership window start; <c>null</c> when unbounded.
    /// Legacy VB equivalent: <c>EffectiveDate As Date</c> (Null sentinel → nullable).
    /// </summary>
    public DateTime? EffectiveDate { get; set; }

    /// <summary>
    /// Gets or sets the date on which this role assignment expires.
    /// Optional membership window end; <c>null</c> when the assignment does not expire.
    /// Legacy VB equivalent: <c>ExpiryDate As Date</c> (Null sentinel → nullable).
    /// </summary>
    public DateTime? ExpiryDate { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the trial period for this role has been used.
    /// Legacy VB equivalent: <c>IsTrialUsed As Boolean</c>.
    /// </summary>
    public bool IsTrialUsed { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the user is currently subscribed to the role.
    /// Legacy VB equivalent: <c>Subscribed As Boolean</c>.
    /// </summary>
    public bool Subscribed { get; set; }

    // --- Navigations (relational join user<->role) ---

    /// <summary>
    /// Gets or sets the navigation to the assigned <see cref="User"/>.
    /// </summary>
    // MIGRATION: replaces the legacy denormalized FullName/Email display fields — those values are
    // reached via this navigation (userRole.User?.FullName / userRole.User?.Email).
    public User? User { get; set; }

    /// <summary>
    /// Gets or sets the navigation to the assigned <see cref="Role"/>.
    /// </summary>
    // MIGRATION: replaces the legacy "Inherits RoleInfo" relationship with an explicit navigation
    // keyed by RoleID, decoupling the assignment record from the Role definition.
    public Role? Role { get; set; }
}
