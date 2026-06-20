using System.Security.Claims;
using AutoMapper;
using DnnMigration.Application.DTOs.Auth;
using DnnMigration.Application.DTOs.User;
using DnnMigration.Application.Interfaces;
using DnnMigration.Domain.Entities;
using DnnMigration.Domain.Interfaces;

namespace DnnMigration.Application.Services;

/// <summary>
/// JWT authentication orchestration service backing the <c>/api/auth/{login,refresh,logout,me}</c> surface.
/// </summary>
/// <remarks>
/// MIGRATION: This service is the SINGLE SANCTIONED BEHAVIOR CHANGE of the DotNetNuke 4.9.0.85 -> .NET 8
/// rewrite (AAP §0.6.2 / §0.7.1). It synthesizes the legacy <c>UserController.UserLogin</c>
/// (Library/Components/Users/UserController.vb:L991-L1008) and <c>GetCurrentUserInfo</c> (L381-L403) flow
/// together with the <c>PortalSecurity.vb</c> security model into a stateless design rather than porting
/// them 1:1. Legacy ASP.NET Forms Authentication (<c>FormsAuthentication.SetAuthCookie</c>/<c>SignOut</c>)
/// and the symmetric 56-bit DES <c>Encrypt</c>/<c>Decrypt</c> routines
/// (Library/Components/Security/PortalSecurity.vb:L79, L138-L211) are replaced by stateless JWT Bearer
/// tokens with refresh-token rotation and one-way adaptive BCrypt password verification. All cryptographic
/// and token primitives live in the Infrastructure layer behind the <see cref="IPasswordHasher"/> and
/// <see cref="IJwtService"/> ports; this service never touches DES, BCrypt, or JWT primitives directly.
/// Every deviation from legacy behavior is annotated with a <c>// MIGRATION:</c> comment and recorded in the
/// root <c>MIGRATION_NOTES.md</c>.
/// </remarks>
public class AuthService : IAuthService
{
    // MIGRATION: the legacy UserController.UserLogin took an explicit portalId, but LoginRequestDto carries no
    // portal context. DNN's primary portal is PortalID=0; logins default to it. A multi-portal login flow is
    // out of Phase-1 scope.
    private const int DefaultPortalId = 0;

    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtService _jwtService;
    private readonly IMapper _mapper;

