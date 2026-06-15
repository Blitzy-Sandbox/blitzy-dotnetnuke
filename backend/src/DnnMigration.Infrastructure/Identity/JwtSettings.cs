namespace DnnMigration.Infrastructure.Identity;

/// <summary>
/// Strongly-typed options for JWT access-token issuance and validation, bound from the
/// appsettings.json "Jwt" section via IOptions&lt;JwtSettings&gt;. Lives inside the Infrastructure
/// project so the Application layer never depends on it directly.
/// </summary>
/// <remarks>
/// Consumed by <c>JwtService</c> (token issuance/validation) and configured in the API composition
/// root via <c>builder.Services.Configure&lt;JwtSettings&gt;(builder.Configuration.GetSection("Jwt"))</c>.
/// The JwtBearer <c>TokenValidationParameters</c> MUST be configured from the same
/// <see cref="Issuer"/>/<see cref="Audience"/>/<see cref="Key"/> values (with <c>ClockSkew = TimeSpan.Zero</c>)
/// for issuance and validation to agree. The binding keys are the property names verbatim:
/// <c>Jwt:Issuer</c>, <c>Jwt:Audience</c>, <c>Jwt:Key</c>, <c>Jwt:AccessTokenExpirationMinutes</c>,
/// and <c>Jwt:RefreshTokenExpirationDays</c>.
/// </remarks>
// MIGRATION: New options POCO. The legacy security model (Library/Components/Security/PortalSecurity.vb)
// had no settings class - it relied on web.config machineKey + hard-coded DES key handling. These
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

    /// <summary>Access-token lifetime in minutes. Default 60 (per AAP sections 0.6.2 / 5.2.7.1).</summary>
    public int AccessTokenExpirationMinutes { get; set; } = 60;

    /// <summary>Refresh-token lifetime in days. Default 7 (supports stateless refresh rotation).</summary>
    public int RefreshTokenExpirationDays { get; set; } = 7;
}
