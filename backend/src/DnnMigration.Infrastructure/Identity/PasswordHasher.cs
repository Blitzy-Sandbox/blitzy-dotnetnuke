using DnnMigration.Application.Interfaces;

namespace DnnMigration.Infrastructure.Identity;

/// <summary>
/// BCrypt-based implementation of <see cref="IPasswordHasher"/>.
/// </summary>
/// <remarks>
/// Delegates directly to <c>BCrypt.Net-Next</c> (4.0.3): <see cref="Hash(string)"/> produces a
/// salted, one-way hash with an embedded adaptive work factor (library default 11), and
/// <see cref="Verify(string, string)"/> performs a constant-time comparison after re-hashing the
/// candidate with the salt embedded in the stored hash. Because every <see cref="Hash(string)"/>
/// call generates a fresh random salt, two hashes of the same password differ by design and must
/// never be compared by string equality. The type is stateless and therefore thread-safe.
/// </remarks>
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
