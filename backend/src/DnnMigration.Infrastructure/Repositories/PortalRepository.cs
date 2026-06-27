using DnnMigration.Domain.Entities;
using DnnMigration.Domain.Interfaces;
using DnnMigration.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DnnMigration.Infrastructure.Repositories;

// MIGRATION: Replaces DotNetNuke.Entities.Portals.PortalController data operations (PortalController.vb) that used
// DataProvider.Instance() (Library/Components/Providers/Data/DataProvider.vb). Stored-procedure ADO.NET +
// FillPortalInfo materialization is replaced by async EF Core LINQ over DnnDbContext. STAGE-ONLY: no SaveChanges
// here — PortalService calls IUnitOfWork.SaveChangesAsync as the commit boundary.
//
// Clean/Onion architecture: this Infrastructure-layer type implements the Domain contract IPortalRepository and
// depends only on the Domain entity (Portal) and the EF Core DbContext. It contains persistence logic ONLY — all
// portal business rules transcribed from PortalController.vb live in the Application-layer PortalService.
public sealed class PortalRepository : IPortalRepository
{
    // MIGRATION: The reflection-instantiated DataProvider.Instance() singleton (DataProvider.vb L48, built via
    // Framework.Reflection.CreateObject) is replaced by a constructor-injected, DI-scoped DnnDbContext. The SAME
    // scoped context instance is shared with the UnitOfWork, so the entity-state changes this repository stages
    // (Add/Update/Remove) are committed by the service's single IUnitOfWork.SaveChangesAsync call.
    private readonly DnnDbContext _context;

    /// <summary>
    /// Initializes the repository with the DI-scoped <see cref="DnnDbContext"/> shared with the unit of work.
    /// </summary>
    /// <param name="context">The EF Core database context for the current request scope.</param>
    public PortalRepository(DnnDbContext context) => _context = context;

    // MIGRATION: PortalController.GetPortals (PortalController.vb L1263) ->
    // FillPortalInfoCollection(DataProvider.Instance().GetPortals()). The IDataReader + FillPortalInfoCollection
    // materialization is replaced by an async LINQ enumeration over the Portals DbSet (the host-level listing of
    // every portal). Returns Domain entities; DTO projection happens in the Application layer.
    public async Task<IEnumerable<Portal>> GetAllAsync()
    {
        return await _context.Portals.ToListAsync();
    }

    // MIGRATION: PortalController.GetPortal(PortalId) (PortalController.vb L1224) ->
    // DataProvider.Instance().GetPortal(PortalId) + FillPortalInfo. The legacy persistent-cache lookup
    // (DataCache.GetPersistentCacheItem / SetCache) is a cross-cutting caching concern handled outside this
    // persistence layer; the repository performs the database read only. Returns null when no portal matches the
    // supplied identifier (nullable reference types are ON, so the absence of a row is modeled as Portal?).
    public async Task<Portal?> GetByIdAsync(int portalId)
    {
        return await _context.Portals.FirstOrDefaultAsync(p => p.PortalId == portalId);
    }

    // MIGRATION: PortalController.GetPortalByAlias -> DataProvider.GetPortalByAlias (DataProvider.vb L98), which
    // looked up the [PortalAlias] table by HTTPAlias and returned the owning portal. The legacy alias->portal
    // mapping is now modeled by the PortalAlias entity (existing [PortalAlias] table) and resolved here with async
    // EF Core LINQ. DNN treats host aliases case-insensitively and trimmed, so the supplied alias and the stored
    // [HTTPAlias] are normalized to lower-case for comparison. The lookup is a two-step read (alias row, then the
    // portal it points to) rather than a join projecting PortalID, because [PortalID] 0 is the valid default DNN
    // portal — a single FirstOrDefault over an int projection could not distinguish "portal 0" from "no match".
    public async Task<Portal?> GetByAliasAsync(string portalAlias)
    {
        if (string.IsNullOrWhiteSpace(portalAlias))
        {
            return null;
        }

        string normalizedAlias = portalAlias.Trim().ToLowerInvariant();

        PortalAlias? alias = await _context.PortalAliases
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.HttpAlias != null && a.HttpAlias.ToLower() == normalizedAlias);

