using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using DnnMigration.Application.DTOs;
using DnnMigration.Application.Interfaces;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

// MIGRATION / DOWNSTREAM WIRING NOTE (this file cannot create the files below; recorded here so the
// Api/config agents align on the shared JWT contract):
//   * Api Program.cs: register the options POCO and the service —
//       builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection(JwtSettings.SectionName));
//       builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();
//       builder.Services.AddSingleton<IPasswordHasher, PasswordHasher>();
//     Then AddAuthentication().AddJwtBearer(...) MUST use the SAME Issuer / Audience / SecretKey
//     (SymmetricSecurityKey over the UTF8 bytes of SecretKey), with ValidateLifetime = true,
//     RoleClaimType = ClaimTypes.Role and NameClaimType = ClaimTypes.Name — matching ValidateToken below.
//   * appsettings.json / appsettings.Development.json: a "Jwt" section (JwtSettings.SectionName) with
//     Issuer, Audience, SecretKey (>= 32 chars = 256-bit for HS256), AccessTokenExpirationMinutes,
//     RefreshTokenExpirationDays. The real secret is supplied via environment variable / user-secrets
//     because the legacy DES-encrypted secrets cannot be carried across.
//   * MIGRATION_NOTES.md: refresh-token revocation/logout (legacy PortalSecurity.SignOut, L77) and the
//     forward-hash-on-login credential migration are orchestrated by AuthService, NOT by this token factory.
//   * PACKAGING: System.IdentityModel.Tokens.Jwt + Microsoft.IdentityModel.Tokens resolve TRANSITIVELY today
//     (Microsoft.EntityFrameworkCore.SqlServer 8.0.11 -> Microsoft.Data.SqlClient ->
//     Microsoft.IdentityModel.Protocols.OpenIdConnect 6.35.0). The Infrastructure .csproj SHOULD ideally add
//     an explicit <PackageReference Include="System.IdentityModel.Tokens.Jwt" Version="7.1.2" /> aligned to the
//     Api's JwtBearer 8.0.11 IdentityModel line, to avoid fragile reliance on the EF-transitive path. Only the
//     stable IdentityModel APIs common to 6.35.x / 7.x / 8.x are used below, so either resolution compiles.

namespace DnnMigration.Infrastructure.Identity;

/// <summary>
/// Strongly-typed options for JWT bearer token issuance and validation, bound from the Api host
/// configuration via the Options pattern.
/// </summary>
/// <remarks>
/// MIGRATION: JWT issuer/audience/signing key/lifetimes are bound from the Api host configuration via
/// the Options pattern (never hardcoded); replaces PortalSecurity DES key derivation.
/// <para>
/// This POCO deliberately lives in the <b>Infrastructure</b> layer (not the Api) because the Api
/// composition root references Infrastructure, whereas Infrastructure must never reference the Api. The
/// string properties default to <see cref="string.Empty"/> so a freshly constructed instance satisfies
/// nullable reference-type analysis (no CS8618). <see cref="SectionName"/> is the configuration section
/// (<c>"Jwt"</c>) the Api binds this type to.
/// </para>
/// </remarks>
public sealed class JwtSettings
{
    /// <summary>
    /// Name of the configuration section (<c>"Jwt"</c>) this options type binds to. Used by the Api host
    /// as <c>builder.Configuration.GetSection(JwtSettings.SectionName)</c>.
    /// </summary>
    public const string SectionName = "Jwt";

    /// <summary>
    /// Token issuer (<c>iss</c>) — the authority that mints the token. Must match the value the Api's
    /// JWT bearer handler validates against.
    /// </summary>
    public string Issuer { get; set; } = string.Empty;

    /// <summary>
    /// Intended token audience (<c>aud</c>). Must match the value the Api's JWT bearer handler validates
    /// against.
    /// </summary>
    public string Audience { get; set; } = string.Empty;

    /// <summary>
    /// Symmetric signing key for HMAC-SHA256 (HS256). MUST be at least 32 bytes (256 bits) of UTF-8 text;
    /// the constructor of <see cref="JwtTokenService"/> fails fast if it is shorter or unset. Supplied via
    /// environment variable / user-secrets, never committed to source control.
    /// </summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>
    /// Access-token lifetime in minutes. Kept short (default 15) so that authorization changes take effect
    /// quickly; long-lived sessions are extended by refresh tokens rather than long access tokens.
    /// </summary>
    public int AccessTokenExpirationMinutes { get; set; } = 15;

    /// <summary>
    /// Refresh-token lifetime in days (default 7). Consumed by <c>AuthService</c> when persisting/rotating
    /// refresh tokens; the opaque refresh-token value itself is produced by
    /// <see cref="JwtTokenService.GenerateRefreshToken"/>.
    /// </summary>
    public int RefreshTokenExpirationDays { get; set; } = 7;
}

