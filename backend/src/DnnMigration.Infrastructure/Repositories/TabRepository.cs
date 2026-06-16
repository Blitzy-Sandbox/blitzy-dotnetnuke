using DnnMigration.Domain.Entities;
using DnnMigration.Domain.Interfaces;
using DnnMigration.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DnnMigration.Infrastructure.Repositories;

/// <summary>
/// EF Core 8 LINQ implementation of <see cref="ITabRepository"/>.
/// A DNN "Tab" is a site Page. Replaces the legacy <c>TabController.vb</c> data methods and
/// <c>SqlDataProvider.vb</c> Tab stored-procedure calls. Reads mirror the legacy <c>vw_Tabs</c> view by
/// filtering out soft-deleted rows.
/// </summary>
public class TabRepository : ITabRepository
{
    private readonly DnnDbContext _context;

    public TabRepository(DnnDbContext context)
    {
        _context = context;
    }

    public async Task<Tab?> GetByIdAsync(int tabId, int portalId, CancellationToken cancellationToken = default)
    {
        // Portal-scoped single-entity lookup; intentionally unfiltered by IsDeleted so a tab can still be
        // resolved by its identifier within the portal.
        return await _context.Tabs
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TabID == tabId && t.PortalID == portalId, cancellationToken);
    }

    public async Task<IEnumerable<Tab>> GetByPortalAsync(int portalId, CancellationToken cancellationToken = default)
    {
        // PERFORMANCE: returns the portal's full (non-deleted) page tree (legacy GetTabs/GetTabsByPortal). A
        // portal's page set is a bounded administrative hierarchy, so this is intentionally not paged, matching
        // the legacy all-rows contract. Ordered by TabOrder for tree rendering. See MIGRATION_NOTES.md §4.6
        // (bounded list reads).
        return await _context.Tabs
            .AsNoTracking()
            .Where(t => t.PortalID == portalId && !t.IsDeleted)
            .OrderBy(t => t.TabOrder)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Tab>> GetByParentAsync(int parentId, int portalId, CancellationToken cancellationToken = default)
    {
        // PERFORMANCE: returns the (non-deleted) direct child pages of one parent tab (legacy
        // GetTabsByParentId/GetTabsByParent). The children of a single page are a bounded set, so this is
        // intentionally not paged, matching the legacy all-rows contract. See MIGRATION_NOTES.md §4.6
        // (bounded list reads).
        return await _context.Tabs
            .AsNoTracking()
            .Where(t => t.ParentId == parentId && t.PortalID == portalId && !t.IsDeleted)
            .OrderBy(t => t.TabOrder)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetCountAsync(int portalId, CancellationToken cancellationToken = default)
    {
        // MIGRATION: reproduces the legacy GetTabCount stored procedure VERBATIM
        // [DotNetNuke.Schema.SqlDataProvider / 04.04.00.SqlDataProvider]:
        //   DECLARE @AdminTabId int = (SELECT AdminTabId FROM Portals WHERE PortalID = @PortalID)
        //   SELECT COUNT(*) - 1 FROM Tabs
        //   WHERE PortalID = @PortalID
        //     AND TabID <> @AdminTabId                          -- exclude the admin tab itself
        //     AND (ParentId <> @AdminTabId OR ParentId IS NULL) -- exclude the admin tab's DIRECT children
        // The "- 1" offset is preserved verbatim for behavioral parity (it is the legacy contract that
        // PortalController/admin screens compare against). TWO behaviours are reconciled and preserved here:
        //   1. NO IsDeleted filter — GetTabCount deliberately counts soft-deleted rows too, UNLIKE the list
        //      reads above. Adding !t.IsDeleted would diverge from the legacy count, so it is intentionally
        //      omitted (the prior straight !IsDeleted count, with no admin-tab exclusion, was the CP3 finding).
        //   2. NULL @AdminTabId — AdminTabId is a nullable Portals column. In T-SQL, `TabID <> @AdminTabId`
        //      is UNKNOWN for every row when @AdminTabId IS NULL, so the proc matches no rows (COUNT = 0) and
        //      returns -1. That branch is reproduced explicitly below rather than relying on C#/EF
        //      null-comparison semantics (under the InMemory provider `TabID != null` would be TRUE for all
        //      rows, which would NOT match the proc). A missing portal likewise yields a null AdminTabId.
        // See MIGRATION_NOTES.md §4.2 (Tabs) / D-033.
        int? adminTabId = await _context.Portals
            .AsNoTracking()
            .Where(p => p.PortalID == portalId)
            .Select(p => p.AdminTabId)
            .FirstOrDefaultAsync(cancellationToken);

        int matching;
        if (adminTabId.HasValue)
        {
            int admin = adminTabId.Value;
            matching = await _context.Tabs
                .AsNoTracking()
                .CountAsync(
                    t => t.PortalID == portalId
                      && t.TabID != admin
                      && (t.ParentId != admin || t.ParentId == null),
                    cancellationToken);
        }
        else
        {
            // @AdminTabId IS NULL (portal missing, or AdminTabId not set): the legacy `TabID <> @AdminTabId`
            // predicate is UNKNOWN for every row, so SQL Server matches no rows (COUNT = 0).
            matching = 0;
        }

        // Legacy COUNT(*) - 1 (preserved verbatim, including the resulting -1 when no rows match).
        return matching - 1;
    }

    public async Task<Tab> AddAsync(Tab tab, CancellationToken cancellationToken = default)
    {
        await _context.Tabs.AddAsync(tab, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return tab;
    }

    public async Task UpdateAsync(Tab tab, CancellationToken cancellationToken = default)
    {
        _context.Tabs.Update(tab);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int tabId, int portalId, CancellationToken cancellationToken = default)
    {
        // MIGRATION: Tab delete is a SOFT delete (IsDeleted = true), matching the Tabs table's [IsDeleted]
        // bit column. The parent-with-children delete guard is a service-layer rule and is NOT enforced here.
        var tab = await _context.Tabs
            .FirstOrDefaultAsync(t => t.TabID == tabId && t.PortalID == portalId, cancellationToken);

        if (tab is null)
        {
            return;
        }

        tab.IsDeleted = true;
        await _context.SaveChangesAsync(cancellationToken);
    }
}
