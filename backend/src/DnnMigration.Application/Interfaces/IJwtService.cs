using System.Security.Claims;
using DnnMigration.Domain.Entities;

namespace DnnMigration.Application.Interfaces;

/// <summary>
/// JWT token abstraction. MIGRATION: replaces legacy ASP.NET Forms Authentication
/// (PortalSecurity.vb / FormsAuthentication) with stateless JWT Bearer tokens plus
/// refresh-token rotation. Implemented by Infrastructure/Identity/JwtService.cs.
/// </summary>
public interface IJwtService
{
    /// <summary>
    /// Issues a signed JWT access token for the supplied user, including identity and
    /// role claims. Role claims are emitted from <see cref="User.Roles"/> (populated by
    /// the service layer prior to this call).
    /// </summary>
    string GenerateAccessToken(User user);

    /// <summary>
    /// Issues a signed JWT refresh token for the supplied user, carrying the subject id and a
    /// <c>token_use=refresh</c> claim. MIGRATION (DEV-032): refresh tokens are signed JWTs (not opaque
    /// strings) so the stateless <see cref="ValidateToken"/> pipeline can validate them and resolve the
    /// subject — Phase 1 retains no server-side refresh-token store (AAP §0.6.2). The token's lifetime is
    /// the configured refresh-token expiration (longer than the access token).
    /// </summary>
    string GenerateRefreshToken(User user);

    /// <summary>
    /// Validates the supplied JWT and returns its <see cref="ClaimsPrincipal"/>, or
    /// <c>null</c> if the token is invalid, expired, or tampered with.
    /// </summary>
    ClaimsPrincipal? ValidateToken(string token);

    /// <summary>
    /// The configured access-token lifetime in minutes (AAP: 60). Used to populate the
    /// expiry fields on the auth response without binding the Application layer to the
    /// Infrastructure JwtSettings options type.
    /// </summary>
    int AccessTokenExpirationMinutes { get; }
}