/// <summary>
/// JWT bearer implementation of <see cref="IJwtTokenService"/>: issues signed HS256 access tokens (both
/// from a <see cref="UserDto"/> and from an explicit claim set), produces cryptographically-random opaque
/// refresh tokens, and validates incoming tokens into a <see cref="ClaimsPrincipal"/>.
/// </summary>
/// <remarks>
/// MIGRATION: replaces FormsAuthentication ticket issuance and PortalSecurity DES crypto with stateless
/// JWT bearer tokens.
/// <para>
/// The type is stateless (its only fields are the immutable, injected <see cref="JwtSettings"/> and a
/// reusable <see cref="JwtSecurityTokenHandler"/>), performs no I/O, and is therefore thread-safe and safe
/// to register as a dependency-injection <b>singleton</b> by the Api composition root. Statelessness is the
/// whole point of the migration: there is no server-side session to invalidate, which is why logout is a
/// refresh-token concern handled upstream by <c>AuthService</c> rather than by this token factory.
/// </para>
/// </remarks>
public sealed class JwtTokenService : IJwtTokenService
{
    // MIGRATION: replaces FormsAuthentication ticket issuance and PortalSecurity DES crypto with stateless JWT bearer tokens.

    /// <summary>Immutable JWT options (issuer, audience, signing key, lifetimes) resolved once at construction.</summary>
    private readonly JwtSettings _settings;

    /// <summary>Reusable, thread-safe handler used to both write and validate tokens.</summary>
    private readonly JwtSecurityTokenHandler _tokenHandler;

    /// <summary>
    /// Initializes the service from injected <see cref="JwtSettings"/> and validates fail-fast that a
    /// usable HS256 signing key is configured.
    /// </summary>
    /// <param name="options">The bound <see cref="JwtSettings"/> options (via the Options pattern).</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <see cref="JwtSettings.SecretKey"/> is missing/whitespace or shorter than 32 bytes
    /// (256 bits), which HMAC-SHA256 requires.
    /// </exception>
    public JwtTokenService(IOptions<JwtSettings> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _settings = options.Value;

        if (string.IsNullOrWhiteSpace(_settings.SecretKey)
            || Encoding.UTF8.GetByteCount(_settings.SecretKey) < 32)
        {
            throw new InvalidOperationException(
                "JWT SecretKey must be configured and at least 32 bytes (256 bits) for HS256.");
        }

        // MapInboundClaims=false keeps claim types exactly as issued (no legacy short->long URI remap).
        _tokenHandler = new JwtSecurityTokenHandler { MapInboundClaims = false };
    }

    /// <summary>
    /// Creates a signed access token for the given user and roles, returning the serialized token together
    /// with its absolute UTC expiry.
    /// </summary>
    /// <param name="user">The user the token represents. Must not be <see langword="null"/>.</param>
    /// <param name="roles">
    /// The roles to embed as role claims. This is used <b>instead of</b> <see cref="UserDto.Roles"/> per the
    /// interface contract, so callers may supply an authoritative, freshly-resolved role set.
    /// </param>
    /// <returns>A tuple of the serialized JWT and the UTC instant at which it expires.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="user"/> is <see langword="null"/>.</exception>
    public (string Token, DateTime ExpiresAt) CreateToken(UserDto user, IEnumerable<string> roles)
    {
        ArgumentNullException.ThrowIfNull(user);
        var claims = BuildClaims(user, roles);
        var expiresAt = DateTime.UtcNow.AddMinutes(_settings.AccessTokenExpirationMinutes);
        var token = WriteToken(claims, expiresAt);
        return (token, expiresAt);
    }

    /// <summary>
    /// Generates a signed access token from an explicit claim set (low-level path; used to re-issue a token
    /// during refresh from claims that already exist on a validated principal).
    /// </summary>
    /// <param name="claims">The claims to embed. Must not be <see langword="null"/>.</param>
    /// <returns>The serialized JWT.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="claims"/> is <see langword="null"/>.</exception>
    public string GenerateAccessToken(IEnumerable<Claim> claims)
    {
        ArgumentNullException.ThrowIfNull(claims);
        var expiresAt = DateTime.UtcNow.AddMinutes(_settings.AccessTokenExpirationMinutes);
        return WriteToken(claims, expiresAt);
    }

