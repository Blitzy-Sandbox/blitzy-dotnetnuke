using DnnMigration.Domain.Common;
using DnnMigration.Domain.Entities;
using DnnMigration.Domain.Interfaces;
using DnnMigration.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DnnMigration.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IRoleRepository"/> backed by <see cref="DnnDbContext"/>.
/// </summary>
// MIGRATION: Re-expresses the data-access portion of the legacy VB.NET
// DotNetNuke.Security.Roles.RoleController (Library/Components/Security/Roles/RoleController.vb) as EF Core
// LINQ. The legacy provider.GetRoles(...) membership-provider indirection (with CBO hydration) is dropped
// in favour of DnnDbContext DbSet<Role> queries. Data access only.
public class RoleRepository : IRoleRepository
{
    private readonly DnnDbContext _context;

    public RoleRepository(DnnDbContext context)
    {
        _context = context;
    }

    // MIGRATION: RoleController.GetRole / provider.GetRole(roleId) single lookup.
    public async Task<Role?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.Roles
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.RoleID == id, cancellationToken);
    }

    // MIGRATION: aggregate of the legacy per-portal role readers; unfiltered AsNoTracking projection to
    // satisfy the generic IRepository<Role> contract.
    public async Task<IEnumerable<Role>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Roles
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    // MIGRATION: RoleController.GetPortalRoles(PortalId) [RoleController.vb L146] -> provider.GetRoles(PortalId)
    // returning an ArrayList of RoleInfo; converted to an async materialized collection.
    public async Task<IEnumerable<Role>> GetByPortalAsync(int portalId, CancellationToken cancellationToken = default)
    {
        return await _context.Roles
            .AsNoTracking()
            .Where(r => r.PortalID == portalId)
            .ToListAsync(cancellationToken);
    }

    // MIGRATION (QA finding — R6 Issue 1): bounded page of GetAllAsync. One COUNT over the full [Roles] set
    // plus one windowed SELECT ordered by the RoleID primary key. AsNoTracking (read path).
    public async Task<PagedResult<Role>> GetPagedAsync(int skip, int take, CancellationToken cancellationToken = default)
    {
        var baseQuery = _context.Roles.AsNoTracking();
        var total = await baseQuery.CountAsync(cancellationToken);
        var items = await baseQuery
            .OrderBy(r => r.RoleID)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
        return new PagedResult<Role>(items, total);
    }

    // MIGRATION (QA finding — R6 Issue 1): bounded page of GetByPortalAsync. The SAME PortalID filter is
    // applied to the base query so COUNT and the page share one predicate; only the Skip/Take window
    // (ordered by RoleID) is materialized. AsNoTracking (read path).
    public async Task<PagedResult<Role>> GetByPortalPagedAsync(int portalId, int skip, int take, CancellationToken cancellationToken = default)
    {
        var baseQuery = _context.Roles
            .AsNoTracking()
            .Where(r => r.PortalID == portalId);
        var total = await baseQuery.CountAsync(cancellationToken);
        var items = await baseQuery
            .OrderBy(r => r.RoleID)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
        return new PagedResult<Role>(items, total);
    }

    // MIGRATION: RoleController.AddRole / provider.CreateRole -> EF Core insert.
    public async Task<Role> AddAsync(Role entity, CancellationToken cancellationToken = default)
    {
        _context.Roles.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    // MIGRATION: RoleController.UpdateRole / provider.UpdateRole -> EF Core update.
    public async Task UpdateAsync(Role entity, CancellationToken cancellationToken = default)
    {
        _context.Roles.Update(entity);
        await _context.SaveChangesAsync(cancellationToken);
    }

    // MIGRATION: RoleController.DeleteRole / provider.DeleteRole -> EF Core delete. Tracked fetch (no
    // AsNoTracking) so EF can mark the entity Deleted, then remove.
    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Roles
            .FirstOrDefaultAsync(r => r.RoleID == id, cancellationToken);
        if (entity is not null)
        {
            _context.Roles.Remove(entity);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
