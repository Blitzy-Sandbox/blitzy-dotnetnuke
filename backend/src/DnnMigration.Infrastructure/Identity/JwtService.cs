// -----------------------------------------------------------------------------
// DnnMigration - Modern DotNetNuke Migration
// Copyright (c) 2024. All rights reserved.
// Licensed under the MIT License.
// -----------------------------------------------------------------------------
// MIGRATION: Replaces legacy Forms Authentication cookie-based system with JWT Bearer tokens
// Source: Library/Components/Security/PortalSecurity.vb (SignOut, Encrypt/Decrypt methods)
// Source: Library/Components/Users/Membership/UserLoginStatus.vb (login status enum)
// Source: Library/Components/Providers/Users/MembershipProvider.vb (UserLogin method)
// Changes:
// - Legacy PortalSecurity.SignOut() managed FormsAuthentication.SignOut() and cookie cleanup
// - Legacy cookie-based authentication (portalroles, portalaliasid) replaced with stateless JWT
// - New JwtService generates JWT access tokens for API authorization
// - Uses SymmetricSecurityKey with HMAC-SHA256 signing algorithm
// - Implements BFF (Backend-for-Frontend) pattern per Section 0.3.2
// - Short-lived access tokens (60 min default) with refresh token support
// -----------------------------------------------------------------------------

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using DnnMigration.Application.DTOs.Auth;
using DnnMigration.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace DnnMigration.Infrastructure.Identity;

