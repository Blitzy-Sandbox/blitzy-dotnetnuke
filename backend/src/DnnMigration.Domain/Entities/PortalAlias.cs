namespace DnnMigration.Domain.Entities;

// MIGRATION: Ported from PortalAliasInfo.vb (DotNetNuke.Entities.Portals). Plain POCO; the legacy
// PortalAliasCollection (DictionaryBase) and PortalAliasController are intentionally out of scope
// (collections become EF navigation/queries; controller logic moves to the Application/Infrastructure
// layers). EF Core mapping (table/column names and keys) is applied via a Fluent
// IEntityTypeConfiguration in the Infrastructure layer, so this entity carries ZERO framework
// dependencies: no EF/DataAnnotation attributes, no XML attributes, and no interfaces.

/// <summary>
/// Represents a single portal HTTP alias record - the association between an inbound host/URL
/// alias and the portal it resolves to. This is a faithful C# port of the legacy
/// <c>PortalAliasInfo</c> value object from DotNetNuke <c>4.9.0.85</c>.
/// </summary>
public class PortalAlias
{
    /// <summary>
    /// Foreign key to the owning <see cref="Portal"/>. Ported verbatim from the legacy
    /// <c>PortalID</c> property (VB.NET <c>Integer</c>).
    /// </summary>
    public int PortalID { get; set; }

    /// <summary>
    /// Primary key identifier for this portal alias. Ported verbatim from the legacy
    /// <c>PortalAliasID</c> property (VB.NET <c>Integer</c>).
    /// </summary>
    public int PortalAliasID { get; set; }

    /// <summary>
    /// The HTTP alias (host name or URL) that maps an inbound request to the owning portal.
    /// The member name is preserved verbatim as all-caps <c>HTTPAlias</c> to honor the
    /// public-contract preservation requirement of the legacy domain object.
    /// </summary>
    public string? HTTPAlias { get; set; }
}
