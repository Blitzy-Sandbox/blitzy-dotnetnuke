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

    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtService _jwtService;
    private readonly IMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthService"/> class.
    /// </summary>
    /// <param name="userRepository">User aggregate data access (EF Core); resolves accounts by username/id.</param>
    /// <param name="passwordHasher">One-way BCrypt hasher; used here to VERIFY a plaintext password against the stored hash.</param>
    /// <param name="jwtService">Stateless JWT token service used to issue, validate, and rotate tokens.</param>
    /// <param name="mapper">AutoMapper instance backed by <c>UserProfile</c>; projects <see cref="User"/> onto the password-free <see cref="UserDto"/>.</param>
    public AuthService(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IJwtService jwtService,
        IMapper mapper)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _jwtService = jwtService;
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
        // ticket — JWT is stateless, so no auth cookie is set and no server-side session is created.
        return BuildAuthResponse(user);
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

        // MIGRATION: refresh-token validation/rotation is delegated to IJwtService (stateless; a server-side
        // refresh-token store is out of Phase-1 scope).
        var principal = _jwtService.ValidateToken(request.RefreshToken);
        if (principal is null)
            throw new UnauthorizedAccessException("Invalid refresh token.");

        // The user-id claim type aligns with IJwtService.GenerateAccessToken's subject claim
        // (standard ClaimTypes.NameIdentifier, with a "sub" fallback for unmapped tokens).
        var userIdValue = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? principal.FindFirst("sub")?.Value;
        if (!int.TryParse(userIdValue, out var userId))
            throw new UnauthorizedAccessException("Invalid refresh token.");

        User? user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null)
            throw new UnauthorizedAccessException("Invalid refresh token.");

        return BuildAuthResponse(user); // rotation: a fresh access + refresh pair is issued
    }

    /// <summary>
    /// Logs the user out. With stateless JWT and no Phase-1 server-side refresh-token store, this is a
    /// no-op acknowledgement; the client is responsible for discarding its tokens.
    /// </summary>
    /// <param name="userId">The id of the user logging out (sourced from JWT claims by the controller). Currently unused.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>A completed task.</returns>
    public Task LogoutAsync(int userId, CancellationToken cancellationToken = default)
    {
        // MIGRATION: replaces FormsAuthentication.SignOut() [PortalSecurity.vb:L79]. JWT is stateless — the
        // server holds no session and the client discards its tokens. With no server-side refresh-token store
        // in Phase-1 scope, this is a no-op acknowledgement.
        // NOTE: deliberately NOT marked async (it performs no awaited work) — an async method without an await
        // raises CS1998 and would fail the --warnaserror build; the unused userId is retained for the contract.
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
    /// Builds the <see cref="AuthResponseDto"/> for an authenticated user: issues a fresh access token and
    /// refresh token via <see cref="IJwtService"/> and projects the user onto the password-free
    /// <see cref="UserDto"/>.
    /// </summary>
    /// <remarks>
    /// Intentionally NOT async — token generation on <see cref="IJwtService"/> is synchronous and there is
    /// no awaited work; marking it <c>async</c> would raise CS1998 and fail the warnings-as-errors build.
    /// </remarks>
    /// <param name="user">The authenticated user for whom tokens are issued.</param>
    /// <returns>A populated <see cref="AuthResponseDto"/> with tokens, expiry, and the safe user projection.</returns>
    private AuthResponseDto BuildAuthResponse(User user)
    {
        var accessToken = _jwtService.GenerateAccessToken(user);
        var refreshToken = _jwtService.GenerateRefreshToken();
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