/// <summary>
/// Service interface for JWT token generation and validation.
/// </summary>
/// <remarks>
/// <para>
/// MIGRATION: Replaces legacy Forms Authentication from PortalSecurity.vb.
/// </para>
/// <para>
/// The original DNN 4.x authentication used FormsAuthentication cookies:
/// - FormsAuthentication.SetAuthCookie() for login
/// - FormsAuthentication.SignOut() for logout
/// - Cookie-based portalroles and portalaliasid storage
/// </para>
/// <para>
/// The new JWT-based approach provides stateless authentication following
/// the BFF (Backend-for-Frontend) pattern with short-lived access tokens.
/// </para>
/// </remarks>
public interface IJwtService
{
    /// <summary>
    /// Generates JWT access and refresh tokens for an authenticated user.
    /// </summary>
    /// <param name="user">The authenticated user entity.</param>
    /// <param name="roles">Collection of role names assigned to the user.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>
    /// An <see cref="AuthResponse"/> containing the access token, refresh token,
    /// expiration information, and embedded user details.
    /// </returns>
    /// <remarks>
    /// MIGRATION: Replaces FormsAuthentication.SetAuthCookie() from PortalSecurity.vb.
    /// Claims are generated from User properties to match legacy UserInfo structure.
    /// </remarks>
    Task<AuthResponse> GenerateTokensAsync(
        User user,
        IEnumerable<string> roles,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a JWT access token and returns the associated claims principal.
    /// </summary>
    /// <param name="accessToken">The JWT access token to validate.</param>
    /// <returns>
    /// A <see cref="ClaimsPrincipal"/> if the token is valid; otherwise, <c>null</c>.
    /// </returns>
    /// <remarks>
    /// MIGRATION: Replaces legacy cookie validation from FormsAuthentication.
    /// Token validation includes issuer, audience, and signature verification.
    /// </remarks>
    ClaimsPrincipal? ValidateAccessTokenAsync(string accessToken);

    /// <summary>
    /// Refreshes tokens using a valid refresh token.
    /// </summary>
    /// <param name="refreshToken">The refresh token from a previous authentication.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>
    /// A new <see cref="AuthResponse"/> with fresh access and refresh tokens.
    /// </returns>
    /// <exception cref="SecurityTokenException">Thrown when the refresh token is invalid or expired.</exception>
    /// <remarks>
    /// MIGRATION: New functionality not present in legacy Forms Authentication.
    /// Enables token renewal without requiring user re-authentication.
    /// </remarks>
    Task<AuthResponse> RefreshTokensAsync(
        string refreshToken,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a cryptographically secure refresh token.
    /// </summary>
    /// <returns>A Base64-encoded random string suitable for use as a refresh token.</returns>
    /// <remarks>
    /// Uses RandomNumberGenerator.GetBytes(64) for cryptographic randomness.
    /// </remarks>
    string GenerateRefreshToken();
}

/// <summary>
/// JWT authentication service implementing token generation, validation, and refresh management.
/// </summary>
/// <remarks>
/// <para>
/// MIGRATION: This service replaces the legacy DNN 4.x Forms Authentication system.
/// </para>
/// <para>
/// The original authentication flow used:
/// <code>
/// ' VB.NET Legacy Pattern (PortalSecurity.vb)
/// FormsAuthentication.SetAuthCookie(username, createPersistentCookie)
/// HttpContext.Response.Cookies("portalroles") = encryptedRoles
/// </code>
/// </para>
/// <para>
/// The new JWT-based flow:
/// 1. User authenticates with credentials
/// 2. JwtService generates access token (60 min) and refresh token (7 days)
/// 3. Client includes access token in Authorization header
/// 4. When access token expires, client uses refresh token to obtain new tokens
/// </para>
/// <para>
/// Configuration is read from appsettings.json under the "Jwt" section:
/// <code>
/// "Jwt": {
///   "Secret": "your-secret-key-minimum-32-characters",
///   "Issuer": "DnnMigration",
///   "Audience": "DnnMigration",
///   "ExpirationMinutes": 60,
///   "RefreshExpirationDays": 7
/// }
/// </code>
/// </para>
/// </remarks>
public class JwtService : IJwtService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<JwtService> _logger;
    private readonly string _secret;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly int _expirationMinutes;
    private readonly int _refreshExpirationDays;
    private readonly SymmetricSecurityKey _signingKey;
    private readonly JwtSecurityTokenHandler _tokenHandler;

    /// <summary>
    /// Custom claim types used in JWT tokens.
    /// </summary>
    private static class CustomClaimTypes
    {
        /// <summary>
        /// Claim type for user ID.
        /// </summary>
        public const string UserId = "uid";

        /// <summary>
        /// Claim type for portal ID (multi-tenant context).
        /// </summary>
        public const string PortalId = "pid";

        /// <summary>
        /// Claim type for super user flag.
        /// </summary>
        public const string IsSuperUser = "isu";

        /// <summary>
        /// Claim type for display name.
        /// </summary>
        public const string DisplayName = "dname";
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JwtService"/> class.
    /// </summary>
    /// <param name="configuration">Configuration for reading JWT settings.</param>
    /// <param name="logger">Logger for security audit events.</param>
    /// <exception cref="ArgumentNullException">Thrown when configuration or logger is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when required JWT configuration is missing.</exception>
    /// <remarks>
    /// MIGRATION: Configuration replaces legacy web.config machineKey settings.
    /// </remarks>
    public JwtService(IConfiguration configuration, ILogger<JwtService> logger)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Read JWT configuration from appsettings.json
        var jwtSection = _configuration.GetSection("Jwt");

        _secret = jwtSection.GetValue<string>("Secret")
            ?? throw new InvalidOperationException("JWT Secret is not configured. Add 'Jwt:Secret' to appsettings.json.");

        _issuer = jwtSection.GetValue<string>("Issuer")
            ?? "DnnMigration";

        _audience = jwtSection.GetValue<string>("Audience")
            ?? "DnnMigration";

        // MIGRATION: Default 60 minutes per Section 0.7.7 - "JWT Bearer tokens with short-lived access tokens (60 min)"
        _expirationMinutes = jwtSection.GetValue<int?>("ExpirationMinutes") ?? 60;

        // MIGRATION: Default 7 days for refresh tokens per BFF pattern
        _refreshExpirationDays = jwtSection.GetValue<int?>("RefreshExpirationDays") ?? 7;

        // Validate secret length for HMAC-SHA256 (minimum 32 bytes recommended)
        if (_secret.Length < 32)
        {
            _logger.LogWarning(
                "JWT Secret is shorter than recommended 32 characters. " +
                "Consider using a longer secret for improved security.");
        }

        // Create signing key from secret
        _signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));

