using DnnMigration.Domain.Entities;

namespace DnnMigration.Domain.Interfaces;

/// <summary>
/// Repository abstraction for the <see cref="Portal"/> aggregate.
/// MIGRATION: extracted from the data-access methods of PortalController.vb / PortalAliasController.vb
/// (the legacy controllers co-mingled data access with business logic). Implemented by the Infrastructure
/// layer with EF Core; consumed by the Application PortalService.
/// </summary>
public interface IPortalRepository
{
    /// <summary>Gets a portal by its identifier, or <c>null</c> if not found. (legacy PortalController.GetPortal)</summary>
    Task<Portal?> GetByIdAsync(int portalId, CancellationToken cancellationToken = default);

    /// <summary>Gets all portals. (legacy PortalController.GetPortals)</summary>
    Task<IEnumerable<Portal>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the total number of portals. (legacy DataProvider.GetPortalCount.) Provided as a count-only
    /// query so callers such as the last-portal delete guard do not materialize every portal entity.
    /// </summary>
    Task<int> CountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a single page of portals whose name matches <paramref name="nameToMatch"/>, together with the
    /// total count of matching rows. (legacy PortalController.GetPortalsByName, which returned an ArrayList
    /// and set a ByRef totalRecords out-parameter.)
    /// </summary>
    Task<(IEnumerable<Portal> Items, int TotalCount)> GetByNameAsync(
        string nameToMatch,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves the portal that owns the given HTTP alias, or <c>null</c> if no alias matches.
    /// (legacy PortalAliasController.GetPortalAlias / GetPortalByPortalAliasID.)
    /// </summary>
    Task<Portal?> GetByAliasAsync(string httpAlias, CancellationToken cancellationToken = default);

    /// <summary>Adds a new portal and returns the persisted entity (with its generated id populated).</summary>
    Task<Portal> AddAsync(Portal portal, CancellationToken cancellationToken = default);

    /// <summary>Updates an existing portal. (legacy PortalController.UpdatePortalInfo)</summary>
    Task UpdateAsync(Portal portal, CancellationToken cancellationToken = default);

    // MIGRATION: Portal uses HARD-delete with a transactional cascade (AAP §0.3.3). The cascade ordering and
    // transaction are implemented in the Infrastructure PortalRepository, not expressed in this contract.
    /// <summary>Deletes a portal by id. (legacy PortalController.DeletePortal / DeletePortalInfo)</summary>
    Task DeleteAsync(int portalId, CancellationToken cancellationToken = default);
}
