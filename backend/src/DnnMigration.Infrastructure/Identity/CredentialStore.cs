using DnnMigration.Application.Interfaces;
using DnnMigration.Domain.Entities;
using DnnMigration.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DnnMigration.Infrastructure.Identity;

// MIGRATION (CP2 review — DependencyInjection #1 / Program.cs #4): concrete Infrastructure ADAPTER for the
// Application ICredentialStore port. Backs the migrated BCrypt identity store ([UserCredentials]) that REPLACES
// the legacy aspnet_Membership credential table (AAP §0.5.2). Wired in Infrastructure/DependencyInjection.cs as
// Scoped so it shares the request's DnnDbContext (and therefore the same EF change-tracker / unit of work) with
// the repositories and IUnitOfWork.
//
// SEPARATION OF CONCERNS: this adapter stores/loads the OPAQUE hash ONLY. Hashing the plaintext and verifying a
// presented password are IPasswordHasher's (BCrypt) responsibility. The stored hash is NEVER logged.
//
// TRANSACTION SEMANTICS — verified against the call-sites:
//   * SetPasswordAsync is STAGE-ONLY (no SaveChanges). UserService.CreateAsync (L231->L232) and
//     PortalService bootstrap (L197->L202) both call SetPasswordAsync and then commit via
//     IUnitOfWork.SaveChangesAsync, so the credential is persisted atomically within the caller's unit of work
//     (mirrors the STAGE-ONLY repositories). Self-committing here would split that transaction.
//   * GetPasswordHashAsync is a pure read; AuthService verifies the presented password against the result and
//     FAILS CLOSED when it is null (AuthService L167-171).
public sealed class CredentialStore : ICredentialStore
{
    private readonly DnnDbContext _context;

    public CredentialStore(DnnDbContext context) => _context = context;

    /// <inheritdoc />
    public async Task SetPasswordAsync(int userId, string passwordHash, CancellationToken cancellationToken = default)
    {
        // MIGRATION: create-or-replace the stored hash. FindAsync checks the local change-tracker first and then
        // the database, so a credential staged earlier in the same unit of work is updated rather than duplicated.
        var existing = await _context.Set<UserCredential>()
            .FindAsync(new object?[] { userId }, cancellationToken);

        if (existing is null)
        {
            // First credential for this user — stamp CreatedDate and stage the insert.
            await _context.Set<UserCredential>().AddAsync(
                new UserCredential
                {
                    UserId = userId,
                    PasswordHash = passwordHash,
                    CreatedDate = DateTime.UtcNow
                },
                cancellationToken);
        }
        else
        {
            // Password replacement — overwrite the hash and stamp LastModifiedDate.
            existing.PasswordHash = passwordHash;
            existing.LastModifiedDate = DateTime.UtcNow;
        }

        // STAGE-ONLY: the change is committed by the caller's IUnitOfWork.SaveChangesAsync (see class remarks).
    }

    /// <inheritdoc />
    public async Task<string?> GetPasswordHashAsync(int userId, CancellationToken cancellationToken = default)
    {
        // MIGRATION: pure read — AsNoTracking so a login lookup never pollutes the change-tracker. Returns null
        // when the user has no persisted credential, which the authentication flow treats as fail-closed.
        var credential = await _context.Set<UserCredential>()
            .AsNoTracking()
            .FirstOrDefaultAsync(uc => uc.UserId == userId, cancellationToken);

        return credential?.PasswordHash;
    }
}
