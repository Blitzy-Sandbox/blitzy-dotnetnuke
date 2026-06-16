using DnnMigration.Domain.Entities;
using DnnMigration.Domain.Interfaces;
using DnnMigration.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DnnMigration.Infrastructure.Repositories;

/// <summary>
/// EF Core 8 LINQ implementation of <see cref="IModuleRepository"/>.
/// Replaces the legacy <c>ModuleController.vb</c> data methods and <c>SqlDataProvider.vb</c> Module
/// stored-procedure calls. Reads mirror the legacy <c>vw_Modules</c> view by filtering out soft-deleted rows.
/// </summary>
/// <remarks>
/// MIGRATION (CP3 schema fidelity): the per-tab module PLACEMENT (pane / order / cache / visibility /
/// container / display flags) lives in <c>dbo.TabModules</c>, not <c>dbo.Modules</c>. Those fields are
/// <c>Ignore()</c>'d on the <see cref="Module"/> entity (see <c>ModuleConfiguration.cs</c>), so this
/// repository reads them via an explicit <c>TabModules</c>-&gt;<c>Modules</c> join and persists them through
/// the schema-faithful <see cref="TabModule"/> entity (reproducing the legacy <c>AddTabModule</c> /
/// <c>UpdateTabModule</c> calls). Recorded in MIGRATION_NOTES.md §4.2 and Deviation Index D-030/D-031.
/// </remarks>
public class ModuleRepository : IModuleRepository
{
    // MIGRATION: DNN's default content pane (Globals.glbDefaultPane). dbo.TabModules.[PaneName] is NOT NULL,
    // so a placement created without an explicit pane falls back to this, matching legacy AddTabModule.
    private const string DefaultPaneName = "ContentPane";

    private readonly DnnDbContext _context;

    public ModuleRepository(DnnDbContext context)
    {
        _context = context;
    }

