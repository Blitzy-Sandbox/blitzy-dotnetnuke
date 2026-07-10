using DnnMigration.Domain.Common;
using DnnMigration.Domain.Entities;
using DnnMigration.Domain.Interfaces;
using DnnMigration.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DnnMigration.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IModuleRepository"/> backed by <see cref="DnnDbContext"/>.
/// </summary>
// MIGRATION: Re-expresses the data-access portion of the legacy VB.NET
// DotNetNuke.Entities.Modules.ModuleController (Library/Components/Modules/ModuleController.vb) as EF Core
// LINQ. Legacy access went through DataProvider.Instance().GetModules/GetModuleByDefinition (IDataReader)
// with FillModuleInfoCollection/FillModuleInfo (CBO reflection hydration) and DataCache; that
// provider/CBO/cache indirection is dropped in favour of DnnDbContext DbSet<Module> queries. Data access only.
public class ModuleRepository : IModuleRepository
{
    private readonly DnnDbContext _context;

    public ModuleRepository(DnnDbContext context)
    {
        _context = context;
    }

    // MIGRATION: ModuleController.GetModule / DataProvider.GetModule single-reader lookup.
    // MIGRATION (QA finding I): the [Modules] row carries only the module's own columns; its PLACEMENT
    // (which tab/pane, order, container, visibility, ...) lives on [TabModules] and its denormalized
    // definition LOOKUP fields (FriendlyName/FolderName/Description/Version/ModuleName/DesktopModuleID)
    // live on [ModuleDefinitions] -> [DesktopModules]. The legacy FillModuleInfo populated all of these
    // onto the returned ModuleInfo; without that, a GET returned default/zero placement + lookup values.
    // HydrateAsync restores those transient carriers so the module round-trips its full field set.
    public async Task<Module?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var module = await _context.Modules
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.ModuleID == id, cancellationToken);
        if (module is null)
        {
            return null;
        }

        await HydrateManyAsync(new[] { module }, cancellationToken);
        return module;
    }

    // MIGRATION: no exact 1:1 legacy "all modules across all portals" reader; provided to satisfy the
    // generic IRepository<Module> contract as an unfiltered AsNoTracking projection.
    // MIGRATION (QA finding I): hydrate placement + definition lookup carriers on every returned module.
    public async Task<IEnumerable<Module>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var modules = await _context.Modules
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        await HydrateManyAsync(modules, cancellationToken);
        return modules;
    }

    // MIGRATION: ModuleController.GetModules(PortalID) [ModuleController.vb L915] —
    // FillModuleInfoCollection(DataProvider.Instance().GetModules(PortalID)). Permission-hydration
    // overload (L928) is omitted (hydration is not repository data access).
    public async Task<IEnumerable<Module>> GetByPortalAsync(int portalId, CancellationToken cancellationToken = default)
    {
        var modules = await _context.Modules
            .AsNoTracking()
            .Where(m => m.PortalID == portalId)
            .ToListAsync(cancellationToken);

        // MIGRATION (QA finding I): hydrate placement + definition lookup carriers on every returned module.
        await HydrateManyAsync(modules, cancellationToken);
        return modules;
    }

    // MIGRATION: ModuleController.GetModuleByDefinition(PortalId, FriendlyName) [ModuleController.vb L955].
    // The legacy Dictionary<string, ModuleInfo> DataCache lookup is dropped; the DB fallback query is
    // preserved.
    //
    // SCHEMA FIDELITY (corrected): this faithfully reproduces the legacy stored procedure
    // {objectQualifier}GetModuleByDefinition (DotNetNuke.Schema.SqlDataProvider), which reads the
    // denormalized [vw_Modules] VIEW and filters
    //   "((PortalId = @PortalID) OR (PortalId IS NULL AND @PortalID IS NULL)) AND FriendlyName = @FriendlyName AND IsDeleted = 0".
    // In [vw_Modules] the FriendlyName column is projected from DM.* — i.e. it is
    // [DesktopModules].[FriendlyName] (the desktop-module registration's friendly name, carrying the
    // UNIQUE [IX_DesktopModules] index), NOT a [Modules] column. The previous implementation filtered
    // m.FriendlyName, which is an Ignore()d (unmapped) TRANSIENT carrier on the Module entity; that
    // produces invalid SQL on a relational provider (EF Core cannot translate an unmapped member) and
    // silently matches nothing on the InMemory provider (the member is never materialized). The lookup
    // is therefore re-expressed as the explicit relational join the view performs —
    //   Modules.ModuleDefID -> ModuleDefinitions.ModuleDefID -> DesktopModules.DesktopModuleID —
    // matching [DesktopModules].[FriendlyName]. Only mapped, fully store-translatable columns
    // participate, so the query is valid against SQL Server and is equally honoured by the EF Core
    // InMemory provider used by the integration tests. The legacy "AND IsDeleted = 0" predicate is
    // preserved for behavioural parity (soft-deleted module instances are excluded).
    public async Task<Module?> GetByDefinitionAsync(int portalId, string friendlyName, CancellationToken cancellationToken = default)
    {
        var query =
            from m in _context.Modules.AsNoTracking()
            join md in _context.ModuleDefinitions.AsNoTracking() on m.ModuleDefID equals md.ModuleDefID
            join dm in _context.DesktopModules.AsNoTracking() on md.DesktopModuleID equals dm.DesktopModuleID
            where m.PortalID == portalId && !m.IsDeleted && dm.FriendlyName == friendlyName
            select m;

        var module = await query.FirstOrDefaultAsync(cancellationToken);
        if (module is null)
        {
            return null;
        }

        // MIGRATION (QA finding I): hydrate placement + definition lookup carriers on the returned module.
        await HydrateManyAsync(new[] { module }, cancellationToken);
        return module;
    }

    // MIGRATION: the legacy Website/admin/Modules/** inventory grid text search. Re-expressed as a
    // case-insensitive substring match over the module's TITLE, optionally scoped to a single portal
    // (preserving ModulesController's per-portal authorization scoping). AsNoTracking (read path).
    // ModuleTitle is the only free-text field PHYSICALLY on the Modules table: FriendlyName and ModuleName
    // are DENORMALIZED lookup members that live on the related ModuleDefinition / DesktopModule tables and
    // are Ignore()d on the Module entity (see ModuleConfiguration), so they have no column to translate and
    // cannot participate in a store-side query (doing so raises EF Core "member is unmapped" at translation
    // time). ToLower()/Contains translate to SQL LOWER(...) LIKE and are also honoured by the EF Core
    // InMemory provider used by the integration tests.
    public async Task<IEnumerable<Module>> SearchAsync(int? portalId, string query, CancellationToken cancellationToken = default)
    {
        var term = query.ToLower();
        var q = _context.Modules.AsNoTracking();
        if (portalId.HasValue)
        {
            q = q.Where(m => m.PortalID == portalId.Value);
        }

        var modules = await q
            .Where(m => m.ModuleTitle != null && m.ModuleTitle.ToLower().Contains(term))
            .ToListAsync(cancellationToken);

        // MIGRATION (QA finding I): hydrate placement + definition lookup carriers on every returned module.
        await HydrateManyAsync(modules, cancellationToken);
        return modules;
    }

    // MIGRATION (QA finding — R6 Issue 1): bounded page of GetAllAsync. One COUNT over the full [Modules]
    // set plus one windowed SELECT ordered by the ModuleID primary key, then the SAME batched
    // HydrateManyAsync so a paged row round-trips the full denormalized placement + definition field set
    // (QA finding I). AsNoTracking (read path).
    public async Task<PagedResult<Module>> GetPagedAsync(int skip, int take, CancellationToken cancellationToken = default)
    {
        var baseQuery = _context.Modules.AsNoTracking();
        var total = await baseQuery.CountAsync(cancellationToken);
        var modules = await baseQuery
            .OrderBy(m => m.ModuleID)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
        await HydrateManyAsync(modules, cancellationToken);
        return new PagedResult<Module>(modules, total);
    }

    // MIGRATION (QA finding — R6 Issue 1): bounded page of GetByPortalAsync. The SAME PortalID filter is
    // applied to the base query (shared by COUNT and the page); only the Skip/Take window (ordered by
    // ModuleID) is materialized and hydrated. AsNoTracking (read path).
    public async Task<PagedResult<Module>> GetByPortalPagedAsync(int portalId, int skip, int take, CancellationToken cancellationToken = default)
    {
        var baseQuery = _context.Modules
            .AsNoTracking()
            .Where(m => m.PortalID == portalId);
        var total = await baseQuery.CountAsync(cancellationToken);
        var modules = await baseQuery
            .OrderBy(m => m.ModuleID)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
        await HydrateManyAsync(modules, cancellationToken);
        return new PagedResult<Module>(modules, total);
    }

    // MIGRATION (QA finding — R6 Issue 1): bounded page of SearchAsync. The SAME optional portal scope +
    // case-insensitive ModuleTitle substring predicate is built onto the base query (shared by COUNT and
    // the page); only the Skip/Take window (ordered by ModuleID) is materialized and hydrated. AsNoTracking.
    public async Task<PagedResult<Module>> SearchPagedAsync(int? portalId, string query, int skip, int take, CancellationToken cancellationToken = default)
    {
        var term = query.ToLower();
        var q = _context.Modules.AsNoTracking();
        if (portalId.HasValue)
        {
            q = q.Where(m => m.PortalID == portalId.Value);
        }

        var baseQuery = q.Where(m => m.ModuleTitle != null && m.ModuleTitle.ToLower().Contains(term));
        var total = await baseQuery.CountAsync(cancellationToken);
        var modules = await baseQuery
            .OrderBy(m => m.ModuleID)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
        await HydrateManyAsync(modules, cancellationToken);
        return new PagedResult<Module>(modules, total);
    }

    // MIGRATION: ModuleController.AddModule -> DataProvider.AddModule stored proc.
    public async Task<Module> AddAsync(Module entity, CancellationToken cancellationToken = default)
    {
        _context.Modules.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    // MIGRATION (QA finding C): atomic module-with-placements insert. The legacy AddModule wrote the
    // [Modules] row and its [TabModules] placement row(s) as one logical operation; persisting them in
    // separate SaveChanges calls meant a placement failure (a bad TabID violating FK_TabModules_Tabs) left
    // an orphaned [Modules] row and returned a raw 500. Here the module is inserted (yielding its
    // store-generated ModuleID), that id is stamped onto every placement, and the placements are inserted -
    // all within a single transaction on relational providers, wrapped in an execution strategy so it is
    // safe even when connection-resiliency retries are enabled. On the EF Core InMemory provider (no
    // transaction support) the saves run without an explicit transaction; the service's up-front TabID
    // pre-validation, not the FK, is what prevents a bad-tab placement there.
    public async Task<Module> AddWithPlacementsAsync(
        Module module,
        IReadOnlyList<TabModule> placements,
        CancellationToken cancellationToken = default)
    {
        // Non-relational (InMemory tests): no transaction available; persist sequentially.
        if (!_context.Database.IsRelational())
        {
            await PersistModuleAndPlacementsAsync(module, placements, cancellationToken);
            return module;
        }

        // Relational (SQL Server host): all-or-nothing via an execution-strategy-wrapped transaction.
        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            await PersistModuleAndPlacementsAsync(module, placements, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return module;
        });
    }

    // Shared insert body for AddWithPlacementsAsync: insert the module to obtain its store-generated
    // ModuleID, stamp it onto each placement, then insert the placements. Kept private so the transactional
    // and non-transactional paths share identical persistence logic.
    private async Task PersistModuleAndPlacementsAsync(
        Module module,
        IReadOnlyList<TabModule> placements,
        CancellationToken cancellationToken)
    {
        _context.Modules.Add(module);
        await _context.SaveChangesAsync(cancellationToken); // assigns the store-generated ModuleID

        if (placements.Count > 0)
        {
            foreach (var placement in placements)
            {
                placement.ModuleID = module.ModuleID;
            }

            _context.TabModules.AddRange(placements);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    // MIGRATION: ModuleController.UpdateModule -> DataProvider.UpdateModule stored proc.
    public async Task UpdateAsync(Module entity, CancellationToken cancellationToken = default)
    {
        _context.Modules.Update(entity);
        await _context.SaveChangesAsync(cancellationToken);
    }

    // MIGRATION: ModuleController.DeleteModule -> DataProvider.DeleteModule stored proc. Tracked fetch
    // (no AsNoTracking) so EF can mark the entity Deleted, then remove.
    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Modules
            .FirstOrDefaultAsync(m => m.ModuleID == id, cancellationToken);
        if (entity is not null)
        {
            _context.Modules.Remove(entity);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    // =========================================================================
    //  TabModule placement surface (see IModuleRepository for the full rationale)
    // =========================================================================

    // MIGRATION: ModuleController.AddModule -> DataProvider.AddTabModule stored proc. TabModules.TabModuleID
    // is a surrogate IDENTITY (ValueGeneratedOnAdd) so the ModuleID/TabID FKs are NON-identifying; the row
    // is written by value (no principal navigation required), which the EF Core change tracker accepts on
    // both SQL Server and the InMemory provider.
    public async Task<TabModule> AddTabModuleAsync(TabModule tabModule, CancellationToken cancellationToken = default)
    {
        _context.TabModules.Add(tabModule);
        await _context.SaveChangesAsync(cancellationToken);
        return tabModule;
    }

    // MIGRATION: the [TabModules] rows a module owns. AsNoTracking read path; the caller (ModuleService)
    // mutates the returned rows and persists them via UpdateTabModuleAsync (which re-attaches them), so no
    // tracked instances are held between the read and the write.
    public async Task<IEnumerable<TabModule>> GetTabModulesByModuleAsync(int moduleId, CancellationToken cancellationToken = default)
    {
        return await _context.TabModules
            .AsNoTracking()
            .Where(tm => tm.ModuleID == moduleId)
            .ToListAsync(cancellationToken);
    }

    // MIGRATION: ModuleController.UpdateModule -> DataProvider.UpdateTabModule stored proc. Update() attaches
    // the (detached, AsNoTracking-read) row as Modified and persists it by its surrogate TabModuleID.
    public async Task UpdateTabModuleAsync(TabModule tabModule, CancellationToken cancellationToken = default)
    {
        _context.TabModules.Update(tabModule);
        await _context.SaveChangesAsync(cancellationToken);
    }

    // MIGRATION: the ON DELETE CASCADE FK_{objectQualifier}TabModules_{objectQualifier}Modules. Performed
    // explicitly because the EF Core InMemory provider does not enforce referential cascade; on SQL Server
    // this mirrors the physical cascade so behaviour is identical across providers. Tracked fetch so the
    // rows can be marked Deleted, then removed as a batch.
    public async Task DeleteTabModulesByModuleAsync(int moduleId, CancellationToken cancellationToken = default)
    {
        var rows = await _context.TabModules
            .Where(tm => tm.ModuleID == moduleId)
            .ToListAsync(cancellationToken);
        if (rows.Count > 0)
        {
            _context.TabModules.RemoveRange(rows);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    // =========================================================================
    //  QA finding I: transient-carrier hydration
    // =========================================================================

    // MIGRATION (QA finding I): the Domain Module is DNN's DENORMALIZED module-instance object,
    // historically materialized from the legacy [vw_Modules] VIEW, which joined
    //   [Modules] (M.*) + [TabModules] (TM.*) + [ModuleDefinitions] + [DesktopModules] (DM.*) + [ModuleControls] (MC.*).
    // In the corrected relational model only the 11 M.* columns are physically on [Modules]; the
    // PLACEMENT (TM.*) and DEFINITION/desktop-module (DM.*) fields are Ignore()d transient carriers
    // (see ModuleConfiguration). A bare "SELECT ... FROM Modules" therefore leaves every placement and
    // lookup carrier at its CLR default, so a GET round-tripped only the M.* fields (ModuleTitle et al.)
    // and reported zero/empty placement + definition data. HydrateManyAsync restores those carriers the
    // way the legacy FillModuleInfo did, in a BATCHED fashion (three set-based queries regardless of how
    // many modules are passed) to avoid an N+1 pattern across the read paths.
    //
    // The [ModuleControls] lookup family (ControlSrc, ControlType, ControlTitle, HelpUrl,
    // ModuleControlId, SupportsPartialRendering) and the retired [Modules] role columns
    // (AuthorizedEditRoles / AuthorizedViewRoles) have NO backing entity in the in-scope migration
    // (ModuleControls was not modelled and the v4.9 [Modules] table no longer carries the role columns),
    // so they intentionally remain at their defaults (ControlSrc stays ""). This limitation is
    // documented in MIGRATION_NOTES.md.
    private async Task HydrateManyAsync(IReadOnlyCollection<Module> modules, CancellationToken cancellationToken)
    {
        if (modules.Count == 0)
        {
            return;
        }

        // --- 1) Placement carriers (TM.*): one representative [TabModules] row per module ---
        // A module can be placed on several tabs; the legacy single-object view surfaced one placement,
        // so the lowest (ModuleOrder, TabModuleID) row per ModuleID is chosen deterministically. The
        // rows are materialized first (AsNoTracking) and grouped in memory, so no provider-specific
        // GroupBy translation is required (identical behaviour on SQL Server and the InMemory provider).
        var moduleIds = modules.Select(m => m.ModuleID).Distinct().ToList();
        var placements = await _context.TabModules
            .AsNoTracking()
            .Where(tm => moduleIds.Contains(tm.ModuleID))
            .ToListAsync(cancellationToken);
        var placementByModule = placements
            .GroupBy(tm => tm.ModuleID)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(tm => tm.ModuleOrder).ThenBy(tm => tm.TabModuleID).First());

        // --- 2) Definition lookup: [ModuleDefinitions] keyed by ModuleDefID (PK, so no dup keys) ---
        var moduleDefIds = modules.Select(m => m.ModuleDefID).Distinct().ToList();
        var definitionById = await _context.ModuleDefinitions
            .AsNoTracking()
            .Where(md => moduleDefIds.Contains(md.ModuleDefID))
            .ToDictionaryAsync(md => md.ModuleDefID, cancellationToken);

        // --- 3) Desktop-module lookup (DM.*): [DesktopModules] keyed by DesktopModuleID (PK) ---
        var desktopModuleIds = definitionById.Values
            .Select(md => md.DesktopModuleID)
            .Distinct()
            .ToList();
        var desktopModuleById = await _context.DesktopModules
            .AsNoTracking()
            .Where(dm => desktopModuleIds.Contains(dm.DesktopModuleID))
            .ToDictionaryAsync(dm => dm.DesktopModuleID, cancellationToken);

        foreach (var module in modules)
        {
            if (placementByModule.TryGetValue(module.ModuleID, out var placement))
            {
                ApplyPlacement(module, placement);
            }

            if (definitionById.TryGetValue(module.ModuleDefID, out var definition))
            {
                module.DefaultCacheTime = definition.DefaultCacheTime;

                if (desktopModuleById.TryGetValue(definition.DesktopModuleID, out var desktopModule))
                {
                    ApplyDesktopModule(module, desktopModule);
                }
            }
        }
    }

    // MIGRATION (QA finding I): copy the TM.* placement carriers from the representative [TabModules]
    // row onto the denormalized Module (mirroring the [vw_Modules] TM.* projection).
    private static void ApplyPlacement(Module module, TabModule placement)
    {
        module.TabID = placement.TabID;
        module.TabModuleID = placement.TabModuleID;
        module.ModuleOrder = placement.ModuleOrder;
        module.PaneName = placement.PaneName;
        module.CacheTime = placement.CacheTime;
        module.Alignment = placement.Alignment;
        module.Color = placement.Color;
        module.Border = placement.Border;
        module.IconFile = placement.IconFile;
        module.ContainerSrc = placement.ContainerSrc;
        module.Visibility = placement.Visibility;
        module.DisplayTitle = placement.DisplayTitle;
        module.DisplayPrint = placement.DisplayPrint;
        module.DisplaySyndicate = placement.DisplaySyndicate;
    }

    // MIGRATION (QA finding I): copy the DM.* desktop-module registration carriers onto the
    // denormalized Module (mirroring the [vw_Modules] DM.* projection). DesktopModuleID is taken from
    // the resolved DesktopModule so the carrier reflects the registration id.
    private static void ApplyDesktopModule(Module module, DesktopModule desktopModule)
    {
        module.DesktopModuleID = desktopModule.DesktopModuleID;
        module.FriendlyName = desktopModule.FriendlyName;
        module.FolderName = desktopModule.FolderName;
        module.Description = desktopModule.Description;
        module.Version = desktopModule.Version;
        module.IsPremium = desktopModule.IsPremium;
        module.IsAdmin = desktopModule.IsAdmin;
        module.BusinessControllerClass = desktopModule.BusinessControllerClass;
        module.ModuleName = desktopModule.ModuleName;
        module.SupportedFeatures = desktopModule.SupportedFeatures;
        module.CompatibleVersions = desktopModule.CompatibleVersions;
        module.Dependencies = desktopModule.Dependencies;
        module.Permissions = desktopModule.Permissions;
    }
}
