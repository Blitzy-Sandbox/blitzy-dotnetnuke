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

    public async Task<IEnumerable<Role>> GetByPortalAsync(int portalId, CancellationToken cancellationToken = default)
    {
        // PERFORMANCE: returns all roles defined for a portal (legacy GetPortalRoles). Roles are an
        // administrator-managed, bounded set (typically well under a hundred per portal), so this is
        // intentionally not paged, matching the legacy all-rows GetPortalRoles contract. PortalID IS a
        // physical dbo.Roles column, so no join is required. See MIGRATION_NOTES.md §4.6 (bounded list reads).
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
        // INTEGRATION/MIGRATION: the IRoleRepository contract (CP2) defines this as the CREATE-only operation —
        // the legacy AddUserRole "If objUserRole Is Nothing" branch [RoleController.vb:L300-307] ->
        // MembershipProvider.AddUserToRole. The create-vs-update decision is made by the CALLER: the admin
        // RoleService.AddUserRoleAsync loads any existing assignment and routes updates to UpdateUserRoleAsync,
        // while RoleService.AutoAssignUsers and UserService auto-assignment insert brand-new memberships. This
        // method therefore performs a DIRECT insert. (An earlier worker revision implemented an upsert here against
        // the pre-CP2 single-method contract; reconciled to the split create/update contract during integration so
        // "Add" no longer silently mutates an existing assignment's window — see UpdateUserRoleAsync.)
        await _context.UserRoles.AddAsync(userRole, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return userRole;
    }

    public async Task<UserRole> UpdateUserRoleAsync(UserRole userRole, CancellationToken cancellationToken = default)
    {
        // INTEGRATION/MIGRATION: the IRoleRepository contract (CP2) defines this as the UPDATE-only operation —
        // the legacy AddUserRole "Else" branch [RoleController.vb:L308-313] -> MembershipProvider.UpdateUserRole:
        // update an EXISTING assignment's effective/expiry window (and trial/subscription flags). The caller
        // (RoleService.AddUserRoleAsync) loads the row via GetUserRolesAsync (AsNoTracking) and mutates it, so the
        // incoming entity may be DETACHED; re-load the tracked row by its primary key (falling back to the
        // UserID+RoleID natural key) and copy the mutable membership fields onto it before saving.
        var existing = await _context.UserRoles
            .FirstOrDefaultAsync(ur => ur.UserRoleID == userRole.UserRoleID, cancellationToken);

        existing ??= await _context.UserRoles
            .FirstOrDefaultAsync(
                ur => ur.UserID == userRole.UserID && ur.RoleID == userRole.RoleID,
                cancellationToken);

        if (existing is null)
        {
            // Defensive no-op: the caller only routes here when an assignment was found, so there is nothing to
            // update. Never inserts — creates go through AddUserRoleAsync (preserves the create/update split).
            return userRole;
        }

        existing.EffectiveDate = userRole.EffectiveDate;
        existing.ExpiryDate = userRole.ExpiryDate;
        existing.IsTrialUsed = userRole.IsTrialUsed;
        // MIGRATION: Subscribed is a CLR-only property (used by the UserRole DTO/AutoMapper surface). It is
        // EF-Ignore()'d in UserConfiguration because dbo.UserRoles has only 6 physical columns and NO
        // Subscribed column (the CP3 schema-fidelity correction — see MIGRATION_NOTES.md §4.2 / D-019). This
        // in-memory copy keeps the returned entity's flag consistent with the caller's intent but is NEVER
        // persisted to dbo.UserRoles (SaveChangesAsync emits no Subscribed column write).
        existing.Subscribed = userRole.Subscribed;
        await _context.SaveChangesAsync(cancellationToken);
        return existing;
    }

    public async Task<IEnumerable<UserRole>> GetUserRolesAsync(int userId, CancellationToken cancellationToken = default)
    {
        // Include the Role navigation so callers receive the role detail alongside the membership
        // window (EffectiveDate/ExpiryDate).
        // PERFORMANCE: returns all role memberships for a SINGLE user (legacy GetUserRoles). A user belongs
        // to a bounded number of roles, so this is intentionally not paged, matching the legacy all-rows
        // GetUserRoles contract. See MIGRATION_NOTES.md §4.6 (bounded list reads).
        return await _context.UserRoles
            .AsNoTracking()
            .Include(ur => ur.Role)
            .Where(ur => ur.UserID == userId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<User>> GetUsersInRoleAsync(int roleId, CancellationToken cancellationToken = default)
    {
        // PERFORMANCE (CARRY-FORWARD): returns every user assigned to a role (legacy GetUserRolesByRoleName /
        // GetUsersInRole). Unlike the other role reads this is NOT inherently bounded — a broad role such as
        // "Registered Users" can hold the entire portal membership (tens of thousands of rows). The current
        // CP3 contract preserves the legacy all-rows behaviour for parity; the IRoleRepository contract has no
        // paging parameter to honour here. Adding a paged overload (and batching RoleService.AutoAssignUsers,
        // which enumerates all portal users) is a documented post-CP3 carry-forward — see MIGRATION_NOTES.md
        // §4.6 (bounded list reads) and the review "Areas of Concern" note on bulk auto-assignment.
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

    public async Task<IEnumerable<RoleGroup>> GetRoleGroupsAsync(int portalId, CancellationToken cancellationToken = default)
    {
        // PERFORMANCE: returns all role groups for a portal (legacy GetRoleGroups). Role groups are an
        // administrator-managed, bounded set (a handful per portal), so this is intentionally not paged,
        // matching the legacy all-rows GetRoleGroups contract. See MIGRATION_NOTES.md §4.6 (bounded list reads).
        return await _context.RoleGroups
            .AsNoTracking()
            .Where(g => g.PortalID == portalId)
            .OrderBy(g => g.RoleGroupID)
            .ToListAsync(cancellationToken);
    }
}
