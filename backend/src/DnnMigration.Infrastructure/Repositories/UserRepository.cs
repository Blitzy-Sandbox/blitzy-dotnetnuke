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
        return await (
            from u in _context.Users.AsNoTracking()
            join up in _context.UserPortals.AsNoTracking() on u.UserID equals up.UserID
            where up.PortalID == portalId
                  && u.Username != null && u.Username.ToLower() == normalized
            select u)
            .FirstOrDefaultAsync(cancellationToken);
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
