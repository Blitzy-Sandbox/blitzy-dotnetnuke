using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using DnnMigration.Application.Interfaces;
using DnnMigration.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace DnnMigration.Infrastructure.Identity;

/// <summary>
/// Stateless JWT Bearer implementation of <see cref="IJwtService"/>.
/// </summary>
/// <remarks>
/// Issues HMAC-SHA256 signed access tokens carrying identity and role claims, mints
/// cryptographically random opaque refresh tokens, and validates inbound tokens against the
/// configured issuer/audience/signing-key. The signing key (<c>JwtSettings.Key</c>) MUST be at
/// least 32 characters (256 bits); a shorter key causes <see cref="SigningCredentials"/> to throw
/// <c>IDX10653</c> at issuance time. The type holds no token state of its own — refresh tokens are
/// opaque values rotated and persisted by the Application-layer <c>AuthService</c>, not here — so the
/// implementation is stateless and thread-safe, enabling horizontal scaling.
/// </remarks>
// MIGRATION: Replaces the legacy cookie-based ASP.NET Forms Authentication model from
// Library/Components/Security/PortalSecurity.vb (FormsAuthentication.SignOut() at L79; SignOut()
// cookie-expiry routine at L77-L95) with STATELESS JWT Bearer access tokens + opaque refresh-token
// rotation. No server-side session is retained, enabling horizontal scaling. Documented in root MIGRATION_NOTES.md.
public sealed class JwtService : IJwtService
{
    private readonly JwtSettings _settings;

    /// <summary>
    /// Initializes a new <see cref="JwtService"/> bound to the configured <see cref="JwtSettings"/>
    /// (the "Jwt" section of configuration, registered via <c>Configure&lt;JwtSettings&gt;</c>).
    /// </summary>
    /// <param name="options">The strongly-typed JWT options snapshot.</param>
    public JwtService(IOptions<JwtSettings> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _settings = options.Value;
    }

    /// <inheritdoc />
    public int AccessTokenExpirationMinutes => _settings.AccessTokenExpirationMinutes;

    /// <inheritdoc />
    public string GenerateAccessToken(User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        // Identity claims. Sub and an explicit NameIdentifier both carry the UserID so the value can be
        // extracted robustly regardless of the consumer's inbound claim-type mapping; Jti gives each token a
        // unique id; Name/Email carry the username/email (guarded against null POCO values); IsSuperUser is a
        // custom flag. UserID is formatted with the invariant culture for a stable, locale-independent value.
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.UserID.ToString(CultureInfo.InvariantCulture)),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.NameIdentifier, user.UserID.ToString(CultureInfo.InvariantCulture)),
            new(ClaimTypes.Name, user.Username ?? string.Empty),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new("IsSuperUser", user.IsSuperUser.ToString())
        };

        // MIGRATION: role claims sourced from User.Roles (EF-ignored, populated by the service layer),
        // replacing the legacy "portalroles" cookie populated by PortalSecurity. One ClaimTypes.Role per role
        // so [Authorize(Roles=...)] and ClaimsPrincipal.IsInRole(...) work as expected on the Api side.
        if (user.Roles is not null)
        {
            foreach (var role in user.Roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }
        }

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Key));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
        var now = DateTime.UtcNow;

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            notBefore: now,
            expires: now.AddMinutes(_settings.AccessTokenExpirationMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <inheritdoc />
    public string GenerateRefreshToken()
    {
        // MIGRATION: cryptographically-random opaque refresh token enabling stateless rotation,
        // replacing legacy Forms Authentication ticket renewal. 64 random bytes (512 bits) of entropy,
        // Base64-encoded; persistence/rotation is the AuthService's concern, not this token factory's.
        var randomBytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(randomBytes);
    }

    /// <inheritdoc />
    public ClaimsPrincipal? ValidateToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        // ClockSkew = TimeSpan.Zero makes the configured lifetime exact (no default 5-minute grace).
        // The Api's JwtBearer TokenValidationParameters must mirror these same values for consistency.
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _settings.Issuer,
            ValidateAudience = true,
            ValidAudience = _settings.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Key)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };

        try
        {
            return new JwtSecurityTokenHandler()
                .ValidateToken(token, validationParameters, out _);
        }
        catch (Exception)
        {
            // MIGRATION: return null on any invalid/expired/tampered token instead of throwing to callers,
            // mirroring the legacy Decrypt() which swallowed crypto errors (PortalSecurity.vb:L216-L218).
            return null;
        }
    }
}
