using System.Security.Claims;
using AutoMapper;
using DnnMigration.Application.DTOs;
using DnnMigration.Application.Interfaces;
using DnnMigration.Domain.Entities;
using DnnMigration.Domain.Interfaces;

namespace DnnMigration.Application.Services;

/// <summary>
/// Application service implementing JWT-based authentication (login, refresh, logout, current user).
/// Acts as the Backend-for-Frontend (BFF) authentication orchestrator: it verifies credentials via
/// <see cref="IPasswordHasher"/>, issues access tokens via <see cref="IJwtTokenService"/>, persists,
/// rotates, and revokes opaque refresh tokens via <see cref="IRefreshTokenStore"/>, derives the trusted
/// portal via <see cref="IPortalContextAccessor"/>, reads users via <see cref="IUserRepository"/>, and
/// projects to DTOs via AutoMapper. It contains no HTTP, cookie, or session concerns — those Web Forms
/// responsibilities are eliminated — and never exposes the <see cref="User"/> domain entity across its
/// public surface.
/// </summary>
// MIGRATION: replaces the legacy DotNetNuke PortalSecurity.vb authentication logic
// (UserLogin L632, SignOut L77) and UserController.UserLogin/ValidateUser. DES encryption and
// ASP.NET Forms Authentication are replaced by JWT bearer tokens + BCrypt password hashing. Refresh-token
// rotation/revocation (finding F2) and portal scoping (finding F3) are orchestrated here — see the
// per-method notes — rather than by the stateless JWT token factory.
public sealed class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IRefreshTokenStore _refreshTokenStore;
    private readonly IPortalContextAccessor _portalContextAccessor;
    private readonly IMapper _mapper;

    /// <summary>
    /// Initializes a new <see cref="AuthService"/> with its injected collaborators. All dependencies are
    /// supplied by the built-in DI container (registered in the Api host <c>Program.cs</c>). The service
    /// holds no application-level mutable state — the refresh-token table lives behind
    /// <see cref="IRefreshTokenStore"/>.
    /// </summary>
    /// <param name="userRepository">Repository used to read users (by username+portal or by id).</param>
    /// <param name="passwordHasher">Synchronous BCrypt password hasher/verifier port.</param>
    /// <param name="jwtTokenService">Synchronous JWT issue/validate port.</param>
    /// <param name="refreshTokenStore">Server-side refresh-token store used to persist/validate/rotate/revoke tokens.</param>
    /// <param name="portalContextAccessor">Trusted ambient-portal accessor (host/alias-derived), used to scope login.</param>
    /// <param name="mapper">AutoMapper instance projecting <see cref="User"/> to DTOs.</param>
    public AuthService(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService,
        IRefreshTokenStore refreshTokenStore,
        IPortalContextAccessor portalContextAccessor,
        IMapper mapper)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
        _refreshTokenStore = refreshTokenStore;
        _portalContextAccessor = portalContextAccessor;
        _mapper = mapper;
    }

    /// <summary>
    /// Authenticates the supplied credentials and, on success, returns a fresh access + refresh token
    /// pair. Returns <c>null</c> (never throws) for an unknown user, a bad password, an unapproved /
    /// locked-out account, or a portal-scope violation, letting the Api layer map the failure to a
    /// 401 / RFC 7807 response.
    /// </summary>
    // MIGRATION: PortalSecurity.UserLogin [L632] -> UserController.UserLogin/ValidateUser [L991]. Verify
    // credentials (IPasswordHasher.Verify instead of the DES/membership provider) and, as the legacy
    // ValidateUser did, reject unapproved or locked-out accounts; on success issue an access + refresh
    // token pair.
    // MIGRATION (finding F1): user.Membership is now GENUINELY populated — UserConfiguration maps it as an
    // EF Core owned type of User, so the BCrypt hash written by UserService.CreateAsync is persisted and
    // loaded here. Credentials for users created through the modern stack (BCrypt) therefore verify
    // correctly; verifying legacy aspnet_Membership SHA1 salted hashes requires the PasswordSalt/
    // PasswordFormat columns that are not modeled on UserMembership, so the forward-hash-on-login step for
    // legacy accounts is documented as a deferred item in MIGRATION_NOTES.md (out of this boundary).
    public async Task<TokenResponseDto?> LoginAsync(LoginRequestDto dto, CancellationToken cancellationToken = default)
    {
        // MIGRATION (finding F3): NEVER trust a client-supplied portal id on its own. Derive the trusted
        // portal from request context (host/alias) when available; a client-supplied id may only AGREE
        // with the trusted portal, never override it.
        var ambientPortalId = _portalContextAccessor.GetPortalId();
        if (ambientPortalId.HasValue && dto.PortalId.HasValue && ambientPortalId.Value != dto.PortalId.Value)
        {
            // The caller tried to authenticate against a portal other than the one their request host
            // resolves to — reject rather than honour the client override.
            return null;
        }

        // Effective portal: the trusted ambient portal wins; otherwise fall back to the request-supplied
        // id (default portal 0), preserving legacy behaviour until the host-aware accessor is wired.
        var portalId = ambientPortalId ?? dto.PortalId ?? 0;

        // MIGRATION: DNN keyed users by (PortalID, Username); the lookup is scoped to the SERVER-derived
        // effective portal, not a raw client value.
        var user = await _userRepository.GetByUsernameAsync(portalId, dto.Username, cancellationToken);
        if (user is null)
        {
            return null;
        }

        // MIGRATION: credentials are ALWAYS checked through the hasher (BCrypt) — plaintext comparison is
        // never used. user.Membership.Password is populated because Membership is an owned type (F1).
        if (!_passwordHasher.Verify(dto.Password, user.Membership.Password))
        {
            return null;
        }

        // MIGRATION: legacy ValidateUser returned LOGIN_FAILURE for unapproved/locked accounts.
        if (!user.Membership.Approved || user.Membership.LockedOut)
        {
            return null;
        }

        return await IssueTokensAsync(user, cancellationToken);
    }

    /// <summary>
    /// Exchanges a valid refresh token for a new token pair (rotating the presented token), or returns
    /// <c>null</c> if the supplied token is unknown / expired / revoked or its subject can no longer be
    /// resolved to a user.
    /// </summary>
    // MIGRATION: no legacy equivalent (Forms auth had no refresh tokens).
    // MIGRATION (finding F2): the refresh token is an OPAQUE random value (IJwtTokenService
    // .GenerateRefreshToken), NOT a JWT, so it is validated by LOOKUP in IRefreshTokenStore — never by
    // JWT validation (the previous ValidateToken call could never succeed for an opaque token). On success
    // the presented token is revoked and a brand-new pair is issued (single-use rotation), so a replayed
    // or stolen refresh token fails after its first legitimate use.
    public async Task<TokenResponseDto?> RefreshAsync(RefreshRequestDto dto, CancellationToken cancellationToken = default)
    {
        var userId = await _refreshTokenStore.ValidateAsync(dto.RefreshToken, cancellationToken);
        if (userId is null)
        {
            return null;
        }

        var user = await _userRepository.GetByIdAsync(userId.Value, cancellationToken);
        if (user is null)
        {
            // The token was valid but its subject no longer exists; revoke it so it cannot be retried.
            await _refreshTokenStore.RevokeAsync(dto.RefreshToken, cancellationToken);
            return null;
        }

        // MIGRATION (finding F2): ROTATION — revoke the presented token before issuing the new pair so a
        // refresh token is single-use.
        await _refreshTokenStore.RevokeAsync(dto.RefreshToken, cancellationToken);
        return await IssueTokensAsync(user, cancellationToken);
    }

    /// <summary>
    /// Logs the current authenticated user out by revoking ALL of their stored refresh tokens, so no
    /// silent re-authentication can occur after logout. Short-lived access tokens still expire naturally.
    /// </summary>
    // MIGRATION: PortalSecurity.SignOut [L77] cleared Forms-auth + cookies. With stateless JWTs there is
    // no server session to clear, but refresh tokens ARE server-side state we can revoke (finding F2):
    // resolve the user id from the principal and revoke every refresh token held for that user, matching
    // the IAuthService contract ("revokes their refresh token(s)").
    public async Task LogoutAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId(user);
        if (userId is not null)
        {
            await _refreshTokenStore.RevokeAllAsync(userId.Value, cancellationToken);
        }
    }

    /// <summary>
    /// Resolves the authenticated user from the supplied principal and projects it to a
    /// <see cref="CurrentUserDto"/> for <c>GET /api/auth/me</c>, or returns <c>null</c> when the
    /// principal is unauthenticated, the user can no longer be found, or the token's portal claim does
    /// not match the resolved user's portal (a portal-scope violation).
    /// </summary>
    // MIGRATION: UserController.GetCurrentUserInfo (used by PortalSecurity.IsInRole L105). Resolve the
    // authenticated user id from claims and project to CurrentUserDto.
    // MIGRATION (finding F3): enforce claim/resource portal scoping so a token cannot be used to read a
    // user outside the portal it was issued for.
    public async Task<CurrentUserDto?> GetCurrentUserAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId(user);
        if (userId is null)
        {
            return null;
        }

        var entity = await _userRepository.GetByIdAsync(userId.Value, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        // MIGRATION (finding F3): claim/resource scoping. The access token stamped the user's
        // server-derived home portal into the "portalId" claim at issue time; a non-super user may only
        // resolve /me for that same portal. A mismatch (a tampered/stale token or a cross-portal attempt)
        // yields null. Super users are host-level and intentionally span portals, so they are exempt.
        if (!entity.IsSuperUser)
        {
            var claimPortalId = GetPortalIdClaim(user);
            if (claimPortalId is null || claimPortalId.Value != entity.PortalID)
            {
                return null;
            }
        }

        return _mapper.Map<CurrentUserDto>(entity);
    }

    /// <summary>
    /// Builds an access + refresh token pair for the given user and persists the refresh token in the
    /// store so it can later be validated, rotated, and revoked. Kept private so the <see cref="User"/>
    /// entity never crosses the service's public surface (the DTO boundary is preserved), while keeping
    /// token issuance DRY across <see cref="LoginAsync"/> and <see cref="RefreshAsync"/>.
    /// </summary>
    /// <param name="user">The authenticated user to issue tokens for.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The issued access + refresh token pair.</returns>
    private async Task<TokenResponseDto> IssueTokensAsync(User user, CancellationToken cancellationToken)
    {
        var userDto = _mapper.Map<UserDto>(user);
        var (accessToken, expiresAt) = _jwtTokenService.CreateToken(userDto, user.Roles);
        var refreshToken = _jwtTokenService.GenerateRefreshToken();

        // MIGRATION (finding F2): persist the opaque refresh token server-side so RefreshAsync can
        // validate it by lookup and LogoutAsync can revoke it. The token value carries no claims, so the
        // store is the sole authority on its validity.
        await _refreshTokenStore.StoreAsync(user.UserID, refreshToken, cancellationToken);

        return new TokenResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = expiresAt,
            TokenType = "Bearer"
        };
    }

    /// <summary>
    /// Extracts the integer user id from a principal's <see cref="ClaimTypes.NameIdentifier"/> (falling
    /// back to the JWT <c>"sub"</c> claim), or returns <c>null</c> when absent or non-numeric. Pure and
    /// static — it holds no state and depends only on its argument.
    /// </summary>
    private static int? GetUserId(ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? principal.FindFirstValue("sub");
        return int.TryParse(value, out var id) ? id : null;
    }

    /// <summary>
    /// Extracts the integer portal id from a principal's <c>"portalId"</c> claim (the claim the JWT token
    /// service stamps at issue time), or returns <c>null</c> when absent or non-numeric. Used to enforce
    /// portal scoping on the current-user flow (finding F3). Pure and static.
    /// </summary>
    private static int? GetPortalIdClaim(ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue("portalId");
        return int.TryParse(value, out var id) ? id : null;
    }
}
