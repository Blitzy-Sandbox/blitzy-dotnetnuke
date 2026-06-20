namespace DnnMigration.Application.Interfaces;

/// <summary>
/// Password hashing abstraction. MIGRATION: replaces the legacy symmetric 56-bit DES
/// Encrypt/Decrypt routines in PortalSecurity.vb with one-way adaptive BCrypt hashing.
/// Implemented by Infrastructure/Identity/PasswordHasher.cs (BCrypt.Net-Next).
/// </summary>
public interface IPasswordHasher
{
    /// <summary>Computes a one-way BCrypt hash of the supplied plaintext password.</summary>
    string Hash(string password);

    /// <summary>Verifies a plaintext password against a previously computed BCrypt hash.</summary>
    bool Verify(string password, string hash);
}
