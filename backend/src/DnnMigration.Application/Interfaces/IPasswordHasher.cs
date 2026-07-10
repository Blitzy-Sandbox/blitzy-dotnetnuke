namespace DnnMigration.Application.Interfaces;

/// <summary>
/// Port for hashing and verifying user passwords. Implemented in
/// <c>DnnMigration.Infrastructure.Identity</c> using BCrypt.Net-Next (dependency inversion keeps the
/// Application layer free of Infrastructure/library references) and consumed by AuthService/UserService.
/// </summary>
// MIGRATION: replaces the legacy DotNetNuke DES encryption (PortalSecurity.Encrypt/Decrypt,
// Library/Components/Security/PortalSecurity.vb L138/L175) and aspnet_Membership salted-hash
// verification with BCrypt hashing. A forward-hash-on-login strategy (verify legacy hash, then re-hash
// with BCrypt) is documented in MIGRATION_NOTES.md. Methods are synchronous (in-memory crypto, no I/O).
public interface IPasswordHasher
{
    /// <summary>Hashes a plaintext password (BCrypt) and returns the encoded hash string.</summary>
    string Hash(string password);

    /// <summary>Verifies a plaintext password against a stored hash; returns <c>true</c> on match.</summary>
    bool Verify(string password, string hash);
}
