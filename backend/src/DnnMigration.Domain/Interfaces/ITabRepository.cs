using DnnMigration.Domain.Entities;

namespace DnnMigration.Domain.Interfaces;

// MIGRATION: Abstracts the tab (page) data-access surface of the legacy DataProvider
// (Library/Components/Providers/Data/DataProvider.vb) and TabController.vb. IDataReader/FillInfo +
// stored procedures replaced by async LINQ returning Tab entities. Concrete EF Core implementation lives in
// DnnMigration.Infrastructure/Repositories/TabRepository.cs. Tabs are portal-scoped (multi-tenant, AAP 0.7.1).
public interface ITabRepository
{
    // MIGRATION: Replaces DataProvider.GetTabs(PortalId)/TabController.GetTabs(PortalId). Portal-scoped to
    // preserve multi-tenant isolation (AAP 0.7.1).
    Task<IEnumerable<Tab>> GetByPortalIdAsync(int portalId);

    // MIGRATION: Replaces DataProvider.GetTab(TabId). Returns null when not found.
    Task<Tab?> GetByIdAsync(int tabId);

    // MIGRATION: Replaces DataProvider.GetTabByName(TabName, PortalId)/TabController.GetTabByName. Portal-scoped
    // because tab names are unique only within a portal. Returns null when no match.
    Task<Tab?> GetByNameAsync(string tabName, int portalId);

    // MIGRATION: Replaces DataProvider.GetTabCount(PortalId) (returned Integer). Used by the Application layer to
    // populate Portal.Pages without the legacy entity-side lazy getter.
    Task<int> GetCountAsync(int portalId);

    // MIGRATION: Replaces DataProvider.AddTab(...)/TabController.AddTab(objTab). Returns the persisted Tab so the
    // database-generated TabId is available.
    Task<Tab> AddAsync(Tab tab);

    // MIGRATION: Replaces DataProvider.UpdateTab(...)/TabController.UpdateTab(objTab).
    Task UpdateAsync(Tab tab);

    // MIGRATION: Replaces DataProvider.DeleteTab(TabId). Legacy TabController.DeleteTab also took PortalId for
    // cache-scoping; the PK alone is sufficient for the delete (TabId is a globally-unique identity column).
    Task DeleteAsync(int tabId);
}
