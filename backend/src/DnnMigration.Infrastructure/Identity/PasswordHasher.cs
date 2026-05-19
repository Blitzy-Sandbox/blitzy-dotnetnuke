// -----------------------------------------------------------------------------
// <copyright file="PasswordHasher.cs" company="DnnMigration">
//   Copyright (c) DnnMigration. All rights reserved.
//   Licensed under the MIT License.
// </copyright>
// <summary>
//   Password hashing service using BCrypt.Net-Next for secure password storage and verification.
//   MIGRATION: Replaces legacy DES-based encryption from PortalSecurity.vb with modern BCrypt algorithm.
// </summary>
// -----------------------------------------------------------------------------

namespace DnnMigration.Infrastructure.Identity;

using BCrypt.Net;
using DnnMigration.Domain.Interfaces;

/// <summary>
/// BCrypt-based password hashing service implementing secure password storage and verification.
/// </summary>
/// <remarks>
/// <para>
/// This implementation uses BCrypt.Net-Next for cross-platform compatibility on .NET 8.
/// BCrypt is an industry-standard algorithm designed specifically for password hashing with:
/// </para>
/// <list type="bullet">
///   <item>Adaptive work factor - can be increased over time as hardware improves</item>
///   <item>Built-in salt generation - each password gets a unique salt</item>
///   <item>Intentionally slow - mitigates brute force and dictionary attacks</item>
///   <item>Rainbow table resistant - due to unique salts per password</item>
/// </list>
/// <para>
/// MIGRATION: This completely replaces the legacy DES-based encryption from PortalSecurity.vb:
/// <list type="bullet">
///   <item>Legacy: DESCryptoServiceProvider with 16-char key (lines 138-173)</item>
///   <item>Legacy: RNGCryptoServiceProvider for key generation (CreateKey, lines 564-571)</item>
///   <item>New: BCrypt with automatic salt generation and configurable work factor</item>
/// </list>
/// </para>
/// <para>
/// Thread Safety: All BCrypt operations are thread-safe. This service can be registered
/// as a singleton in the DI container.
/// </para>
/// </remarks>
public sealed class PasswordHasher : IPasswordHasher
{
    /// <summary>
    /// Default BCrypt work factor (2^11 = 2048 iterations).
    /// </summary>
    /// <remarks>
    /// Work factor 11 provides a good balance between security and performance:
    /// - Takes approximately 200-300ms to hash on modern hardware
    /// - Can be increased to 12 or higher for more security at the cost of performance
    /// - Should be reviewed and potentially increased every 1-2 years
    /// 
    /// NIST and OWASP recommend BCrypt with work factor 10+ for password storage.
    /// </remarks>
    private const int DefaultWorkFactor = 11;

    /// <summary>
    /// The configured work factor for BCrypt hashing.
    /// </summary>
    private readonly int _workFactor;

    /// <summary>
    /// Initializes a new instance of the <see cref="PasswordHasher"/> class with the default work factor.
    /// </summary>
    public PasswordHasher() : this(DefaultWorkFactor)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PasswordHasher"/> class with a custom work factor.
    /// </summary>
    /// <param name="workFactor">
    /// The BCrypt work factor (cost). Valid range is 4-31.
    /// Higher values increase security but also increase hashing time exponentially.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when workFactor is less than 4 or greater than 31.
    /// </exception>
    public PasswordHasher(int workFactor)
    {
        if (workFactor is < 4 or > 31)
        {
            throw new ArgumentOutOfRangeException(
                nameof(workFactor),
                workFactor,
                "Work factor must be between 4 and 31 inclusive.");
        }

        _workFactor = workFactor;
    }

    /// <inheritdoc />
    /// <example>
    /// <code>
    /// var hasher = new PasswordHasher();
    /// string hash = hasher.HashPassword("MySecurePassword123!");
    /// // Returns: "$2a$11$[22-character-salt][31-character-hash]"
    /// </code>
    /// </example>
    public string HashPassword(string password)
    {
        if (string.IsNullOrEmpty(password))
        {
            throw new ArgumentException("Password cannot be null or empty.", nameof(password));
        }

        // BCrypt automatically generates a secure random salt and embeds it in the hash
        // The returned hash includes: algorithm version ($2a$), work factor, salt, and hash
        // Format: $2a$[workFactor]$[22-char-salt][31-char-hash]
        // MIGRATION: Replaces PortalSecurity.Encrypt which used DES symmetric encryption
        return BCrypt.HashPassword(password, workFactor: _workFactor);
    }

    /// <inheritdoc />
    /// <example>
    /// <code>
    /// var hasher = new PasswordHasher();
    /// string hash = hasher.HashPassword("MySecurePassword123!");
    /// 
    /// bool isValid = hasher.VerifyPassword("MySecurePassword123!", hash);
    /// // Returns: true
    /// 
    /// bool isInvalid = hasher.VerifyPassword("WrongPassword", hash);
    /// // Returns: false
    /// </code>
    /// </example>
    public bool VerifyPassword(string password, string hashedPassword)
    {
        // Return false for null/empty inputs rather than throwing
        // This provides graceful handling and prevents information leakage
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(hashedPassword))
        {
            return false;
        }

