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
        // MIGRATION (DEV-065 — full membership provisioning): the legacy UserController.CreateUser did far more
        // than INSERT a [Users] row — it provisioned the whole ASP.NET 2.0 Membership graph the rest of the
        // system reads back: the credential pair ([aspnet_Users] + [aspnet_Membership]) AND a [UserPortals]
        // association so the user is visible to portal-scoped queries. Persisting only [Users] left API-created
        // users INVISIBLE to GetByPortalAsync (which JOINs [UserPortals]) and UNABLE to authenticate
        // (GetByUsernameAsync sources the hash from [aspnet_Membership] via the lowered-username bridge). This
        // method now restores that full provisioning, mirroring the integration-fixture seed template and
        // faithful to the legacy create flow. Per ADR-002 every row targets an EXISTING physical table (no
        // schema change, no migration, no data migration).
        //
        // user.Password arrives ALREADY BCrypt-hashed from UserService.CreateAsync (IPasswordHasher.Hash runs
        // BEFORE AddAsync) — it is copied verbatim into [aspnet_Membership].Password and is NEVER re-hashed here.
        // The write is wrapped in a relational transaction so the four rows commit atomically; the EF Core
        // InMemory provider is non-relational (IsRelational() == false), so the transaction is skipped under
        // Gate-5 integration tests, exactly as the Portal/Role cascade deletes do.
        var transaction = _context.Database.IsRelational()
            ? await _context.Database.BeginTransactionAsync(cancellationToken)
            : null;
        try
        {
            // 1) The core [Users] row. SaveChanges assigns the store-generated UserID used by the satellite rows.
            await _context.Users.AddAsync(user, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            // The legacy membership/portal graph is keyed by Username; without one there is nothing to bridge,
            // so a username-less row (never produced by the validated create flow) is left as the [Users] row only.
            if (!string.IsNullOrEmpty(user.Username))
            {
                // 2) The ASP.NET Membership identity pair (a shared uniqueidentifier), reached from the lowered
                //    username — the canonical InstallMembership.sql bridge that GetByUsernameAsync reads back.
                var membershipId = Guid.NewGuid();
                var loweredUserName = user.Username.ToLower();

                await _context.AspNetUsers.AddAsync(new AspNetUser
                {
                    UserId = membershipId,
                    LoweredUserName = loweredUserName
                }, cancellationToken);

                await _context.AspNetMemberships.AddAsync(new AspNetMembership
                {
                    UserId = membershipId,
                    Password = user.Password ?? string.Empty,   // already BCrypt-hashed by the service layer
                    IsApproved = user.Approved,
                    IsLockedOut = false,
                    FailedPasswordAttemptCount = 0,
                    LastLockoutDate = AspNetMembership.NeverLockedOutDate
                }, cancellationToken);

                // 3) The [UserPortals] association so the user appears in portal-scoped queries. UserPortalID is
                //    store-generated (IDENTITY, DEV-065); Authorised mirrors the approved state (legacy flag).
                //    user.PortalID is the in-memory request value (EF-Ignore()d on [Users] but carried on the entity).
                await _context.UserPortals.AddAsync(new UserPortal
                {
                    UserID = user.UserID,
                    PortalID = user.PortalID,
                    CreatedDate = DateTime.UtcNow,
                    Authorised = user.Approved
                }, cancellationToken);

                await _context.SaveChangesAsync(cancellationToken);
            }

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

        return user;
    }

    public async Task UpdateAsync(User user, CancellationToken cancellationToken = default)
    {
        _context.Users.Update(user);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int userId, CancellationToken cancellationToken = default)
    {
        // MIGRATION (DEV-066 — non-destructive delete): the AAP / final-checkpoint fidelity contract requires
        // User to use a NON-destructive (soft) delete. The preserved DNN 4.9 [Users] table has NO IsDeleted
        // column and ADR-002 forbids adding one, so the soft delete is realized SCHEMA-FAITHFULLY by removing
        // the user's [UserPortals] association row(s) rather than the durable [Users] / aspnet_* rows. This
        // DE-AUTHORIZES the account and removes it from every portal-scoped query (GetByPortalAsync JOINs
        // [UserPortals]) while preserving the identity and credential rows — the SAME observable "excluded from
        // the portal list" outcome the legacy delete produced, but without destroying data. The account can be
        // re-instated later by re-adding the membership row. Idempotent: a user with no membership rows is a
        // quiet no-op. Documented in MIGRATION_NOTES.md.
        var memberships = await _context.UserPortals
            .Where(up => up.UserID == userId)
            .ToListAsync(cancellationToken);

        if (memberships.Count == 0)
        {
            return;
        }

        var transaction = _context.Database.IsRelational()
            ? await _context.Database.BeginTransactionAsync(cancellationToken)
            : null;
        try
        {
            _context.UserPortals.RemoveRange(memberships);
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

    public async Task<AspNetMembership?> GetMembershipAsync(int userId, CancellationToken cancellationToken = default)
    {
        // MIGRATION (DEV-067): resolve the membership-state row for a user via the canonical
        // InstallMembership.sql bridge — [Users].Username -> LOWER -> [aspnet_Users].LoweredUserName ->
        // [aspnet_Membership].UserId. The approval/lockout state lives in [aspnet_Membership]
        // (User.Approved / User.LockedOut are EF-Ignore()d), so this second hop is required. AsNoTracking: this
        // is the read projection consumed by GET {id}/membership.
        var normalized = await GetLoweredUsernameAsync(userId, cancellationToken);
        if (normalized is null)
        {
            return null;
        }

        return await (
            from au in _context.AspNetUsers.AsNoTracking()
            join m in _context.AspNetMemberships.AsNoTracking() on au.UserId equals m.UserId
            where au.LoweredUserName == normalized
            select m)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> SetApprovedAsync(int userId, bool approved, CancellationToken cancellationToken = default)
    {
        // MIGRATION (DEV-067): the authorize / unauthorize transitions (legacy cmdAuthorize / cmdUnAuthorize).
        // Flip [aspnet_Membership].IsApproved and, for parity with the legacy per-portal [UserPortals].Authorised
        // flag, keep the user's portal association(s) in sync. The membership is loaded TRACKED (this is a write
        // path) via the lowered-username bridge.
        var membership = await GetTrackedMembershipAsync(userId, cancellationToken);
        if (membership is null)
        {
            return false;
        }

        membership.IsApproved = approved;

        // Keep the legacy per-portal [UserPortals].Authorised flag consistent with the membership approval so a
        // de-authorized user is also flagged unauthorized at the portal-association level.
        var memberships = await _context.UserPortals
            .Where(up => up.UserID == userId)
            .ToListAsync(cancellationToken);
        foreach (var up in memberships)
        {
            up.Authorised = approved;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> UnlockAsync(int userId, CancellationToken cancellationToken = default)
    {
        // MIGRATION (DEV-067): the unlock transition (legacy cmdUnLock), faithful to aspnet_Membership_UnlockUser
        // (InstallMembership.sql L1052-1057): clear IsLockedOut, reset FailedPasswordAttemptCount to 0, and reset
        // LastLockoutDate to the "never locked out" sentinel.
        var membership = await GetTrackedMembershipAsync(userId, cancellationToken);
        if (membership is null)
        {
            return false;
        }

        membership.IsLockedOut = false;
        membership.FailedPasswordAttemptCount = 0;
        membership.LastLockoutDate = AspNetMembership.NeverLockedOutDate;

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    // MIGRATION (DEV-067): the lowered-username half of the InstallMembership.sql bridge, shared by the
    // membership read and write paths. Returns null when the user is missing or has no Username to bridge on.
    private async Task<string?> GetLoweredUsernameAsync(int userId, CancellationToken cancellationToken)
    {
        var username = await _context.Users
            .AsNoTracking()
            .Where(u => u.UserID == userId)
            .Select(u => u.Username)
            .FirstOrDefaultAsync(cancellationToken);

        return string.IsNullOrEmpty(username) ? null : username.ToLower();
    }

    // MIGRATION (DEV-067): tracked twin of GetMembershipAsync used by the write transitions — the returned
    // [aspnet_Membership] entity MUST be change-tracked so SaveChanges persists the mutation. Same bridge, but
    // NOTE: NO AsNoTracking() anywhere in this query. In EF Core, AsNoTracking() applied to ANY source sets the
    // tracking behavior for the WHOLE query, so marking the [aspnet_Users] source no-tracking would also detach
    // the selected [aspnet_Membership] entity and silently drop the IsApproved/IsLockedOut mutation at SaveChanges.
    private async Task<AspNetMembership?> GetTrackedMembershipAsync(int userId, CancellationToken cancellationToken)
    {
        var normalized = await GetLoweredUsernameAsync(userId, cancellationToken);
        if (normalized is null)
        {
            return null;
        }

        return await (
            from au in _context.AspNetUsers
            join m in _context.AspNetMemberships on au.UserId equals m.UserId
            where au.LoweredUserName == normalized
            select m)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
