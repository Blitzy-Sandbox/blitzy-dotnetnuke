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
        return await _context.Tabs
            .AsNoTracking()
            .Where(t => t.PortalID == portalId && !t.IsDeleted)
            .OrderBy(t => t.TabOrder)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Tab>> GetByParentAsync(int parentId, int portalId, CancellationToken cancellationToken = default)
    {
        return await _context.Tabs
            .AsNoTracking()
            .Where(t => t.ParentId == parentId && t.PortalID == portalId && !t.IsDeleted)
            .OrderBy(t => t.TabOrder)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetCountAsync(int portalId, CancellationToken cancellationToken = default)
    {
        // MIGRATION: the legacy GetTabCount returned COUNT(*) - 1 to exclude the portal's admin tab.
        // This returns a straight count of non-deleted tabs; excluding the admin tab is a service-layer
        // concern (out of scope for the repository) and is documented in MIGRATION_NOTES.md.
        return await _context.Tabs
            .AsNoTracking()
            .CountAsync(t => t.PortalID == portalId && !t.IsDeleted, cancellationToken);
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
