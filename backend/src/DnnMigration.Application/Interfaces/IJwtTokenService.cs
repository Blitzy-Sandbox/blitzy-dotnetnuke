using System.Security.Claims;
using DnnMigration.Application.DTOs;

namespace DnnMigration.Application.Interfaces;

/// <summary>
/// Port for issuing and validating JWT access/refresh tokens. Implemented in
/// <c>DnnMigration.Infrastructure.Identity</c> (dependency inversion keeps the Application layer
/// free of Infrastructure references) and consumed by <c>AuthService</c>.
/// </summary>
// MIGRATION: replaces the legacy DotNetNuke FormsAuthentication ticket issuance and
// PortalSecurity.CreateKey (Library/Components/Security/PortalSecurity.vb L564) with stateless JWT
// bearer tokens. Methods are synchronous (in-memory crypto, no I/O). Signing key/issuer/audience/
// lifetimes are supplied to the concrete implementation via the Api host Options pattern (not here).
public interface IJwtTokenService
{
    /// <summary>Creates a signed access token for a user with the given roles, and its absolute UTC expiry.</summary>
    (string Token, DateTime ExpiresAt) CreateToken(UserDto user, IEnumerable<string> roles);

    /// <summary>Generates a signed access token from an explicit claim set.</summary>
    string GenerateAccessToken(IEnumerable<Claim> claims);

    /// <summary>Generates a cryptographically-random opaque refresh token.</summary>
    string GenerateRefreshToken();

    /// <summary>Validates a token and returns its principal, or <c>null</c> if the token is invalid/expired.</summary>
    ClaimsPrincipal? ValidateToken(string token);
}
