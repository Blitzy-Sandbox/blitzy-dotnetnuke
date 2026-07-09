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
    public async Task<Module?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.Modules
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.ModuleID == id, cancellationToken);
    }

    // MIGRATION: no exact 1:1 legacy "all modules across all portals" reader; provided to satisfy the
    // generic IRepository<Module> contract as an unfiltered AsNoTracking projection.
    public async Task<IEnumerable<Module>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Modules
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    // MIGRATION: ModuleController.GetModules(PortalID) [ModuleController.vb L915] —
    // FillModuleInfoCollection(DataProvider.Instance().GetModules(PortalID)). Permission-hydration
    // overload (L928) is omitted (hydration is not repository data access).
    public async Task<IEnumerable<Module>> GetByPortalAsync(int portalId, CancellationToken cancellationToken = default)
    {
        return await _context.Modules
            .AsNoTracking()
            .Where(m => m.PortalID == portalId)
            .ToListAsync(cancellationToken);
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

        return await query.FirstOrDefaultAsync(cancellationToken);
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

        return await q
            .Where(m => m.ModuleTitle != null && m.ModuleTitle.ToLower().Contains(term))
            .ToListAsync(cancellationToken);
    }

    // MIGRATION: ModuleController.AddModule -> DataProvider.AddModule stored proc.
    public async Task<Module> AddAsync(Module entity, CancellationToken cancellationToken = default)
    {
        _context.Modules.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return entity;
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
}
