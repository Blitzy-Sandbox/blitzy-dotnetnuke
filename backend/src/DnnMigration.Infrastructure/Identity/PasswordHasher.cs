using DnnMigration.Application.Interfaces;

namespace DnnMigration.Infrastructure.Identity;

/// <summary>
/// BCrypt-based implementation of <see cref="IPasswordHasher"/>. Produces salted, one-way
/// adaptive hashes (work factor embedded in the hash string) and verifies candidate passwords
/// in constant time. The type is stateless and therefore safe to register as a singleton or a
/// scoped dependency in the DI container.
/// </summary>
// MIGRATION: Replaces the REVERSIBLE 56-bit DES Encrypt/Decrypt routines at
// Library/Components/Security/PortalSecurity.vb:L138-L220 (DESCryptoServiceProvider; key
// padded/truncated to 16 chars -> 8-byte key Left(key,8) + 8-byte IV Right(key,8); UTF8; Base64)
// with a ONE-WAY adaptive BCrypt hash. This is a deliberate security upgrade and a semantic change
// (reversible encryption -> one-way salted hash; no Decrypt equivalent). Documented in root MIGRATION_NOTES.md.
public sealed class PasswordHasher : IPasswordHasher
{
    /// <inheritdoc />
    public string Hash(string password)
        => BCrypt.Net.BCrypt.HashPassword(password);

    /// <inheritdoc />
    public bool Verify(string password, string hash)
        => BCrypt.Net.BCrypt.Verify(password, hash);
}
