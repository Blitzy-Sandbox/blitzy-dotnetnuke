using DnnMigration.Domain.Entities;
using DnnMigration.Domain.Interfaces;
using DnnMigration.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DnnMigration.Infrastructure.Repositories;

// MIGRATION: EF Core 8 implementation of DnnMigration.Domain.Interfaces.IModuleRepository. Replaces the
// data-access portions of the legacy VB.NET DotNetNuke.Entities.Modules.ModuleController
// (Library/Components/Modules/ModuleController.vb, 1456 lines). The legacy controller reached the database
// through the reflection-instantiated DataProvider.Instance() singleton -> SqlHelper -> stored procedure ->
// IDataReader, then materialized rows with the private FillModuleInfo / FillModuleInfoCollection /
// FillModuleInfoDictionary helpers. That entire pipeline is replaced here by the injected DnnDbContext and
// async LINQ returning Domain Module entities.
// MIGRATION: STAGE-ONLY persistence. This repository only STAGES changes on the DbContext change tracker
// (AddAsync/Update/Remove); it deliberately performs NO SaveChanges / SaveChangesAsync. Flushing the unit of
// work is the responsibility of IUnitOfWork.SaveChangesAsync, invoked by the Application-layer ModuleService.
// Business rules and orchestration live in ModuleService.cs — this class is persistence only (Clean/Onion,
// AAP 0.3.3 / 0.7.3). Modules stay scoped by PortalId (tenant) and TabId (page placement), preserving DNN
// multi-tenant isolation (AAP 0.7.1).
/// <summary>
/// Entity Framework Core 8 implementation of <see cref="IModuleRepository"/>. Provides persistence-only
/// (stage-only) access to <see cref="Module"/> aggregates over the injected <see cref="DnnDbContext"/>,
/// replacing the legacy <c>ModuleController</c> data-access surface. The two collection reads
/// (<see cref="GetByPortalIdAsync"/> and <see cref="GetByTabIdAsync"/>) exclude soft-deleted modules to
/// preserve the legacy "active modules" behavior; single-entity lookups do not, so update/soft-delete flows
/// can still load a soft-deleted row by id.
/// </summary>
public sealed class ModuleRepository : IModuleRepository
{
    private readonly DnnDbContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="ModuleRepository"/> class.
    /// </summary>
    /// <param name="context">
    /// The application <see cref="DnnDbContext"/> (DI-registered, scoped). Replaces the legacy
    /// <c>DataProvider.Instance()</c> reflection singleton from <c>ModuleController.vb</c>.
    /// </param>
    // MIGRATION: Constructor injection of DnnDbContext replaces the legacy DataProvider.Instance() singleton
    // lookup (Framework.Reflection.CreateObject) that ModuleController.vb used for every data operation.
    public ModuleRepository(DnnDbContext context) => _context = context;

    /// <inheritdoc />
    // MIGRATION: ModuleController.GetModules(PortalID) L915 ->
    // FillModuleInfoCollection(DataProvider.Instance().GetModules(PortalID)). Tenant-scoped by PortalId for
    // multi-tenant isolation (AAP 0.7.1); the !IsDeleted predicate preserves the legacy GetModules stored
    // procedure's active-modules filter (soft-deleted rows were excluded at the SQL level).
    public async Task<IEnumerable<Module>> GetByPortalIdAsync(int portalId)
    {
        return await _context.Modules
            .Where(m => m.PortalId == portalId && !m.IsDeleted)
            .ToListAsync();
    }

    /// <inheritdoc />
    // MIGRATION: CP1 review (performance, AAP 0.7.7) - paged, portal-scoped variant of GetModules(PortalID) that returns ONLY the
    // requested page plus the total matching count, replacing fetch-all + in-memory paging. Tenant-scoped by PortalId (AAP 0.7.1);
    // the !IsDeleted predicate preserves the legacy active-modules filter and is applied to BOTH the count and the page so the total
    // reflects active modules only. PageIndex is ZERO-BASED; a deterministic OrderBy(ModuleOrder, ModuleId) gives a stable page.
    public async Task<(IEnumerable<Module> Items, int TotalCount)> GetByPortalPagedAsync(int portalId, int pageIndex, int pageSize)
    {
        var query = _context.Modules.Where(m => m.PortalId == portalId && !m.IsDeleted);
        var total = await query.CountAsync();
        var items = await query
            .OrderBy(m => m.ModuleOrder)
            .ThenBy(m => m.ModuleId)
            .Skip(pageIndex * pageSize)
            .Take(pageSize)
            .ToListAsync();
        return (items, total);
    }

