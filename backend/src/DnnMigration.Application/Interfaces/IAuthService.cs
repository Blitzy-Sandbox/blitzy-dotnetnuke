using System.Security.Claims;
using DnnMigration.Application.DTOs;

namespace DnnMigration.Application.Interfaces;

/// <summary>
/// Application service contract for authentication, exposing the login / refresh / logout / me
/// surface consumed by the <c>/api/auth</c> endpoints (BFF pattern with JWT bearer tokens).
/// </summary>
// MIGRATION: replaces the legacy DotNetNuke Forms-authentication flow (PortalSecurity.UserLogin L632
// -> UserController.UserLogin; PortalSecurity.SignOut L77 -> FormsAuthentication.SignOut) with stateless
// JWT bearer tokens. Tokens are issued/validated via the IJwtTokenService port and credentials verified
// via the IPasswordHasher port (BCrypt). Refresh and Me are new BFF concepts. DTO-only, async.
public interface IAuthService
{
    /// <summary>Authenticates credentials and returns a token pair, or <c>null</c> if authentication fails.</summary>
    Task<TokenResponseDto?> LoginAsync(LoginRequestDto dto, CancellationToken cancellationToken = default);

    /// <summary>Exchanges a valid refresh token for a new token pair, or <c>null</c> if the refresh token is invalid/expired.</summary>
    Task<TokenResponseDto?> RefreshAsync(RefreshRequestDto dto, CancellationToken cancellationToken = default);

    /// <summary>Logs a user out by revoking the refresh token(s) associated with the supplied refresh token.</summary>
    // MIGRATION: legacy PortalSecurity.SignOut(); with JWTs there is no server session to drop, so the
    // implementation revokes server-side refresh-token state. The token to revoke travels in the request
    // body (LogoutRequestDto) rather than being derived from the ClaimsPrincipal: the SPA auth interceptor
    // does not attach a bearer to auth-flow routes and the access token may be expired at logout, so the
    // opaque refresh token is the revocation credential. Revoking an unknown/blank token is an idempotent
    // no-op. See LogoutRequestDto for the full contract rationale.
    Task LogoutAsync(LogoutRequestDto dto, CancellationToken cancellationToken = default);

    /// <summary>Returns the current authenticated user's profile for <c>/api/auth/me</c>, or <c>null</c> if unresolvable.</summary>
    Task<CurrentUserDto?> GetCurrentUserAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default);
}