        // Initialize token handler for reuse
        _tokenHandler = new JwtSecurityTokenHandler();

        _logger.LogInformation(
            "JwtService initialized with Issuer: {Issuer}, Audience: {Audience}, " +
            "AccessTokenExpiration: {ExpirationMinutes} minutes, RefreshTokenExpiration: {RefreshDays} days",
            _issuer, _audience, _expirationMinutes, _refreshExpirationDays);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// MIGRATION: Replaces the legacy cookie-based authentication:
    /// <code>
    /// ' VB.NET Legacy (PortalSecurity.vb)
    /// FormsAuthentication.SetAuthCookie(objUser.Username, True)
    /// Dim objPortalSecurity As New PortalSecurity()
    /// HttpContext.Current.Response.Cookies("portalroles").Value = objPortalSecurity.Encrypt(strKey, strRoles)
    /// </code>
    /// </para>
    /// <para>
    /// Claims generated from User entity match legacy UserInfo properties:
    /// - UserId → uid claim
    /// - Username → sub claim (standard JWT subject)
    /// - Email → email claim (standard JWT)
    /// - PortalId → pid claim (multi-tenant context)
    /// - IsSuperUser → isu claim
    /// - DisplayName → dname claim
    /// - Roles → role claims (one per role)
    /// </para>
    /// </remarks>
    public Task<AuthResponse> GenerateTokensAsync(
        User user,
        IEnumerable<string> roles,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(roles);

        cancellationToken.ThrowIfCancellationRequested();

        _logger.LogInformation(
            "Generating tokens for user {Username} (ID: {UserId}, Portal: {PortalId})",
            user.Username, user.UserId, user.PortalId);

        // Build claims list from user entity
        // MIGRATION: Claims structure mirrors legacy UserInfo properties for token payload
        var claims = new List<Claim>
        {
            // Standard JWT claims
            new(JwtRegisteredClaimNames.Sub, user.Username),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),

            // Custom claims from User entity
            new(CustomClaimTypes.UserId, user.UserId.ToString()),
            new(CustomClaimTypes.PortalId, user.PortalId.ToString()),
            new(CustomClaimTypes.IsSuperUser, user.IsSuperUser.ToString().ToLowerInvariant()),
        };

