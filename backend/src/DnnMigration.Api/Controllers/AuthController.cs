using System.Security.Claims;
using DnnMigration.Application.Common;
using DnnMigration.Application.DTOs.Auth;
using DnnMigration.Application.Interfaces;
using DnnMigration.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace DnnMigration.Api.Controllers;

/// <summary>JWT authentication endpoints (/api/auth/{login,refresh,logout,me}).</summary>
// MIGRATION: Replaces legacy ASP.NET Forms Authentication + 56-bit DES (PortalSecurity.vb SignOut/Encrypt/
// Decrypt, IsInRole) with stateless JWT Bearer tokens + BCrypt. The API holds no session: identity is carried
// in JWT claims, so logout/me extract the user id from the authenticated ClaimsPrincipal and pass it to the
// service (the Application layer never touches HttpContext/ClaimsPrincipal).
//
// MIGRATION (CP-FINAL / Code-Review G3): the refresh token is NEVER returned in the JSON body. Instead it is
// issued to the client as an HttpOnly + Secure + SameSite=Strict cookie scoped to the /api/auth path, so it
// is unreadable by JavaScript and cannot be exfiltrated by XSS on the SPA origin (the SPA keeps only the
// short-lived access token in memory). /refresh reads the token from that cookie (falling back to the body
// only for non-browser/legacy callers) and /logout deletes it. Documented in root MIGRATION_NOTES.md.
[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public sealed class AuthController : ControllerBase
{
    /// <summary>
    /// Name of the HttpOnly refresh-token cookie. Scoped to <see cref="RefreshCookiePath"/> so it is sent
    /// only to the auth endpoints that need it.
    /// </summary>
    private const string RefreshCookieName = "dnn_refresh_token";

    /// <summary>Path the refresh cookie is scoped to (it is sent only to /api/auth/*).</summary>
    private const string RefreshCookiePath = "/api/auth";

    private readonly IAuthService _authService;
    private readonly JwtSettings _jwtSettings;

    /// <summary>Initializes the controller with the auth orchestration service and JWT options (for cookie lifetime).</summary>
    /// <param name="authService">The authentication orchestration service.</param>
    /// <param name="jwtOptions">JWT options; <see cref="JwtSettings.RefreshTokenExpirationDays"/> sets the refresh-cookie lifetime so it matches the refresh token.</param>
    public AuthController(IAuthService authService, IOptions<JwtSettings> jwtOptions)
    {
        _authService = authService;
        _jwtSettings = jwtOptions.Value;
    }

    /// <summary>Authenticate with credentials and receive an access token; the refresh token is set as an HttpOnly cookie.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request, CancellationToken cancellationToken = default)
    {
        var result = await _authService.LoginAsync(request, cancellationToken);
        IssueRefreshCookie(result);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>Exchange the refresh-token cookie for a fresh access token and a rotated refresh cookie.</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequestDto? request, CancellationToken cancellationToken = default)
    {
        // MIGRATION (CP-FINAL / Code-Review G3): prefer the HttpOnly cookie; fall back to the request body only
        // for non-browser/legacy callers that do not use the cookie transport.
        var cookieToken = Request.Cookies[RefreshCookieName];
        var refreshToken = !string.IsNullOrWhiteSpace(cookieToken) ? cookieToken : request?.RefreshToken;

        var result = await _authService.RefreshAsync(
            new RefreshTokenRequestDto { RefreshToken = refreshToken },
            cancellationToken);

        IssueRefreshCookie(result);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>Log the current user out: revoke their refresh-token families server-side and delete the cookie.</summary>
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken = default)
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdValue, out var userId))
        {
            return Unauthorized();
        }

        await _authService.LogoutAsync(userId, cancellationToken);
        DeleteRefreshCookie();
        return NoContent();
    }

    /// <summary>Return the authenticated user's profile.</summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me(CancellationToken cancellationToken = default)
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdValue, out var userId))
        {
            return Unauthorized();
        }

        var user = await _authService.GetCurrentUserAsync(userId, cancellationToken);
        return user is null ? NotFound() : Ok(ApiResponse.Success(user));
    }

    // -------------------------------------------------------------------------
    // Refresh-cookie helpers (CP-FINAL / Code-Review G3)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Writes the freshly issued refresh token from <paramref name="result"/> to the HttpOnly cookie and then
    /// REMOVES it from the response body, so the refresh credential travels ONLY in the non-JS-readable cookie.
    /// </summary>
    /// <param name="result">The auth result whose <see cref="AuthResponseDto.RefreshToken"/> is moved to the cookie.</param>
    private void IssueRefreshCookie(AuthResponseDto result)
    {
        if (!string.IsNullOrEmpty(result.RefreshToken))
        {
            Response.Cookies.Append(RefreshCookieName, result.RefreshToken, BuildRefreshCookieOptions(
                // The cookie lifetime matches the refresh-token lifetime so the two expire together.
                DateTimeOffset.UtcNow.AddDays(_jwtSettings.RefreshTokenExpirationDays)));
        }

        // SECURITY: never expose the refresh token to JavaScript via the JSON body — the cookie is the sole
        // transport. The access token (memory-only on the client) remains in the body.
        result.RefreshToken = null;
    }

    /// <summary>Deletes the refresh cookie on logout, using attributes that match how it was set so browsers remove it.</summary>
    private void DeleteRefreshCookie() =>
        // Expires is irrelevant for a delete; pass a value so the same options builder can be reused. The
        // Path/Secure/SameSite/HttpOnly attributes MUST match the original cookie for the delete to take effect.
        Response.Cookies.Delete(RefreshCookieName, BuildRefreshCookieOptions(DateTimeOffset.UtcNow));

    /// <summary>
    /// Builds the cookie attributes shared by issuance and deletion: HttpOnly (no JS access), Secure
    /// (HTTPS-only), SameSite=Strict (CSRF defense), and the <see cref="RefreshCookiePath"/> scope.
    /// </summary>
    /// <param name="expires">Absolute expiry for the cookie (ignored by a delete).</param>
    /// <returns>The configured <see cref="CookieOptions"/>.</returns>
    private static CookieOptions BuildRefreshCookieOptions(DateTimeOffset expires) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = RefreshCookiePath,
        Expires = expires
    };
}
