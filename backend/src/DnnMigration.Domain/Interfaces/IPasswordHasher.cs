// -----------------------------------------------------------------------------
// <copyright file="IPasswordHasher.cs" company="DnnMigration">
//   Copyright (c) DnnMigration. All rights reserved.
//   Licensed under the MIT License.
// </copyright>
// <summary>
//   Interface for password hashing operations, providing secure password storage and verification.
//   MIGRATION: Replaces legacy DES-based encryption from PortalSecurity.vb with modern BCrypt algorithm.
// </summary>
// -----------------------------------------------------------------------------

namespace DnnMigration.Domain.Interfaces;

/// <summary>
/// Interface for password hashing operations, providing secure password storage and verification.
/// </summary>
/// <remarks>
/// <para>
/// MIGRATION: This interface replaces the legacy DES-based Encrypt/Decrypt methods from
/// DotNetNuke.Security.PortalSecurity (lines 138-173 in PortalSecurity.vb).
/// </para>
/// <para>
/// The legacy implementation used DESCryptoServiceProvider with symmetric encryption,
/// which is fundamentally unsuitable for password storage. This new implementation uses
/// BCrypt, which is designed specifically for secure password hashing with:
/// <list type="bullet">
///   <item>Automatic salt generation for each password</item>
///   <item>Configurable work factor to slow down brute force attacks</item>
///   <item>One-way hashing (cannot be reversed, unlike encryption)</item>
/// </list>
/// </para>
/// </remarks>
public interface IPasswordHasher
{
    /// <summary>
    /// Hashes a password using BCrypt with automatic salt generation.
    /// </summary>
    /// <param name="password">The plaintext password to hash.</param>
    /// <returns>The BCrypt hashed password in the format $2a$[workFactor]$[salt+hash].</returns>
    /// <exception cref="ArgumentException">Thrown when password is null or empty.</exception>
    /// <remarks>
    /// MIGRATION: Replaces legacy DES encryption from PortalSecurity.Encrypt method.
    /// The legacy method used symmetric encryption with a shared key, which could be decrypted.
    /// BCrypt produces a one-way hash that cannot be reversed.
    /// </remarks>
    string HashPassword(string password);

    /// <summary>
    /// Verifies a password against a stored BCrypt hash.
    /// </summary>
    /// <param name="password">The plaintext password to verify.</param>
    /// <param name="hashedPassword">The stored BCrypt hash to verify against.</param>
    /// <returns>True if the password matches the hash; otherwise, false.</returns>
    /// <remarks>
    /// <para>
    /// This method uses constant-time comparison to prevent timing attacks.
    /// Invalid hash formats will return false instead of throwing exceptions.
    /// </para>
    /// <para>
    /// MIGRATION: This replaces the legacy pattern of decrypting stored passwords
    /// and comparing plaintext values. BCrypt verification compares hashes securely.
    /// </para>
    /// </remarks>
    bool VerifyPassword(string password, string hashedPassword);

    /// <summary>
    /// Verifies a password and determines if the hash needs to be upgraded.
    /// </summary>
    /// <param name="password">The plaintext password to verify.</param>
    /// <param name="hashedPassword">The stored hash to verify against.</param>
    /// <returns>
    /// A tuple containing:
    /// <list type="bullet">
    ///   <item><c>IsValid</c> - True if the password matches the hash.</item>
    ///   <item><c>NeedsUpgrade</c> - True if the hash should be rehashed with current settings.</item>
    /// </list>
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method supports gradual migration of password hashes when:
    /// <list type="bullet">
    ///   <item>The work factor has been increased for better security</item>
    ///   <item>Legacy password formats are detected (requires separate handling)</item>
    /// </list>
    /// </para>
    /// <para>
    /// MIGRATION NOTE: Legacy DES-encrypted passwords cannot be verified by BCrypt.
    /// These would need to be detected separately and users prompted to reset passwords.
    /// Legacy passwords from PasswordFormat.Encrypted (value 2) used Base64-encoded
    /// DES encryption and cannot be converted without the original encryption key.
    /// </para>
    /// </remarks>
    (bool IsValid, bool NeedsUpgrade) VerifyAndUpgradeHash(string password, string hashedPassword);
}
