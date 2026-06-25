namespace DnnMigration.Application.Interfaces;

// MIGRATION: Application-layer PORT (abstraction) for one-way password hashing, replacing the legacy
// reversible DES credential cipher in Library/Components/Security/PortalSecurity.vb (Encrypt L138-173 /
// Decrypt L175-226 / CreateKey L564) with BCrypt (AAP §0.7.6). Declared in the Application project — per
// Clean/Onion architecture the Application layer owns the abstractions and references Domain ONLY; the
// concrete adapter (DnnMigration.Infrastructure.Identity.PasswordHasher, BCrypt.Net-Next) implements this
// contract and is wired in Infrastructure/DependencyInjection.cs.
//
// COORDINATION GAP: this port was missing from the realized plan even though the concrete
// Infrastructure/Identity/PasswordHasher.cs (already committed) declares `: IPasswordHasher` against this
// namespace. Without it neither the Infrastructure project nor Application/Services/AuthService.cs can
// compile. Created here (Application/Interfaces) to close the gap. Recorded in MIGRATION_NOTES.md.
//
// CRITICAL SEMANTIC CHANGE: legacy DES Encrypt/Decrypt was a TWO-WAY reversible cipher; BCrypt is a ONE-WAY
// adaptive hash. There is intentionally NO decrypt operation — "decrypt-then-compare" authentication becomes
// Verify(plaintext, storedHash). This is a deliberate security upgrade, not a 1:1 port.

/// <summary>
/// Abstraction over a one-way password hashing algorithm (BCrypt). Used by the authentication flow to
/// hash new credentials and to verify a presented plaintext password against a stored hash.
/// </summary>
public interface IPasswordHasher
{
    /// <summary>
    /// Computes a one-way, salted hash of the supplied plaintext password. The salt is generated and
    /// embedded by the algorithm, so the returned string is self-describing and requires no separate
    /// salt/key storage.
    /// </summary>
    /// <param name="password">The plaintext password to hash. Never logged.</param>
    /// <returns>The encoded hash string suitable for persistence.</returns>
    string Hash(string password);

    /// <summary>
    /// Verifies a plaintext password against a previously stored hash. Implementations MUST fail closed
    /// (return <c>false</c>) for an empty, null, malformed, or non-BCrypt (e.g. legacy) hash and MUST NOT
    /// throw.
    /// </summary>
    /// <param name="password">The plaintext password presented by the caller. Never logged.</param>
    /// <param name="hash">The stored hash to verify against.</param>
    /// <returns><c>true</c> when the password matches the hash; otherwise <c>false</c>.</returns>
    bool Verify(string password, string hash);
}
