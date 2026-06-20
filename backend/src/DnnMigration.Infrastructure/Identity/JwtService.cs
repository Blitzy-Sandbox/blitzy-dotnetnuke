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
/// Issues HMAC-SHA256 signed access tokens carrying identity and role claims, issues HMAC-SHA256 signed
/// refresh tokens (carrying the subject + a <c>token_use=refresh</c> claim) for stateless rotation, and
/// validates inbound tokens, returning <c>null</c> (never throwing) on any failure.
/// </summary>
// MIGRATION: Replaces the legacy cookie-based ASP.NET Forms Authentication model from
// Library/Components/Security/PortalSecurity.vb (FormsAuthentication.SignOut() at L79; SignOut()
// cookie-expiry routine at L77-L95) with STATELESS JWT Bearer access tokens + signed-JWT refresh-token
// rotation (DEV-032). No server-side session is retained, enabling horizontal scaling. Documented in root MIGRATION_NOTES.md.
public sealed class JwtService : IJwtService
{
    private readonly JwtSettings _settings;

    /// <summary>
    /// Minimum signing-key length in characters. HMAC-SHA256 requires a 256-bit (32-byte) key; a shorter
    /// key throws an opaque IDX10653 deep inside token issuance, so it is rejected fail-fast at construction.
    /// </summary>
    private const int MinimumKeyLength = 32;

    // MIGRATION (DEV-032): "token_use" distinguishes an access token from a refresh token so the refresh
    // endpoint can reject an access token presented as a refresh token (token-type confusion). The matching
    // literals are read by AuthService.RefreshAsync (Application layer cannot reference this Infrastructure type).
    private const string TokenUseClaim = "token_use";
    private const string TokenUseAccess = "access";
    private const string TokenUseRefresh = "refresh";

    /// <summary>
    /// Initializes a new instance of the <see cref="JwtService"/> class.
    /// </summary>
    /// <param name="options">The bound <see cref="JwtSettings"/> options (issuer, audience, signing key, lifetime).</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the configured signing key (<c>Jwt:Key</c>) is null, empty, or shorter than
    /// <see cref="MinimumKeyLength"/> characters.
    /// </exception>
    public JwtService(IOptions<JwtSettings> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _settings = options.Value;

        // MIGRATION (M1 / DEV-035): fail-fast configuration validation. The AAP/checkpoint requires the JWT
        // signing key to be at least 32 characters (256 bits) for HMAC-SHA256. JwtSettings only DOCUMENTED
        // this and the base appsettings ships an empty placeholder, so a missing/short key would otherwise
        // fail at the first login with an opaque IDX10653 rather than a clear configuration error. Validating
        // once at construction surfaces a misconfigured Jwt:Key immediately for every JwtService consumer.
        // (Program.cs adds AddOptions().Validate(...) when it enters scope; this guard is the authoritative
        // enforcement that does not depend on the composition root being wired.)
        if (string.IsNullOrWhiteSpace(_settings.Key) || _settings.Key.Length < MinimumKeyLength)
        {
            throw new InvalidOperationException(
                $"JWT signing key (Jwt:Key) must be configured with at least {MinimumKeyLength} characters " +
                "(256 bits) for HMAC-SHA256. Set the 'Jwt:Key' configuration value (e.g., the Jwt__Key " +
                "environment variable) to a sufficiently long secret.");
        }
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
            // MIGRATION (DEV-032): mark this as an access token so it cannot be replayed as a refresh token.
            new(TokenUseClaim, TokenUseAccess),
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
    public string GenerateRefreshToken(User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        // MIGRATION (C6 / DEV-032): the refresh token is now a SIGNED JWT, not an opaque random string.
        // The previous opaque Base64 token could never validate through ValidateToken (which only accepts
        // signed JWTs), so /api/auth/refresh and the frontend 401-recovery flow were permanently broken.
        // Because Phase-1 retains NO server-side refresh-token store (AAP §0.6.2 — stateless rotation),
        // validity must be established cryptographically: the refresh token is signed with the same HMAC-SHA256
        // key/issuer/audience as the access token so the existing ValidateToken pipeline accepts it and
        // RefreshAsync can resolve the subject from its claims. It carries the user id (sub + NameIdentifier),
        // a unique jti (rotation identity), and token_use=refresh (so an access token cannot be replayed here),
        // and lives for RefreshTokenExpirationDays (longer than the access token).
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.UserID.ToString(CultureInfo.InvariantCulture)),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(TokenUseClaim, TokenUseRefresh),
            new(ClaimTypes.NameIdentifier, user.UserID.ToString(CultureInfo.InvariantCulture))
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