    /// <summary>
    /// Single source of token construction shared by <see cref="CreateToken"/> and
    /// <see cref="GenerateAccessToken"/>, so the returned <c>ExpiresAt</c> equals the token's <c>exp</c>.
    /// The caller computes <paramref name="expiresAt"/> once and passes it in.
    /// </summary>
    /// <param name="claims">The claims to embed in the token.</param>
    /// <param name="expiresAt">The absolute UTC expiry to stamp on the token.</param>
    /// <returns>The serialized JWT.</returns>
    /// <remarks>
    /// The JWT <c>exp</c> claim is stored as whole Unix seconds, so the token's effective expiry is
    /// <paramref name="expiresAt"/> truncated to the second; the value returned to callers is derived from
    /// the same instant (there is no second <see cref="DateTime.UtcNow"/> read), so there is no drift.
    /// </remarks>
    private string WriteToken(IEnumerable<Claim> claims, DateTime expiresAt)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.SecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var jwt = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAt,
            signingCredentials: credentials);
        return _tokenHandler.WriteToken(jwt);
    }

    /// <summary>
    /// Projects a <see cref="UserDto"/> and its roles into the JWT claim set. Uses explicit
    /// <see cref="ClaimTypes"/> URIs so claims round-trip identically regardless of inbound-claim mapping,
    /// and so they align with the Api's default <c>NameClaimType</c>/<c>RoleClaimType</c>.
    /// </summary>
    /// <param name="user">The user to project.</param>
    /// <param name="roles">The roles to embed (may be <see langword="null"/>; null/whitespace entries are skipped).</param>
    /// <returns>The claims to embed in the token.</returns>
    /// <remarks>
    /// Design rationale: explicit <see cref="ClaimTypes.NameIdentifier"/>/<see cref="ClaimTypes.Name"/>/
    /// <see cref="ClaimTypes.Email"/>/<see cref="ClaimTypes.Role"/> serialize as full URIs that round-trip
    /// identically regardless of <c>MapInboundClaims</c>, and align with the Api's default
    /// <c>NameClaimType = ClaimTypes.Name</c> / <c>RoleClaimType = ClaimTypes.Role</c>, so
    /// <c>[Authorize(Roles = ...)]</c> and <c>User.Identity.Name</c> work downstream.
    /// <see cref="CultureInfo.InvariantCulture"/> avoids locale-dependent number formatting (and the
    /// corresponding globalization analyzer warning under <c>--warnaserror</c>).
    /// </remarks>
    private static IEnumerable<Claim> BuildClaims(UserDto user, IEnumerable<string> roles)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserID.ToString(CultureInfo.InvariantCulture)),
            new(ClaimTypes.Name, user.Username ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("portalId", user.PortalID.ToString(CultureInfo.InvariantCulture)),
            new("isSuperUser", user.IsSuperUser ? "true" : "false")
        };

        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            claims.Add(new Claim(ClaimTypes.Email, user.Email));
        }

        if (!string.IsNullOrWhiteSpace(user.DisplayName))
        {
            claims.Add(new Claim("displayName", user.DisplayName));
        }

        if (roles is not null)
        {
            foreach (var role in roles)
            {
                if (!string.IsNullOrWhiteSpace(role))
                {
                    claims.Add(new Claim(ClaimTypes.Role, role));
                }
            }
        }

        return claims;
    }

    /// <summary>
    /// Generates a cryptographically-random opaque refresh token (512 bits of entropy, Base64-encoded).
    /// </summary>
    /// <returns>A Base64 string suitable for use as an opaque refresh-token value.</returns>
    public string GenerateRefreshToken()
    {
        // MIGRATION: replaces PortalSecurity.CreateKey (RNGCryptoServiceProvider.GetBytes, L564) +
        // BytesToHexString (L585). RNGCryptoServiceProvider is obsolete in .NET 8; use
        // RandomNumberGenerator.GetBytes. Emits a Base64 opaque token (legacy emitted hex); both are
        // opaque cryptographically-random strings, so the change is cosmetic.
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    }

    /// <summary>
    /// Validates a token against the configured issuer/audience/signing-key/lifetime and returns the
    /// resulting principal, or <see langword="null"/> if the token is missing, malformed, expired, or
    /// tampered with. Never throws.
    /// </summary>
    /// <param name="token">The serialized JWT to validate.</param>
    /// <returns>The validated <see cref="ClaimsPrincipal"/>, or <see langword="null"/> on any failure.</returns>
    public ClaimsPrincipal? ValidateToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _settings.Issuer,
            ValidateAudience = true,
            ValidAudience = _settings.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.SecretKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            RoleClaimType = ClaimTypes.Role,
            NameClaimType = ClaimTypes.Name
        };

        try
        {
            return _tokenHandler.ValidateToken(token, validationParameters, out _);
        }
        catch
        {
            // MIGRATION: invalid/expired/tampered token -> null (replaces exception/redirect-based
            // FormsAuthentication failure). A catch-all is intentional: any IdentityModel validation
            // exception means "not authenticated".
            return null;
        }
    }
}
