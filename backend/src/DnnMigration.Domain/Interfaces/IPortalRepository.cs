using DnnMigration.Domain.Entities;

namespace DnnMigration.Domain.Interfaces;

/// <summary>
/// Repository contract for the <see cref="Portal"/> aggregate. Extends the generic
/// <see cref="IRepository{T}"/> CRUD surface with portal-specific lookups.
/// </summary>
public interface IPortalRepository : IRepository<Portal>
{
    /// <summary>Retrieves the portal matching an HTTP alias, or <c>null</c> if none matches.</summary>
    // MIGRATION: legacy DataProvider.GetPortalByAlias(PortalAlias) [DataProvider.vb L98] returned an
    // IDataReader that PortalController hydrated to a PortalInfo; converted to an async single-entity
    // lookup. Implemented downstream as a LINQ query joining PortalAlias -> Portal in
    // DnnMigration.Infrastructure (no stored proc).
    Task<Portal?> GetByAliasAsync(string httpAlias, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the portals whose name, description, or keywords contain the supplied free-text
    /// <paramref name="query"/> (case-insensitive substring match).
    /// </summary>
    // MIGRATION: PortalController.GetPortalsByName(nameToMatch, pageIndex, pageSize)
    // (Library/Components/Portal/PortalController.vb) drove the legacy Portals.ascx.vb grid's letter /
    // text search via a LIKE-based stored proc. Re-expressed here as an async, materialized substring
    // filter (LINQ .ToLower().Contains downstream) so the AAP §0.7.2 "Search/Filter -> GET
    // /api/{entity}?query=..." contract is served entirely server-side rather than being silently
    // ignored. An empty/whitespace query is treated as "no filter" by the caller (the service), so this
    // method is only invoked with a meaningful term.
    Task<IEnumerable<Portal>> SearchAsync(string query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves every portal's HTTP aliases, keyed by <c>PortalID</c>, in a single round-trip.
    /// </summary>
    // MIGRATION: the legacy Portals.ascx.vb grid rendered a "Portal Aliases" column via
    // FormatPortalAliases(PortalID), which read PortalAliasController.GetPortalAliasArrayList and filtered
    // by portal. Because the Portal entity intentionally carries no PortalAlias navigation collection
    // (keeping the EF model/snapshot unchanged), the read model is populated by this dedicated lookup.
    // Returning the whole set as a dictionary avoids an N+1 query when projecting a portal list.
    Task<IReadOnlyDictionary<int, IReadOnlyList<string>>> GetAliasesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the HTTP aliases for a single portal (empty when the portal has none).
    /// </summary>
    // MIGRATION: the single-portal counterpart of <see cref="GetAliasesAsync"/>; mirrors the legacy
    // FormatPortalAliases(PortalID) lookup for one portal (used when projecting GET /api/portals/{id}).
    Task<IReadOnlyList<string>> GetAliasesForPortalAsync(int portalId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists a new HTTP alias for a portal and returns it carrying its store-generated key.
    /// </summary>
    // MIGRATION: the WRITE counterpart of the read-only GetAliases* lookups above. The legacy
    // PortalController.CreatePortal [Library/Components/Portal/PortalController.vb L980] registered the
    // initial portal alias via PortalAliasController.AddPortalAlias -> DataProvider.AddPortalAlias (the
    // *PortalAlias* stored procedures dispatched by SqlDataProvider.vb), which returned the new
    // PortalAliasID. Because the Portal aggregate intentionally carries no PortalAlias navigation
    // collection (the EF model/snapshot is left unchanged to preserve schema fidelity), alias persistence
    // is exposed as this dedicated repository port rather than through the Portal entity graph.
    // PortalAlias.PortalAliasID is IDENTITY(1,1), so the store generates the key on insert.
    Task<PortalAlias> AddAliasAsync(PortalAlias alias, CancellationToken cancellationToken = default);
}
