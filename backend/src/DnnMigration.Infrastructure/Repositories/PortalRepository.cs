using DnnMigration.Domain.Entities;
using DnnMigration.Domain.Interfaces;
using DnnMigration.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DnnMigration.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IPortalRepository"/> backed by <see cref="DnnDbContext"/>.
/// </summary>
// MIGRATION: Re-expresses the data-access portion of the legacy VB.NET
// DotNetNuke.Entities.Portals.PortalController (Library/Components/Portal/PortalController.vb) as EF Core
// LINQ. Legacy access went through DataProvider.Instance().GetPortal/GetPortals (IDataReader) with
// FillPortalInfo/FillPortalInfoCollection (CBO reflection hydration) and a DataCache layer; that
// provider/CBO/cache indirection is dropped in favour of DnnDbContext DbSet<Portal> queries. No business
// logic lives here — data access only.
public class PortalRepository : IPortalRepository
{
    private readonly DnnDbContext _context;

    public PortalRepository(DnnDbContext context)
    {
        _context = context;
    }

    // MIGRATION: PortalController.GetPortal(PortalId) [PortalController.vb L1224]. The legacy DataCache
    // wrapper is intentionally dropped (caching is a cross-cutting concern, not repository data access).
    public async Task<Portal?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.Portals
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PortalID == id, cancellationToken);
    }

    // MIGRATION: PortalController.GetPortals() [PortalController.vb L1263] —
    // FillPortalInfoCollection(DataProvider.Instance().GetPortals()) -> materialized async list.
    public async Task<IEnumerable<Portal>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Portals
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    // MIGRATION: legacy portal-by-alias resolution (DataProvider.GetPortalByAlias -> FillPortalInfo).
    // Portal has no HTTPAlias column (it lives on PortalAlias), so this is expressed as a join from
    // PortalAliases (HTTPAlias == httpAlias) to Portals (PortalID).
    public async Task<Portal?> GetByAliasAsync(string httpAlias, CancellationToken cancellationToken = default)
    {
        return await (from alias in _context.PortalAliases.AsNoTracking()
                      join portal in _context.Portals.AsNoTracking()
                          on alias.PortalID equals portal.PortalID
                      where alias.HTTPAlias == httpAlias
                      select portal)
            .FirstOrDefaultAsync(cancellationToken);
    }

    // MIGRATION: PortalController.CreatePortal/AddPortalInfo -> DataProvider.AddPortalInfo stored proc
    // (returned the new PortalID). EF Core tracks the insert and populates the generated key on save.
    public async Task<Portal> AddAsync(Portal entity, CancellationToken cancellationToken = default)
    {
        _context.Portals.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    // MIGRATION: PortalController.UpdatePortalInfo -> DataProvider.UpdatePortalInfo stored proc.
    public async Task UpdateAsync(Portal entity, CancellationToken cancellationToken = default)
    {
        _context.Portals.Update(entity);
        await _context.SaveChangesAsync(cancellationToken);
    }

    // MIGRATION: PortalController.DeletePortalInfo -> DataProvider.DeletePortalInfo stored proc. The
    // entity is fetched WITHOUT AsNoTracking (tracking is required so EF can mark it Deleted), then removed.
    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Portals
            .FirstOrDefaultAsync(p => p.PortalID == id, cancellationToken);
        if (entity is not null)
        {
            _context.Portals.Remove(entity);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
