using DnnMigration.Domain.Entities;
using DnnMigration.Domain.Interfaces;
using DnnMigration.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DnnMigration.Infrastructure.Repositories;

// MIGRATION: Replaces DotNetNuke.Security.Roles.RoleController data operations (RoleController.vb). Legacy persisted
// roles via the RoleProvider (provider.*); replaced by async EF Core LINQ over DnnDbContext. STAGE-ONLY: RoleService
// calls IUnitOfWork.SaveChangesAsync as the commit boundary.
public sealed class RoleRepository : IRoleRepository
{
    private readonly DnnDbContext _context;

    public RoleRepository(DnnDbContext context) => _context = context;

    /// <summary>
    /// Returns every role that belongs to the specified portal, preserving DNN multi-tenant isolation.
    /// </summary>
    /// <param name="portalId">The tenant (portal) discriminator to scope the query by.</param>
    /// <returns>The portal's roles as Domain <see cref="Role"/> entities.</returns>
    public async Task<IEnumerable<Role>> GetByPortalIdAsync(int portalId)
    {
        // MIGRATION: RoleController.GetPortalRoles L146 (provider.GetRoles(PortalId) returned an ArrayList) — tenant-scoped by PortalId.
        return await _context.Roles
            .Where(r => r.PortalId == portalId)
            .ToListAsync();
    }

    /// <summary>
    /// Returns a single page of the portal's roles plus the total matching count, for server-side paging.
    /// </summary>
    /// <param name="portalId">The tenant (portal) discriminator to scope the query by.</param>
    /// <param name="pageIndex">The zero-based page index.</param>
    /// <param name="pageSize">The number of roles per page.</param>
    /// <returns>A tuple of the page's <see cref="Role"/> items and the total count of the portal's roles.</returns>
    public async Task<(IEnumerable<Role> Items, int TotalCount)> GetByPortalPagedAsync(int portalId, int pageIndex, int pageSize)
    {
        // MIGRATION: CP1 review (performance, AAP 0.7.7) - paged, portal-scoped variant of GetPortalRoles that returns ONLY the
        // requested page plus the total count, replacing fetch-all + in-memory paging. Tenant-scoped by PortalId (AAP 0.7.1).
        // PageIndex is ZERO-BASED; a deterministic OrderBy(RoleId) is required so Skip/Take produce a stable page ordering.
        var query = _context.Roles.Where(r => r.PortalId == portalId);
        var total = await query.CountAsync();
        var items = await query
            .OrderBy(r => r.RoleId)
            .Skip(pageIndex * pageSize)
            .Take(pageSize)
            .ToListAsync();
        return (items, total);
    }

    /// <summary>
    /// Fetches a single role by its identifier, or <c>null</c> when no matching role exists.
    /// </summary>
    /// <param name="roleId">The identifier of the role to fetch.</param>
    /// <returns>The matching <see cref="Role"/>, or <c>null</c> if not found.</returns>
    public async Task<Role?> GetByIdAsync(int portalId, int roleId)
    {
        // MIGRATION: RoleController.GetRole L163 (provider.GetRole(PortalID, RoleID)). CP1 review (IRoleRepository): the lookup is
        // PORTAL-SCOPED (PortalId + RoleId) to preserve multi-tenant isolation (AAP 0.7.1) so a portal can only read its own roles;
        // this restores the legacy PortalID argument. Returns null when no role matches.
        return await _context.Roles
            .FirstOrDefaultAsync(r => r.PortalId == portalId && r.RoleId == roleId);
    }

    /// <summary>
    /// Stages a new role for insertion. STAGE-ONLY: the commit is deferred to IUnitOfWork.SaveChangesAsync.
    /// </summary>
    /// <param name="role">The role to add.</param>
    /// <returns>The tracked <see cref="Role"/> instance (its database-generated RoleId is populated after save).</returns>
    public async Task<Role> AddAsync(Role role)
    {
        // MIGRATION: RoleController.AddRole L100 (provider.CreateRole). STAGE-ONLY — no SaveChanges here; RoleService commits via IUnitOfWork.
        await _context.Roles.AddAsync(role);
        return role;
    }

