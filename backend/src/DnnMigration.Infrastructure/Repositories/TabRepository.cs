using DnnMigration.Domain.Entities;
using DnnMigration.Domain.Interfaces;
using DnnMigration.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DnnMigration.Infrastructure.Repositories;

// MIGRATION: EF Core 8 implementation of DnnMigration.Domain.Interfaces.ITabRepository. Replaces the
// data-access portions of the legacy DotNetNuke.Entities.Tabs.TabController (Library/Components/Tabs/TabController.vb),
// which reached SQL Server through the reflection-built DataProvider.Instance() singleton
// (Library/Components/Providers/Data/DataProvider.vb) -> SqlDataProvider stored procedures -> IDataReader, then
// materialized rows with the private FillTabInfo / FillTabInfoCollection helpers. Those SP + IDataReader + FillInfo
// call chains are replaced here by DnnDbContext.Tabs + async LINQ returning Domain Tab entities.
//
// STAGE-ONLY: this repository only stages changes on the DbContext change-tracker (Add / Update / Remove); it NEVER
// calls SaveChanges/SaveChangesAsync. Persistence is committed by the Application layer (TabService.cs) through the
// IUnitOfWork boundary — mirroring how the legacy DataProvider owned the transaction scope while TabController
// orchestrated the workflow. All business logic (tab-path generation, parent/child ordering, permission
// propagation, cache invalidation) stays in TabService.cs; this class is persistence only.
//
// NOTE: the legacy DataProvider.GetTabsByParentId has no ITabRepository counterpart by design — any parent/child
// filtering is performed in-memory by TabService over GetByPortalIdAsync results, not by this repository.
public sealed class TabRepository : ITabRepository
{
    private readonly DnnDbContext _context;

    public TabRepository(DnnDbContext context) => _context = context;

    // MIGRATION: TabController.GetTabs(PortalId) L516 (which enumerated GetTabsByPortal). Tenant-scoped by PortalId
    // to preserve DNN multi-tenant isolation (AAP 0.7.1). The int -> int? lift on the comparison is translated by
    // EF Core to NULL-excluding SQL equality (WHERE PortalId = @portalId), so host/system tabs carrying a NULL
    // PortalId are correctly excluded from a portal's tab set.
    public async Task<IEnumerable<Tab>> GetByPortalIdAsync(int portalId)
    {
        return await _context.Tabs
            .Where(t => t.PortalId == portalId)
            .ToListAsync();
    }

    // MIGRATION: TabController.GetTab(TabId) L1270 -> DataProvider.Instance().GetTab(TabId) + FillTabInfo. CP1 review
    // (ITabRepository): the lookup is PORTAL-SCOPED (PortalId + TabId) to preserve multi-tenant isolation (AAP 0.7.1)
    // so a portal can only read its own tabs; host/system tabs (NULL PortalId) are excluded from a portal's lookup.
    // Returns null when no row matches (legacy FillTabInfo returned Nothing for an empty reader).
    public async Task<Tab?> GetByIdAsync(int portalId, int tabId)
    {
        return await _context.Tabs
            .FirstOrDefaultAsync(t => t.PortalId == portalId && t.TabId == tabId);
    }

    // MIGRATION: TabController.GetTabByName(TabName, PortalId) L504 (delegated to GetTabByNameAndParent). Portal-scoped
    // because tab names are unique only within a portal. Parameter order is (tabName, portalId) to match
    // ITabRepository byte-for-byte. Returns null when no match.
    public async Task<Tab?> GetByNameAsync(string tabName, int portalId)
    {
        return await _context.Tabs
            .FirstOrDefaultAsync(t => t.PortalId == portalId && t.TabName == tabName);
    }

    // MIGRATION: TabController.GetTabCount(portalId) L512 -> DataProvider.Instance().GetTabCount(portalId). Tenant-scoped
    // CountAsync; consumed by the Application layer (e.g. to populate Portal.Pages without the legacy lazy getter).
    public async Task<int> GetCountAsync(int portalId)
    {
        return await _context.Tabs
            .CountAsync(t => t.PortalId == portalId);
    }

    // MIGRATION: TabController.AddTab(objTab) L326 -> DataProvider.Instance().AddTab(...). STAGE-ONLY: the entity is
    // tracked as Added; the database-generated TabId becomes available only after the Application layer calls
    // SaveChanges through IUnitOfWork. The tracked entity is returned so the caller can read the generated key.
    public async Task<Tab> AddAsync(Tab tab)
    {
        await _context.Tabs.AddAsync(tab);
        return tab;
    }

    // MIGRATION: TabController.UpdateTab(objTab) L780 -> DataProvider.Instance().UpdateTab(...). Marks the entity as
    // Modified on the change-tracker; the UPDATE is flushed by the Application layer's SaveChanges. Not declared
    // async (returns a completed Task) because Update is a synchronous change-tracker operation.
    public Task UpdateAsync(Tab tab)
    {
        _context.Tabs.Update(tab);
        return Task.CompletedTask;
    }

    // MIGRATION: TabController.DeleteTab(TabId, PortalId) L446 -> DataProvider.Instance().DeleteTab(TabId). CP1 review
    // (ITabRepository): the delete is PORTAL-SCOPED (PortalId + TabId) to preserve multi-tenant isolation (AAP 0.7.1)
    // so a portal can only delete its own tabs; this restores the legacy PortalId argument (in TabController PortalId
    // also drove cache invalidation and tab-order resequencing). STAGE-ONLY: the entity is staged as Removed;
    // SaveChanges is the Application layer's responsibility. No-op when the tab does not exist.
    public async Task DeleteAsync(int portalId, int tabId)
    {
        var tab = await _context.Tabs
            .FirstOrDefaultAsync(t => t.PortalId == portalId && t.TabId == tabId);

        if (tab is not null)
        {
            _context.Tabs.Remove(tab);
        }
    }
}