        try
        {
            // BCrypt.Verify extracts the salt from the stored hash and uses it
            // to hash the provided password, then compares the results
            // The comparison is done in constant time to prevent timing attacks
            // MIGRATION: Replaces legacy pattern of decrypting and comparing plaintext
            return BCrypt.Verify(password, hashedPassword);
        }
        catch (SaltParseException)
        {
            // Invalid hash format (not a valid BCrypt hash)
            // This could indicate a legacy encrypted password format
            // MIGRATION NOTE: Legacy DES-encrypted passwords will fail here
            // and should be handled by the calling code (prompt user to reset)
            return false;
        }
        catch (HashInformationException)
        {
            // Unable to extract hash information from the string
            // This indicates a malformed or corrupted hash
            return false;
        }
    }

    /// <inheritdoc />
    /// <example>
    /// <code>
    /// var hasher = new PasswordHasher(workFactor: 12);
    /// 
    /// // Old hash created with work factor 10
    /// string oldHash = "$2a$10$N9qo8uLOickgx2ZMRZoMyeIjZAgcfl7p92ldGxad68LJZdL17lhWy";
    /// 
    /// var (isValid, needsUpgrade) = hasher.VerifyAndUpgradeHash("password", oldHash);
    /// // isValid: true (password matches)
    /// // needsUpgrade: true (hash uses work factor 10, current is 12)
    /// </code>
    /// </example>
    public (bool IsValid, bool NeedsUpgrade) VerifyAndUpgradeHash(string password, string hashedPassword)
    {
        // Handle null/empty inputs
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(hashedPassword))
        {
            return (IsValid: false, NeedsUpgrade: false);
        }

        // First, check if this might be a legacy (non-BCrypt) format
        if (IsLegacyPasswordFormat(hashedPassword))
        {
            // MIGRATION: Legacy passwords cannot be verified with BCrypt
            // They need user intervention (password reset) to upgrade
            // Returning false for IsValid since we can't verify, and true for NeedsUpgrade
            // The calling code should detect this and prompt for password reset
            return (IsValid: false, NeedsUpgrade: true);
        }

        try
        {
            // Verify the password first
            bool isValid = BCrypt.Verify(password, hashedPassword);

            if (!isValid)
            {
                return (IsValid: false, NeedsUpgrade: false);
            }

            // Check if the hash needs to be upgraded to a higher work factor
            // BCrypt.Net provides a built-in method to check this
            bool needsRehash = BCrypt.PasswordNeedsRehash(hashedPassword, _workFactor);

            return (IsValid: true, NeedsUpgrade: needsRehash);
        }
        catch (SaltParseException)
        {
            // Invalid BCrypt hash format
            // Could be legacy or corrupted - mark for upgrade
            return (IsValid: false, NeedsUpgrade: true);
        }
        catch (HashInformationException)
        {
            // Malformed hash - mark for upgrade
            return (IsValid: false, NeedsUpgrade: true);
        }
    }

    /// <summary>
    /// Detects if a stored password appears to be in a legacy (non-BCrypt) format.
    /// </summary>
    /// <param name="storedPassword">The stored password/hash to check.</param>
    /// <returns>True if the format appears to be legacy; otherwise, false.</returns>
    /// <remarks>
    /// <para>
    /// MIGRATION: Legacy DNN password formats include:
    /// <list type="bullet">
    ///   <item>PasswordFormat.Clear (0): Plaintext passwords (very rare)</item>
    ///   <item>PasswordFormat.Hashed (1): SHA1/SHA256 hashed (no BCrypt prefix)</item>
    ///   <item>PasswordFormat.Encrypted (2): DES-encrypted Base64 (PortalSecurity.Encrypt)</item>
    /// </list>
    /// </para>
    /// <para>
    /// BCrypt hashes always start with "$2" followed by version ("a", "b", or "y")
    /// and work factor. Legacy formats will not have this prefix.
    /// </para>
    /// <para>
    /// Note: This method cannot definitively determine the legacy format type,
    /// only that it is NOT a valid BCrypt hash. The calling code should handle
    /// legacy passwords by requiring the user to reset their password.
    /// </para>
    /// </remarks>
    private static bool IsLegacyPasswordFormat(string storedPassword)
    {
        if (string.IsNullOrEmpty(storedPassword))
        {
            return false;
        }

        // BCrypt hashes start with $2a$, $2b$, or $2y$ followed by the work factor
        // Valid formats: $2a$XX$, $2b$XX$, $2y$XX$ where XX is the work factor (04-31)
        // If it doesn't match this pattern, it's likely a legacy format
        
        // Quick check: BCrypt hashes are always 60 characters and start with "$2"
        if (storedPassword.Length == 60 && storedPassword.StartsWith("$2", StringComparison.Ordinal))
        {
            // Likely a valid BCrypt hash
            return false;
        }

        // Additional check for valid BCrypt prefix pattern
        // Pattern: $2[aby]$[0-3][0-9]$
        if (storedPassword.Length >= 7 &&
            storedPassword[0] == '$' &&
            storedPassword[1] == '2' &&
            (storedPassword[2] == 'a' || storedPassword[2] == 'b' || storedPassword[2] == 'y') &&
            storedPassword[3] == '$' &&
            char.IsDigit(storedPassword[4]) &&
            char.IsDigit(storedPassword[5]) &&
            storedPassword[6] == '$')
        {
            // Has valid BCrypt prefix structure
            return false;
        }

        // Not a BCrypt hash - likely legacy format
        // MIGRATION NOTE: Legacy DES-encrypted passwords from PortalSecurity.Encrypt
        // would be Base64-encoded strings without the $2$ prefix
        return true;
    }
}
