namespace DnnMigration.Domain.Entities;

// MIGRATION: NEW relational entity introduced to restore schema fidelity (ADR-002). In legacy DotNetNuke
// 4.9.0.85 a user's membership of a portal lives in the physical [UserPortals] table (composite key
// UserId+PortalId), NOT on the [Users] table. The legacy UserInfo value object FLATTENED PortalID onto the
// user, so CP2 UserConfiguration Ignore()s User.PortalID because it is not a physical [Users] column. This
// POCO maps the real [UserPortals] table so the repository can JOIN [Users] -> [UserPortals] to resolve
// portal-scoped user queries WITHOUT filtering the ignored User.PortalID member, which would not translate
// against the preserved SQL Server schema.
// The legacy copyright header and Imports are dropped. EF Core persistence/column mapping (including the
// composite key and the lowercase-d physical column casing) is configured separately via Fluent API
// (IEntityTypeConfiguration<UserPortal> in UserPortalConfiguration) in the Infrastructure layer; this Domain
// type intentionally carries zero framework dependencies.

/// <summary>
/// Pure POCO entity for the DotNetNuke <c>UserPortals</c> table, which records a user's membership of a
/// portal - the physical source of the user-to-portal association that the legacy <c>UserInfo</c> object
/// flattened onto its <c>PortalID</c> property.
/// </summary>
/// <remarks>
/// The table has a composite primary key over (<see cref="UserID"/>, <see cref="PortalID"/>); the
/// <see cref="UserPortalID"/> column is a surrogate <c>IDENTITY</c> that is NOT part of the key. Within the
/// current scope this entity is read-only (queried via a join to resolve portal membership); it carries data
/// only and has no framework dependencies, keeping the Domain layer the dependency-free inner ring.
/// </remarks>
public class UserPortal
{
    /// <summary>
    /// Gets or sets the identifier of the member user (composite-key part; physical column <c>UserId</c>).
    /// </summary>
    public int UserID { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the portal the user belongs to (composite-key part; physical column
    /// <c>PortalId</c>).
    /// </summary>
    public int PortalID { get; set; }

    /// <summary>
    /// Gets or sets the surrogate identity of the membership row (physical column <c>UserPortalId</c>,
    /// <c>IDENTITY</c>; not part of the primary key).
    /// </summary>
    public int UserPortalID { get; set; }

    /// <summary>
    /// Gets or sets the date the user was added to the portal.
    /// </summary>
    public DateTime CreatedDate { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the user's portal membership is authorised.
    /// </summary>
    public bool Authorised { get; set; }
}
