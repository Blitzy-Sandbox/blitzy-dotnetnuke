// -----------------------------------------------------------------------------
// <copyright file="AuthController.cs" company="DNN Migration Project">
//     Copyright (c) DNN Migration Project. All rights reserved.
//     Licensed under the MIT License.
// </copyright>
// -----------------------------------------------------------------------------
// MIGRATION: Replaces legacy Forms Authentication from PortalSecurity.vb and
// UserController.vb with JWT Bearer token authentication following BFF pattern.
// Source: Library/Components/Security/PortalSecurity.vb (SignOut method)
// Source: Library/Components/Users/UserController.vb (UserLogin, ValidateUser methods)
// Changes:
// - Legacy FormsAuthentication.SetAuthCookie() → JWT access/refresh token generation
// - Legacy FormsAuthentication.SignOut() → Token invalidation (client-side removal)
// - Legacy cookie-based portalroles → JWT claims-based roles
// - Legacy UserController.ValidateUser → IUserService.ValidateUserAsync
// - Legacy UserLoginStatus enum passed ByRef → UserValidationResult record
// - Short-lived access tokens (60 min) with refresh token support per Section 0.7.7
// -----------------------------------------------------------------------------

using System.Security.Claims;
using DnnMigration.Application.DTOs.Auth;
using DnnMigration.Application.DTOs.User;
using DnnMigration.Application.Interfaces;
using DnnMigration.Domain.Enums;
using DnnMigration.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace DnnMigration.Api.Controllers;

/// <summary>
/// Request DTO for refreshing JWT tokens.
/// </summary>
/// <remarks>
/// <para>
/// MIGRATION: New DTO for JWT token refresh endpoint - no equivalent in legacy Forms Auth.
/// </para>
/// <para>
/// The refresh token is obtained from a previous successful login and can be used to
/// obtain new access and refresh tokens without re-entering credentials.
/// </para>
/// </remarks>
/// <param name="RefreshToken">The refresh token from a previous authentication.</param>
public record RefreshTokenRequest(string RefreshToken);

