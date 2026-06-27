namespace DnnMigration.Application.Interfaces;

// MIGRATION: Application-layer PORT (abstraction) for persisting and retrieving a user's stored password
// HASH, replacing the legacy ASP.NET 2.0 membership credential store (the aspnet_Membership table written
// through AspNetSqlMembershipProvider / AspNetMembershipProvider.vb — CreateUser persisted the password,
// UserLogin read it back to verify). In DNN the credential material lived OUTSIDE the user row (the
// UserInfo/Users table never held the password), so the migrated User entity likewise carries NO credential
// fields (AAP §0.7.6) and credential persistence is a separate concern behind this port.
//
// Clean/Onion: declared in the Application project (Application owns its abstractions and references Domain
// ONLY). The concrete adapter — which maps onto the migrated membership schema with BCrypt-hashed values —
// lives in DnnMigration.Infrastructure/Identity/CredentialStore.cs and is wired in Infrastructure/
// DependencyInjection.cs (Scoped). The adapter maps onto the EXISTING legacy membership schema (aspnet_Users +
// aspnet_Membership; AAP 0.7.1 - no new table), storing a one-way BCrypt hash in aspnet_Membership.Password. The
// Application services FAIL CLOSED when no credential is found (GetPasswordHashAsync returns null) rather than
// creating credentialless users or skipping verification (CP1 review UserService #5 / Security #1). Recorded in
// MIGRATION_NOTES.md.
//
// SEPARATION OF CONCERNS: this port stores/loads the OPAQUE hash only. Computing the hash and verifying a
// presented password are the responsibility of IPasswordHasher (BCrypt). A typical create flow is
// hash = IPasswordHasher.Hash(plaintext); ICredentialStore.SetPasswordAsync(userId, hash). A typical login
// flow is hash = ICredentialStore.GetPasswordHashAsync(userId); IPasswordHasher.Verify(plaintext, hash).

/// <summary>
/// Abstraction over persistence of a user's stored password hash, decoupled from where the credential
/// material physically lives (the migrated membership schema). The Application layer hashes via
/// <see cref="IPasswordHasher"/> and persists/loads the resulting hash through this port; it never stores
/// plaintext and never reverses a hash.
/// </summary>
public interface ICredentialStore
{
    /// <summary>
    /// Persists (creates or replaces) the stored password hash for the specified user. Callers MUST pass a
    /// value already produced by <see cref="IPasswordHasher.Hash(string)"/> — never a plaintext password.
    /// </summary>
    /// <param name="userId">Identifier of the user whose credential is being set.</param>
    /// <param name="passwordHash">The one-way (BCrypt) hash to persist. Never a plaintext password; never logged.</param>
    /// <param name="cancellationToken">Token to observe for cancellation.</param>
    Task SetPasswordAsync(int userId, string passwordHash, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the stored password hash for the specified user, or <c>null</c> when no credential has been
    /// persisted. The authentication flow verifies the presented plaintext against this value via
    /// <see cref="IPasswordHasher.Verify(string, string)"/> and MUST fail closed when this returns <c>null</c>.
    /// </summary>
    /// <param name="userId">Identifier of the user whose stored hash is requested.</param>
    /// <param name="cancellationToken">Token to observe for cancellation.</param>
    /// <returns>The stored hash, or <c>null</c> when the user has no persisted credential.</returns>
    Task<string?> GetPasswordHashAsync(int userId, CancellationToken cancellationToken = default);

    // MIGRATION (CP-FINAL review - Critical #2 "auth approval/last-login state persist"): the migrated User entity
    // exposes IsApproved and LastLoginDate, but those columns physically live in [aspnet_Membership] (not [Users]),
    // so they are Ignore()d on the User mapping and a plain UserRepository.UpdateAsync(user) does NOT persist them.
    // These two ports let AuthService persist the approval and last-login lifecycle to the existing membership row.
    // Both are STAGE-ONLY (no SaveChanges); the caller commits them inside its own unit of work, exactly like
    // SetPasswordAsync, so the User update and the membership-state update commit atomically together.

    /// <summary>
    /// Persists the approval state for the specified user onto the existing membership row
    /// (<c>aspnet_Membership.IsApproved</c>). Used by the login verification-code branch, which approves a
    /// previously-unapproved account before credential verification. No-op when the user has no membership row.
    /// </summary>
    /// <param name="userId">Identifier of the user whose approval state is being set.</param>
    /// <param name="isApproved">The approval state to persist.</param>
    /// <param name="cancellationToken">Token to observe for cancellation.</param>
    Task SetApprovedAsync(int userId, bool isApproved, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a successful authentication by stamping the last-login timestamp onto the existing membership row
    /// (<c>aspnet_Membership.LastLoginDate</c>). No-op when the user has no membership row.
    /// </summary>
    /// <param name="userId">Identifier of the user who authenticated.</param>
    /// <param name="lastLoginUtc">The successful-login timestamp (UTC) to persist.</param>
    /// <param name="cancellationToken">Token to observe for cancellation.</param>
    Task RecordLoginAsync(int userId, DateTime lastLoginUtc, CancellationToken cancellationToken = default);
}
