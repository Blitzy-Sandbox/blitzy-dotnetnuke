namespace DnnMigration.Domain.Entities;

// MIGRATION: New Domain POCO modeling the legacy DNN [PortalAlias] table (DotNetNuke.Schema.SqlDataProvider
// L1288). In DNN 4.x a portal could be reached via one or more host names ("aliases"); the alias -> portal
// mapping lived in this separate table and was resolved by DataProvider.GetPortalByAlias (DataProvider.vb L98).
// The legacy VB PortalAliasInfo class carried extra cache/derived members; this POCO keeps ONLY the three
// physical columns required to resolve an alias to its owning portal (the migration does not alter the schema).
//
// Plain entity (no XML-serialization attributes), nullable reference types ON. Mapped to the existing table via
// PortalAliasConfiguration (Fluent API) — no navigation to Portal is modeled so the alias lookup stays a simple,
// explicit two-step read in PortalRepository (avoiding an inferred relationship and any PortalId == 0 ambiguity).
public sealed class PortalAlias
{
    /// <summary>
    /// Identity primary key of the alias row ([PortalAliasID], IDENTITY(1,1) in the legacy schema).
    /// </summary>
    public int PortalAliasId { get; set; }

    /// <summary>
    /// The portal (tenant) this alias resolves to ([PortalID]). Preserves DNN multi-tenant scoping: an alias is
    /// owned by exactly one portal.
    /// </summary>
    public int PortalId { get; set; }

    /// <summary>
    /// The host/URL alias for the portal ([HTTPAlias], nullable nvarchar(200) in the legacy schema), e.g.
    /// "www.example.com" or "localhost/dnn". DNN treats aliases case-insensitively.
    /// </summary>
    public string? HttpAlias { get; set; }
}