    /// <summary>
    /// Marks an existing role as modified. STAGE-ONLY: the commit is deferred to IUnitOfWork.SaveChangesAsync.
    /// </summary>
    /// <param name="role">The role carrying the updated values.</param>
    /// <returns>A completed task; the update is synchronous on the change tracker.</returns>
    public Task UpdateAsync(Role role)
    {
        // MIGRATION: RoleController.UpdateRole L254 (provider.UpdateRole). STAGE-ONLY — change tracker marks the entity Modified; RoleService commits via IUnitOfWork.
        _context.Roles.Update(role);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Stages the deletion of a role by identifier when it exists. STAGE-ONLY: the commit is deferred to IUnitOfWork.SaveChangesAsync.
    /// </summary>
    /// <param name="roleId">The identifier of the role to delete.</param>
    public async Task DeleteAsync(int portalId, int roleId)
    {
        // MIGRATION: RoleController.DeleteRole L125 (provider.DeleteRole). CP1 review (IRoleRepository): the delete is PORTAL-SCOPED
        // (PortalId + RoleId) to preserve multi-tenant isolation (AAP 0.7.1) so a portal can only delete its own roles; this restores
        // the legacy PortalId argument. STAGE-ONLY - the Remove is staged on the change tracker; RoleService commits via IUnitOfWork.
        var role = await _context.Roles
            .FirstOrDefaultAsync(r => r.PortalId == portalId && r.RoleId == roleId);

        if (role is not null)
        {
            _context.Roles.Remove(role);
        }
    }

    /// <summary>
    /// Returns the user-role assignments for a user, eager-loading each assignment's <see cref="Role"/>.
    /// </summary>
    /// <param name="userId">The identifier of the user whose role memberships are requested.</param>
    /// <returns>The <see cref="UserRole"/> join entities that model the user's role memberships.</returns>
    public async Task<IEnumerable<UserRole>> GetUserRolesAsync(int portalId, int userId)
    {
        // MIGRATION: RoleController.GetUserRoles L392 (provider.GetUserRoles(PortalId, UserId, True) returned an ArrayList of UserRoleInfo).
        // CP1 review (IRoleRepository): PORTAL-SCOPED to preserve multi-tenant isolation (AAP 0.7.1) - this restores the legacy PortalId
        // argument. The UserRole join entity carries NO PortalId column, so the portal filter is applied through each assignment's Role
        // (Role.PortalId): the result is the user's memberships in roles that belong to the requested portal. Eager-load Role for the
        // user-role assignment view.
        // MIGRATION (CP-final review): the user-role WRITE methods below (GetUserRoleAsync/AddUserRoleAsync/
        // UpdateUserRoleAsync/RemoveUserRoleAsync) now provide a direct assignment surface, replacing the legacy
        // provider.GetUserRole/AddUserToRole/UpdateUserRole/RemoveUserFromRole calls. They write directly to the
        // [UserRoles] DbSet (the join row carries the scalar UserID/RoleID FKs); Role has no inverse UserRoles
        // collection, so writes do NOT go through it.
        return await _context.UserRoles
            .Include(ur => ur.Role)
            .Where(ur => ur.UserId == userId && ur.Role != null && ur.Role.PortalId == portalId)
            .ToListAsync();
    }

    /// <summary>
    /// Fetches the single user-role assignment for (portal, user, role), or <c>null</c> when the user does not hold
    /// that role in the portal. The assignment's <see cref="Role"/> is eager-loaded.
    /// </summary>
    /// <param name="portalId">The tenant (portal) discriminator the assignment's role must belong to.</param>
    /// <param name="userId">The identifier of the user.</param>
    /// <param name="roleId">The identifier of the role.</param>
    /// <returns>The matching <see cref="UserRole"/> assignment, or <c>null</c> if none exists in the portal.</returns>
    public async Task<UserRole?> GetUserRoleAsync(int portalId, int userId, int roleId)
    {
        // MIGRATION: RoleController.GetUserRole L356 (provider.GetUserRole(PortalID, UserId, RoleId)). The UserRole join
        // row has NO PortalId column, so the portal filter is applied through the assignment's Role (Role.PortalId),
        // exactly as the multi-row GetUserRolesAsync does (multi-tenant isolation, AAP 0.7.1). Eager-load the Role so the
        // service can read Role.ServiceFee for the legacy Cancel branch without a second round-trip.
        return await _context.UserRoles
            .Include(ur => ur.Role)
            .FirstOrDefaultAsync(ur => ur.UserId == userId
                                       && ur.RoleId == roleId
                                       && ur.Role != null
                                       && ur.Role.PortalId == portalId);
    }

    /// <summary>
    /// Stages a new user-role assignment for insertion. STAGE-ONLY: the commit is deferred to IUnitOfWork.SaveChangesAsync.
    /// </summary>
    /// <param name="userRole">The assignment to add (UserId/RoleId scalar FKs set).</param>
    public async Task AddUserRoleAsync(UserRole userRole)
    {
        // MIGRATION: provider.AddUserToRole (RoleController.AddUserRole L277/L295). STAGE-ONLY - RoleService commits via IUnitOfWork.
        await _context.UserRoles.AddAsync(userRole);
    }

    /// <summary>
    /// Marks an existing user-role assignment as modified. STAGE-ONLY: the commit is deferred to IUnitOfWork.SaveChangesAsync.
    /// </summary>
    /// <param name="userRole">The assignment carrying the updated EffectiveDate/ExpiryDate.</param>
    /// <returns>A completed task; the update is synchronous on the change tracker.</returns>
    public Task UpdateUserRoleAsync(UserRole userRole)
    {
        // MIGRATION: provider.UpdateUserRole. STAGE-ONLY - the change tracker marks the row Modified; RoleService commits via IUnitOfWork.
        _context.UserRoles.Update(userRole);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Stages the removal of a user-role assignment. STAGE-ONLY: the commit is deferred to IUnitOfWork.SaveChangesAsync.
    /// </summary>
    /// <param name="userRole">The assignment to remove.</param>
    /// <returns>A completed task; the removal is staged on the change tracker.</returns>
    public Task RemoveUserRoleAsync(UserRole userRole)
    {
        // MIGRATION: provider.RemoveUserFromRole (RoleController.DeleteUserRole L330). STAGE-ONLY - RoleService commits via IUnitOfWork.
        _context.UserRoles.Remove(userRole);
        return Task.CompletedTask;
    }
}
