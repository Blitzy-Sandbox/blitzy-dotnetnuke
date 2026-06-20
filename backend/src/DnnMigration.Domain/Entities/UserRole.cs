namespace DnnMigration.Domain.Entities;

// MIGRATION: Ported from Library/Components/Users/UserRoleInfo.vb (DotNetNuke.Entities.Users).
// The legacy "Public Class UserRoleInfo Inherits RoleInfo" inheritance is intentionally NOT reproduced.
// Per the AAP intent, this is modeled as a clean relational JOIN entity (user<->role) with explicit
// foreign keys (UserID, RoleID) plus navigation properties to User and Role, rather than inheriting Role.
//   - RoleID is declared explicitly here because the legacy class obtained RoleID implicitly via RoleInfo.RoleID.
//   - The legacy denormalized display fields FullName/Email are dropped in favor of the User navigation
//     (userRole.User?.FullName / userRole.User?.Email), per the folder spec "prefer navigation to User/Role".
//   - VB Date -> C# DateTime; the optional EffectiveDate/ExpiryDate windows map to nullable DateTime?.
// The legacy copyright header and Imports are dropped. EF Core persistence/column mapping is configured
// separately via Fluent API (IEntityTypeConfiguration<UserRole>) in the Infrastructure layer; this Domain
// type intentionally carries zero framework dependencies (no attributes, interfaces, or using directives).

/// <summary>
/// Pure POCO join entity representing the association between a <see cref="User"/> and a <see cref="Role"/>.
/// </summary>
/// <remarks>
/// C# port of the legacy <c>UserRoleInfo</c> business object. In the legacy DotNetNuke 4.9.0.85 codebase
/// this type inherited <c>RoleInfo</c>; in the rewrite it is flattened into a relational join carrying its
/// own primary key (<see cref="UserRoleID"/>) and explicit foreign keys (<see cref="UserID"/>,
/// <see cref="RoleID"/>) together with the role's subscription/trial window. This type carries data only;
/// it has no framework dependencies so the Domain layer remains the dependency-free inner ring of the
/// Clean Architecture solution.
/// </remarks>
public class UserRole
{
    /// <summary>
    /// Gets or sets the unique identifier of the user-role association (primary key).
    /// </summary>
    public int UserRoleID { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the associated user (foreign key to <see cref="User"/>).
    /// </summary>
    public int UserID { get; set; }

    // MIGRATION: explicit FK — the legacy UserRoleInfo inherited RoleID via RoleInfo.RoleID; modeled here as a
    // first-class foreign key on the join entity since Role inheritance is intentionally not reproduced.
    /// <summary>
    /// Gets or sets the identifier of the associated role (foreign key to <see cref="Role"/>).
    /// </summary>
    public int RoleID { get; set; }

    /// <summary>
    /// Gets or sets the date the role assignment becomes effective.
    /// </summary>
    /// <remarks>
    /// Optional window boundary. The legacy VB <c>Date</c> field maps to a nullable
    /// <see cref="System.DateTime"/>; an unset value indicates no effective-from constraint.
    /// </remarks>
    public DateTime? EffectiveDate { get; set; }

    /// <summary>
    /// Gets or sets the date the role assignment expires.
    /// </summary>
    /// <remarks>
    /// Optional window boundary. The legacy VB <c>Date</c> field maps to a nullable
    /// <see cref="System.DateTime"/>; an unset value indicates the assignment does not expire.
    /// </remarks>
    public DateTime? ExpiryDate { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the role's trial period has already been used by the user.
    /// </summary>
    public bool IsTrialUsed { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the user is subscribed to the role.
    /// </summary>
    public bool Subscribed { get; set; }

    // MIGRATION: relational navigations replace the legacy denormalized FullName/Email display fields.
    // Consumers read userRole.User?.FullName / userRole.User?.Email instead of duplicated columns.

    /// <summary>
    /// Gets or sets the navigation property to the associated <see cref="User"/>.
    /// </summary>
    public User? User { get; set; }

    /// <summary>
    /// Gets or sets the navigation property to the associated <see cref="Role"/>.
    /// </summary>
    public Role? Role { get; set; }
}