    public async Task<Module?> GetByIdAsync(int moduleId, CancellationToken cancellationToken = default)
    {
        // Single-entity lookup is intentionally unfiltered by IsDeleted so callers (and the soft-delete
        // path below) can still resolve a module by its identifier. No tab context is supplied, so per-tab
        // placement fields are NOT rehydrated here (a module can be placed on multiple tabs).
        return await _context.Modules
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.ModuleID == moduleId, cancellationToken);
    }

    // PERFORMANCE: returns every module placed on a SINGLE tab/page (legacy GetTabModules). This is bounded
    // by construction — a page hosts a small, fixed number of modules — so it is intentionally not paged,
    // matching the legacy all-rows GetTabModules contract. See MIGRATION_NOTES.md §4.6 (bounded list reads).
    public async Task<IEnumerable<Module>> GetByTabAsync(int tabId, CancellationToken cancellationToken = default)
    {
        // MIGRATION (CP3 schema fidelity): TabID and ModuleOrder are dbo.TabModules columns, NOT dbo.Modules
        // columns, and are Ignore()'d on the Module entity (ModuleConfiguration.cs). Querying them directly is
        // untranslatable by EF Core (it threw at runtime). Instead JOIN the schema-faithful TabModule
        // placement rows to Modules on ModuleID, filter by the placement's TabID, exclude soft-deleted
        // modules, and order by the REAL TabModules.ModuleOrder column — reproducing the legacy GetTabModules
        // join. Recorded in MIGRATION_NOTES.md §4.2 / D-030.
        var placed = await (
            from tm in _context.TabModules.AsNoTracking()
            join m in _context.Modules.AsNoTracking() on tm.ModuleID equals m.ModuleID
            where tm.TabID == tabId && !m.IsDeleted
            orderby tm.ModuleOrder
            select new { Module = m, Placement = tm })
            .ToListAsync(cancellationToken);

        // Rehydrate the TabModules-sourced placement fields onto each returned Module (in-memory only; the
        // entities are AsNoTracking, so these assignments never persist and never touch dbo.Modules).
        foreach (var row in placed)
        {
            RehydratePlacement(row.Module, row.Placement);
        }

        return placed.Select(row => row.Module).ToList();
    }

    // PERFORMANCE: returns all non-deleted modules for a portal (legacy GetModules). Portal-scoped module
    // counts are a bounded administrative set; this is intentionally not paged, matching the legacy all-rows
    // GetModules contract. See MIGRATION_NOTES.md §4.6 (bounded list reads). PortalID IS a physical
    // dbo.Modules column, so this query needs no TabModules join; per-tab placement is not rehydrated because
    // there is no single-tab context for a portal-wide read.
    public async Task<IEnumerable<Module>> GetByPortalAsync(int portalId, CancellationToken cancellationToken = default)
    {
        return await _context.Modules
            .AsNoTracking()
            .Where(m => m.PortalID == portalId && !m.IsDeleted)
            .OrderBy(m => m.ModuleID)
            .ToListAsync(cancellationToken);
    }

    public async Task<Module> AddAsync(Module module, CancellationToken cancellationToken = default)
    {
        // Insert the physical dbo.Modules row first so EF assigns the IDENTITY ModuleID.
        await _context.Modules.AddAsync(module, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        // MIGRATION (CP3 schema fidelity): legacy AddModule also inserted the per-tab placement row via
        // AddTabModule (SqlDataProvider.vb). The TabModules placement fields are Ignore()'d on the Module
        // entity, so persist them through the schema-faithful TabModule entity here when the module is placed
        // on a tab (TabID > 0). The DB-generated TabModuleID is rehydrated back onto the returned module.
        // Recorded in MIGRATION_NOTES.md §4.2 / D-030. (The legacy ModuleOrder bottom-of-pane auto-positioning
        // — UpdateModuleOrder when ModuleOrder = -1 — remains out of Phase-1 scope; the caller supplies the order.)
        if (module.TabID > 0)
        {
            var placement = new TabModule
            {
                TabID = module.TabID,
                ModuleID = module.ModuleID,
            };
            ApplyPlacement(placement, module);

            await _context.TabModules.AddAsync(placement, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            module.TabModuleID = placement.TabModuleID;
        }

        return module;
    }

    public async Task UpdateAsync(Module module, CancellationToken cancellationToken = default)
    {
        _context.Modules.Update(module);

        // MIGRATION (CP3 schema fidelity): legacy UpdateModule synced the per-tab placement via
        // UpdateTabModule. The update contract treats TabID as immutable and carries no tab selector, so the
        // placement-settings columns (PaneName/ModuleOrder/CacheTime/Alignment/Color/Border/IconFile/
        // Visibility/ContainerSrc/Display*) are synced onto every TabModules row for this module. The legacy
        // ModuleOrder bottom-of-pane auto-positioning (UpdateModuleOrder) and tab RE-placement (moving a
        // module between tabs) remain out of Phase-1 scope. Recorded in MIGRATION_NOTES.md §4.2 / D-031.
        var placements = await _context.TabModules
            .Where(tm => tm.ModuleID == module.ModuleID)
            .ToListAsync(cancellationToken);

        foreach (var placement in placements)
        {
            ApplyPlacement(placement, module);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int moduleId, CancellationToken cancellationToken = default)
    {
        // MIGRATION: Module delete is a SOFT delete (IsDeleted = true), matching the Modules table's
        // [IsDeleted] bit column and the legacy DeleteModule behavior (delete strategy, MIGRATION_NOTES §6.3).
        // The fetch is intentionally unfiltered so an already-loaded module can still be flagged. The
        // TabModules placement rows are intentionally retained (the module record is only logically deleted).
        var module = await _context.Modules
            .FirstOrDefaultAsync(m => m.ModuleID == moduleId, cancellationToken);

        if (module is null)
        {
            return;
        }

        module.IsDeleted = true;
        await _context.SaveChangesAsync(cancellationToken);
    }

    // -------------------------------------------------------------------------
    // Placement helpers (dbo.TabModules <-> Module). The Module entity Ignore()s the TabModules-sourced
    // fields (they are not dbo.Modules columns); these helpers move placement data between the two.
    // -------------------------------------------------------------------------

    /// <summary>
    /// Copies the module-instance placement settings onto a <see cref="TabModule"/> row. Used by
    /// <see cref="AddAsync"/> (new placement) and <see cref="UpdateAsync"/> (sync existing placements).
    /// <c>PaneName</c> is NOT NULL in <c>dbo.TabModules</c>: a supplied value wins; otherwise an existing
    /// pane is preserved and a brand-new placement defaults to the DNN content pane.
    /// </summary>
    private static void ApplyPlacement(TabModule target, Module source)
    {
        target.ModuleOrder = source.ModuleOrder;
        target.CacheTime = source.CacheTime;
        target.Alignment = source.Alignment;
        target.Color = source.Color;
        target.Border = source.Border;
        target.IconFile = source.IconFile;
        target.Visibility = source.Visibility;
        target.ContainerSrc = source.ContainerSrc;
        target.DisplayTitle = source.DisplayTitle;
        target.DisplayPrint = source.DisplayPrint;
        target.DisplaySyndicate = source.DisplaySyndicate;

        if (!string.IsNullOrEmpty(source.PaneName))
        {
            target.PaneName = source.PaneName;
        }
        else if (string.IsNullOrEmpty(target.PaneName))
        {
            target.PaneName = DefaultPaneName;
        }
    }

    /// <summary>
    /// Rehydrates the <c>TabModules</c>-sourced placement fields onto a <see cref="Module"/> read via a
    /// <c>TabModules</c> join. The <see cref="Module"/> entity <c>Ignore()</c>s these fields (they are not
    /// <c>dbo.Modules</c> columns); this restores them in-memory for the DTO projection. The module is read
    /// <c>AsNoTracking</c>, so these assignments never persist.
    /// </summary>
    private static void RehydratePlacement(Module module, TabModule placement)
    {
        module.TabModuleID = placement.TabModuleID;
        module.TabID = placement.TabID;
        module.PaneName = placement.PaneName;
        module.ModuleOrder = placement.ModuleOrder;
        module.CacheTime = placement.CacheTime;
        module.Alignment = placement.Alignment;
        module.Color = placement.Color;
        module.Border = placement.Border;
        module.IconFile = placement.IconFile;
        module.Visibility = placement.Visibility;
        module.ContainerSrc = placement.ContainerSrc;
        module.DisplayTitle = placement.DisplayTitle;
        module.DisplayPrint = placement.DisplayPrint;
        module.DisplaySyndicate = placement.DisplaySyndicate;
    }
}
