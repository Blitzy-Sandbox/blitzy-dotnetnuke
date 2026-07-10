using DnnMigration.Domain.Common;
using DnnMigration.Domain.Entities;
using DnnMigration.Domain.Interfaces;
using DnnMigration.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DnnMigration.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="ITabRepository"/> backed by <see cref="DnnDbContext"/>.
/// </summary>
// MIGRATION: Re-expresses the data-access portion of the legacy VB.NET
// DotNetNuke.Entities.Tabs.TabController (Library/Components/Tabs/TabController.vb) as EF Core LINQ.
// Legacy access went through DataProvider.Instance().GetTab/GetTabsByParentId (IDataReader) with
// FillTabInfo/FillTabInfoCollection (CBO reflection hydration) and DataCache; that provider/CBO/cache
// indirection is dropped in favour of DnnDbContext DbSet<Tab> queries. Data access only.
public class TabRepository : ITabRepository
{
    private readonly DnnDbContext _context;

    public TabRepository(DnnDbContext context)
    {
        _context = context;
    }

    // MIGRATION: TabController.GetTab(TabId) [TabController.vb L1270] — FillTabInfo(DataProvider.GetTab(TabId)).
    public async Task<Tab?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.Tabs
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TabID == id, cancellationToken);
    }

    // MIGRATION: aggregate of the legacy per-portal tab readers; unfiltered AsNoTracking projection to
    // satisfy the generic IRepository<Tab> contract.
    public async Task<IEnumerable<Tab>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Tabs
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    // MIGRATION: TabController.GetTabs(PortalId) [TabController.vb L516], which enumerated the cached
    // GetTabsByPortal(PortalId) dictionary. The DataCache layer is dropped; the portal filter is preserved.
    public async Task<IEnumerable<Tab>> GetByPortalAsync(int portalId, CancellationToken cancellationToken = default)
    {
        return await _context.Tabs
            .AsNoTracking()
            .Where(t => t.PortalID == portalId)
            .ToListAsync(cancellationToken);
    }

    // MIGRATION: TabController.GetTabsByParentId(ParentId) [TabController.vb L1282, single-arg] —
    // FillTabInfoCollection(DataProvider.GetTabsByParentId(ParentId)). Note: Tab.ParentId uses lowercase
    // 'd' casing. The legacy <Obsolete> attribute is a DNN in-tree note and is intentionally not carried over.
    public async Task<IEnumerable<Tab>> GetByParentAsync(int parentId, CancellationToken cancellationToken = default)
    {
        return await _context.Tabs
            .AsNoTracking()
            .Where(t => t.ParentId == parentId)
            .ToListAsync(cancellationToken);
    }

    // MIGRATION (QA finding — R6 Issue 1): bounded page of GetAllAsync. One COUNT over the full [Tabs] set
    // plus one windowed SELECT ordered by the TabID primary key. AsNoTracking (read path).
    public async Task<PagedResult<Tab>> GetPagedAsync(int skip, int take, CancellationToken cancellationToken = default)
    {
        var baseQuery = _context.Tabs.AsNoTracking();
        var total = await baseQuery.CountAsync(cancellationToken);
        var items = await baseQuery
            .OrderBy(t => t.TabID)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
        return new PagedResult<Tab>(items, total);
    }

    // MIGRATION (QA finding — R6 Issue 1): bounded page of GetByPortalAsync. The SAME PortalID filter is
    // applied to the base query (shared by COUNT and the page); only the Skip/Take window (ordered by
    // TabID) is materialized. AsNoTracking (read path).
    public async Task<PagedResult<Tab>> GetByPortalPagedAsync(int portalId, int skip, int take, CancellationToken cancellationToken = default)
    {
        var baseQuery = _context.Tabs
            .AsNoTracking()
            .Where(t => t.PortalID == portalId);
        var total = await baseQuery.CountAsync(cancellationToken);
        var items = await baseQuery
            .OrderBy(t => t.TabID)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
        return new PagedResult<Tab>(items, total);
    }

    // MIGRATION (QA finding — R6 Issue 1): bounded page of GetByParentAsync. The SAME ParentId filter is
    // applied to the base query (shared by COUNT and the page); only the Skip/Take window (ordered by
    // TabID) is materialized. AsNoTracking (read path). Note Tab.ParentId uses lowercase 'd' casing.
    public async Task<PagedResult<Tab>> GetByParentPagedAsync(int parentId, int skip, int take, CancellationToken cancellationToken = default)
    {
        var baseQuery = _context.Tabs
            .AsNoTracking()
            .Where(t => t.ParentId == parentId);
        var total = await baseQuery.CountAsync(cancellationToken);
        var items = await baseQuery
            .OrderBy(t => t.TabID)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
        return new PagedResult<Tab>(items, total);
    }

    // MIGRATION: TabController.AddTab -> DataProvider.AddTab stored proc.
    public async Task<Tab> AddAsync(Tab entity, CancellationToken cancellationToken = default)
    {
        _context.Tabs.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    // MIGRATION: TabController.UpdateTab -> DataProvider.UpdateTab stored proc.
    public async Task UpdateAsync(Tab entity, CancellationToken cancellationToken = default)
    {
        _context.Tabs.Update(entity);
        await _context.SaveChangesAsync(cancellationToken);
    }

    // MIGRATION: TabController.DeleteTab -> DataProvider.DeleteTab stored proc. Tracked fetch (no
    // AsNoTracking) so EF can mark the entity Deleted, then remove.
    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Tabs
            .FirstOrDefaultAsync(t => t.TabID == id, cancellationToken);
        if (entity is not null)
        {
            _context.Tabs.Remove(entity);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