    /// <inheritdoc />
    // MIGRATION: ModuleController.GetTabModules(TabId) L1044 ->
    // FillModuleInfoDictionary(DataProvider.Instance().GetTabModules(TabId)). CP1 review (IModuleRepository): PORTAL-SCOPED
    // (PortalId + TabId) to preserve multi-tenant isolation (AAP 0.7.1) so a portal can only read modules on its own pages.
    // Returns the active modules placed on the given page (tab); the !IsDeleted predicate mirrors the legacy active-modules filter.
    public async Task<IEnumerable<Module>> GetByTabIdAsync(int portalId, int tabId)
    {
        return await _context.Modules
            .Where(m => m.PortalId == portalId && m.TabId == tabId && !m.IsDeleted)
            .ToListAsync();
    }

    /// <inheritdoc />
    // MIGRATION: ModuleController.GetModule(ModuleId, TabId) L1418 -> GetModule(ModuleId, TabId, True). CP1 review
    // (IModuleRepository): the lookup is PORTAL-SCOPED (PortalId + ModuleId) to preserve multi-tenant isolation
    // (AAP 0.7.1) so a portal can only read its own modules; the legacy TabId argument remains dropped because a
    // module could appear on multiple tabs via TabModule rows. No soft-delete filter is applied on a single-id
    // lookup, so a soft-deleted module can still be fetched by id - matching the service's update-then-soft-delete
    // flow. Returns null when no module matches.
    public async Task<Module?> GetByIdAsync(int portalId, int moduleId)
    {
        return await _context.Modules
            .FirstOrDefaultAsync(m => m.PortalId == portalId && m.ModuleId == moduleId);
    }

    /// <inheritdoc />
    // MIGRATION: ModuleController.GetModuleByDefinition(PortalId, FriendlyName) L955 ->
    // DataProvider.Instance().GetModuleByDefinition(PortalId, FriendlyName). Portal-scoped lookup of the first
    // module instance whose module-definition friendly name matches. No soft-delete filter (parity with the
    // legacy definition lookup). Returns null when no module matches.
    public async Task<Module?> GetByDefinitionAsync(int portalId, string friendlyName)
    {
        return await _context.Modules
            .FirstOrDefaultAsync(m => m.PortalId == portalId && m.FriendlyName == friendlyName);
    }

    /// <inheritdoc />
    // MIGRATION: ModuleController.AddModule(objModule) L645 -> DataProvider.Instance().AddModule(...). STAGE-ONLY:
    // the new row is tracked for insert here; the actual INSERT is flushed later by IUnitOfWork.SaveChangesAsync
    // (called by ModuleService), after which the database-generated ModuleId is populated on the returned entity.
    public async Task<Module> AddAsync(Module module)
    {
        await _context.Modules.AddAsync(module);
        return module;
    }

    /// <inheritdoc />
    // MIGRATION: ModuleController.UpdateModule(objModule) L1095 -> DataProvider.Instance().UpdateModule(...).
    // STAGE-ONLY: marks the entity as modified on the change tracker; SaveChanges is the service's IUnitOfWork
    // responsibility. This is also the path ModuleService uses for the SOFT delete (sets IsDeleted = true and
    // TabId = null, then calls UpdateAsync). Synchronous body wrapped in a completed Task to satisfy the
    // async-returning interface contract without a redundant state machine.
    public Task UpdateAsync(Module module)
    {
        _context.Modules.Update(module);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    // MIGRATION: ModuleController.DeleteModule(ModuleId) L819 -> DataProvider.Instance().DeleteModule(ModuleId),
    // a HARD delete (permanent row removal). CP1 review (IModuleRepository): PORTAL-SCOPED (PortalId + ModuleId) to
    // preserve multi-tenant isolation (AAP 0.7.1) so a portal can only delete its own modules. STAGE-ONLY: loads the
    // tracked entity and marks it for removal; the DELETE is flushed by IUnitOfWork.SaveChangesAsync. NOTE:
    // ModuleService performs a SOFT delete via UpdateAsync (IsDeleted = true; TabId = null) and does NOT call this
    // member; this HARD delete is retained to honor the IModuleRepository contract. A missing module is a no-op.
    public async Task DeleteAsync(int portalId, int moduleId)
    {
        var module = await _context.Modules
            .FirstOrDefaultAsync(m => m.PortalId == portalId && m.ModuleId == moduleId);

        if (module is not null)
        {
            _context.Modules.Remove(module);
        }
    }
}
