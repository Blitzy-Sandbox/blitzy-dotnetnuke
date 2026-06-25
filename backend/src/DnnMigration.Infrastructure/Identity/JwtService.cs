using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using DnnMigration.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace DnnMigration.Infrastructure.Identity;

// MIGRATION: Replaces AspNetSqlMembershipProvider + Forms-auth ticket with JWT issuance (60-min access token +
// refresh rotation, AAP 0.7.6). Library/Components/Users/Membership/UserMembership.vb was a pure membership
// DATA HOLDER (no logic; its lifecycle fields are flattened onto the User domain entity), so this is a FRESH JWT
// issuer, NOT a 1:1 port. Logout maps from PortalSecurity.SignOut (L77); the cryptographically-random refresh
// token mirrors the intent of PortalSecurity.CreateKey (L564, RNG).
//
// Implements DnnMigration.Application.Interfaces.IJwtService (the contract is declared in the Application
// project and consumed by Application/Services/AuthService.cs). This Infrastructure type is the concrete
// issuer; it deliberately does NOT declare the interface here (that would create a reverse dependency and
// break AuthService). The class is sealed because it is a leaf service with no intended inheritance.
public sealed class JwtService : IJwtService
{
    private readonly string _issuer;
    private readonly string _audience;
    private readonly string _signingKey;
    private readonly int _accessTokenMinutes;

    // MIGRATION: in-memory refresh-token store (token -> userId, or null when no userId is associated). Because
    // JwtService is DI-registered as a SINGLETON (../DependencyInjection.cs), this store survives across requests.
    // ConcurrentDictionary is thread-safe. If a DB-backed store is introduced later, switch the registration to Scoped.
    private readonly ConcurrentDictionary<string, int?> _refreshTokens = new();

    public JwtService(IConfiguration configuration)
    {
        // MIGRATION: bound directly from IConfiguration (the DI setup does not call Configure<JwtOptions>(), so an
        // IOptions<JwtOptions> would bind to empty/default values). IConfiguration is a registered singleton, which
        // matches this service's singleton lifetime (no captive-dependency issue). The Api layer's appsettings.json
        // "Jwt" section and Program.cs token-validation parameters read the SAME keys, keeping issuer/audience/key
        // consistent between issuance (here) and validation (the JWT Bearer handler).
        _issuer = configuration["Jwt:Issuer"] ?? string.Empty;
        _audience = configuration["Jwt:Audience"] ?? string.Empty;
        _signingKey = configuration["Jwt:SigningKey"] ?? string.Empty;

        // MIGRATION: 60-minute default access-token lifetime (AAP 0.7.6) when the key is missing or unparseable.
        // Uses int.TryParse over the IConfiguration indexer (only requires Microsoft.Extensions.Configuration
        // .Abstractions, which the parent .csproj references) rather than the GetValue<T> binder extension.
        _accessTokenMinutes = int.TryParse(configuration["Jwt:AccessTokenMinutes"], out var minutes) ? minutes : 60;
    }

    // Issues a signed HMAC-SHA256 JWT access token carrying the caller's identity and authorization claims.
    // The tuple element NAMES below are intentionally identical to the IJwtService contract
    // (string AccessToken, DateTime ExpiresAtUtc, int ExpiresInSeconds) to avoid CS8141 (tuple-name mismatch).
    public (string AccessToken, DateTime ExpiresAtUtc, int ExpiresInSeconds) GenerateAccessToken(
        int userId, string username, int portalId, bool isSuperUser, IEnumerable<string> roles)
    {
        if (string.IsNullOrEmpty(_signingKey))
        {
            // Fail fast with a clear, non-sensitive message. NEVER include the key value in the message/logs (0.7.6).
            throw new InvalidOperationException("JWT signing key is not configured (Jwt:SigningKey).");
        }

        var claims = new List<Claim>
        {
            // sub -> ClaimTypes.NameIdentifier under the default JwtBearer inbound mapping (userId), so Api
            // controllers can read the user id via User.FindFirst(ClaimTypes.NameIdentifier).
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            // unique_name -> ClaimTypes.Name under the default inbound mapping (username).
            new Claim(JwtRegisteredClaimNames.UniqueName, username),
            // Custom claims preserve the DNN multi-tenant + super-user model (portal scoping / host elevation).
            new Claim("portalId", portalId.ToString()),
            new Claim("isSuperUser", isSuperUser.ToString()),
            // jti -> unique token identifier (supports auditing / future deny-listing).
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        // MIGRATION: one ClaimTypes.Role claim per role preserves the legacy user -> role -> permission model
        // (the roles formerly carried by DNN's portalroles cookie are now signed into the bearer token).
        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_signingKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        // Capture a single UTC instant so notBefore and expires are derived from the same reference time.
        var now = DateTime.UtcNow;
        var expiresAtUtc = now.AddMinutes(_accessTokenMinutes);
        var expiresInSeconds = _accessTokenMinutes * 60;

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            notBefore: now,
            expires: expiresAtUtc,
            signingCredentials: credentials);

        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);
        return (accessToken, expiresAtUtc, expiresInSeconds);
    }

    public string GenerateRefreshToken()
    {
        // MIGRATION: cryptographically-random opaque token (mirrors PortalSecurity.CreateKey RNG intent, L564:
        // RNGCryptoServiceProvider.GetBytes). No userId association on this no-arg path, so ValidateRefreshToken
        // returns null for it (the documented fail-closed gap in MIGRATION_NOTES.md). RandomNumberGenerator
        // .GetBytes is the modern .NET 8 replacement for the legacy RNGCryptoServiceProvider.
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        _refreshTokens[token] = null;
        return token;
    }

    // HEDGE additive overload (not strictly required by the current AuthService call sites): associates a real
    // userId so ValidateRefreshToken can return it. Satisfies the interface if it declares this overload; otherwise
    // it is simply an extra public method for later use (the userId-aware path that closes the fail-closed gap).
    // Additive and unambiguous (distinct arity from the no-arg overload).
    public string GenerateRefreshToken(int userId)
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        _refreshTokens[token] = userId;
        return token;
    }

    public int? ValidateRefreshToken(string refreshToken)
    {
        // MIGRATION: fail-closed — unknown tokens, or tokens stored with no associated userId, return null.
        if (string.IsNullOrEmpty(refreshToken))
        {
            return null;
        }

        if (_refreshTokens.TryGetValue(refreshToken, out var userId) && userId.HasValue)
        {
            return userId;
        }

        return null;
    }

    public void RevokeRefreshToken(string refreshToken)
    {
        // MIGRATION: replaces FormsAuthentication.SignOut (PortalSecurity.vb L77). A stateless JWT access token
        // cannot be server-invalidated, so "logout" = server-side refresh-token revocation (remove from store).
        if (string.IsNullOrEmpty(refreshToken))
        {
            return;
        }

        _refreshTokens.TryRemove(refreshToken, out _);
    }
}
