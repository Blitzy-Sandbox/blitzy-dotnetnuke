namespace DnnMigration.Domain.Entities;

/// <summary>
/// UserPortal junction entity: the many-to-many association between a <see cref="User"/> and a
/// <see cref="Portal"/> the user belongs to.
/// </summary>
// MIGRATION (SCHEMA FIDELITY — finding #1): the legacy DotNetNuke schema has NO [Users].[PortalID]
// scalar column. A user's portal membership is represented by the dedicated [UserPortals] junction
// table, keyed by the COMPOSITE primary key ([UserId], [PortalId]) (PK_{objectQualifier}UserPortals),
// with foreign keys to [Users].[UserID] and [Portals].[PortalID]. The previous model invented a
// [Users].[PortalID] column; this entity restores the real relational shape. Column set and
// nullability verified verbatim against the [UserPortals] CREATE TABLE in
// DotNetNuke.Schema.SqlDataProvider.
public class UserPortal
{
    /// <summary>FK to [Users].[UserID]; first half of the composite key — [UserId] int NOT NULL.</summary>
    public int UserId { get; set; }

    /// <summary>FK to [Portals].[PortalID]; second half of the composite key — [PortalId] int NOT NULL.</summary>
    public int PortalId { get; set; }

    /// <summary>Surrogate identity column — [UserPortalId] int IDENTITY(1,1) (NOT the primary key).</summary>
    public int UserPortalId { get; set; }

    /// <summary>Row creation timestamp — [CreatedDate] datetime NOT NULL DEFAULT getdate().</summary>
    public DateTime CreatedDate { get; set; }

    /// <summary>Whether the user is authorised on the portal — [Authorised] bit NOT NULL DEFAULT 1.</summary>
    public bool Authorised { get; set; } = true;
}