        if (alias is null)
        {
            return null;
        }

        return await _context.Portals
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PortalId == alias.PortalId);
    }

    // MIGRATION: PortalController.CreatePortal (PortalController.vb L980) -> ultimately
    // DataProvider.Instance().CreatePortal(PortalName, HomeDirectory). The insert is STAGED only: the
    // database-generated PortalId is populated on the returned entity by EF Core change tracking only AFTER the
    // service's IUnitOfWork.SaveChangesAsync commits. The same tracked instance is returned so the caller can read
    // the generated key post-commit.
    public async Task<Portal> AddAsync(Portal portal)
    {
        await _context.Portals.AddAsync(portal);
        return portal;
    }

    // MIGRATION: PortalController.UpdatePortalInfo (PortalController.vb L1568) ->
    // DataProvider.Instance().UpdatePortalInfo(...) + cache clear. Marks the entity as Modified (stage only); the
    // database write happens via the service's IUnitOfWork.SaveChangesAsync. Intentionally NOT 'async' — there is no
    // awaitable work, so Task.CompletedTask is returned (avoids CS1998).
    public Task UpdateAsync(Portal portal)
    {
        _context.Portals.Update(portal);
        return Task.CompletedTask;
    }

    // MIGRATION: PortalController.DeletePortalInfo (PortalController.vb L1191) -> deletes portal users
    // (UserController.DeleteUsers) THEN DataProvider.Instance().DeletePortalInfo(PortalId) + DataCache.ClearHostCache.
    // Here the repository stages the portal removal ONLY; the portal-user cascade is staged by PortalService.DeleteAsync,
    // which then issues the single IUnitOfWork.SaveChangesAsync commit boundary. The entity is loaded first because EF
    // Core's change tracker requires a tracked instance to stage a delete. 'is not null' is the C# translation of the
    // legacy VB 'IsNot Nothing' guard.
    public async Task DeleteAsync(int portalId)
    {
        var portal = await _context.Portals.FirstOrDefaultAsync(p => p.PortalId == portalId);
        if (portal is not null)
        {
            _context.Portals.Remove(portal);
        }
    }

    // MIGRATION: CP1 review (performance, AAP 0.7.7) - server-side paged variant of the host-level portal listing
    // (PortalController.GetPortals, PortalController.vb L1263). Returns ONLY the requested page plus the total row
    // count instead of fetch-all + in-memory paging, so a host with many portals does not over-fetch. PageIndex is
    // ZERO-BASED; a deterministic OrderBy(PortalId) is required so Skip/Take produce a stable, repeatable page.
    public async Task<(IEnumerable<Portal> Items, int TotalCount)> GetPagedAsync(int pageIndex, int pageSize)
    {
        var total = await _context.Portals.CountAsync();
        var items = await _context.Portals
            .OrderBy(p => p.PortalId)
            .Skip(pageIndex * pageSize)
            .Take(pageSize)
            .ToListAsync();
        return (items, total);
    }

    // MIGRATION: DOCUMENTED GAP. The legacy PortalController reported bytes consumed by a portal's Home Directory
    // through the file-system provisioning layer (DataProvider.GetPortalSpaceUsed); that file/disk-usage provider is
    // explicitly OUT OF SCOPE for this phase (AAP 0.6.2), and the Portal entity has NO "space used" column - adding
    // one would alter the existing schema, which is forbidden in this phase (AAP 0.7.1). Returning 0 (no space
    // consumed) as a fail-safe so the Application-layer quota check (PortalService.HasSpaceAvailable) stays
    // non-blocking until the file provider is migrated in a later phase. Recorded in MIGRATION_NOTES.md. NOT declared
    // 'async' - there is no awaitable work, so Task.FromResult avoids CS1998 while matching the
    // IPortalRepository.GetSpaceUsedBytesAsync(int) signature exactly.
    public Task<long> GetSpaceUsedBytesAsync(int portalId)
    {
        return Task.FromResult(0L);
    }
}
