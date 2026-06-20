using DnnMigration.Domain.Entities;
using DnnMigration.Domain.Interfaces;
using DnnMigration.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DnnMigration.Infrastructure.Repositories;

/// <summary>
/// EF Core 8 LINQ implementation of <see cref="IRoleRepository"/>.
/// Replaces the legacy <c>RoleController.vb</c> data methods and <c>SqlDataProvider.vb</c> Role/UserRole/RoleGroup
/// stored-procedure calls. Covers role CRUD, user-role membership, and read-only role-group access.
/// </summary>
public class RoleRepository : IRoleRepository
{
    private readonly DnnDbContext _context;

    public RoleRepository(DnnDbContext context)
    {
        _context = context;
    }

    public async Task<Role?> GetByIdAsync(int roleId, CancellationToken cancellationToken = default)
    {
        return await _context.Roles
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.RoleID == roleId, cancellationToken);
    }

    /// <summary>
    /// Returns all roles defined for a portal, ordered by RoleID.
    /// </summary>
    // PERF/MIGRATION: Intentional all-rows read that reproduces the legacy
    // RoleController.GetPortalRoles / SqlDataProvider GetPortalRoles stored procedure, which returns
    // every role row for the given portal. The result set is naturally bounded by the foreign-key
    // PortalID filter -- a single portal has a small, administrator-curated number of roles (typically
    // a handful to low dozens), so no paging is applied (preserving legacy parity per the Minimal
    // Change Clause). The sole caller, RoleService.GetByPortalAsync, maps the bounded set directly to
    // a role-list DTO for the admin grid.
    public async Task<IEnumerable<Role>> GetByPortalAsync(int portalId, CancellationToken cancellationToken = default)
    {
        return await _context.Roles
            .AsNoTracking()
            .Where(r => r.PortalID == portalId)
            .OrderBy(r => r.RoleID)
            .ToListAsync(cancellationToken);
    }

    public async Task<Role> AddAsync(Role role, CancellationToken cancellationToken = default)
    {
        await _context.Roles.AddAsync(role, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return role;
    }

    public async Task UpdateAsync(Role role, CancellationToken cancellationToken = default)
    {
        _context.Roles.Update(role);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int roleId, CancellationToken cancellationToken = default)
    {
        var role = await _context.Roles
            .FirstOrDefaultAsync(r => r.RoleID == roleId, cancellationToken);

        if (role is null)
        {
            return;
        }

        // MIGRATION: Role delete is a HARD delete with an explicit dependent cascade, mirroring the legacy
        // DeleteRole stored procedure (which removed Folder/Module/Tab permissions for the role, with the
        // UserRoles FK cascading at the database). The EF InMemory provider (Gate 5 integration tests) does
        // NOT enforce FK cascades, so every dependent set - including UserRoles - is removed explicitly.
        // It also does not support transactions, so the ambient transaction is opened only for a relational provider.
        var transaction = _context.Database.IsRelational()
            ? await _context.Database.BeginTransactionAsync(cancellationToken)
            : null;

        try
        {
            var folderPermissions = await _context.FolderPermissions
                .Where(p => p.RoleID == roleId)
                .ToListAsync(cancellationToken);
            _context.FolderPermissions.RemoveRange(folderPermissions);

            var modulePermissions = await _context.ModulePermissions
                .Where(p => p.RoleID == roleId)
                .ToListAsync(cancellationToken);
            _context.ModulePermissions.RemoveRange(modulePermissions);

            var tabPermissions = await _context.TabPermissions
                .Where(p => p.RoleID == roleId)
                .ToListAsync(cancellationToken);
            _context.TabPermissions.RemoveRange(tabPermissions);

            var userRoles = await _context.UserRoles
                .Where(ur => ur.RoleID == roleId)
                .ToListAsync(cancellationToken);
            _context.UserRoles.RemoveRange(userRoles);

            _context.Roles.Remove(role);

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

    public async Task<UserRole> AddUserRoleAsync(UserRole userRole, CancellationToken cancellationToken = default)
    {
        // MIGRATION (M3/DEV-034): INSERT-ONLY. The legacy RoleController.UpdateUserRole took an add-vs-update
        // split (UserRoleId <> -1 -> provider.UpdateUserRole, else -> provider.AddUserRoleToPortal); this method
        // is the insert side only and does NOT upsert. An existing assignment is routed to UpdateUserRoleAsync
        // instead, removing the duplicate-join-row / undocumented-upsert risk the IRoleRepository contract calls
        // out. Every call site (the RoleService new-assignment branch, and RoleService/UserService
        // auto-assignment) supplies a brand-new assignment.
        await _context.UserRoles.AddAsync(userRole, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return userRole;
    }

    public async Task UpdateUserRoleAsync(UserRole userRole, CancellationToken cancellationToken = default)
    {
        // MIGRATION (M3/DEV-034): UPDATE-only counterpart to the insert-only AddUserRoleAsync, mirroring the
        // legacy RoleController.UpdateUserRole -> provider.UpdateUserRole path for an existing assignment.
        // Locates the tracked join row by its UserRoleID primary key and writes the membership window
        // (EffectiveDate/ExpiryDate) plus the trial-used flag; UserID/RoleID are immutable identity and are not
        // reassigned. A row that no longer exists is an idempotent no-op, consistent with RemoveUserRoleAsync.
        var existing = await _context.UserRoles
            .FirstOrDefaultAsync(ur => ur.UserRoleID == userRole.UserRoleID, cancellationToken);

        if (existing is null)
        {
            return;
        }

        existing.EffectiveDate = userRole.EffectiveDate;
        existing.ExpiryDate = userRole.ExpiryDate;
        existing.IsTrialUsed = userRole.IsTrialUsed;

        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Returns every role-membership row for a single user, with the Role navigation eager-loaded.
    /// </summary>
    // PERF/MIGRATION: Intentional all-rows read reproducing the legacy RoleController.GetUserRoles /
    // SqlDataProvider GetUserRoles stored procedure. The result set is naturally bounded by the
    // foreign-key UserID filter -- one user belongs to a small number of roles -- so no paging is
    // applied (legacy parity per the Minimal Change Clause). Callers (RoleService membership lookup
    // and the user-role projection) consume the bounded set directly.
    public async Task<IEnumerable<UserRole>> GetUserRolesAsync(int userId, CancellationToken cancellationToken = default)
    {
        // Include the Role navigation so callers receive the role detail alongside the membership
        // window (EffectiveDate/ExpiryDate).
        return await _context.UserRoles
            .AsNoTracking()
            .Include(ur => ur.Role)
            .Where(ur => ur.UserID == userId)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Returns every user that holds the specified role.
    /// </summary>
    // PERF/MIGRATION: Intentional all-rows read reproducing the legacy RoleController.GetUsersByRoleName /
    // SqlDataProvider GetRoleUsers stored procedure, which returns the full membership of a role. The
    // result set is bounded by the foreign-key RoleID filter and backs the role-membership admin grid;
    // legacy parity returns the complete membership (no paging) per the Minimal Change Clause. Callers
    // request a single role's membership at a time, keeping the surface bounded.
    public async Task<IEnumerable<User>> GetUsersInRoleAsync(int roleId, CancellationToken cancellationToken = default)
    {
        return await (from ur in _context.UserRoles.AsNoTracking()
                      join user in _context.Users.AsNoTracking()
                          on ur.UserID equals user.UserID
                      where ur.RoleID == roleId
                      select user)
            .ToListAsync(cancellationToken);
    }

    public async Task RemoveUserRoleAsync(int userId, int roleId, CancellationToken cancellationToken = default)
    {
        // The CanRemoveUserFromRole guard from the legacy controller is a service-layer rule and is NOT
        // enforced here; the repository simply removes the membership row when present.
        var userRole = await _context.UserRoles
            .FirstOrDefaultAsync(ur => ur.UserID == userId && ur.RoleID == roleId, cancellationToken);

        if (userRole is null)
        {
            return;
        }

        _context.UserRoles.Remove(userRole);
        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Returns all role groups defined for a portal, ordered by RoleGroupID.
    /// </summary>
    // PERF/MIGRATION: Intentional all-rows read reproducing the legacy RoleController.GetRoleGroups /
    // SqlDataProvider GetRoleGroups stored procedure. The result set is naturally bounded by the
    // foreign-key PortalID filter -- a portal defines very few role groups -- so no paging is applied
    // (legacy parity per the Minimal Change Clause).
    public async Task<IEnumerable<RoleGroup>> GetRoleGroupsAsync(int portalId, CancellationToken cancellationToken = default)
    {
        return await _context.RoleGroups
            .AsNoTracking()
            .Where(g => g.PortalID == portalId)
            .OrderBy(g => g.RoleGroupID)
            .ToListAsync(cancellationToken);
    }
}
