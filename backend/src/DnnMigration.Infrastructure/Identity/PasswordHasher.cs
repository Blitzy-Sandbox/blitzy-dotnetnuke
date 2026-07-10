using DnnMigration.Application.Interfaces;

namespace DnnMigration.Infrastructure.Identity;

/// <summary>
/// BCrypt-based implementation of <see cref="IPasswordHasher"/>. Produces and verifies one-way
/// password hashes using the <c>BCrypt.Net-Next</c> library.
/// <para>
/// This type replaces the legacy DotNetNuke security machinery: the reversible DES encryption of
/// <c>PortalSecurity.Encrypt</c>/<c>Decrypt</c>
/// (<c>Library/Components/Security/PortalSecurity.vb</c> L138/L175) and the <c>aspnet_Membership</c>
/// salted-SHA1 password storage. BCrypt embeds the algorithm version, the cost (work) factor, and a
/// randomly generated salt inside the produced hash string, so no separate salt column is required
/// (unlike the legacy <c>aspnet_Membership</c> design). This self-describing hash is what makes the
/// "forward-hash-on-login" credential migration possible.
/// </para>
/// <para>
/// The class operates on strings only: it performs no database access and no I/O, holds no mutable
/// state, and is therefore thread-safe and safely registered as a dependency-injection singleton by
/// the API composition root (<c>Program.cs</c>). The forward-hash-on-login migration strategy
/// (verify a legacy hash, then re-hash with BCrypt on success) is orchestrated upstream by
/// <c>AuthService</c>/<c>UserRepository</c> and documented in <c>MIGRATION_NOTES.md</c> — it is
/// intentionally NOT implemented here.
/// </para>
/// </summary>
public sealed class PasswordHasher : IPasswordHasher
{
    /// <summary>
    /// BCrypt cost (work) factor. 11 is the <c>BCrypt.Net-Next</c> default; it is declared explicitly
    /// here so the computational cost of hashing is visible and tunable from a single location.
    /// </summary>
    private const int WorkFactor = 11;

    // MIGRATION: replaces PortalSecurity.vb reversible DES Encrypt/Decrypt (L138/L175) and legacy aspnet_Membership salted-SHA1 password storage with one-way BCrypt hashing (work factor 11).

    /// <summary>
    /// Hashes a plaintext password with BCrypt (using <see cref="WorkFactor"/>) and returns the
    /// encoded hash string. The returned value embeds the algorithm version, cost factor, and a
    /// randomly generated salt, so it is fully self-describing for later verification via
    /// <see cref="Verify(string, string)"/>.
    /// </summary>
    /// <param name="password">The plaintext password to hash. Must not be <see langword="null"/>.</param>
    /// <returns>The BCrypt-encoded hash of <paramref name="password"/>.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="password"/> is <see langword="null"/>.
    /// </exception>
    public string Hash(string password)
    {
        // Fully qualify BCrypt.Net.BCrypt: the assembly exposes a class named "BCrypt" inside the
        // "BCrypt.Net" namespace, so a `using BCrypt.Net;` directive would make the bare identifier
        // "BCrypt" ambiguous between the namespace and the class.
        ArgumentNullException.ThrowIfNull(password);
        return BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);
    }

    /// <summary>
    /// Verifies a plaintext password against a previously produced BCrypt hash.
    /// </summary>
    /// <param name="password">The plaintext password supplied by the caller.</param>
    /// <param name="hash">The stored hash to compare against.</param>
    /// <returns>
    /// <see langword="true"/> if <paramref name="password"/> matches <paramref name="hash"/>;
    /// otherwise <see langword="false"/>. Also returns <see langword="false"/> when either argument is
    /// <see langword="null"/> or empty, or when <paramref name="hash"/> is not a parseable BCrypt hash
    /// (for example a legacy DES / <c>aspnet_Membership</c> value).
    /// </returns>
    public bool Verify(string password, string hash)
    {
        // Guard first: an empty/absent password or stored hash can never constitute a match, and
        // passing an empty hash to BCrypt would otherwise surface as a parse failure anyway.
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(hash))
        {
            return false;
        }

        try
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            // MIGRATION: a non-BCrypt (legacy DES / aspnet_Membership) hash is unparseable by
            // BCrypt, so return false here. AuthService/UserRepository then fall back to legacy
            // verification and, on success, re-hash the password with BCrypt (forward-hash-on-login;
            // see MIGRATION_NOTES.md). This class never reads the database.
            return false;
        }
    }
}
