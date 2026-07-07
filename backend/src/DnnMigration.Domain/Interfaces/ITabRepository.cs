using DnnMigration.Domain.Entities;

namespace DnnMigration.Domain.Interfaces;

/// <summary>
/// Repository contract for the <see cref="Tab"/> (portal page) aggregate. Extends the generic
/// <see cref="IRepository{T}"/> CRUD surface with tab-hierarchy lookups.
/// </summary>
public interface ITabRepository : IRepository<Tab>
{
    /// <summary>Retrieves all tabs (pages) belonging to the specified portal.</summary>
    // MIGRATION: legacy DataProvider.GetTabs(PortalId) [DataProvider.vb L115] /
    // TabController.GetTabs(PortalId) [TabController.vb L516] returned an ArrayList of TabInfo;
    // converted to an async materialized collection.
    Task<IEnumerable<Tab>> GetByPortalAsync(int portalId, CancellationToken cancellationToken = default);

    /// <summary>Retrieves the immediate child tabs of the specified parent tab.</summary>
    // MIGRATION: legacy DataProvider.GetTabsByParentId(ParentId) [DataProvider.vb L119] /
    // TabController.GetTabsByParentId(ParentId) [TabController.vb L1282] returned an ArrayList of
    // TabInfo; converted to an async materialized collection (page-hierarchy traversal).
    Task<IEnumerable<Tab>> GetByParentAsync(int parentId, CancellationToken cancellationToken = default);
}
