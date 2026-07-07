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
/// <see cref="IPasswordHasher"/>, issues/validates tokens via <see cref="IJwtTokenService"/>, reads
/// users via <see cref="IUserRepository"/>, and projects to DTOs via AutoMapper. It contains no HTTP,
/// cookie, or session concerns — those Web Forms responsibilities are eliminated — and never exposes
/// the <see cref="User"/> domain entity across its public surface.
/// </summary>
// MIGRATION: replaces the legacy DotNetNuke PortalSecurity.vb authentication logic
// (UserLogin L632, SignOut L77) and UserController.UserLogin/ValidateUser. DES encryption and
// ASP.NET Forms Authentication are replaced by JWT bearer tokens + BCrypt password hashing.
public sealed class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IMapper _mapper;

    /// <summary>
    /// Initializes a new <see cref="AuthService"/> with its injected collaborators. All dependencies
    /// are supplied by the built-in DI container (registered in the Api host <c>Program.cs</c>);
    /// the service is stateless and holds no application-level mutable state.
    /// </summary>
    /// <param name="userRepository">Repository used to read users (by username+portal or by id).</param>
    /// <param name="passwordHasher">Synchronous BCrypt password hasher/verifier port.</param>
    /// <param name="jwtTokenService">Synchronous JWT issue/validate port.</param>
    /// <param name="mapper">AutoMapper instance projecting <see cref="User"/> to DTOs.</param>
    public AuthService(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService,
        IMapper mapper)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
        _mapper = mapper;
    }

    /// <summary>
    /// Authenticates the supplied credentials and, on success, returns a fresh access + refresh token
    /// pair. Returns <c>null</c> (never throws) for an unknown user, a bad password, or an
    /// unapproved / locked-out account, letting the Api layer map the failure to a 401 / RFC 7807 response.
    /// </summary>
    // MIGRATION: PortalSecurity.UserLogin [L632] -> UserController.UserLogin/ValidateUser [L991]. Verify credentials
    // (IPasswordHasher.Verify instead of the DES/membership provider) and, as the legacy ValidateUser did, reject
    // unapproved or locked-out accounts. On success issue an access + refresh token pair. Legacy aspnet_Membership
    // salted hashes are read for verification with a forward-hash-on-login strategy documented in MIGRATION_NOTES.md.
    public async Task<TokenResponseDto?> LoginAsync(LoginRequestDto dto, CancellationToken cancellationToken = default)
    {
        // MIGRATION: DNN keyed users by (PortalID, Username); default to portal 0 when unspecified.
        var user = await _userRepository.GetByUsernameAsync(dto.PortalId ?? 0, dto.Username, cancellationToken);
        if (user is null)
        {
            return null;
        }

        // MIGRATION: credentials are ALWAYS checked through the hasher (BCrypt) — plaintext comparison is never used.
        if (!_passwordHasher.Verify(dto.Password, user.Membership.Password))
        {
            return null;
        }

        // MIGRATION: legacy ValidateUser returned LOGIN_FAILURE for unapproved/locked accounts.
        if (!user.Membership.Approved || user.Membership.LockedOut)
        {
            return null;
        }

        return IssueTokens(user);
    }

    /// <summary>
    /// Exchanges a valid refresh token for a new token pair, or returns <c>null</c> if the supplied
    /// token is invalid/expired or its subject can no longer be resolved to a user.
    /// </summary>
    // MIGRATION: no legacy equivalent (Forms auth had no refresh tokens). Validate the supplied token, resolve the
    // user, and re-issue a token pair. Persistent refresh-token rotation/revocation is an Infrastructure concern
    // documented in MIGRATION_NOTES.md.
    public async Task<TokenResponseDto?> RefreshAsync(RefreshRequestDto dto, CancellationToken cancellationToken = default)
    {
        var principal = _jwtTokenService.ValidateToken(dto.RefreshToken);
        if (principal is null)
        {
            return null;
        }

        var userId = GetUserId(principal);
        if (userId is null)
        {
            return null;
        }

        var user = await _userRepository.GetByIdAsync(userId.Value, cancellationToken);
        if (user is null)
        {
            return null;
        }

        return IssueTokens(user);
    }

    /// <summary>
    /// Logs the current authenticated user out. With stateless JWTs there is no server session to drop,
    /// so this is an intentional no-op at the service level.
    /// </summary>
    // MIGRATION: PortalSecurity.SignOut [L77] cleared Forms-auth + cookies. Stateless JWT has no server session to
    // clear; token revocation (if adopted) belongs to an Infrastructure token store. Intentional no-op here.
    // NOTE: intentionally NOT marked `async` — an async method without an `await` raises CS1998, which fails the
    // Gate 1 warnings-as-errors build. Returning Task.CompletedTask from a synchronous method satisfies the contract.
    public Task LogoutAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Resolves the authenticated user from the supplied principal and projects it to a
    /// <see cref="CurrentUserDto"/> for <c>GET /api/auth/me</c>, or returns <c>null</c> when the
    /// principal is unauthenticated or the user can no longer be found.
    /// </summary>
    // MIGRATION: UserController.GetCurrentUserInfo (used by PortalSecurity.IsInRole L105). Resolve the authenticated
    // user id from claims and project to CurrentUserDto.
    public async Task<CurrentUserDto?> GetCurrentUserAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId(user);
        if (userId is null)
        {
            return null;
        }

        var entity = await _userRepository.GetByIdAsync(userId.Value, cancellationToken);
        return entity is null ? null : _mapper.Map<CurrentUserDto>(entity);
    }

    /// <summary>
    /// Builds an access + refresh token pair for the given user. Kept private so the <see cref="User"/>
    /// entity never crosses the service's public surface (the DTO boundary is preserved), while keeping
    /// token creation DRY across <see cref="LoginAsync"/> and <see cref="RefreshAsync"/>.
    /// </summary>
    private TokenResponseDto IssueTokens(User user)
    {
        var userDto = _mapper.Map<UserDto>(user);
        var (accessToken, expiresAt) = _jwtTokenService.CreateToken(userDto, user.Roles);
        var refreshToken = _jwtTokenService.GenerateRefreshToken();

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
}
