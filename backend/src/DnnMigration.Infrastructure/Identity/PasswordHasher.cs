using DnnMigration.Application.Interfaces;

namespace DnnMigration.Infrastructure.Identity;

// MIGRATION: Replaces the legacy DES credential cipher in Library/Components/Security/PortalSecurity.vb
// (Encrypt L138-173 / Decrypt L175-226 / CreateKey L564) with one-way BCrypt hashing (BCrypt.Net-Next, AAP 0.7.6).
// CRITICAL SEMANTIC CHANGE: legacy DES Encrypt/Decrypt is a REVERSIBLE two-way cipher; BCrypt is a ONE-WAY adaptive
// hash. Encrypt -> Hash (BCrypt.HashPassword, which auto-generates a per-hash salt). Decrypt has NO BCrypt equivalent:
// the legacy "decrypt-then-compare" authentication becomes Verify(plaintext, storedHash) (BCrypt.Verify). Legacy
// CreateKey (RNG manual key) is superseded by BCrypt's built-in auto-salt (no manual key). This is a deliberate
// security UPGRADE, not a 1:1 port. Documented in MIGRATION_NOTES.md.
public sealed class PasswordHasher : IPasswordHasher
{
    // Work factor 11 = BCrypt.Net-Next default; 2^11 rounds. Explicit for determinism/clarity.
    private const int WorkFactor = 11;

    public string Hash(string password)
    {
        // MIGRATION: PortalSecurity.Encrypt(strKey, strData) -> BCrypt.HashPassword. The salt is generated
        // automatically and embedded in the returned hash string (no separate key/salt storage needed).
        return BCrypt.Net.BCrypt.HashPassword(password, workFactor: WorkFactor);
    }

    public bool Verify(string password, string hash)
    {
        // MIGRATION: replaces the legacy Decrypt-then-string-compare. Fail-closed: an empty/null stored hash
        // or a malformed/legacy non-BCrypt hash must return false and MUST NOT throw.
        if (string.IsNullOrEmpty(hash))
        {
            return false;
        }

        try
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            // A non-BCrypt / corrupted hash (e.g., a leftover legacy DES Base64 string) fails closed.
            return false;
        }
    }
}
