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
    /// Claim type carrying the refresh token's <i>family</i> id — the logical session established at login
    /// and preserved across every rotation. MIGRATION (CP-FINAL / Code-Review G2): the family lets the
    /// server-side <see cref="IRefreshTokenStore"/> revoke a whole session and detect replay of a superseded
    /// refresh token; emitted only on refresh tokens, alongside a per-token <c>jti</c>.
    /// </summary>
    public const string TokenFamilyClaim = "token_family";

    /// <summary>
    /// Issues a signed JWT access token for the supplied user, including identity and
    /// role claims plus a <see cref="TokenTypeClaim"/> = <see cref="AccessTokenType"/> marker.
    /// Role claims are emitted from <see cref="User.Roles"/> (populated by the service layer
    /// prior to this call).
    /// </summary>
    string GenerateAccessToken(User user);

    /// <summary>
    /// Issues a <b>signed JWT</b> refresh token for the supplied user, bound to a rotation <paramref name="tokenFamily"/>
    /// and a per-token <paramref name="tokenId"/>. It carries the user subject, a
    /// <see cref="TokenTypeClaim"/> = <see cref="RefreshTokenType"/> marker, the <see cref="TokenFamilyClaim"/>,
    /// a <c>jti</c> equal to <paramref name="tokenId"/>, and a longer lifetime
    /// (<c>JwtSettings.RefreshTokenExpirationDays</c>); it is signed with the same key/issuer/audience as the
    /// access token so <see cref="ValidateToken"/> can validate it.
    /// </summary>
    /// <param name="user">The authenticated user the refresh token represents.</param>
    /// <param name="tokenFamily">The rotation family id, emitted as the <see cref="TokenFamilyClaim"/>.</param>
    /// <param name="tokenId">The unique token id for THIS refresh token, emitted as the <c>jti</c> claim.</param>
    /// <remarks>
    /// MIGRATION (CP-FINAL / Code-Review G2): the family + token-id parameters let the Application-layer
    /// <c>AuthService</c> register the token with <see cref="IRefreshTokenStore"/> so rotation becomes
    /// revoking and replay of a superseded token is detected. CP2 history: a refresh token is a SIGNED JWT
    /// (not an opaque blob) so <see cref="ValidateToken"/> can validate it — the original opaque token could
    /// never be validated, which broke <c>/api/auth/refresh</c> and the frontend 401-recovery flow.
    /// </remarks>
    string GenerateRefreshToken(User user, string tokenFamily, string tokenId);

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

    /// <summary>
    /// The configured refresh-token lifetime in days (AAP/JwtSettings default: 7). Lets the Application-layer
    /// <c>AuthService</c> compute the absolute expiry it records with <see cref="IRefreshTokenStore"/> when
    /// registering or rotating a refresh token, without binding the Application layer to the Infrastructure
    /// <c>JwtSettings</c> options type.
    /// </summary>
    int RefreshTokenExpirationDays { get; }
}
