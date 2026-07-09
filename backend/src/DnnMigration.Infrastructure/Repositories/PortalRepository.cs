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

    // MIGRATION: PortalController.GetPortalsByName(nameToMatch, ...) letter/text search. Legacy used a
    // LIKE-based stored proc; re-expressed as a case-insensitive substring match over the portal's
    // textual identity fields (name/description/keywords). AsNoTracking (read path). The null guards keep
    // the SQL translation total in case a legacy row has a NULL in one of these NOT-NULL-by-convention
    // columns; ToLower()/Contains translate to a SQL LOWER(...) LIKE and are also honoured by the
    // EF Core InMemory provider used by the integration tests.
    public async Task<IEnumerable<Portal>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        var term = query.ToLower();
        return await _context.Portals
            .AsNoTracking()
            .Where(p =>
                (p.PortalName != null && p.PortalName.ToLower().Contains(term)) ||
                (p.Description != null && p.Description.ToLower().Contains(term)) ||
                (p.KeyWords != null && p.KeyWords.ToLower().Contains(term)))
            .ToListAsync(cancellationToken);
    }

    // MIGRATION: FormatPortalAliases(PortalID) (Portals.ascx.vb) read PortalAliasController aliases per
    // portal for the grid's "Portal Aliases" column. Because Portal has no PortalAlias navigation
    // collection (the EF model/snapshot is intentionally left unchanged), the read model is filled from
    // this single grouped lookup — one query for the whole list, avoiding an N+1. AsNoTracking (read path).
    public async Task<IReadOnlyDictionary<int, IReadOnlyList<string>>> GetAliasesAsync(CancellationToken cancellationToken = default)
    {
        var rows = await _context.PortalAliases
            .AsNoTracking()
            .Where(a => a.HTTPAlias != null && a.HTTPAlias != string.Empty)
            .Select(a => new { a.PortalID, a.HTTPAlias })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(a => a.PortalID)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<string>)g.Select(a => a.HTTPAlias).ToList());
    }

    // MIGRATION: the single-portal counterpart of GetAliasesAsync (used when projecting GET
    // /api/portals/{id}). Returns an empty list when the portal has no aliases. AsNoTracking (read path).
    public async Task<IReadOnlyList<string>> GetAliasesForPortalAsync(int portalId, CancellationToken cancellationToken = default)
    {
        return await _context.PortalAliases
            .AsNoTracking()
            .Where(a => a.PortalID == portalId && a.HTTPAlias != null && a.HTTPAlias != string.Empty)
            .Select(a => a.HTTPAlias)
            .ToListAsync(cancellationToken);
    }

    // MIGRATION: PortalController.CreatePortal/AddPortalInfo -> DataProvider.AddPortalInfo stored proc
    // (returned the new PortalID). EF Core tracks the insert and populates the generated key on save.
    public async Task<Portal> AddAsync(Portal entity, CancellationToken cancellationToken = default)
    {
        _context.Portals.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    // MIGRATION: PortalController.CreatePortal [L980] -> PortalAliasController.AddPortalAlias ->
    // DataProvider.AddPortalAlias stored proc (which returned the new PortalAliasID). EF Core tracks the
    // insert and populates the store-generated PortalAliasID (IDENTITY(1,1)) on save. This is the WRITE
    // counterpart of the read-only GetAliasesAsync/GetAliasesForPortalAsync lookups. PortalAlias.PortalID
    // is a plain (non-identifying) foreign key to Portals - the PortalAlias primary key is the surrogate
    // PortalAliasID - so writing the alias by its PortalID value is safe even for portal id 0 (contrast the
    // UserPortals junction, whose composite key IS its FK and therefore is written by value without an EF
    // relationship). The physical FK constraint to Portals still lives in the database schema unchanged.
    public async Task<PortalAlias> AddAliasAsync(PortalAlias alias, CancellationToken cancellationToken = default)
    {
        _context.PortalAliases.Add(alias);
        await _context.SaveChangesAsync(cancellationToken);
        return alias;
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
