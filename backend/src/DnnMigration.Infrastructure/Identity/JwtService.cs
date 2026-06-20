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
/// Issues HMAC-SHA256 signed access tokens carrying identity and role claims,
/// generates cryptographically-random opaque refresh tokens, and validates
/// inbound tokens, returning <c>null</c> (never throwing) on any failure.
/// </summary>
// MIGRATION: Replaces the legacy cookie-based ASP.NET Forms Authentication model from
// Library/Components/Security/PortalSecurity.vb (FormsAuthentication.SignOut() at L79; SignOut()
// cookie-expiry routine at L77-L95) with STATELESS JWT Bearer access tokens + opaque refresh-token
// rotation. No server-side session is retained, enabling horizontal scaling. Documented in root MIGRATION_NOTES.md.
public sealed class JwtService : IJwtService
{
    private readonly JwtSettings _settings;

    /// <summary>
    /// Initializes a new instance of the <see cref="JwtService"/> class.
    /// </summary>
    /// <param name="options">The bound <see cref="JwtSettings"/> options (issuer, audience, signing key, lifetime).</param>
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
        // replacing the legacy "portalroles" cookie populated by PortalSecurity.
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
        // replacing legacy Forms Authentication ticket renewal.
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
