using DnnMigration.Domain.Entities;
using DnnMigration.Domain.Interfaces;
using DnnMigration.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DnnMigration.Infrastructure.Repositories;

/// <summary>
/// EF Core 8 LINQ implementation of <see cref="IUserRepository"/>.
/// Replaces the legacy <c>UserController.vb</c> data methods and <c>SqlDataProvider.vb</c> User
/// stored-procedure calls. Authentication and role-membership concerns are intentionally NOT handled
/// here (role membership lives in <c>RoleRepository</c>; authentication in the Identity layer / AuthService).
/// </summary>
public class UserRepository : IUserRepository
{
    private readonly DnnDbContext _context;

    public UserRepository(DnnDbContext context)
    {
        _context = context;
    }

    public async Task<User?> GetByIdAsync(int userId, CancellationToken cancellationToken = default)
    {
        return await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserID == userId, cancellationToken);
    }

    public async Task<User?> GetByUsernameAsync(int portalId, string username, CancellationToken cancellationToken = default)
    {
        // Case-insensitive match for parity with SQL Server's case-insensitive collation; the nullable
        // Username column is guarded to satisfy CS8602 (nullable warnings are treated as errors).
        var normalized = username.ToLower();

        // MIGRATION (schema fidelity, ADR-002): portal scoping is resolved by JOINING the physical
        // [UserPortals] membership table (composite key UserId+PortalId) rather than filtering User.PortalID,
        // which CP2 UserConfiguration Ignore()s because the [Users] table has no PortalID column. Filtering
        // the ignored member would not translate against the preserved SQL Server schema. The composite
        // [UserPortals] PK guarantees at most one membership row per (user, portal), so the join cannot
        // introduce duplicate users.
        var user = await (
            from u in _context.Users.AsNoTracking()
            join up in _context.UserPortals.AsNoTracking() on u.UserID equals up.UserID
            where up.PortalID == portalId
                  && u.Username != null && u.Username.ToLower() == normalized
            select u)
            .FirstOrDefaultAsync(cancellationToken);

        if (user is null)
        {
            return null;
        }

        // MIGRATION (Finding CP5 MAJOR — membership-password sourcing): the credential hash is NOT a physical
        // [Users] column, which is why CP2 UserConfiguration correctly Ignore()s User.Password. Legacy DotNetNuke
        // delegates authentication to the ASP.NET 2.0 Membership provider, whose password hash lives in the
        // physical [aspnet_Membership] table, reached from the lowered username via [aspnet_Users] — the canonical
        // bridge in Website/Providers/DataProviders/SqlDataProvider/InstallMembership.sql is
        // "LOWER(@UserName) = u.LoweredUserName AND u.UserId = m.UserId" (u = aspnet_Users, m = aspnet_Membership).
        // Source the hash with a second read against the mapped membership tables and assign it onto the
        // AsNoTracking() user instance — an IN-MEMORY only mutation (the instance is untracked, so this is never
        // persisted and changes no schema, ADR-002). AuthService.LoginAsync then BCrypt-verifies user.Password,
        // unblocking the valid-login path (Gate 5). When no membership row exists the hash is null and LoginAsync
        // short-circuits to 401, exactly as a missing/unknown credential should.
        var passwordHash = await (
            from au in _context.AspNetUsers.AsNoTracking()
            join m in _context.AspNetMemberships.AsNoTracking() on au.UserId equals m.UserId
            where au.LoweredUserName == normalized
            select m.Password)
            .FirstOrDefaultAsync(cancellationToken);

        user.Password = passwordHash;

        return user;
    }

    public async Task<User?> GetByEmailAsync(int portalId, string email, CancellationToken cancellationToken = default)
    {
        // MIGRATION: the legacy provider exposed e-mail search as a paged "find" (GetUsersByEmail).
        // This repository deliberately narrows it to an exact-match single lookup, which is what the
        // Application/Auth layers require; the broader search is out of scope.
        var normalized = email.ToLower();

        // MIGRATION (schema fidelity, ADR-002): portal scoping JOINS the physical [UserPortals] membership
        // table rather than filtering the Ignore()d User.PortalID (no [Users].PortalID column exists).
        return await (
            from u in _context.Users.AsNoTracking()
            join up in _context.UserPortals.AsNoTracking() on u.UserID equals up.UserID
            where up.PortalID == portalId
                  && u.Email != null && u.Email.ToLower() == normalized
            select u)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<(IEnumerable<User> Items, int TotalCount)> GetByPortalAsync(
        int portalId,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        // MIGRATION (schema fidelity, ADR-002): portal-scoped enumeration JOINS the physical [UserPortals]
        // membership table (composite key UserId+PortalId) instead of filtering the Ignore()d User.PortalID
        // ([Users] has no PortalID column). The composite [UserPortals] PK guarantees at most one membership
        // row per (user, portal), so the join introduces no duplicate users. Paging is preserved below
        // (including the legacy pageIndex == -1 "return all rows" sentinel).
        var query = from u in _context.Users.AsNoTracking()
                    join up in _context.UserPortals.AsNoTracking() on u.UserID equals up.UserID
                    where up.PortalID == portalId
                    select u;

        var totalCount = await query.CountAsync(cancellationToken);

        // MIGRATION: preserve the legacy pageIndex == -1 "return all rows" sentinel.
        if (pageIndex == -1)
        {
            pageIndex = 0;
            pageSize = int.MaxValue;
        }

        var items = await query
            .OrderBy(u => u.Username)
            .Skip(pageIndex * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<User> AddAsync(User user, CancellationToken cancellationToken = default)
    {
        await _context.Users.AddAsync(user, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return user;
    }

    public async Task UpdateAsync(User user, CancellationToken cancellationToken = default)
    {
        _context.Users.Update(user);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int userId, CancellationToken cancellationToken = default)
    {
        // MIGRATION: User delete is a HARD delete. The DNN 4.9 Users table has NO IsDeleted column
        // (verified against the install schema), so a soft delete is impossible without a schema change,
        // which ADR-002 forbids. The legacy UserController.DeleteUser also performed a hard delete via the
        // membership provider. Documented in MIGRATION_NOTES.md.
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.UserID == userId, cancellationToken);

        if (user is null)
        {
            return;
        }

        _context.Users.Remove(user);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
