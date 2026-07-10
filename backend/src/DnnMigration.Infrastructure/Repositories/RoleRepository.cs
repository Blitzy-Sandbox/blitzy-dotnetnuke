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

    // MIGRATION (QA finding - R10 Issue 13): bounded, searchable page of roles across all portals. The SAME
    // case-insensitive RoleName/Description substring predicate is applied to both the COUNT and the
    // Skip/Take window (ordered by RoleID) so paging over the search results is consistent. ToLower().Contains
    // translates to SQL LOWER()+LIKE on SqlServer and is honoured ordinally by the InMemory provider, matching
    // the Portal/Module/User SearchPagedAsync repositories. AsNoTracking (read path).
    public async Task<PagedResult<Role>> SearchPagedAsync(string query, int skip, int take, CancellationToken cancellationToken = default)
    {
        var term = (query ?? string.Empty).ToLower();
        var baseQuery = _context.Roles
            .AsNoTracking()
            .Where(r =>
                r.RoleName.ToLower().Contains(term) ||
                (r.Description != null && r.Description.ToLower().Contains(term)));
        var total = await baseQuery.CountAsync(cancellationToken);
        var items = await baseQuery
            .OrderBy(r => r.RoleID)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
        return new PagedResult<Role>(items, total);
    }

    // MIGRATION (QA finding - R10 Issue 13): portal-scoped counterpart of SearchPagedAsync. The PortalID
    // filter AND the case-insensitive RoleName/Description substring predicate are both applied to the base
    // query, so COUNT and the page share one predicate. AsNoTracking (read path).
    public async Task<PagedResult<Role>> SearchByPortalPagedAsync(int portalId, string query, int skip, int take, CancellationToken cancellationToken = default)
    {
        var term = (query ?? string.Empty).ToLower();
        var baseQuery = _context.Roles
            .AsNoTracking()
            .Where(r =>
                r.PortalID == portalId &&
                (r.RoleName.ToLower().Contains(term) ||
                 (r.Description != null && r.Description.ToLower().Contains(term))));
        var total = await baseQuery.CountAsync(cancellationToken);
        var items = await baseQuery
            .OrderBy(r => r.RoleID)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
        return new PagedResult<Role>(items, total);
    }

    // MIGRATION (QA finding - R10 Issue 12): case-insensitive, portal-scoped role-name lookup used to
    // enforce per-portal uniqueness. Mirrors the legacy LOWER(@RoleName) = LoweredRoleName comparison the
    // aspnet_Roles provider used under its UNIQUE (ApplicationId, LoweredRoleName) index
    // [InstallRoles.sql L86]. ToLower() on both sides translates to SQL LOWER() (SqlServer) and is honoured
    // ordinally by the InMemory provider, so the comparison is deterministic under both. AsNoTracking (read
    // path): the result is only inspected for existence, never mutated.
    public async Task<Role?> GetByNameAsync(int portalId, string roleName, CancellationToken cancellationToken = default)
    {
        var lowered = (roleName ?? string.Empty).ToLower();
        return await _context.Roles
            .AsNoTracking()
            .FirstOrDefaultAsync(
                r => r.PortalID == portalId && r.RoleName.ToLower() == lowered,
                cancellationToken);
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
