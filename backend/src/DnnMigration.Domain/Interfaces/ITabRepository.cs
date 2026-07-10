using DnnMigration.Domain.Common;
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

    /// <summary>
    /// Retrieves a single bounded page of tabs (ordered by <c>TabID</c>) together with the total tab
    /// count, for server-side pagination of <c>GET /api/tabs</c>.
    /// </summary>
    // MIGRATION (QA finding — R6 Issue 1): the BOUNDED counterpart of <see cref="IRepository{T}.GetAllAsync"/>.
    // The full-list GetAllAsync is left untouched (internal descendant-walk callers in TabService depend on
    // it); this fetches only the Skip/Take window (ordered by the TabID primary key) plus a COUNT.
    Task<PagedResult<Tab>> GetPagedAsync(int skip, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a single bounded page of tabs belonging to the specified portal (ordered by <c>TabID</c>)
    /// together with the total count for that portal.
    /// </summary>
    // MIGRATION (QA finding — R6 Issue 1): the BOUNDED counterpart of <see cref="GetByPortalAsync"/>.
    Task<PagedResult<Tab>> GetByPortalPagedAsync(int portalId, int skip, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a single bounded page of the immediate child tabs of the specified parent tab (ordered by
    /// <c>TabID</c>) together with the total child count.
    /// </summary>
    // MIGRATION (QA finding — R6 Issue 1): the BOUNDED counterpart of <see cref="GetByParentAsync"/>. The
    // existing GetByParentAsync is left untouched (TabService's recursive descendant walk depends on the
    // full child set).
    Task<PagedResult<Tab>> GetByParentPagedAsync(int parentId, int skip, int take, CancellationToken cancellationToken = default);
}
