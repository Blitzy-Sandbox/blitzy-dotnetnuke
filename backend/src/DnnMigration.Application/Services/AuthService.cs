using System.Security.Claims;
using AutoMapper;
using DnnMigration.Application.DTOs.Auth;
using DnnMigration.Application.DTOs.User;
using DnnMigration.Application.Interfaces;
using DnnMigration.Domain.Entities;
using DnnMigration.Domain.Interfaces;

namespace DnnMigration.Application.Services;

/// <summary>
/// JWT authentication orchestration service — the concrete implementation of <see cref="IAuthService"/>
/// backing the <c>/api/auth/{login,refresh,logout,me}</c> surface.
/// </summary>
/// <remarks>
/// MIGRATION: this service is THE single sanctioned behavior change in the DNN 4.x → .NET 8 migration
/// (AAP §0.6.2 / §0.7.1). It REPLACES legacy ASP.NET Forms Authentication + 56-bit DES
/// (<c>Library/Components/Security/PortalSecurity.vb</c>: <c>SignOut</c> L79, DES <c>Encrypt</c>/<c>Decrypt</c>
/// L138-L215) and the cookie-issuing <c>UserController</c> login flow
/// (<c>Library/Components/Users/UserController.vb</c>: <c>UserLogin</c> L991-L1008,
/// <c>FormsAuthentication.SetAuthCookie</c> L1033, <c>GetCurrentUserInfo</c> L381-L403) with stateless
/// JWT Bearer tokens and one-way BCrypt password verification. It is a SYNTHESIS of those legacy entry
/// points — not a 1:1 port.
///
/// By design this service touches NO cryptographic or token primitives directly: BCrypt verification is
/// delegated to <see cref="IPasswordHasher"/> and all JWT issue/validate/rotate work to
/// <see cref="IJwtService"/> (both implemented in <c>DnnMigration.Infrastructure</c>). User records are
/// loaded through <see cref="IUserRepository"/> (EF Core materialization, which replaces the legacy
/// provider model + CBO reflection hydration). Identity is carried in the JWT claims, so the service holds
/// no server-side session and can scale horizontally. Every deviation from legacy behavior is annotated
/// with a <c>// MIGRATION:</c> comment and recorded in the root <c>MIGRATION_NOTES.md</c>.
/// </remarks>
public class AuthService : IAuthService
{
    // MIGRATION: LoginRequestDto carries no portal context, whereas the legacy entry points were
    // portal-scoped (UserController.UserLogin(portalId, ...)). Phase-1 logins default to the DNN primary
    // portal (PortalID = 0). A portal-aware login is out of Phase-1 scope and documented in MIGRATION_NOTES.md.
    private const int DefaultPortalId = 0;

