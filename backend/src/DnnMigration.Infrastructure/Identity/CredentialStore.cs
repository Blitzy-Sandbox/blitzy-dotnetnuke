using DnnMigration.Application.Interfaces;
using DnnMigration.Domain.Entities;
using DnnMigration.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DnnMigration.Infrastructure.Identity;

// MIGRATION (CP-FINAL review - Critical #4 / DnnDbContext / CredentialStore): concrete Infrastructure ADAPTER for
// the Application ICredentialStore port. It REPLACES the earlier [UserCredentials] design (a NEW table, which
// violated the no-schema-alteration mandate) by mapping credential storage onto the EXISTING legacy ASP.NET 2.0
// membership schema that already ships with the DNN database: [aspnet_Applications] / [aspnet_Users] /
// [aspnet_Membership] (InstallCommon.sql + InstallMembership.sql). The migrated one-way BCrypt hash (AAP 0.7.6,
// replacing the legacy reversible/SHA value) is stored in the EXISTING [aspnet_Membership].[Password] column, so
// authentication works against an unchanged DNN database with NO operator-applied DDL (AAP 0.7.1; Rules item #2 -
// the project runs no Database.Migrate / EnsureCreated / schema SQL).
//
// BRIDGE: DNN keys users by the integer [Users].UserID, while the membership chain is GUID-keyed. The two are
// bridged by Username, exactly as the legacy AspNetSqlMembershipProvider did: [Users].Username ==
// aspnet_Users.UserName, and aspnet_Membership.UserId == aspnet_Users.UserId (1:1). Every membership row is scoped
// to the DNN application "DotNetNuke" (Website/release.config L246).
//
// CREDENTIAL MIGRATION NOTE (MIGRATION_NOTES.md): rows that PRE-EXIST in aspnet_Membership for legacy users hold a
// legacy (DES/SHA) password, NOT a BCrypt hash, so IPasswordHasher.Verify will fail for them and those accounts
// require a password reset to obtain a BCrypt hash. Accounts CREATED through the migrated stack get a BCrypt hash
// written here and authenticate normally.
//
// Wired in Infrastructure/DependencyInjection.cs as Scoped, so it shares the request's DnnDbContext (the same EF
// change-tracker / unit of work) with the repositories and IUnitOfWork.
//
// SEPARATION OF CONCERNS: this adapter stores/loads the OPAQUE hash ONLY. Hashing the plaintext and verifying a
// presented password are IPasswordHasher's (BCrypt) responsibility. The stored hash is NEVER logged.
//
// TRANSACTION SEMANTICS - verified against the call-sites:
//   * SetPasswordAsync / SetApprovedAsync / RecordLoginAsync are STAGE-ONLY (no SaveChanges). UserService.CreateAsync
//     and PortalService bootstrap call SetPasswordAsync and then commit via IUnitOfWork.SaveChangesAsync; AuthService
//     calls SetApprovedAsync / RecordLoginAsync and then commits the User update in the same unit of work, so the
//     membership-state change persists atomically with the caller's other writes (mirrors the STAGE-ONLY repositories).
//   * GetPasswordHashAsync is a pure read; AuthService verifies the presented password against the result and FAILS
//     CLOSED when it is null (AuthService L167-171).
public sealed class CredentialStore : ICredentialStore
{
    // MIGRATION: the DNN membership application name (Website/release.config L246). All migrated credential rows are
    // scoped to this application, matching the single-application convention the legacy provider used.
    private const string ApplicationName = "DotNetNuke";
    private static readonly string ApplicationNameLowered = ApplicationName.ToLowerInvariant();

