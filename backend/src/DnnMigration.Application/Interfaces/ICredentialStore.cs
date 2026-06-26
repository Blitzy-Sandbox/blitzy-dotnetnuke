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
// lives in DnnMigration.Infrastructure and is wired in Infrastructure/DependencyInjection.cs. That adapter is
// DEFERRED to CP2 (Infrastructure is mid-pipeline); this port is introduced now so the Application services
// can depend on it and FAIL CLOSED rather than silently creating credentialless users (CP1 review
// UserService #5 / Security #1) or skipping credential verification. Recorded in MIGRATION_NOTES.md.
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
}
