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
/// <remarks>
/// MIGRATION (CP3 schema fidelity): portal membership (<c>PortalID</c>) is NOT a <c>dbo.Users</c> column —
/// the real DNN 4.9 <c>dbo.Users</c> table has exactly 9 physical columns. Membership lives in
/// <c>dbo.UserPortals</c>, which the legacy <c>vw_Users</c> view exposes via a LEFT OUTER JOIN
/// (<c>dbo.Users U LEFT JOIN dbo.UserPortals UP ON U.UserId = UP.UserId</c>). <c>PortalID</c> is therefore
/// <c>Ignore()</c>'d on the <see cref="User"/> entity (see <c>UserConfiguration.cs</c>); this repository
/// reproduces the <c>vw_Users</c> join (and the legacy <c>GetUserByUsername</c> / <c>GetUsersByEmail</c> /
/// <c>GetUsers</c> portal-scoping WHERE clauses, including the superuser bypass) via explicit queries against
/// the schema-faithful <see cref="UserPortal"/> entity, and rehydrates <see cref="User.PortalID"/> from the
/// matching membership row. Recorded in MIGRATION_NOTES.md §4.2 (Users / UserPortals) and Deviation Index
/// D-014 / D-018 / D-019.
/// </remarks>
public class UserRepository : IUserRepository
{
    // MIGRATION: DNN's Null.NullInteger (-1) sentinel. The legacy GetUser*/GetUsers procs treat a NULL
    // @PortalID as "any portal" (the host/superuser context). The repository signatures carry a non-nullable
    // int portalId, so a value < 0 reproduces the proc's "@PortalID IS NULL" branch, and -1 is the value a
    // user's rehydrated PortalID takes when they have no portal membership (e.g. a host superuser).
    private const int NullPortalId = -1;

    private readonly DnnDbContext _context;

    public UserRepository(DnnDbContext context)
    {
        _context = context;
    }

    public async Task<User?> GetByIdAsync(int userId, CancellationToken cancellationToken = default)
    {
        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserID == userId, cancellationToken);

        if (user is null)
        {
            return null;
        }

