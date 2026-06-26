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
    /// Generates a new cryptographically-random, opaque refresh token BOUND to the issuing user AND portal
    /// (tenant). The implementation records the (user, portal) binding so that <see cref="ValidateRefreshToken"/>
    /// can later resolve both, allowing the caller to enforce multi-tenant isolation on refresh.
    /// </summary>
    /// <param name="userId">The user the refresh token is issued to.</param>
    /// <param name="portalId">The portal (tenant) the refresh token is scoped to.</param>
    /// <returns>The opaque refresh-token string.</returns>
    // MIGRATION: CP1 review (IJwtService #1) — refresh tokens MUST be tenant-bound. The earlier parameterless
    // overload produced unbound tokens whose portal could not be validated on refresh; it is replaced by this
    // (userId, portalId) overload so a token issued for one portal cannot be replayed against another.
    string GenerateRefreshToken(int userId, int portalId);

    /// <summary>
    /// Validates a refresh token and resolves the tenant-bound identity it was issued for.
    /// </summary>
    /// <param name="refreshToken">The refresh token presented by the caller.</param>
    /// <returns>
    /// A <see cref="RefreshTokenInfo"/> carrying BOTH the associated user id and portal (tenant) id when the token
    /// is valid; otherwise <c>null</c> (unknown, expired, or revoked tokens fail closed). Callers MUST scope the
    /// subsequent user lookup by <see cref="RefreshTokenInfo.PortalId"/> so a refresh token issued for one portal
    /// cannot be used to act in another (AAP §0.7.1 multi-tenant isolation).
    /// </returns>
    // MIGRATION: CP1 review (IJwtService #1 / AuthService #6) — previously returned only `int? userId`, which
    // could not carry the portal binding, so a refresh could not enforce portal/tenant consistency. Now returns
    // the user + portal value object and still fails closed (null) on unknown/revoked tokens.
    RefreshTokenInfo? ValidateRefreshToken(string refreshToken);

    /// <summary>
    /// Revokes a refresh token so it can no longer be used. Used for logout and for rotation (revoking the
    /// presented token before issuing a replacement). Implementations MUST be a no-op for an unknown or
    /// empty token rather than throwing.
    /// </summary>
    /// <param name="refreshToken">The refresh token to revoke.</param>
    void RevokeRefreshToken(string refreshToken);
}

// MIGRATION: CP1 review (IJwtService #1) — tenant-bound refresh-token identity. The refresh token now resolves
// to BOTH the user and the portal it was issued for, so the refresh flow can scope the user lookup by portal and
// reject cross-tenant replay (AAP §0.7.1). A record (value semantics) is used so equality compares the bound
// identity, not a reference. Kept intentionally minimal (user + portal); a persistent token store may later add
// issued-at / expiry metadata without changing the consuming contract.
/// <summary>
/// The tenant-bound identity a valid refresh token resolves to: the owning user and the portal (tenant) the
/// token was issued for.
/// </summary>
/// <param name="UserId">The user the refresh token was issued to.</param>
/// <param name="PortalId">The portal (tenant) the refresh token is scoped to.</param>
public sealed record RefreshTokenInfo(int UserId, int PortalId);
