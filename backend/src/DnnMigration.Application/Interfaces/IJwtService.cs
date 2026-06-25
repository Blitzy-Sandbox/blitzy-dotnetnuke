namespace DnnMigration.Application.Interfaces;

// MIGRATION: Application-layer PORT (abstraction) for JWT access-token issuance and refresh-token
// lifecycle, replacing the ASP.NET 2.0 AspNetSqlMembershipProvider + Forms-authentication ticket
// (Website/release.config) and the obsolete PortalSecurity.UserLogin flow with short-lived JWT Bearer
// access tokens (60-minute lifetime) plus refresh-token rotation (AAP §0.7.6). Declared in the Application
// project so Application/Services/AuthService.cs depends on Domain + Application abstractions only; the
// concrete adapter (DnnMigration.Infrastructure.Identity.JwtService, System.IdentityModel.Tokens.Jwt) lives
// in Infrastructure and is wired in Infrastructure/DependencyInjection.cs.
//
// COORDINATION GAP: this port was missing from the realized plan even though the concrete
// Infrastructure/Identity/JwtService.cs (already committed) declares `: IJwtService` against this namespace.
// Without it neither the Infrastructure project nor AuthService.cs can compile. Created here
// (Application/Interfaces) to close the gap. Recorded in MIGRATION_NOTES.md.
//
// NOTE: the tuple element names on GenerateAccessToken (AccessToken, ExpiresAtUtc, ExpiresInSeconds) are part
// of the contract and MUST match the concrete implementation's return tuple to avoid CS8141 tuple-name
// mismatches. The cryptographically-random refresh token mirrors the intent of the legacy
// PortalSecurity.CreateKey RNG (PortalSecurity.vb L564), replacing the persistent Forms-auth cookie.

/// <summary>
/// Abstraction over JWT access-token issuance and refresh-token management. Consumed by the authentication
/// service to issue tokens on login and to rotate/revoke refresh tokens. Token signing, validation and
/// storage are implementation concerns owned by the Infrastructure Identity layer.
/// </summary>
public interface IJwtService
{
    /// <summary>
    /// Issues a signed JWT access token carrying the caller's identity and authorization claims.
    /// </summary>
    /// <param name="userId">The authenticated user's identifier (emitted as the <c>sub</c> claim).</param>
    /// <param name="username">The authenticated user's username (emitted as the unique-name claim).</param>
    /// <param name="portalId">The portal (tenant) the user is scoped to (custom <c>portalId</c> claim).</param>
    /// <param name="isSuperUser">Whether the user is a host/super-user (custom <c>isSuperUser</c> claim).</param>
    /// <param name="roles">The user's role names, each emitted as a role claim.</param>
    /// <returns>
    /// A tuple of the encoded access token, its absolute UTC expiry, and its lifetime in seconds.
    /// </returns>
    (string AccessToken, DateTime ExpiresAtUtc, int ExpiresInSeconds) GenerateAccessToken(
        int userId,
        string username,
        int portalId,
        bool isSuperUser,
        IEnumerable<string> roles);

    /// <summary>
    /// Generates a new cryptographically-random, opaque refresh token.
    /// </summary>
    /// <returns>The opaque refresh-token string.</returns>
    string GenerateRefreshToken();

    /// <summary>
    /// Validates a refresh token and resolves the associated user identifier.
    /// </summary>
    /// <param name="refreshToken">The refresh token presented by the caller.</param>
    /// <returns>
    /// The associated user identifier when the token is valid; otherwise <c>null</c> (unknown, expired, or
    /// revoked tokens fail closed).
    /// </returns>
    int? ValidateRefreshToken(string refreshToken);

    /// <summary>
    /// Revokes a refresh token so it can no longer be used. Used for logout and for rotation (revoking the
    /// presented token before issuing a replacement). Implementations MUST be a no-op for an unknown or
    /// empty token rather than throwing.
    /// </summary>
    /// <param name="refreshToken">The refresh token to revoke.</param>
    void RevokeRefreshToken(string refreshToken);
}