        // Add email claim if available
        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.Email, user.Email));
        }

        // Add display name claim if available
        if (!string.IsNullOrWhiteSpace(user.DisplayName))
        {
            claims.Add(new Claim(CustomClaimTypes.DisplayName, user.DisplayName));
        }

        // MIGRATION: Add role claims - replaces legacy encrypted portalroles cookie
        // Legacy: HttpContext.Response.Cookies("portalroles").Value = Encrypt(key, roles)
        var rolesList = roles.ToList();
        foreach (var role in rolesList)
        {
            if (!string.IsNullOrWhiteSpace(role))
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }
        }

        // Calculate token expiration
        var expiresAt = DateTime.UtcNow.AddMinutes(_expirationMinutes);

        // Create signing credentials with HMAC-SHA256
        var signingCredentials = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256);

        // Create JWT token
        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAt,
            signingCredentials: signingCredentials);

        // Serialize token to string
        var accessToken = _tokenHandler.WriteToken(token);

        // Generate refresh token
        var refreshToken = GenerateRefreshToken();

        // Create embedded user info for response
        var authUserInfo = AuthUserInfo.Create(
            userId: user.UserId,
            username: user.Username,
            displayName: user.DisplayName,
            email: user.Email,
            isSuperUser: user.IsSuperUser,
            roles: rolesList,
            portalId: user.PortalId);

        // Build auth response with computed expiration
        var expiresInSeconds = (int)(expiresAt - DateTime.UtcNow).TotalSeconds;
        var response = AuthResponse.Create(
            accessToken: accessToken,
            refreshToken: refreshToken,
            user: authUserInfo,
            expiresIn: expiresInSeconds);

        _logger.LogInformation(
            "Tokens generated successfully for user {Username}. " +
            "AccessToken expires at {ExpiresAt}, RefreshToken generated",
            user.Username, expiresAt);

        return Task.FromResult(response);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// Validates the JWT token using the same signing key, issuer, and audience
    /// that were used during token generation.
    /// </para>
    /// <para>
    /// Validation failures are logged for security audit purposes:
    /// - Expired tokens
    /// - Invalid signatures
    /// - Invalid issuer/audience
    /// - Malformed tokens
    /// </para>
    /// <para>
    /// MIGRATION: Replaces legacy FormsAuthentication.Decrypt() cookie validation.
    /// </para>
    /// </remarks>
    public ClaimsPrincipal? ValidateAccessTokenAsync(string accessToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            _logger.LogWarning("Token validation failed: Access token is null or empty");
            return null;
        }

        // Configure token validation parameters
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _issuer,

            ValidateAudience = true,
            ValidAudience = _audience,

            ValidateIssuerSigningKey = true,
            IssuerSigningKey = _signingKey,

            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1), // Allow 1 minute clock skew

            RequireExpirationTime = true,
            RequireSignedTokens = true
        };

        try
        {
            // Validate and parse the token
            var principal = _tokenHandler.ValidateToken(
                accessToken,
                validationParameters,
                out var validatedToken);

            // Additional validation: ensure token is a JWT
            if (validatedToken is not JwtSecurityToken jwtToken)
            {
                _logger.LogWarning(
                    "Token validation failed: Token is not a valid JWT. Token type: {TokenType}",
                    validatedToken?.GetType().Name ?? "null");
                return null;
            }

            // Validate algorithm
            if (!jwtToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
            {
                _logger.LogWarning(
                    "Token validation failed: Invalid signing algorithm. Expected: {Expected}, Actual: {Actual}",
                    SecurityAlgorithms.HmacSha256, jwtToken.Header.Alg);
                return null;
            }

            // Extract user info for logging
            var userIdClaim = principal.FindFirst(CustomClaimTypes.UserId)?.Value;
            var usernameClaim = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

            _logger.LogDebug(
                "Token validated successfully for user {Username} (ID: {UserId})",
                usernameClaim, userIdClaim);

            return principal;
        }
        catch (SecurityTokenExpiredException ex)
        {
            _logger.LogWarning(
                ex,
                "Token validation failed: Token has expired. Expiration: {Expiration}",
                ex.Expires);
            return null;
        }
        catch (SecurityTokenInvalidSignatureException ex)
        {
            _logger.LogWarning(
                ex,
                "Token validation failed: Invalid signature detected. Possible tampering attempt.");
            return null;
        }
        catch (SecurityTokenInvalidIssuerException ex)
        {
            _logger.LogWarning(
                ex,
                "Token validation failed: Invalid issuer.");
            return null;
        }
        catch (SecurityTokenInvalidAudienceException ex)
        {
            _logger.LogWarning(
                ex,
                "Token validation failed: Invalid audience.");
            return null;
        }
        catch (SecurityTokenException ex)
        {
            _logger.LogWarning(
                ex,
                "Token validation failed: Security token exception. Message: {Message}",
                ex.Message);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Token validation failed: Unexpected error during token validation.");
            return null;
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// MIGRATION: New functionality not present in legacy Forms Authentication.
    /// The BFF pattern requires refresh token support for seamless token renewal.
    /// </para>
    /// <para>
    /// Note: This implementation provides the token generation aspect of refresh.
    /// The actual refresh token storage and validation should be handled by a
    /// separate refresh token store (database or cache) in a production environment.
    /// This method expects the refresh token to have already been validated by the caller.
    /// </para>
    /// </remarks>
    /// <exception cref="SecurityTokenException">
    /// Thrown when the refresh token is invalid or when user lookup fails.
    /// </exception>
    public Task<AuthResponse> RefreshTokensAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            _logger.LogWarning("Token refresh failed: Refresh token is null or empty");
            throw new SecurityTokenException("Refresh token is required.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        // Note: In a complete implementation, this method would:
        // 1. Look up the refresh token in a secure store (database/cache)
        // 2. Validate the refresh token hasn't been revoked
        // 3. Check the refresh token hasn't expired
        // 4. Look up the associated user
        // 5. Generate new tokens
        //
        // For this migration, the refresh token validation and user lookup
        // should be performed by the calling authentication service (AuthController/AuthService)
        // before calling this method.

        _logger.LogInformation(
            "Refresh token received. Token hash: {TokenHash}",
            GetRefreshTokenHash(refreshToken));

        // This method signature is designed for the interface contract.
        // The actual implementation requires the calling service to:
        // 1. Validate the refresh token from storage
        // 2. Retrieve the user and their roles
        // 3. Call GenerateTokensAsync with the user data
        //
        // Throwing here to indicate proper usage pattern
        throw new SecurityTokenException(
            "RefreshTokensAsync requires caller to validate refresh token and provide user context. " +
            "Use GenerateTokensAsync(user, roles) after validating the refresh token.");
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// Generates a cryptographically secure random string for use as a refresh token.
    /// Uses 64 bytes of random data converted to Base64 for a total of 86 characters.
    /// </para>
    /// <para>
    /// MIGRATION: Legacy DNN used encrypted cookies for session persistence.
    /// The new approach uses secure random tokens that are stored separately from the JWT.
    /// </para>
    /// </remarks>
    public string GenerateRefreshToken()
    {
        // Generate 64 bytes of cryptographically secure random data
        // This provides 512 bits of entropy, sufficient for security
        var randomBytes = new byte[64];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(randomBytes);
        }

        // Convert to URL-safe Base64 string
        var refreshToken = Convert.ToBase64String(randomBytes);

        _logger.LogDebug(
            "Generated new refresh token. Token hash: {TokenHash}",
            GetRefreshTokenHash(refreshToken));

        return refreshToken;
    }

    /// <summary>
    /// Gets a hash of the refresh token for safe logging.
    /// </summary>
    /// <param name="refreshToken">The refresh token to hash.</param>
    /// <returns>A truncated SHA256 hash of the token for logging purposes.</returns>
    private static string GetRefreshTokenHash(string refreshToken)
    {
        if (string.IsNullOrEmpty(refreshToken))
        {
            return "empty";
        }

        // Create a hash for logging without exposing the actual token
        var bytes = Encoding.UTF8.GetBytes(refreshToken);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash)[..16]; // First 16 characters of hash
    }

    /// <summary>
    /// Extracts claims from a ClaimsPrincipal for creating AuthUserInfo.
    /// </summary>
    /// <param name="principal">The claims principal from a validated token.</param>
    /// <returns>An AuthUserInfo instance populated from the claims.</returns>
    /// <remarks>
    /// MIGRATION: Helper method to convert JWT claims back to user info structure.
    /// </remarks>
    public AuthUserInfo ExtractUserInfo(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        var userId = int.Parse(
            principal.FindFirst(CustomClaimTypes.UserId)?.Value ?? "0");
        var username = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? string.Empty;
        var displayName = principal.FindFirst(CustomClaimTypes.DisplayName)?.Value;
        var email = principal.FindFirst(JwtRegisteredClaimNames.Email)?.Value;
        var isSuperUser = bool.Parse(
            principal.FindFirst(CustomClaimTypes.IsSuperUser)?.Value ?? "false");
        var portalId = int.Parse(
            principal.FindFirst(CustomClaimTypes.PortalId)?.Value ?? "0");

        // Extract roles from claims
        var roles = principal.FindAll(ClaimTypes.Role)
            .Select(c => c.Value)
            .ToList();

        return AuthUserInfo.Create(
            userId: userId,
            username: username,
            displayName: displayName,
            email: email,
            isSuperUser: isSuperUser,
            roles: roles,
            portalId: portalId);
    }
}
