namespace DnnMigration.Infrastructure.Identity;

/// <summary>
/// Strongly-typed options for JWT access-token issuance and validation, bound from the
/// appsettings.json "Jwt" section via IOptions&lt;JwtSettings&gt;. Lives inside the Infrastructure
/// project so the Application layer never depends on it directly; consumers in the Application
/// layer read the token lifetime through the <c>IJwtService</c> contract instead of this type.
/// </summary>
// MIGRATION: New options POCO. The legacy security model (Library/Components/Security/PortalSecurity.vb)
// had no settings class — it relied on web.config machineKey + hard-coded DES key handling. These
// values now drive stateless JWT Bearer issuance/validation. Documented in root MIGRATION_NOTES.md.
public sealed class JwtSettings
{
    /// <summary>Token issuer. Default "DnnMigration".</summary>
    public string Issuer { get; set; } = "DnnMigration";

    /// <summary>Token audience. Default "DnnMigration".</summary>
    public string Audience { get; set; } = "DnnMigration";

    /// <summary>
    /// HMAC-SHA256 signing secret. MUST be at least 32 characters (256 bits); a shorter key
    /// causes SigningCredentials to throw IDX10653 at token-generation time.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Access-token lifetime in minutes. Default 60 (per AAP §0.6.2 / §5.2.7.1).</summary>
    public int AccessTokenExpirationMinutes { get; set; } = 60;

    /// <summary>Refresh-token lifetime in days. Default 7 (supports stateless refresh rotation).</summary>
    public int RefreshTokenExpirationDays { get; set; } = 7;
}
