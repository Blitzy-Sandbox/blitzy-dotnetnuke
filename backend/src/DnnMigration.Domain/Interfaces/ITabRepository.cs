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

    // MIGRATION: Replaces DataProvider.GetTab(TabId, PortalId) / TabController.GetTab(TabId, PortalId). PORTAL-SCOPED
    // (CP1 review ITabRepository #1 / multi-tenant isolation, AAP 0.7.1): the lookup MUST carry portalId so the
    // service can enforce that a tab is only read/mutated within its owning portal — the earlier tabId-only collapse
    // is rejected by the review because it cannot express the legacy portal-scoped operation. The implementation MUST
    // eager-load the TabPermissions navigation so the service can run the legacy permission diff (UpdateTab L799-808).
    // Returns null when no tab with that id exists IN that portal.
    Task<Tab?> GetByIdAsync(int portalId, int tabId);

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

    // MIGRATION: Replaces DataProvider.DeleteTab(TabId) / TabController.DeleteTab(TabId, PortalId). PORTAL-SCOPED
    // (CP1 review ITabRepository #1): carries portalId so a delete is constrained to the owning portal (the
    // tabId-only collapse is rejected — it cannot enforce the legacy portal-scoped delete that DeleteTab performed
    // with its PortalId argument for tenant safety + sibling re-ordering).
    Task DeleteAsync(int portalId, int tabId);
}
