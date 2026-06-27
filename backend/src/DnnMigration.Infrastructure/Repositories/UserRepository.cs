using DnnMigration.Domain.Entities;
using DnnMigration.Domain.Interfaces;
using DnnMigration.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DnnMigration.Infrastructure.Repositories;

// MIGRATION: Replaces DotNetNuke.Entities.Users.UserController data operations (Library/Components/Users/UserController.vb).
// Legacy DNN persisted users through the ASP.NET MembershipProvider singleton (memberProvider = MembershipProvider.Instance(),
// UserController.vb L60) — NOT through DataProvider; the reflection-based DataProvider.vb singleton
// (Library/Components/Providers/Data/DataProvider.vb L26-L80) is cited here only as the general provider-pattern reference.
// The legacy memberProvider.* + IDataReader/ArrayList pipeline is replaced by async EF Core LINQ over DnnDbContext.
// This class is PERSISTENCE ONLY: business rules (validation, display-name composition, role orchestration, credential
// handling) live in the Application-layer UserService.cs and the Infrastructure Identity layer (AAP 0.7.3, 0.7.6), not here.
// STAGE-ONLY: every mutation below merely stages a change on the EF Core change tracker; the Application UserService calls
// IUnitOfWork.SaveChangesAsync as the single commit boundary (there is intentionally no SaveChanges/SaveChangesAsync in this
// repository). Users remain portal-scoped to preserve multi-tenant isolation (AAP 0.7.1).
public sealed class UserRepository : IUserRepository
{
    private readonly DnnDbContext _context;

    public UserRepository(DnnDbContext context) => _context = context;

    // MIGRATION: UserController.GetUsers(portalId) L685 (legacy returned an ArrayList of UserInfo, ultimately sourced from
    // memberProvider.GetUsers). Tenant-scoped by PortalId for multi-tenant isolation (AAP 0.7.1).
    public async Task<IEnumerable<User>> GetByPortalIdAsync(int portalId)
    {
        return await _context.Users
            .Where(u => u.PortalId == portalId)
            .ToListAsync();
    }

    // MIGRATION: CP1 review (performance #22, AAP 0.7.7) - paged, portal-scoped user listing that returns ONLY the requested page
    // plus the total matching count, replacing fetch-all + in-memory paging on the larger Users table. Tenant-scoped by PortalId
    // (AAP 0.7.1). PageIndex is ZERO-BASED; a deterministic OrderBy(UserId) is required so Skip/Take produce a stable page. The
    // UserRoles graph is intentionally NOT eager-loaded here (mirrors GetByPortalIdAsync) to avoid over-fetching on list views.
    public async Task<(IEnumerable<User> Items, int TotalCount)> GetByPortalPagedAsync(int portalId, int pageIndex, int pageSize)
    {
        var query = _context.Users.Where(u => u.PortalId == portalId);
        var total = await query.CountAsync();
        var items = await query
            .OrderBy(u => u.UserId)
            .Skip(pageIndex * pageSize)
            .Take(pageSize)
            .ToListAsync();
        return (items, total);
    }

    // MIGRATION: UserController.GetUser(portalId, userId) L1245 -> memberProvider.GetUser(portalId, userId, False). CP1 review
    // (IUserRepository): the read is PORTAL-SCOPED (PortalId + UserId) so one portal can never read another portal's user by id,
    // preserving DNN multi-tenant isolation (AAP 0.7.1) - this restores the legacy portalId argument that an earlier draft had
    // collapsed away. UserRoles are eager-loaded so the Application service / AutoMapper can project the user's role assignments.
    // Returns null when no user matches.
    public async Task<User?> GetByIdAsync(int portalId, int userId)
    {
        return await _context.Users
            .Include(u => u.UserRoles)
            .FirstOrDefaultAsync(u => u.PortalId == portalId && u.UserId == userId);
    }

    // MIGRATION: UserController.GetUserByUsername(PortalID, Username) L1321 -> GetUserByName(portalId, username, False).
    // Tenant + username scoped because usernames are unique only within a portal. Returns null when no match. (Consumed by the
    // Application AuthService for login lookup; credential VERIFICATION still happens in the Infrastructure Identity layer.)
    public async Task<User?> GetByUsernameAsync(int portalId, string username)
    {
        return await _context.Users
            .FirstOrDefaultAsync(u => u.PortalId == portalId && u.Username == username);
    }

