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
    /// Claim type that distinguishes a token's kind (access vs refresh). Both token kinds are signed JWTs
    /// validated by <see cref="ValidateToken"/>; this marker lets the refresh endpoint reject an access
    /// token presented as a refresh token (token-type confusion).
    /// </summary>
    public const string TokenTypeClaim = "token_type";

    /// <summary><see cref="TokenTypeClaim"/> value identifying an access token.</summary>
    public const string AccessTokenType = "access";

    /// <summary><see cref="TokenTypeClaim"/> value identifying a refresh token.</summary>
    public const string RefreshTokenType = "refresh";

    /// <summary>
    /// Issues a signed JWT access token for the supplied user, including identity and
    /// role claims plus a <see cref="TokenTypeClaim"/> = <see cref="AccessTokenType"/> marker.
    /// Role claims are emitted from <see cref="User.Roles"/> (populated by the service layer
    /// prior to this call).
    /// </summary>
    string GenerateAccessToken(User user);

    /// <summary>
    /// Issues a <b>signed JWT</b> refresh token for the supplied user. It carries the user subject, a
    /// <see cref="TokenTypeClaim"/> = <see cref="RefreshTokenType"/> marker and a longer lifetime
    /// (<c>JwtSettings.RefreshTokenExpirationDays</c>), and is signed with the same key/issuer/audience as
    /// the access token so <see cref="ValidateToken"/> can validate it. MIGRATION (CP2 auth-chain fix):
    /// replaces the earlier opaque random refresh token that <see cref="ValidateToken"/> could never
    /// validate — which broke <c>/api/auth/refresh</c> and the frontend 401-recovery flow.
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