    // MIGRATION: the ASP.NET membership "never" sentinel for NOT NULL datetime columns (LastLockoutDate and the two
    // failed-attempt window starts). DateTime.MinValue (0001-01-01) is OUTSIDE the SQL Server `datetime` range
    // (1753-01-01 .. 9999), so the legacy SqlMembershipProvider used 1754-01-01 for "has never happened"; preserved.
    private static readonly DateTime NeverDate = new(1754, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly DnnDbContext _context;

    public CredentialStore(DnnDbContext context) => _context = context;

    /// <inheritdoc />
    public async Task SetPasswordAsync(int userId, string passwordHash, CancellationToken cancellationToken = default)
    {
        // MIGRATION: resolve the DNN user (FindAsync checks the change-tracker first, so the row just inserted by
        // UserService.CreateAsync / PortalService bootstrap in the same unit of work is found without a query). The
        // Username bridges to the GUID-keyed membership chain. A credential for a non-existent user is a caller bug.
        var user = await _context.Users.FindAsync(new object?[] { userId }, cancellationToken);
        if (user is null)
        {
            throw new InvalidOperationException(
                $"Cannot set a credential for user id {userId}: no matching [Users] row exists.");
        }

        var application = await GetOrCreateApplicationAsync(cancellationToken);
        var aspUser = await GetOrCreateAspNetUserAsync(application, user, cancellationToken);

        // Get-or-create the 1:1 membership row keyed on the aspnet_Users GUID.
        var membership = await _context.Set<AspNetMembership>()
            .FirstOrDefaultAsync(m => m.UserId == aspUser.UserId, cancellationToken);

        var now = DateTime.UtcNow;
        if (membership is null)
        {
            // First credential for this user - stage a fully-populated, schema-valid row (every NOT NULL column set).
            await _context.Set<AspNetMembership>().AddAsync(
                new AspNetMembership
                {
                    UserId = aspUser.UserId,
                    ApplicationId = application.ApplicationId,
                    Password = passwordHash,
                    PasswordFormat = 1,                 // Hashed (BCrypt one-way), not Clear(0)/Encrypted(2).
                    PasswordSalt = string.Empty,        // BCrypt embeds its own salt inside the hash.
                    MobilePIN = null,
                    Email = user.Email,
                    LoweredEmail = user.Email?.ToLowerInvariant(),
                    PasswordQuestion = null,
                    PasswordAnswer = null,
                    IsApproved = user.IsApproved,
                    IsLockedOut = false,
                    CreateDate = now,
                    LastLoginDate = now,
                    LastPasswordChangedDate = now,
                    LastLockoutDate = NeverDate,
                    FailedPasswordAttemptCount = 0,
                    FailedPasswordAttemptWindowStart = NeverDate,
                    FailedPasswordAnswerAttemptCount = 0,
                    FailedPasswordAnswerAttemptWindowStart = NeverDate,
                    Comment = null
                },
                cancellationToken);
        }
        else
        {
            // Password replacement - overwrite the hash and stamp the change date (faithful to the legacy provider,
            // which updated Password + LastPasswordChangedDate on ChangePassword).
            membership.Password = passwordHash;
            membership.PasswordFormat = 1;
            membership.PasswordSalt = string.Empty;
            membership.LastPasswordChangedDate = now;
        }

        // STAGE-ONLY: the change is committed by the caller's IUnitOfWork.SaveChangesAsync (see class remarks).
    }

    /// <inheritdoc />
    public async Task<string?> GetPasswordHashAsync(int userId, CancellationToken cancellationToken = default)
    {
        // MIGRATION: resolve the Username for the integer userId (change-tracker first), then read the stored hash
        // from the existing membership row via the username bridge. AsNoTracking - a login lookup never mutates.
        var user = await _context.Users.FindAsync(new object?[] { userId }, cancellationToken);
        if (user is null)
        {
            return null; // fail-closed: no user => no credential.
        }

        var loweredUser = user.Username.ToLowerInvariant();

        var hash = await (
            from m in _context.Set<AspNetMembership>().AsNoTracking()
            join u in _context.Set<AspNetUser>().AsNoTracking() on m.UserId equals u.UserId
            join a in _context.Set<AspNetApplication>().AsNoTracking() on u.ApplicationId equals a.ApplicationId
            where a.LoweredApplicationName == ApplicationNameLowered && u.LoweredUserName == loweredUser
            select m.Password).FirstOrDefaultAsync(cancellationToken);

        return hash;
    }

    /// <inheritdoc />
    public async Task SetApprovedAsync(int userId, bool isApproved, CancellationToken cancellationToken = default)
    {
        // MIGRATION: persist User.IsApproved to its physical home, [aspnet_Membership].IsApproved. Tracked lookup so
        // the mutation is committed by the caller's unit of work. No-op when there is no membership row (the account
        // could not authenticate without one anyway).
        var membership = await FindMembershipAsync(userId, cancellationToken);
        if (membership is null)
        {
            return;
        }

        membership.IsApproved = isApproved;

        // STAGE-ONLY: committed by the caller's IUnitOfWork.SaveChangesAsync.
    }

    /// <inheritdoc />
    public async Task RecordLoginAsync(int userId, DateTime lastLoginUtc, CancellationToken cancellationToken = default)
    {
        // MIGRATION: persist User.LastLoginDate to its physical home, [aspnet_Membership].LastLoginDate (legacy
        // UserMembership.UpdateUserLastLogin). Tracked lookup; no-op when there is no membership row.
        var membership = await FindMembershipAsync(userId, cancellationToken);
        if (membership is null)
        {
            return;
        }

        membership.LastLoginDate = lastLoginUtc;

        // STAGE-ONLY: committed by the caller's IUnitOfWork.SaveChangesAsync.
    }

    // MIGRATION: get-or-create the membership application row. The Local (staged) set is checked before the store so
    // two credential writes inside the SAME unit of work cannot stage two "DotNetNuke" application rows (which would
    // violate the physical UNIQUE index on LoweredApplicationName). On a real DNN database the "DotNetNuke" row
    // already exists and is simply found.
    private async Task<AspNetApplication> GetOrCreateApplicationAsync(CancellationToken cancellationToken)
    {
        var application = _context.Set<AspNetApplication>().Local
                .FirstOrDefault(a => a.LoweredApplicationName == ApplicationNameLowered)
            ?? await _context.Set<AspNetApplication>()
                .FirstOrDefaultAsync(a => a.LoweredApplicationName == ApplicationNameLowered, cancellationToken);

        if (application is null)
        {
            application = new AspNetApplication
            {
                ApplicationId = Guid.NewGuid(),
                ApplicationName = ApplicationName,
                LoweredApplicationName = ApplicationNameLowered,
                Description = null
            };
            await _context.Set<AspNetApplication>().AddAsync(application, cancellationToken);
        }

        return application;
    }

    // MIGRATION: get-or-create the aspnet_Users identity for the DNN user, matched by (ApplicationId,
    // LoweredUserName). Local-first so a row staged earlier in the same unit of work is reused. On a real DNN
    // database the legacy provider already created this row; it is simply found.
    private async Task<AspNetUser> GetOrCreateAspNetUserAsync(
        AspNetApplication application, User user, CancellationToken cancellationToken)
    {
        var loweredUser = user.Username.ToLowerInvariant();

        var aspUser = _context.Set<AspNetUser>().Local
                .FirstOrDefault(u => u.ApplicationId == application.ApplicationId && u.LoweredUserName == loweredUser)
            ?? await _context.Set<AspNetUser>()
                .FirstOrDefaultAsync(
                    u => u.ApplicationId == application.ApplicationId && u.LoweredUserName == loweredUser,
                    cancellationToken);

        if (aspUser is null)
        {
            aspUser = new AspNetUser
            {
                UserId = Guid.NewGuid(),
                ApplicationId = application.ApplicationId,
                UserName = user.Username,
                LoweredUserName = loweredUser,
                MobileAlias = null,
                IsAnonymous = false,
                LastActivityDate = DateTime.UtcNow
            };
            await _context.Set<AspNetUser>().AddAsync(aspUser, cancellationToken);
        }

        return aspUser;
    }

    // MIGRATION: resolve the TRACKED membership row for an integer DNN userId via the username bridge, so the
    // membership-state setters (SetApprovedAsync / RecordLoginAsync) can mutate it and have the caller's unit of work
    // persist the change. Returns null when any link in the bridge is absent.
    private async Task<AspNetMembership?> FindMembershipAsync(int userId, CancellationToken cancellationToken)
    {
        var user = await _context.Users.FindAsync(new object?[] { userId }, cancellationToken);
        if (user is null)
        {
            return null;
        }

        var loweredUser = user.Username.ToLowerInvariant();

        return await (
            from m in _context.Set<AspNetMembership>()
            join u in _context.Set<AspNetUser>() on m.UserId equals u.UserId
            join a in _context.Set<AspNetApplication>() on u.ApplicationId equals a.ApplicationId
            where a.LoweredApplicationName == ApplicationNameLowered && u.LoweredUserName == loweredUser
            select m).FirstOrDefaultAsync(cancellationToken);
    }
}
