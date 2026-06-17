using DnnMigration.Domain.Entities;
using DnnMigration.Domain.Interfaces;
using DnnMigration.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DnnMigration.Infrastructure.Repositories;

/// <summary>
/// EF Core 8 LINQ implementation of <see cref="IPortalRepository"/>.
/// Replaces the legacy <c>PortalController.vb</c> / <c>PortalAliasController.vb</c> data methods and the
/// <c>SqlDataProvider.vb</c> Portal stored-procedure calls (GetPortal/GetPortals/GetPortalByAlias/GetPortalsByName).
/// CBO reflection hydration is eliminated; EF Core materializes entities directly.
/// </summary>
public class PortalRepository : IPortalRepository
{
    private readonly DnnDbContext _context;

    public PortalRepository(DnnDbContext context)
    {
        _context = context;
    }

    public async Task<Portal?> GetByIdAsync(int portalId, CancellationToken cancellationToken = default)
    {
        return await _context.Portals
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PortalID == portalId, cancellationToken);
    }

    public async Task<IEnumerable<Portal>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        // PERFORMANCE: returns every portal (legacy GetPortals returned an ArrayList of all portals with no
        // paging). A DNN installation's portal set is an inherently bounded administrative collection — each
        // portal is a whole site/tenant, so an install holds only a handful — therefore this all-rows read is
        // intentionally not paged, preserving the legacy all-rows contract. It backs the admin portal-list
        // endpoint (GET /api/v1/portals) and the last-portal delete guard (PortalService.DeleteAsync). A paged
        // contract IS available for name-filtered UI/API lists via GetByNameAsync (which preserves the legacy
        // pageIndex == -1 "return all rows" sentinel). See MIGRATION_NOTES.md §4.6 (bounded list reads).
        return await _context.Portals
            .AsNoTracking()
            .OrderBy(p => p.PortalID)
            .ToListAsync(cancellationToken);
    }

    public async Task<(IEnumerable<Portal> Items, int TotalCount)> GetByNameAsync(
        string nameToMatch,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Portals.AsNoTracking();

        // Case-insensitive partial match on PortalName. Guard the nullable string to satisfy CS8602
        // (the project treats nullable warnings as errors except CS8618).
        if (!string.IsNullOrWhiteSpace(nameToMatch))
        {
            var normalized = nameToMatch.ToLower();
            query = query.Where(p => p.PortalName != null && p.PortalName.ToLower().Contains(normalized));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        // MIGRATION: legacy GetPortalsByName treated pageIndex == -1 as a "return all rows" sentinel
        // (it set pageIndex = 0 and pageSize = Integer.MaxValue). That semantic is preserved here.
        if (pageIndex == -1)
        {
            pageIndex = 0;
            pageSize = int.MaxValue;
        }

        // Defensive bounds (second line of defence behind the API-boundary PaginationGuard): a negative
        // index would produce a negative Skip and a non-positive size a zero/negative Take, both of which EF
        // rejects at runtime. External callers are already validated to a 400 by PaginationGuard; this guard
        // protects any internal caller that bypasses the controller.
        if (pageIndex < 0)
        {
            pageIndex = 0;
        }
        if (pageSize < 1)
        {
            pageSize = 1;
        }

        var items = await query
            .OrderBy(p => p.PortalName)
            .Skip(pageIndex * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<Portal?> GetByAliasAsync(string httpAlias, CancellationToken cancellationToken = default)
    {
        // PortalAlias has only a scalar PortalID foreign key (no navigation property to Portal),
        // so the alias->portal resolution is performed as an explicit join across the two DbSets.
        // MIGRATION: legacy DNN stored HTTPAlias lower-cased; match case-insensitively for parity
        // (the EF InMemory provider used by integration tests compares strings ordinally/case-sensitively).
        var normalized = httpAlias.ToLower();

        return await (from alias in _context.PortalAliases.AsNoTracking()
                      join portal in _context.Portals.AsNoTracking()
                          on alias.PortalID equals portal.PortalID
                      where alias.HTTPAlias != null && alias.HTTPAlias.ToLower() == normalized
                      select portal)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Portal> AddAsync(Portal portal, CancellationToken cancellationToken = default)
    {
        await _context.Portals.AddAsync(portal, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return portal;
    }

    public async Task UpdateAsync(Portal portal, CancellationToken cancellationToken = default)
    {
        _context.Portals.Update(portal);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int portalId, CancellationToken cancellationToken = default)
    {
        var portal = await _context.Portals
            .FirstOrDefaultAsync(p => p.PortalID == portalId, cancellationToken);

        if (portal is null)
        {
            return;
        }

        // MIGRATION: Portal delete is a HARD delete with a transactional cascade, mirroring the legacy
        // DeletePortalInfo stored procedure (delete dependent Modules, then the Portal). The DB schema
        // FK-cascades the remaining dependents; under the EF InMemory provider (Gate 5 integration tests)
        // cascades are NOT enforced, so dependent Modules and PortalAliases are removed explicitly here.
        // The EF InMemory provider also does not support transactions, so the ambient transaction is only
        // opened for a relational provider.
        var transaction = _context.Database.IsRelational()
            ? await _context.Database.BeginTransactionAsync(cancellationToken)
            : null;

        try
        {
            var modules = await _context.Modules
                .Where(m => m.PortalID == portalId)
                .ToListAsync(cancellationToken);
            _context.Modules.RemoveRange(modules);

            var aliases = await _context.PortalAliases
                .Where(a => a.PortalID == portalId)
                .ToListAsync(cancellationToken);
            _context.PortalAliases.RemoveRange(aliases);

            _context.Portals.Remove(portal);

            await _context.SaveChangesAsync(cancellationToken);

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }
    }
}
