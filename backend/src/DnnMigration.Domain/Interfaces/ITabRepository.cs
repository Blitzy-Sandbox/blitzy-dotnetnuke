using DnnMigration.Domain.Entities;

namespace DnnMigration.Domain.Interfaces;

/// <summary>
/// Repository abstraction for the <see cref="Tab"/> aggregate (a DNN Tab == a site page).
/// MIGRATION: extracted from the data-access methods of TabController.vb. Implemented by the Infrastructure
/// layer with EF Core; consumed by the Application TabService.
/// </summary>
public interface ITabRepository
{
    /// <summary>Gets a tab by id within a portal, or <c>null</c> if not found. (legacy TabController.GetTab)</summary>
    Task<Tab?> GetByIdAsync(int tabId, int portalId, CancellationToken cancellationToken = default);

    /// <summary>Gets all tabs for a portal. (legacy TabController.GetTabs / GetTabsByPortal)</summary>
    Task<IEnumerable<Tab>> GetByPortalAsync(int portalId, CancellationToken cancellationToken = default);

    /// <summary>Gets the child tabs of a parent tab within a portal. (legacy TabController.GetTabsByParentId)</summary>
    Task<IEnumerable<Tab>> GetByParentAsync(int parentId, int portalId, CancellationToken cancellationToken = default);

    /// <summary>Gets the count of tabs in a portal. (legacy TabController.GetTabCount)</summary>
    Task<int> GetCountAsync(int portalId, CancellationToken cancellationToken = default);

    /// <summary>Adds a new tab and returns the persisted entity (id populated). (legacy TabController.AddTab)</summary>
    Task<Tab> AddAsync(Tab tab, CancellationToken cancellationToken = default);

    /// <summary>Updates an existing tab. (legacy TabController.UpdateTab)</summary>
    Task UpdateAsync(Tab tab, CancellationToken cancellationToken = default);

    // MIGRATION: Tab uses SOFT-delete via the IsDeleted flag (AAP §0.3.3). The Infrastructure implementation
    // sets IsDeleted = true rather than removing the row, and list reads (GetByPortalAsync / GetByParentAsync)
    // filter IsDeleted == false. (Legacy also blocked deleting a parent tab that still has children — that
    // business rule lives in the service/implementation, not in this contract.)
    /// <summary>Soft-deletes a tab by id within a portal. (legacy TabController.DeleteTab)</summary>
    Task DeleteAsync(int tabId, int portalId, CancellationToken cancellationToken = default);
}
