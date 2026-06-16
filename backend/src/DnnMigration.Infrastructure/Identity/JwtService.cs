using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
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
/// Issues HMAC-SHA256 signed access tokens carrying identity and role claims, issues signed JWT
/// refresh tokens (token_type=refresh, longer lifetime) that <see cref="ValidateToken"/> can validate,
/// and validates inbound tokens against the configured issuer/audience/signing-key. The signing key
/// (<c>JwtSettings.Key</c>) MUST be at least 32 characters / 256 bits; the constructor fails fast with a
/// clear <see cref="InvalidOperationException"/> if it is missing or too short, rather than deferring to
/// the opaque <c>IDX10653</c> that <see cref="SigningCredentials"/> would otherwise throw at first
/// issuance. The type holds no token state of its own — refresh tokens are self-contained signed JWTs
/// rotated by the Application-layer <c>AuthService</c>, not persisted here — so the implementation is
/// stateless and thread-safe, enabling horizontal scaling.
/// </remarks>
// MIGRATION: Replaces the legacy cookie-based ASP.NET Forms Authentication model from
// Library/Components/Security/PortalSecurity.vb (FormsAuthentication.SignOut() at L79; SignOut()
// cookie-expiry routine at L77-L95) with STATELESS JWT Bearer access tokens + signed JWT refresh-token
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

        // MIGRATION (CP2 hardening): fail fast with a clear configuration error when the HMAC-SHA256
        // signing key is missing or shorter than 32 bytes (256 bits). Without this guard, a default/empty
        // or too-short Jwt:Key surfaces only as the opaque IDX10653 thrown by SymmetricSecurityKey at the
        // FIRST token issuance (a runtime 500), instead of at construction/startup. Validating here means a
        // misconfigured key is rejected when the service is composed in the DI container.
        if (string.IsNullOrEmpty(_settings.Key) || Encoding.UTF8.GetByteCount(_settings.Key) < 32)
        {
            throw new InvalidOperationException(
                "JWT signing key (Jwt:Key) must be configured with at least 32 characters (256 bits) for HMAC-SHA256 token signing.");
        }
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
            new("IsSuperUser", user.IsSuperUser.ToString()),
            // MIGRATION (CP2 auth-chain fix): mark token kind so the refresh endpoint can reject an access
            // token replayed as a refresh token (token-type confusion).
            new(IJwtService.TokenTypeClaim, IJwtService.AccessTokenType)
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
    public string GenerateRefreshToken(User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        // MIGRATION (CP2 auth-chain fix): the refresh token is now a SIGNED JWT (not an opaque random
        // string), so the Application-layer AuthService.RefreshAsync can validate it via ValidateToken — the
        // previous opaque token could NEVER be validated, which broke /api/auth/refresh and the frontend
        // 401-recovery flow. It carries the user subject (so the refresh endpoint resolves the user), a
        // unique Jti, a token_type=refresh marker (so an access token cannot be replayed as a refresh token),
        // and a longer lifetime (JwtSettings.RefreshTokenExpirationDays). It is signed with the SAME
        // key/issuer/audience as the access token so the existing TokenValidationParameters validate it
        // unchanged. Rotation remains the AuthService's concern; no server-side store is used (stateless).
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.UserID.ToString(CultureInfo.InvariantCulture)),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.NameIdentifier, user.UserID.ToString(CultureInfo.InvariantCulture)),
            new(IJwtService.TokenTypeClaim, IJwtService.RefreshTokenType)
        };

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Key));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
        var now = DateTime.UtcNow;

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            notBefore: now,
            expires: now.AddDays(_settings.RefreshTokenExpirationDays),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
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
