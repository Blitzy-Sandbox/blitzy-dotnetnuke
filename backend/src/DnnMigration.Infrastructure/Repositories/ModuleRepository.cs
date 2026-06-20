using DnnMigration.Domain.Entities;
using DnnMigration.Domain.Enums;
using DnnMigration.Domain.Interfaces;
using DnnMigration.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DnnMigration.Infrastructure.Repositories;

/// <summary>
/// EF Core 8 LINQ implementation of <see cref="IModuleRepository"/>.
/// Replaces the legacy <c>ModuleController.vb</c> data methods and <c>SqlDataProvider.vb</c> Module
/// stored-procedure calls. Reads mirror the legacy <c>vw_Modules</c> view by filtering out soft-deleted rows.
/// </summary>
public class ModuleRepository : IModuleRepository
{
    private readonly DnnDbContext _context;

    public ModuleRepository(DnnDbContext context)
    {
        _context = context;
    }

    public async Task<Module?> GetByIdAsync(int moduleId, CancellationToken cancellationToken = default)
    {
        // Single-entity lookup is intentionally unfiltered by IsDeleted so callers (and the soft-delete
        // path below) can still resolve a module by its identifier.
        return await _context.Modules
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.ModuleID == moduleId, cancellationToken);
    }

    public async Task<IEnumerable<Module>> GetByTabAsync(int tabId, CancellationToken cancellationToken = default)
    {
        // MIGRATION (schema fidelity, ADR-002): reproduces the legacy ModuleController.GetTabModules /
        // SqlDataProvider GetTabModules, which JOINED the physical [TabModules] placement table to [Modules].
        // TabID and ModuleOrder are denormalized [TabModules] columns that CP2 ModuleConfiguration Ignore()s
        // on the Module entity (they are NOT physical [Modules] columns), so this query MUST source them from
        // [TabModules] rather than from the ignored Module members - filtering/ordering on the ignored
        // members would not translate against the preserved SQL Server schema.
        //
        // PERF: the result is naturally bounded - it is the set of module placements for a SINGLE tab (page),
        // a small, finite collection in DNN - faithfully reproducing the legacy all-placements-for-tab
        // contract without unbounded full-table materialization.
        var rows = await (
            from tm in _context.TabModules.AsNoTracking()
            join m in _context.Modules.AsNoTracking() on tm.ModuleID equals m.ModuleID
            where tm.TabID == tabId && !m.IsDeleted
            orderby tm.ModuleOrder
            select new { Module = m, Placement = tm })
            .ToListAsync(cancellationToken);

        // Project the denormalized [TabModules] placement columns onto each (detached, AsNoTracking) Module
        // so the returned graph reproduces the legacy flattened ModuleInfo surface consumed by ModuleDto. The
        // raw [TabModules].[Visibility] int is converted to the VisibilityState enum here at the projection
        // site (CP2 ModuleConfiguration deliberately deferred this conversion to the repository/service layer).
        var modules = new List<Module>(rows.Count);
        foreach (var row in rows)
        {
            var module = row.Module;
            var placement = row.Placement;

            module.TabModuleID = placement.TabModuleID;
            module.TabID = placement.TabID;
            module.PaneName = placement.PaneName;
            module.ModuleOrder = placement.ModuleOrder;
            module.CacheTime = placement.CacheTime;
            module.Alignment = placement.Alignment;
            module.Color = placement.Color;
            module.Border = placement.Border;
            module.IconFile = placement.IconFile;
            module.Visibility = (VisibilityState)placement.Visibility;
            module.ContainerSrc = placement.ContainerSrc;
            module.DisplayTitle = placement.DisplayTitle;
            module.DisplayPrint = placement.DisplayPrint;
            module.DisplaySyndicate = placement.DisplaySyndicate;

            modules.Add(module);
        }

        return modules;
    }

    public async Task<IEnumerable<Module>> GetByPortalAsync(int portalId, CancellationToken cancellationToken = default)
    {
        // PERF / MIGRATION: reproduces the legacy ModuleController.GetModules(portalId) admin module list.
        // PortalID is a REAL, physical [Modules] column (mapped by CP2 ModuleConfiguration), so this query is
        // schema-faithful. The result is bounded by a single portal's non-deleted module definitions - a
        // finite, administratively-bounded set - and intentionally returns all rows for that portal to
        // preserve the legacy all-modules-for-portal behavior consumed by bounded admin management screens
        // (paged UI projection is layered above in the Application/API tier).
        return await _context.Modules
            .AsNoTracking()
            .Where(m => m.PortalID == portalId && !m.IsDeleted)
            .OrderBy(m => m.ModuleID)
            .ToListAsync(cancellationToken);
    }

    public async Task<Module> AddAsync(Module module, CancellationToken cancellationToken = default)
    {
        await _context.Modules.AddAsync(module, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return module;
    }

    public async Task UpdateAsync(Module module, CancellationToken cancellationToken = default)
    {
        _context.Modules.Update(module);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int moduleId, CancellationToken cancellationToken = default)
    {
        // MIGRATION: Module delete is a SOFT delete (IsDeleted = true), matching the Modules table's
        // [IsDeleted] bit column and the legacy DeleteTabModule behavior. The fetch is intentionally
        // unfiltered so an already-loaded module can still be flagged.
        var module = await _context.Modules
            .FirstOrDefaultAsync(m => m.ModuleID == moduleId, cancellationToken);

        if (module is null)
        {
            return;
        }

        module.IsDeleted = true;
        await _context.SaveChangesAsync(cancellationToken);
    }
}