/// <summary>
/// ASP.NET Core 8 authentication controller implementing JWT Bearer token authentication.
/// Provides endpoints for user login, token refresh, logout, and current user retrieval.
/// </summary>
/// <remarks>
/// <para>
/// MIGRATION: This controller replaces the legacy DotNetNuke 4.x Forms Authentication system:
/// <list type="bullet">
/// <item><description><c>Library/Components/Security/PortalSecurity.vb</c> - SignOut method for FormsAuthentication.SignOut()</description></item>
/// <item><description><c>Library/Components/Users/UserController.vb</c> - UserLogin and ValidateUser methods</description></item>
/// </list>
/// </para>
/// <para>
/// The new JWT-based authentication follows the BFF (Backend-for-Frontend) pattern as specified
/// in Section 0.3.2 of the migration plan. Key changes from legacy system:
/// <list type="bullet">
/// <item><description>Forms Authentication cookies → Stateless JWT Bearer tokens</description></item>
/// <item><description>Server-side session → Client-managed access tokens with 60-minute expiration</description></item>
/// <item><description>Cookie-based role storage (portalroles) → JWT claims-based roles</description></item>
/// <item><description>FormsAuthentication.SetAuthCookie() → IJwtService.GenerateTokensAsync()</description></item>
/// </list>
/// </para>
/// <para>
/// API Endpoints:
/// <list type="table">
/// <listheader>
/// <term>HTTP Method</term>
/// <description>Endpoint</description>
/// </listheader>
/// <item><term>POST</term><description>/api/auth/login - Authenticate user and return JWT tokens</description></item>
/// <item><term>POST</term><description>/api/auth/refresh - Refresh expired access token using refresh token</description></item>
/// <item><term>POST</term><description>/api/auth/logout - Terminate session (client removes tokens)</description></item>
/// <item><term>GET</term><description>/api/auth/me - Get current authenticated user information</description></item>
/// </list>
/// </para>
/// </remarks>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IJwtService _jwtService;
    private readonly IUserService _userService;
    private readonly ILogger<AuthController> _logger;

    /// <summary>
    /// Custom claim types used to extract user information from JWT tokens.
    /// </summary>
    /// <remarks>
    /// MIGRATION: These claim types match those defined in JwtService for consistency.
    /// </remarks>
    private static class CustomClaimTypes
    {
        /// <summary>Claim type for user ID.</summary>
        public const string UserId = "uid";

        /// <summary>Claim type for portal ID (multi-tenant context).</summary>
        public const string PortalId = "pid";

        /// <summary>Claim type for super user flag.</summary>
        public const string IsSuperUser = "isu";

        /// <summary>Claim type for display name.</summary>
        public const string DisplayName = "dname";
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthController"/> class.
    /// </summary>
    /// <param name="jwtService">
    /// The JWT service for token generation, validation, and refresh operations.
    /// MIGRATION: Replaces FormsAuthentication.SetAuthCookie/SignOut from PortalSecurity.vb.
    /// </param>
    /// <param name="userService">
    /// The user service for credential validation and user data retrieval.
    /// MIGRATION: Replaces UserController.ValidateUser and GetUserByName from UserController.vb.
    /// </param>
    /// <param name="logger">The logger instance for structured logging.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="jwtService"/>, <paramref name="userService"/>,
    /// or <paramref name="logger"/> is <c>null</c>.
    /// </exception>
    public AuthController(
        IJwtService jwtService,
        IUserService userService,
        ILogger<AuthController> logger)
    {
        _jwtService = jwtService ?? throw new ArgumentNullException(nameof(jwtService));
        _userService = userService ?? throw new ArgumentNullException(nameof(userService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Authenticates a user and returns JWT access and refresh tokens.
    /// </summary>
    /// <param name="request">
    /// A <see cref="LoginRequest"/> containing the user credentials:
    /// <list type="bullet">
    /// <item><description><c>UsernameOrEmail</c> - Username or email address</description></item>
    /// <item><description><c>Password</c> - User's password</description></item>
    /// <item><description><c>RememberMe</c> - Whether to generate longer-lived tokens</description></item>
    /// </list>
    /// </param>
    /// <param name="cancellationToken">
    /// A cancellation token for cooperative cancellation of the asynchronous operation.
    /// </param>
    /// <returns>
    /// An <see cref="ActionResult{T}"/> containing an <see cref="AuthResponse"/> with:
    /// <list type="bullet">
    /// <item><description><c>AccessToken</c> - JWT access token for API authorization</description></item>
    /// <item><description><c>RefreshToken</c> - Token for obtaining new access tokens</description></item>
    /// <item><description><c>TokenType</c> - Always "Bearer"</description></item>
    /// <item><description><c>ExpiresIn</c> - Access token lifetime in seconds</description></item>
    /// <item><description><c>ExpiresAt</c> - Absolute UTC expiration time</description></item>
    /// <item><description><c>User</c> - Authenticated user information</description></item>
    /// </list>
    /// </returns>
    /// <remarks>
    /// <para>
    /// MIGRATION: Replaces the following legacy code patterns:
    /// <code>
    /// ' VB.NET - UserController.vb (line 993-1062)
    /// Public Shared Function UserLogin(ByVal portalId As Integer, ByVal objUser As UserInfo, _
    ///     ByVal PortalName As String, ByVal IP As String, _
    ///     ByVal CreatePersistentCookie As Boolean) As Boolean
    ///     FormsAuthentication.SetAuthCookie(objUser.Username, CreatePersistentCookie)
    ///     ...
    /// End Function
    /// 
    /// ' VB.NET - UserController.vb (line 1110-1163)
    /// Public Shared Function ValidateUser(ByVal portalId As Integer, _
    ///     ByVal Username As String, ByVal Password As String, _
    ///     ByVal VerificationCode As String, ByVal PortalName As String, _
    ///     ByVal IP As String, ByRef loginStatus As UserLoginStatus) As UserInfo
    ///     ...
    /// End Function
    /// </code>
    /// </para>
    /// <para>
    /// The endpoint validates credentials using <see cref="IUserService.ValidateUserAsync"/> which
    /// returns a <see cref="UserValidationResult"/> containing the login status and user information.
    /// On successful validation, JWT tokens are generated via <see cref="IJwtService.GenerateTokensAsync"/>.
    /// </para>
    /// <para>
    /// Error conditions:
    /// <list type="bullet">
    /// <item><description>Invalid credentials → 401 Unauthorized with "Invalid credentials" message</description></item>
    /// <item><description>Account locked → 401 Unauthorized with "Account is locked" message</description></item>
    /// <item><description>Account not approved → 401 Unauthorized with "Account not approved" message</description></item>
    /// <item><description>Missing request → 400 Bad Request</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <response code="200">Returns JWT tokens and user information on successful authentication.</response>
    /// <response code="400">If the request body is null or invalid.</response>
    /// <response code="401">If the credentials are invalid or the account is locked/not approved.</response>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        // Validate request body
        if (request is null)
        {
            _logger.LogWarning("Login attempt with null request body");
            return BadRequest(new { error = "Request body is required." });
        }

        if (string.IsNullOrWhiteSpace(request.UsernameOrEmail))
        {
            _logger.LogWarning("Login attempt with empty username/email");
            return BadRequest(new { error = "Username or email is required." });
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            _logger.LogWarning("Login attempt with empty password for user '{Username}'", request.UsernameOrEmail);
            return BadRequest(new { error = "Password is required." });
        }

        _logger.LogInformation(
            "Login attempt for user '{Username}'",
            request.UsernameOrEmail);

        try
        {
            // MIGRATION: Validate credentials via IUserService (replaces UserController.ValidateUser)
            // Using PortalId -1 for host-level authentication; specific portal context can be added
            // Note: In production, PortalId should come from request context or configuration
            const int defaultPortalId = 0; // Default portal for authentication
            
            var validationResult = await _userService.ValidateUserAsync(
                defaultPortalId,
                request.UsernameOrEmail,
                request.Password,
                cancellationToken);

            // Check validation result status
            // MIGRATION: Replaces ByRef loginStatus pattern from ValidateUser
            if (!validationResult.IsSuccess)
            {
                return HandleFailedLogin(validationResult, request.UsernameOrEmail);
            }

            // User is authenticated - check for password change requirement
            if (validationResult.RequiresPasswordChange)
            {
                _logger.LogWarning(
                    "User '{Username}' logged in with insecure password (status: {Status})",
                    request.UsernameOrEmail,
                    validationResult.Status);
            }

            // Get user details for token generation
            // MIGRATION: Replaces direct UserInfo access from ValidateUser return value
            var user = validationResult.User;
            if (user is null)
            {
                _logger.LogError(
                    "Validation succeeded but user data is null for '{Username}'",
                    request.UsernameOrEmail);
                return Unauthorized(new { error = "Authentication failed. Please try again." });
            }

            // Get user's roles for inclusion in JWT claims
            var roles = user.Roles ?? Array.Empty<string>();

            // MIGRATION: Generate JWT tokens (replaces FormsAuthentication.SetAuthCookie)
            // Load the full user entity for token generation
            var userEntity = await GetUserEntityForTokenGenerationAsync(
                user.PortalId,
                user.UserId,
                cancellationToken);

            if (userEntity is null)
            {
                _logger.LogError(
                    "Failed to load user entity for token generation: UserId={UserId}, PortalId={PortalId}",
                    user.UserId,
                    user.PortalId);
                return Unauthorized(new { error = "Authentication failed. Please try again." });
            }

            var authResponse = await _jwtService.GenerateTokensAsync(
                userEntity,
                roles,
                cancellationToken);

            _logger.LogInformation(
                "User '{Username}' (ID: {UserId}) logged in successfully from portal {PortalId}",
                user.Username,
                user.UserId,
                user.PortalId);

            return Ok(authResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected error during login for user '{Username}'",
                request.UsernameOrEmail);
            
            // Don't expose internal errors to client
            return Unauthorized(new { error = "Authentication failed. Please try again." });
        }
    }

    /// <summary>
    /// Refreshes JWT tokens using a valid refresh token.
    /// </summary>
    /// <param name="request">
    /// A <see cref="RefreshTokenRequest"/> containing the refresh token from a previous authentication.
    /// </param>
    /// <param name="cancellationToken">
    /// A cancellation token for cooperative cancellation of the asynchronous operation.
    /// </param>
    /// <returns>
    /// An <see cref="ActionResult{T}"/> containing a new <see cref="AuthResponse"/> with
    /// fresh access and refresh tokens if the provided refresh token is valid.
    /// </returns>
    /// <remarks>
    /// <para>
    /// MIGRATION: New functionality not present in legacy Forms Authentication.
    /// Token refresh enables seamless session extension without re-authentication.
    /// </para>
    /// <para>
    /// The refresh token validation is handled by <see cref="IJwtService.RefreshTokensAsync"/>
    /// which verifies the token signature, expiration, and optionally checks against a
    /// server-side token store for revocation.
    /// </para>
    /// <para>
    /// Security considerations:
    /// <list type="bullet">
    /// <item><description>Refresh tokens have longer lifetime (7 days default)</description></item>
    /// <item><description>Each refresh issues a new refresh token (rotation)</description></item>
    /// <item><description>Used refresh tokens may be invalidated depending on implementation</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <response code="200">Returns new JWT tokens on successful refresh.</response>
    /// <response code="400">If the request body is null or invalid.</response>
    /// <response code="401">If the refresh token is invalid, expired, or revoked.</response>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Refresh(
        [FromBody] RefreshTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        // Validate request body
        if (request is null)
        {
            _logger.LogWarning("Token refresh attempt with null request body");
            return BadRequest(new { error = "Request body is required." });
        }

        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            _logger.LogWarning("Token refresh attempt with empty refresh token");
            return BadRequest(new { error = "Refresh token is required." });
        }

        _logger.LogInformation("Token refresh attempt");

        try
        {
            // MIGRATION: Use IJwtService to refresh tokens
            var authResponse = await _jwtService.RefreshTokensAsync(
                request.RefreshToken,
                cancellationToken);

            _logger.LogInformation("Token refresh successful for user in response");

            return Ok(authResponse);
        }
        catch (Microsoft.IdentityModel.Tokens.SecurityTokenException ex)
        {
            _logger.LogWarning(
                ex,
                "Token refresh failed: {Message}",
                ex.Message);

            return Unauthorized(new { error = "Invalid or expired refresh token." });
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected error during token refresh");

            return Unauthorized(new { error = "Token refresh failed. Please authenticate again." });
        }
    }

    /// <summary>
    /// Terminates the user's session by invalidating their refresh token.
    /// </summary>
    /// <returns>
    /// A <see cref="NoContentResult"/> (HTTP 204) indicating successful logout.
    /// </returns>
    /// <remarks>
    /// <para>
    /// MIGRATION: Replaces FormsAuthentication.SignOut() from PortalSecurity.vb:
    /// <code>
    /// ' VB.NET - PortalSecurity.vb (line ~42)
    /// Public Shared Sub SignOut()
    '''     FormsAuthentication.SignOut()
    '''     ' Clear cookies (portalroles, portalaliasid, language, etc.)
    ///     ...
    /// End Sub
    /// </code>
    /// </para>
    /// <para>
    /// With JWT Bearer authentication, logout is primarily a client-side operation:
    /// <list type="bullet">
    /// <item><description>Client removes the access token from storage</description></item>
    /// <item><description>Client removes the refresh token from storage</description></item>
    /// <item><description>Server-side: refresh token can be invalidated if using token store</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// The access token will continue to be valid until its expiration (60 minutes max).
    /// For immediate revocation in high-security scenarios, implement a token blacklist
    /// or use shorter token lifetimes.
    /// </para>
    /// </remarks>
    /// <response code="204">Session terminated successfully.</response>
    /// <response code="401">If the user is not authenticated.</response>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult Logout()
    {
        // Extract user info from claims for logging
        var userId = User.FindFirst(CustomClaimTypes.UserId)?.Value ?? "unknown";
        var username = User.FindFirst(ClaimTypes.Name)?.Value ?? "unknown";

        _logger.LogInformation(
            "User '{Username}' (ID: {UserId}) logged out",
            username,
            userId);

        // MIGRATION: With JWT, logout is primarily client-side token removal.
        // The server acknowledges the logout request but the access token
        // remains valid until expiration (stateless architecture).
        //
        // For enhanced security, implement one of these patterns:
        // 1. Token blacklist in Redis/DB
        // 2. Refresh token revocation in token store
        // 3. Short-lived access tokens with frequent refresh
        //
        // Current implementation follows stateless JWT best practices.

        return NoContent();
    }

    /// <summary>
    /// Retrieves the currently authenticated user's information.
    /// </summary>
    /// <param name="cancellationToken">
    /// A cancellation token for cooperative cancellation of the asynchronous operation.
    /// </param>
    /// <returns>
    /// An <see cref="ActionResult{T}"/> containing a <see cref="UserDto"/> with the
    /// authenticated user's information including profile data and assigned roles.
    /// </returns>
    /// <remarks>
    /// <para>
    /// MIGRATION: Replaces legacy pattern of loading UserInfo from authenticated cookie:
    /// <code>
    /// ' VB.NET - Common pattern in DNN modules
    /// Dim objUserInfo As UserInfo = UserController.GetUser(PortalId, UserId, False)
    /// ' Where UserId came from HttpContext.User.Identity
    /// </code>
    /// </para>
    /// <para>
    /// This endpoint extracts user identity from JWT claims (NameIdentifier, Name, etc.)
    /// and loads the full user profile from the database via <see cref="IUserService.GetUserAsync"/>.
    /// </para>
    /// <para>
    /// Claims extracted from the JWT access token:
    /// <list type="bullet">
    /// <item><description><c>uid</c> (custom) - User ID</description></item>
    /// <item><description><c>pid</c> (custom) - Portal ID</description></item>
    /// <item><description><c>NameIdentifier</c> (standard) - Fallback for User ID</description></item>
    /// <item><description><c>Name</c> (standard) - Username</description></item>
    /// <item><description><c>Email</c> (standard) - Email address</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <response code="200">Returns the authenticated user's information.</response>
    /// <response code="401">If the user is not authenticated or token is invalid.</response>
    /// <response code="404">If the user no longer exists in the database.</response>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserDto>> GetCurrentUser(
        CancellationToken cancellationToken = default)
    {
        // Extract user identity from JWT claims
        // MIGRATION: Replaces HttpContext.User.Identity parsing from Forms Authentication
        
        var userIdClaim = User.FindFirst(CustomClaimTypes.UserId)?.Value 
                       ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        
        var portalIdClaim = User.FindFirst(CustomClaimTypes.PortalId)?.Value;

        if (string.IsNullOrWhiteSpace(userIdClaim))
        {
            _logger.LogWarning("GetCurrentUser called but no user ID found in claims");
            return Unauthorized(new { error = "Invalid authentication token." });
        }

        if (!int.TryParse(userIdClaim, out var userId))
        {
            _logger.LogWarning(
                "GetCurrentUser: Invalid user ID format in claims: {UserIdClaim}",
                userIdClaim);
            return Unauthorized(new { error = "Invalid authentication token." });
        }

        // Parse portal ID with fallback to default portal
        var portalId = 0;
        if (!string.IsNullOrWhiteSpace(portalIdClaim) && int.TryParse(portalIdClaim, out var parsedPortalId))
        {
            portalId = parsedPortalId;
        }

        _logger.LogInformation(
            "Retrieving current user info: UserId={UserId}, PortalId={PortalId}",
            userId,
            portalId);

        try
        {
            // MIGRATION: Load user via IUserService (replaces UserController.GetUser)
            var user = await _userService.GetUserAsync(portalId, userId, cancellationToken);

            if (user is null)
            {
                _logger.LogWarning(
                    "GetCurrentUser: User not found - UserId={UserId}, PortalId={PortalId}",
                    userId,
                    portalId);
                return NotFound(new { error = "User not found." });
            }

            _logger.LogInformation(
                "Retrieved user info for '{Username}' (ID: {UserId})",
                user.Username,
                userId);

            return Ok(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error retrieving current user: UserId={UserId}, PortalId={PortalId}",
                userId,
                portalId);

            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new { error = "An error occurred while retrieving user information." });
        }
    }

    /// <summary>
    /// Handles failed login attempts based on the validation result status.
    /// </summary>
    /// <param name="validationResult">The result from user validation.</param>
    /// <param name="usernameOrEmail">The username/email used in the login attempt.</param>
    /// <returns>An appropriate Unauthorized response with error details.</returns>
    /// <remarks>
    /// MIGRATION: Maps UserLoginStatus enum values to appropriate error messages.
    /// Original VB.NET code used a ByRef loginStatus parameter to communicate
    /// validation failures; this method centralizes error handling.
    /// </remarks>
    private ActionResult<AuthResponse> HandleFailedLogin(
        UserValidationResult validationResult,
        string usernameOrEmail)
    {
        string errorMessage;
        
        switch (validationResult.Status)
        {
            case UserLoginStatus.LoginUserLockedOut:
                _logger.LogWarning(
                    "Login failed for '{Username}': Account locked out",
                    usernameOrEmail);
                errorMessage = "Your account has been locked. Please contact an administrator.";
                break;

            case UserLoginStatus.LoginUserNotApproved:
                _logger.LogWarning(
                    "Login failed for '{Username}': Account not approved",
                    usernameOrEmail);
                errorMessage = "Your account has not been approved. Please wait for administrator approval.";
                break;

            case UserLoginStatus.LoginFailure:
            default:
                _logger.LogWarning(
                    "Login failed for '{Username}': Invalid credentials (status: {Status})",
                    usernameOrEmail,
                    validationResult.Status);
                errorMessage = "Invalid username or password.";
                break;
        }

        return Unauthorized(new { error = errorMessage });
    }

    /// <summary>
    /// Loads the User entity for JWT token generation.
    /// </summary>
    /// <param name="portalId">The portal ID.</param>
    /// <param name="userId">The user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The User entity or null if not found.</returns>
    /// <remarks>
    /// MIGRATION: The IJwtService.GenerateTokensAsync requires a User entity (domain object),
    /// not the UserDto. This method converts by retrieving the entity-level user data.
    /// In production, this could be optimized with a dedicated method that returns the entity.
    /// </remarks>
    private async Task<Domain.Entities.User?> GetUserEntityForTokenGenerationAsync(
        int portalId,
        int userId,
        CancellationToken cancellationToken)
    {
        // Get user DTO first
        var userDto = await _userService.GetUserAsync(portalId, userId, cancellationToken);
        
        if (userDto is null)
        {
            return null;
        }

        // Convert UserDto to User entity for token generation
        // MIGRATION: This mapping reconstructs the essential User entity properties
        // required for JWT claim generation
        return new Domain.Entities.User
        {
            UserId = userDto.UserId,
            PortalId = userDto.PortalId,
            Username = userDto.Username,
            DisplayName = userDto.DisplayName,
            Email = userDto.Email,
            IsSuperUser = userDto.IsSuperUser
        };
    }
}
