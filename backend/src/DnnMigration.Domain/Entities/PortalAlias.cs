namespace DnnMigration.Domain.Entities;

/// <summary>
/// Represents a single portal HTTP alias record — the mapping between an inbound
/// HTTP host name (and optional virtual application path) and a specific portal
/// within the DotNetNuke multi-portal model.
/// </summary>
/// <remarks>
/// MIGRATION: Ported verbatim from the legacy VB.NET <c>PortalAliasInfo</c> class
/// (<c>Library/Components/Portal/PortalAliasInfo.vb</c>, namespace
/// <c>DotNetNuke.Entities.Portals</c>) of DotNetNuke 4.9.0.85. This is a plain POCO
/// in the Domain layer carrying ZERO framework dependencies; persistence mapping onto
/// the existing (unchanged) database schema is supplied by an EF Core Fluent
/// <c>IEntityTypeConfiguration&lt;PortalAlias&gt;</c> in the Infrastructure layer
/// rather than by attributes on this type.
///
/// The legacy <c>PortalAliasCollection</c> (a <c>DictionaryBase</c> wrapper) and the
/// <c>PortalAliasController</c> data/business class are intentionally out of scope:
/// collection semantics are expressed through EF Core navigation properties, while
/// data access and business logic move to repositories and services in the
/// Infrastructure and Application layers respectively.
///
/// The all-caps member name <c>HTTPAlias</c> is preserved exactly as authored in the
/// legacy source to retain the public contract of the original domain object.
/// </remarks>
public class PortalAlias
{
    /// <summary>
    /// Gets or sets the identifier of the portal this alias resolves to.
    /// Acts as the foreign key to the owning <c>Portal</c> entity.
    /// Legacy VB equivalent: <c>PortalID As Integer</c>.
    /// </summary>
    public int PortalID { get; set; }

    /// <summary>
    /// Gets or sets the unique identifier (primary key) of this portal alias record.
    /// Legacy VB equivalent: <c>PortalAliasID As Integer</c>.
    /// </summary>
    public int PortalAliasID { get; set; }

    /// <summary>
    /// Gets or sets the HTTP alias used to match an inbound request to its portal —
    /// a host name with an optional virtual path (for example <c>www.example.com</c>
    /// or <c>localhost/dnn</c>). Legacy VB equivalent: <c>HTTPAlias As String</c>.
    /// The original all-caps casing is preserved verbatim for contract compatibility.
    /// </summary>
    public string? HTTPAlias { get; set; }
}