    /// <summary>
    /// Initializes a new <see cref="AuthService"/> with its injected collaborators.
    /// </summary>
    /// <param name="userRepository">Persistence gateway for the <see cref="User"/> aggregate (credential and profile lookup).</param>
    /// <param name="passwordHasher">One-way BCrypt verifier. MIGRATION: replaces the legacy 56-bit DES routines.</param>
    /// <param name="jwtService">Stateless JWT issuer/validator. MIGRATION: replaces ASP.NET Forms Authentication.</param>
    /// <param name="mapper">AutoMapper instance providing the <see cref="User"/> -> <see cref="UserDto"/> safe projection.</param>
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
    /// Validates the supplied credentials with BCrypt and, on success, issues a JWT access token plus a
    /// rotated refresh token. MIGRATION: synthesizes <c>UserController.UserLogin</c> + <c>ValidateUser</c>.
    /// </summary>
    /// <param name="request">The login credentials (username/email and plaintext password).</param>
    /// <param name="cancellationToken">Token used to observe cancellation requests.</param>
    /// <returns>The authentication response carrying the tokens, expiry, and safe user projection.</returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when credentials are missing or invalid.</exception>
    public async Task<AuthResponseDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default)
    {
        // MIGRATION: no Auth validator exists; enforce non-empty credentials here.
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            throw new UnauthorizedAccessException("Invalid username or password.");

        // MIGRATION: LoginRequestDto carries no portal context; default to the primary portal (PortalID=0).
        User? user = await _userRepository.GetByUsernameAsync(DefaultPortalId, request.Username, cancellationToken);

        // MIGRATION: replaces legacy ValidateUser + DES check; BCrypt verify via IPasswordHasher. Generic error avoids user enumeration.
        if (user is null || string.IsNullOrEmpty(user.Password) || !_passwordHasher.Verify(request.Password, user.Password))
            throw new UnauthorizedAccessException("Invalid username or password.");

        // MIGRATION: replaces FormsAuthentication.SetAuthCookie [UserController.vb:L1033] / persistent-cookie ticket — JWT is stateless, no cookie is set.
        return BuildAuthResponse(user);
    }

    /// <summary>
    /// Validates a refresh token and rotates it, returning a fresh access + refresh token pair.
    /// </summary>
    /// <param name="request">The payload carrying the refresh token to validate and rotate.</param>
    /// <param name="cancellationToken">Token used to observe cancellation requests.</param>
    /// <returns>A new authentication response with rotated tokens.</returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when the refresh token is missing, invalid, or its subject cannot be resolved.</exception>
    public async Task<AuthResponseDto> RefreshAsync(RefreshTokenRequestDto request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            throw new UnauthorizedAccessException("Invalid refresh token.");

        // MIGRATION: refresh-token validation/rotation is delegated to IJwtService (stateless; a server-side refresh-token store is out of Phase-1 scope).
        var principal = _jwtService.ValidateToken(request.RefreshToken);
        if (principal is null)
            throw new UnauthorizedAccessException("Invalid refresh token.");

        // MIGRATION (C5/DEV-032): the presented token must be a REFRESH token. IJwtService stamps
        // token_use=refresh on refresh tokens and token_use=access on access tokens; rejecting anything
        // other than "refresh" here prevents an access token from being replayed at the refresh endpoint
        // (token-type confusion). The literal mirrors the Infrastructure JwtService constant (the Application
        // layer cannot reference that Infrastructure type).
        if (!string.Equals(principal.FindFirst("token_use")?.Value, "refresh", StringComparison.Ordinal))
            throw new UnauthorizedAccessException("Invalid refresh token.");

        // The user-id claim type aligns with IJwtService.GenerateAccessToken's subject claim
        // (standard ClaimTypes.NameIdentifier), with a "sub" fallback for unmapped tokens.
        var userIdValue = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? principal.FindFirst("sub")?.Value;
        if (!int.TryParse(userIdValue, out var userId))
            throw new UnauthorizedAccessException("Invalid refresh token.");

        User? user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null)
            throw new UnauthorizedAccessException("Invalid refresh token.");

        return BuildAuthResponse(user); // rotation: a fresh access + refresh pair is issued
    }

    /// <summary>
    /// Logs the user out. MIGRATION: replaces <c>FormsAuthentication.SignOut()</c> [PortalSecurity.vb:L79].
    /// </summary>
    /// <param name="userId">The id of the user logging out (supplied from the authenticated JWT principal).</param>
    /// <param name="cancellationToken">Token used to observe cancellation requests.</param>
    /// <returns>A completed task.</returns>
    public Task LogoutAsync(int userId, CancellationToken cancellationToken = default)
    {
        // MIGRATION: replaces FormsAuthentication.SignOut() [PortalSecurity.vb:L79]. JWT is stateless — the server holds no session and the client discards its tokens. With no server-side refresh-token store in Phase-1 scope, this is a no-op acknowledgement.
        return Task.CompletedTask;
    }

    /// <summary>
    /// Returns the authenticated user's safe profile projection (<c>/api/auth/me</c>), or <c>null</c> if the
    /// user no longer exists. MIGRATION: replaces <c>UserController.GetCurrentUserInfo</c> [L381-L403].
    /// </summary>
    /// <param name="userId">The id of the authenticated user (supplied from the JWT claims by the controller).</param>
    /// <param name="cancellationToken">Token used to observe cancellation requests.</param>
    /// <returns>The mapped <see cref="UserDto"/>, or <c>null</c> if the user was not found.</returns>
    public async Task<UserDto?> GetCurrentUserAsync(int userId, CancellationToken cancellationToken = default)
    {
        // MIGRATION: replaces GetCurrentUserInfo HttpContext/Thread.CurrentPrincipal mechanics; userId is supplied from JWT claims by the controller.
        User? user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        return user is null ? null : _mapper.Map<UserDto>(user);
    }

    /// <summary>
    /// Builds the <see cref="AuthResponseDto"/> envelope for an authenticated user: a freshly issued JWT
    /// access token, a rotated refresh token, the absolute and relative expiry, and the safe user projection.
    /// </summary>
    /// <param name="user">The authenticated user the tokens are issued for.</param>
    /// <returns>The populated authentication response.</returns>
    /// <remarks>
    /// Intentionally synchronous: token generation via <see cref="IJwtService"/> is CPU-bound and exposes no
    /// awaitable surface, so marking this method <c>async</c> would raise CS1998 under the zero-warning build
    /// gate. SECURITY: the returned <see cref="UserDto"/> carries no password material.
    /// </remarks>
    private AuthResponseDto BuildAuthResponse(User user)
    {
        var accessToken = _jwtService.GenerateAccessToken(user);
        // MIGRATION (C5/DEV-032): the refresh token is now a signed JWT bound to this user (sub +
        // token_use=refresh), replacing the broken opaque token that ValidateToken could never accept.
        var refreshToken = _jwtService.GenerateRefreshToken(user);
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