    // MIGRATION (CP-FINAL / Code-Review G2): the refresh token's per-token id is carried in the standard JWT
    // "jti" claim. The probe confirmed IJwtService.ValidateToken surfaces it verbatim as "jti" (it is not
    // remapped by the inbound claim-type map), so it is read with this literal — keeping the Application layer
    // free of a System.IdentityModel.Tokens.Jwt package dependency.
    private const string JtiClaim = "jti";

    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtService _jwtService;
    private readonly IRefreshTokenStore _refreshTokenStore;
    private readonly IMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthService"/> class.
    /// </summary>
    /// <param name="userRepository">User aggregate data access (EF Core); resolves accounts by username/id.</param>
    /// <param name="passwordHasher">One-way BCrypt hasher; used here to VERIFY a plaintext password against the stored hash.</param>
    /// <param name="jwtService">Stateless JWT token service used to issue, validate, and rotate tokens.</param>
    /// <param name="refreshTokenStore">Server-side refresh-token family state enabling revoking rotation, replay detection, and logout.</param>
    /// <param name="mapper">AutoMapper instance backed by <c>UserProfile</c>; projects <see cref="User"/> onto the password-free <see cref="UserDto"/>.</param>
    public AuthService(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IJwtService jwtService,
        IRefreshTokenStore refreshTokenStore,
        IMapper mapper)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _jwtService = jwtService;
        _refreshTokenStore = refreshTokenStore;
        _mapper = mapper;
    }

    /// <summary>
    /// Validates the supplied credentials and, on success, issues a stateless JWT access token plus a
    /// rotating refresh token. Throws <see cref="UnauthorizedAccessException"/> on any failure.
    /// </summary>
    /// <param name="request">The login request carrying the username/email and plaintext password.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>An <see cref="AuthResponseDto"/> carrying the issued tokens, expiry, and the safe user projection.</returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when credentials are missing or invalid (generic message; no user enumeration).</exception>
    public async Task<AuthResponseDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default)
    {
        // MIGRATION: no Auth validator exists; enforce non-empty credentials here.
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            throw new UnauthorizedAccessException("Invalid username or password.");

        // MIGRATION: LoginRequestDto carries no portal context; default to the primary portal (PortalID=0).
        User? user = await _userRepository.GetByUsernameAsync(DefaultPortalId, request.Username, cancellationToken);

        // MIGRATION: replaces legacy ValidateUser + DES check (PortalSecurity.vb Decrypt L138-L215) with a
        // BCrypt verify via IPasswordHasher. The generic error message avoids user-enumeration disclosure.
        if (user is null || string.IsNullOrEmpty(user.Password) || !_passwordHasher.Verify(request.Password, user.Password))
            throw new UnauthorizedAccessException("Invalid username or password.");

        // MIGRATION: replaces FormsAuthentication.SetAuthCookie [UserController.vb:L1033] / the persistent-cookie
        // ticket. The JWT access token is stateless, but the refresh token is now bound to a server-tracked
        // rotation family (Code-Review G2) so it can be revoked and replay-detected. The AuthController issues
        // the returned refresh token to the client as an HttpOnly, Secure, SameSite cookie (it is NOT exposed
        // to JavaScript).
        return IssueNewTokenFamily(user);
    }

    /// <summary>
    /// Validates a refresh token and rotates it, returning a fresh access + refresh token pair for the
    /// user identified by the token's subject claim. Throws <see cref="UnauthorizedAccessException"/> when
    /// the token is missing, invalid, or its subject cannot be resolved to an existing user.
    /// </summary>
    /// <param name="request">The refresh request carrying the previously issued refresh token.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>An <see cref="AuthResponseDto"/> carrying a newly rotated token pair.</returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when the refresh token is missing, invalid, or unresolvable.</exception>
    public async Task<AuthResponseDto> RefreshAsync(RefreshTokenRequestDto request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            throw new UnauthorizedAccessException("Invalid refresh token.");

        // MIGRATION (CP-FINAL / Code-Review G2): the refresh token is FIRST cryptographically validated by
        // IJwtService (signature/issuer/audience/lifetime), then validated and rotated against the server-side
        // IRefreshTokenStore below. Rotation is therefore stateful and revoking (replay-/reuse-resistant), not
        // the earlier stateless re-issue.
        var principal = _jwtService.ValidateToken(request.RefreshToken);
        if (principal is null)
            throw new UnauthorizedAccessException("Invalid refresh token.");

        // MIGRATION (CP2 auth-chain fix): the refresh token is a signed JWT carrying token_type=refresh.
        // Reject any validated token that is NOT a refresh token (e.g. an access token replayed at this
        // endpoint) so the two token kinds cannot be confused even though both validate against the same
        // signing key, issuer, and audience.
        var tokenType = principal.FindFirst(IJwtService.TokenTypeClaim)?.Value;
        if (!string.Equals(tokenType, IJwtService.RefreshTokenType, StringComparison.Ordinal))
            throw new UnauthorizedAccessException("Invalid refresh token.");

        // The user-id claim type aligns with IJwtService.GenerateRefreshToken's subject claim
        // (standard ClaimTypes.NameIdentifier, with a "sub" fallback for unmapped tokens).
        var userIdValue = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? principal.FindFirst("sub")?.Value;
        if (!int.TryParse(userIdValue, out var userId))
            throw new UnauthorizedAccessException("Invalid refresh token.");

        // MIGRATION (CP-FINAL / Code-Review G2): a refresh token also carries its rotation family
        // (token_family) and a per-token id (jti). Both are required to validate the token against the
        // server-side store; a token missing either is not a token this server issued under the current
        // contract and is rejected.
        var tokenFamily = principal.FindFirst(IJwtService.TokenFamilyClaim)?.Value;
        var presentedTokenId = principal.FindFirst(JtiClaim)?.Value;
        if (string.IsNullOrEmpty(tokenFamily) || string.IsNullOrEmpty(presentedTokenId))
            throw new UnauthorizedAccessException("Invalid refresh token.");

        User? user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null)
            throw new UnauthorizedAccessException("Invalid refresh token.");

        // MIGRATION (CP-FINAL / Code-Review G2): REVOKING rotation. Atomically supersede the presented token
        // id with a freshly minted one within the same family. TryRotate returns false when the family is
        // unknown/revoked (e.g. after logout), when the stored entry has expired, or when a SUPERSEDED token
        // id is presented — the last case is a replay and revokes the entire family inside the store. Any
        // false result is surfaced as the same generic 401 (no enumeration of the failure reason).
        var newTokenId = Guid.NewGuid().ToString("N");
        var newExpiresUtc = DateTime.UtcNow.AddDays(_jwtService.RefreshTokenExpirationDays);
        if (!_refreshTokenStore.TryRotate(tokenFamily, presentedTokenId, newTokenId, newExpiresUtc))
            throw new UnauthorizedAccessException("Invalid refresh token.");

        // Issue the rotated pair: a fresh access token plus a refresh token bound to the SAME family but the
        // NEW token id now recorded as current in the store.
        var rotatedRefreshToken = _jwtService.GenerateRefreshToken(user, tokenFamily, newTokenId);
        return BuildAuthResponse(user, rotatedRefreshToken);
    }

    /// <summary>
    /// Logs the user out by revoking every refresh-token family they own, so no further token rotation is
    /// possible; the short-lived access token then simply expires. The client must also discard its tokens.
    /// </summary>
    /// <param name="userId">The id of the user logging out (sourced from JWT claims by the controller).</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>A completed task.</returns>
    public Task LogoutAsync(int userId, CancellationToken cancellationToken = default)
    {
        // MIGRATION (CP-FINAL / Code-Review G2): replaces FormsAuthentication.SignOut() [PortalSecurity.vb:L79]
        // and the earlier no-op logout. The access token is stateless (it expires on its own short lifetime),
        // but the refresh token is now revocable: revoke ALL of the user's families so every active session's
        // refresh token is invalidated immediately. The AuthController additionally deletes the refresh cookie.
        // NOTE: deliberately NOT marked async — the store operation is synchronous, so adding `async` without an
        // `await` would raise CS1998 and fail the --warnaserror build.
        _refreshTokenStore.RevokeAllForUser(userId);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Returns the authenticated user's profile (<c>/api/auth/me</c>) for the supplied id, or <c>null</c>
    /// when no such user exists.
    /// </summary>
    /// <param name="userId">The user id, supplied from the authenticated principal's JWT claims by the controller.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>The password-free <see cref="UserDto"/> projection, or <c>null</c> if not found.</returns>
    public async Task<UserDto?> GetCurrentUserAsync(int userId, CancellationToken cancellationToken = default)
    {
        // MIGRATION: replaces GetCurrentUserInfo HttpContext/Thread.CurrentPrincipal mechanics
        // [UserController.vb:L381-L403]; userId is supplied from JWT claims by the controller.
        User? user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        return user is null ? null : _mapper.Map<UserDto>(user);
    }

    /// <summary>
    /// Establishes a NEW refresh-token family for the user (used at login): mints a family id + first token
    /// id, registers them with <see cref="IRefreshTokenStore"/>, issues the signed refresh JWT bound to them,
    /// and builds the response.
    /// </summary>
    /// <remarks>
    /// MIGRATION (CP-FINAL / Code-Review G2): registering the family BEFORE issuing the token ensures a
    /// subsequent <c>/api/auth/refresh</c> can validate and rotate it. Intentionally NOT async — all calls are
    /// synchronous; marking it <c>async</c> would raise CS1998 and fail the warnings-as-errors build.
    /// </remarks>
    /// <param name="user">The authenticated user for whom a new session/token family is created.</param>
    /// <returns>A populated <see cref="AuthResponseDto"/> with tokens, expiry, and the safe user projection.</returns>
    private AuthResponseDto IssueNewTokenFamily(User user)
    {
        var tokenFamily = Guid.NewGuid().ToString("N");
        var tokenId = Guid.NewGuid().ToString("N");
        var expiresUtc = DateTime.UtcNow.AddDays(_jwtService.RefreshTokenExpirationDays);

        _refreshTokenStore.Register(tokenFamily, tokenId, user.UserID, expiresUtc);

        var refreshToken = _jwtService.GenerateRefreshToken(user, tokenFamily, tokenId);
        return BuildAuthResponse(user, refreshToken);
    }

    /// <summary>
    /// Builds the <see cref="AuthResponseDto"/> for an authenticated user: issues a fresh access token via
    /// <see cref="IJwtService"/>, pairs it with the already-issued <paramref name="refreshToken"/>, and
    /// projects the user onto the password-free <see cref="UserDto"/>.
    /// </summary>
    /// <remarks>
    /// Intentionally NOT async — access-token generation on <see cref="IJwtService"/> is synchronous and there
    /// is no awaited work; marking it <c>async</c> would raise CS1998 and fail the warnings-as-errors build.
    /// The refresh token is supplied by the caller (<see cref="IssueNewTokenFamily"/> at login, or the rotation
    /// path in <see cref="RefreshAsync"/>) because it is bound to server-tracked family/token-id state.
    /// </remarks>
    /// <param name="user">The authenticated user for whom the access token is issued.</param>
    /// <param name="refreshToken">The refresh token already issued and registered/rotated against the store.</param>
    /// <returns>A populated <see cref="AuthResponseDto"/> with tokens, expiry, and the safe user projection.</returns>
    private AuthResponseDto BuildAuthResponse(User user, string refreshToken)
    {
        var accessToken = _jwtService.GenerateAccessToken(user);
        var expiresInMinutes = _jwtService.AccessTokenExpirationMinutes;

        return new AuthResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddMinutes(expiresInMinutes), // absolute UTC expiry
            ExpiresIn = expiresInMinutes * 60,                         // OAuth2-style seconds
            User = _mapper.Map<UserDto>(user)                          // SECURITY: UserDto carries NO password material
        };
    }
}