    // MIGRATION: UserController.AddUser(objUser) L1275 (legacy delegated to CreateUser then returned objUser.UserID). STAGE-ONLY:
    // stages the insert on the change tracker; the database-generated UserId is populated after the Application service commits
    // via IUnitOfWork.SaveChangesAsync. Returns the tracked entity so the caller can read that generated key.
    public async Task<User> AddAsync(User user)
    {
        await _context.Users.AddAsync(user);

        // MIGRATION (QA-FINAL Issue #1/#2, CRITICAL): persist the user<->portal membership into the physical
        // [UserPortals] table. The User write model maps to [Users], which has NO PortalId column; vw_Users surfaces
        // PortalId/Authorised via the [UserPortals] join on reads, so the membership row must be created alongside the
        // user. Setting the User navigation lets EF fix up UserPortal.UserId from the store-generated User.UserId within
        // the single SaveChanges commit boundary (IUnitOfWork.SaveChangesAsync, invoked by UserService). Authorised
        // mirrors the user's IsApproved at creation; CreatedDate is the membership timestamp (legacy DEFAULT getdate()).
        var membership = new UserPortal
        {
            User = user,
            PortalId = user.PortalId,
            Authorised = user.IsApproved,
            CreatedDate = DateTime.UtcNow,
        };
        await _context.Set<UserPortal>().AddAsync(membership);

        return user;
    }

    // MIGRATION: UserController.UpdateUser(objUser) L1361 (legacy delegated to UpdateUser(objUser.PortalID, objUser)). STAGE-ONLY:
    // marks the entity (and its tracked UserRoles graph) Modified; the commit happens in IUnitOfWork.SaveChangesAsync. This is
    // also the path the Application RoleService/UserService use to write role assignments through the User.UserRoles navigation.
    // No asynchronous work is required, so a completed task is returned.
    public Task UpdateAsync(User user)
    {
        _context.Users.Update(user);
        return Task.CompletedTask;
    }

    // MIGRATION: UserController.DeleteUser(PortalId, UserId) L1292 (legacy loaded the user then delegated to the shared DeleteUser
    // overload). CP1 review (IUserRepository): the delete is PORTAL-SCOPED (PortalId + UserId) to preserve multi-tenant isolation
    // (AAP 0.7.1) so a portal can only delete its own users - this restores the legacy PortalId argument. STAGE-ONLY: stages the
    // delete on the change tracker and is a no-op when no matching user exists; the commit happens in IUnitOfWork.SaveChangesAsync.
    public async Task DeleteAsync(int portalId, int userId)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.PortalId == portalId && u.UserId == userId);
        if (user is not null)
        {
            // MIGRATION (QA-FINAL Issue #1/#2, CRITICAL): remove the user's [UserPortals] membership row(s) before
            // deleting the [Users] row so no [UserPortals].UserId FK is left orphaned on a real SQL Server. The User
            // entity is modeled single-portal-scoped in this phase, so all membership rows for the user are removed
            // together with the user. STAGE-ONLY: the DELETEs are flushed by IUnitOfWork.SaveChangesAsync. Documented
            // in MIGRATION_NOTES.md.
            var memberships = await _context.Set<UserPortal>()
                .Where(up => up.UserId == userId)
                .ToListAsync();
            if (memberships.Count > 0)
            {
                _context.Set<UserPortal>().RemoveRange(memberships);
            }

            _context.Users.Remove(user);
        }
    }

    // MIGRATION (CP-final review - profile workflow parity): ProfileController.GetPropertyDefinitionsByPortal(portalId,
    // True) returned the portal's non-deleted property definitions. Portal-scoped (multi-tenant isolation, AAP 0.7.1)
    // and ordered by ViewOrder to match the legacy collection ordering. AsNoTracking - definitions are reference data
    // read for resolution, never mutated by the profile workflow.
    public async Task<IReadOnlyList<ProfilePropertyDefinition>> GetProfileDefinitionsAsync(int portalId)
    {
        return await _context.ProfilePropertyDefinitions
            .AsNoTracking()
            .Where(d => d.PortalId == portalId && !d.Deleted)
            .OrderBy(d => d.ViewOrder)
            .ToListAsync();
    }

    // MIGRATION (CP-final review - profile workflow parity): the per-user stored VALUE rows (legacy GetUserProfile
    // hydrated each definition's PropertyValue from these). TRACKED (no AsNoTracking) so that when UserService mutates
    // an existing row in UpdateProfileAsync the change is staged on the change tracker and flushed by the single
    // IUnitOfWork.SaveChangesAsync commit boundary.
    public async Task<IReadOnlyList<UserProfileValue>> GetProfileValuesAsync(int userId)
    {
        return await _context.UserProfileValues
            .Where(v => v.UserId == userId)
            .ToListAsync();
    }

    // MIGRATION (CP-final review - profile workflow parity): stages a new [UserProfile] value row (legacy
    // UpdateUserProfile inserted a row when the property had no existing value). STAGE-ONLY: the insert is flushed by
    // IUnitOfWork.SaveChangesAsync in UserService; the database generates ProfileID.
    public async Task AddProfileValueAsync(UserProfileValue value)
    {
        await _context.UserProfileValues.AddAsync(value);
    }
}