        // Rehydrate the Ignore()'d PortalID from the user's UserPortals membership (the legacy vw_Users
        // join). A by-id lookup carries no portal context, so use the user's first membership (lowest
        // PortalId); fall back to Null.NullInteger (-1) when the user has none (e.g. a host superuser).
        await RehydratePortalIdAsync(user, cancellationToken);
        return user;
    }

    public async Task<User?> GetByUsernameAsync(int portalId, string username, CancellationToken cancellationToken = default)
    {
        // MIGRATION: reproduces the legacy GetUserByUsername stored procedure
        //   SELECT * FROM vw_Users WHERE Username = @Username
        //     AND (PortalId = @PortalID OR IsSuperUser = 1 OR @PortalID IS NULL)
        // vw_Users is (dbo.Users LEFT JOIN dbo.UserPortals ON UserId); PortalId comes from UserPortals.
        // The IsSuperUser bypass is CRITICAL: a host/superuser typically has NO UserPortals row, so without
        // it GetByUsernameAsync(portalId, "host") returns null and the host can never authenticate
        // (AuthService logs in against DefaultPortalId = 0). Case-insensitive match preserves SQL Server
        // collation parity; the nullable Username is guarded to satisfy CS8602.
        var normalized = username.ToLower();
        bool nullPortal = portalId < 0;

        var row = await (
            from u in _context.Users.AsNoTracking()
            where u.Username != null && u.Username.ToLower() == normalized
            // LEFT JOIN dbo.UserPortals for THIS portal (composite PK [UserId, PortalId] => at most one row).
            from up in _context.UserPortals.AsNoTracking()
                .Where(p => p.UserId == u.UserID && p.PortalId == portalId)
                .DefaultIfEmpty()
            // (PortalId = @PortalID)        => up != null
            // OR IsSuperUser = 1            => u.IsSuperUser
            // OR @PortalID IS NULL          => nullPortal
            where up != null || u.IsSuperUser || nullPortal
            select new { User = u, Membership = up })
            .FirstOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            return null;
        }

        row.User.PortalID = row.Membership != null
            ? row.Membership.PortalId
            : (nullPortal ? NullPortalId : portalId);
        return row.User;
    }

    public async Task<User?> GetByEmailAsync(int portalId, string email, CancellationToken cancellationToken = default)
    {
        // MIGRATION: the legacy provider exposed e-mail search as a paged "find" (GetUsersByEmail). This
        // repository deliberately narrows it to an exact-match single lookup, which is what the
        // Application/Auth layers require (uniqueness/registration/reset checks); the broader paged search is
        // out of scope. The portal-scoping reproduces GetUsersByEmail's WHERE clause against vw_Users:
        //   (PortalId = @PortalID OR (PortalId IS NULL AND @PortalID IS NULL))
        // NOTE: unlike GetUserByUsername there is NO IsSuperUser bypass here (the legacy proc has none).
        var normalized = email.ToLower();
        bool nullPortal = portalId < 0;

        var row = await (
            from u in _context.Users.AsNoTracking()
            where u.Email != null && u.Email.ToLower() == normalized
            // LEFT JOIN dbo.UserPortals for THIS portal (composite PK => at most one row).
            from up in _context.UserPortals.AsNoTracking()
                .Where(p => p.UserId == u.UserID && p.PortalId == portalId)
                .DefaultIfEmpty()
            // (PortalId = @PortalID)                    => up != null
            // OR (PortalId IS NULL AND @PortalID IS NULL) => null-portal sentinel AND the user has NO membership
            where up != null
               || (nullPortal && !_context.UserPortals.Any(p => p.UserId == u.UserID))
            select new { User = u, Membership = up })
            .FirstOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            return null;
        }

        row.User.PortalID = row.Membership != null
            ? row.Membership.PortalId
            : (nullPortal ? NullPortalId : portalId);
        return row.User;
    }

    public async Task<(IEnumerable<User> Items, int TotalCount)> GetByPortalAsync(
        int portalId,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        // MIGRATION: reproduces the legacy GetUsers stored procedure
        //   SELECT * FROM dbo.Users U LEFT JOIN dbo.UserPortals UP ON U.UserId = UP.UserId
        //   WHERE (UP.PortalId = @PortalID OR @PortalID IS NULL)
        // A concrete portal scopes to its members (the LEFT JOIN + UP.PortalId = @p collapses to an inner
        // join); the Null.NullInteger sentinel (@PortalID IS NULL) returns all users. PortalID is NOT a
        // dbo.Users column, so membership is resolved through the dbo.UserPortals join, never a u.PortalID
        // filter. The existing ordering (by Username) is preserved.
        bool nullPortal = portalId < 0;

        IQueryable<User> query = nullPortal
            ? _context.Users.AsNoTracking()
            : from u in _context.Users.AsNoTracking()
              join up in _context.UserPortals.AsNoTracking() on u.UserID equals up.UserId
              where up.PortalId == portalId
              select u;

        var totalCount = await query.CountAsync(cancellationToken);

        // MIGRATION: preserve the legacy pageIndex == -1 "return all rows" sentinel.
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
            .OrderBy(u => u.Username)
            .Skip(pageIndex * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        // Rehydrate the Ignore()'d PortalID. For a concrete portal every returned user is a member of it; the
        // cross-portal (null-portal) listing has no single portal, so it carries the Null.NullInteger sentinel.
        var resolvedPortalId = nullPortal ? NullPortalId : portalId;
        foreach (var user in items)
        {
            user.PortalID = resolvedPortalId;
        }

        return (items, totalCount);
    }

    public async Task<User> AddAsync(User user, CancellationToken cancellationToken = default)
    {
        // Insert the physical dbo.Users row first so EF assigns the IDENTITY UserID. PortalID is Ignore()'d,
        // so it is never written to dbo.Users (it is not a column there).
        await _context.Users.AddAsync(user, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        // MIGRATION: legacy AddUser also recorded portal membership via AddUserPortal (a dbo.UserPortals
        // row). Persist that membership through the schema-faithful UserPortal entity when the user belongs
        // to a concrete portal (PortalID >= 0). The CLR PortalID carries the intended portal (the mapping
        // layer sets it from CreateUserDto.PortalID). Recorded in MIGRATION_NOTES.md §4.2 (Users) / D-018.
        if (user.PortalID >= 0)
        {
            bool exists = await _context.UserPortals
                .AnyAsync(up => up.UserId == user.UserID && up.PortalId == user.PortalID, cancellationToken);

            if (!exists)
            {
                await _context.UserPortals.AddAsync(
                    new UserPortal
                    {
                        UserId = user.UserID,
                        PortalId = user.PortalID,
                        CreatedDate = DateTime.UtcNow,
                        Authorised = true,
                    },
                    cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
            }
        }

        return user;
    }

    public async Task UpdateAsync(User user, CancellationToken cancellationToken = default)
    {
        // Updates the physical dbo.Users row. PortalID and the aspnet_* membership/profile scalars are
        // Ignore()'d on the entity, so Update() never marks them modified and never emits SQL against the
        // non-existent columns. Portal-membership changes (UserPortals add/remove) are a distinct admin
        // operation and are not driven by the basic user update.
        _context.Users.Update(user);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int userId, CancellationToken cancellationToken = default)
    {
        // MIGRATION: User delete is a HARD delete. The DNN 4.9 dbo.Users table has NO IsDeleted column
        // (verified against the install schema: 9 physical columns), so a soft delete is impossible without a
        // schema change, which ADR-002 forbids. The legacy UserController.DeleteUser likewise removed the
        // user (and its portal membership) via the membership/UserPortals providers. Recorded in
        // MIGRATION_NOTES.md §6.3 (delete strategy) and Deviation Index D-014. The fetch is intentionally
        // unfiltered so an already-loaded user can still be removed.
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.UserID == userId, cancellationToken);

        if (user is null)
        {
            return;
        }

        // Remove the dbo.UserPortals membership rows first (the rows that bind the user to portals), then the
        // dbo.Users row — reproducing the legacy DeleteUserPortal + DeleteUser cleanup. (UserRoles cleanup is
        // the RoleRepository's concern.)
        var memberships = await _context.UserPortals
            .Where(up => up.UserId == userId)
            .ToListAsync(cancellationToken);

        if (memberships.Count > 0)
        {
            _context.UserPortals.RemoveRange(memberships);
        }

        _context.Users.Remove(user);
        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Rehydrates the <c>Ignore()</c>'d <see cref="User.PortalID"/> from the user's <c>dbo.UserPortals</c>
    /// membership (the legacy <c>vw_Users</c> join). Uses the user's first membership (lowest
    /// <c>PortalId</c>); falls back to <see cref="NullPortalId"/> (Null.NullInteger, -1) when the user has no
    /// portal membership (e.g. a host superuser). Used by the no-portal-context <see cref="GetByIdAsync"/>.
    /// </summary>
    private async Task RehydratePortalIdAsync(User user, CancellationToken cancellationToken)
    {
        var portalId = await _context.UserPortals
            .AsNoTracking()
            .Where(up => up.UserId == user.UserID)
            .OrderBy(up => up.PortalId)
            .Select(up => (int?)up.PortalId)
            .FirstOrDefaultAsync(cancellationToken);

        user.PortalID = portalId ?? NullPortalId;
    }
}
