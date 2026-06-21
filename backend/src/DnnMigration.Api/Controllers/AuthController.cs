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
// service (the Application layer never touches HttpContext/ClaimsPrincipal). Documented in root MIGRATION_NOTES.md.
//
// MIGRATION (Finding CP-FINAL-2, CWE-922): the long-lived REFRESH token is NEVER returned in the response
// body or exposed to client-side JavaScript. It is transported exclusively in an httpOnly, SameSite=Strict
// cookie scoped to /api/auth so that a cross-site-scripting payload cannot read or exfiltrate it. The body
// of /login and /refresh carries ONLY the short-lived access token (held in memory by the SPA). The
// AuthService remains HTTP-agnostic (it returns both tokens in the DTO); this controller is the single place
// that moves the refresh token into the cookie and strips it from the body. Refresh-token ROTATION stays
// stateless per AAP §0.6.2 (no server-side store, to permit horizontal scaling); the accepted residual risk
// (a stolen refresh token is valid until expiry, with no server-side revocation) is recorded in MIGRATION_NOTES.md.
[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public sealed class AuthController : ControllerBase
{
    // MIGRATION (Finding CP-FINAL-2): the httpOnly refresh-token cookie. Scoped to /api/auth (Path) so it is
    // only ever sent to the refresh/logout endpoints, never to resource APIs, minimizing its transmission surface.
    private const string RefreshTokenCookieName = "dnn_refresh_token";

    private readonly IAuthService _authService;
    private readonly JwtSettings _jwtSettings;

    public AuthController(IAuthService authService, IOptions<JwtSettings> jwtOptions)
    {
        _authService = authService;
        _jwtSettings = jwtOptions.Value;
    }

    /// <summary>Authenticate with credentials and receive an access token; the refresh token is set as an httpOnly cookie.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    // MIGRATION (Finding F5 DoS defense-in-depth, AAP §0.7.2 non-functional security): cap the request body at
    // 8 KiB — ~18x the largest legitimate credential payload (a 256-char username + 256-char password JSON is
    // well under 1 KiB) — so an attacker cannot stream a multi-megabyte body into model binding. Kestrel aborts
    // the over-limit read with a 413 BadHttpRequestException, which ExceptionHandlingMiddleware renders as a
    // clean RFC 7807 response. (In the integration TestServer the size feature is absent, so this is a no-op.)
    // Recorded as DEV-073 in root MIGRATION_NOTES.md.
    [RequestSizeLimit(8192)]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request, CancellationToken cancellationToken = default)
    {
        var result = await _authService.LoginAsync(request, cancellationToken);
        // MIGRATION (Finding CP-FINAL-2): move the refresh token into the httpOnly cookie and strip it from the body.
        IssueRefreshTokenCookie(result.RefreshToken);
        result.RefreshToken = null;
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>
    /// Exchange the httpOnly refresh-token cookie for a fresh access + refresh token pair (single-use rotation).
    /// The incoming refresh token is read from the <c>dnn_refresh_token</c> cookie, NOT from the request body.
    /// </summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    // MIGRATION (Finding F5 DoS defense-in-depth): the refresh token arrives in the httpOnly cookie, so the
    // request body is empty; an 8 KiB cap bounds any abusive payload with ample headroom. Same rationale and
    // 413 handling as Login above (no-op under the integration TestServer). Recorded as DEV-073 in MIGRATION_NOTES.md.
    [RequestSizeLimit(8192)]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken = default)
    {
        // MIGRATION (Finding CP-FINAL-2): the refresh token arrives in the httpOnly cookie, not the body. A
        // missing/blank cookie yields a blank token, which AuthService.RefreshAsync rejects as 401 (Unauthorized).
        var refreshToken = Request.Cookies[RefreshTokenCookieName];
        var result = await _authService.RefreshAsync(new RefreshTokenRequestDto { RefreshToken = refreshToken }, cancellationToken);
        // Rotate: write the NEW refresh token back into the cookie and strip it from the body.
        IssueRefreshTokenCookie(result.RefreshToken);
        result.RefreshToken = null;
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>
    /// Log the current user out. MIGRATION (m1/DEV-028 + Finding CP-FINAL-2): logout is STATELESS — the server
    /// keeps no refresh-token store, so it performs NO server-side token revocation (AAP §0.6.2). It DOES expire
    /// the browser-managed httpOnly refresh-token cookie (the SPA cannot clear it from JavaScript), and the
    /// short-lived access token held in the SPA's memory is discarded client-side and simply expires. The
    /// accepted residual risk (a previously-issued refresh token remains valid until expiry) is recorded in
    /// root MIGRATION_NOTES.md.
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken = default)
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdValue, out var userId))
        {
            // MIGRATION (M9/DEV-037): RFC 7807 ProblemDetails (application/problem+json) instead of a bare
            // Unauthorized(), per the AAP error contract; the future ExceptionHandlingMiddleware emits the same shape.
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Unauthorized", detail: "The access token does not contain a valid user identifier.");
        }

        await _authService.LogoutAsync(userId, cancellationToken);
        // MIGRATION (Finding CP-FINAL-2): logout is stateless server-side (AAP §0.6.2), but the client's
        // httpOnly refresh-token cookie is browser-managed and cannot be cleared by JavaScript — so the
        // server expires it here. The short-lived access token is discarded client-side and simply expires.
        ClearRefreshTokenCookie();
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
            // MIGRATION (M9/DEV-037): RFC 7807 ProblemDetails instead of a bare Unauthorized().
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Unauthorized", detail: "The access token does not contain a valid user identifier.");
        }

        var user = await _authService.GetCurrentUserAsync(userId, cancellationToken);
        return user is null
            ? Problem(statusCode: StatusCodes.Status404NotFound, title: "User not found", detail: $"No user exists with id {userId}.")
            : Ok(ApiResponse.Success(user));
    }

    // -------------------------------------------------------------------------
    // MIGRATION (Finding CP-FINAL-2): httpOnly refresh-token cookie helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Writes the rotated refresh token into the httpOnly <c>dnn_refresh_token</c> cookie. The cookie is:
    /// <list type="bullet">
    /// <item><c>HttpOnly</c> — unreadable by JavaScript, defeating XSS token theft (CWE-922).</item>
    /// <item><c>Secure</c> when the (forwarded) request scheme is HTTPS — sent over TLS only in production
    /// behind nginx; left unset over plain HTTP so it still round-trips in local dev and the in-process
    /// integration-test host (Gate 5), where there is no TLS.</item>
    /// <item><c>SameSite=Strict</c> — never attached to cross-site requests, mitigating CSRF on refresh.</item>
    /// <item><c>Path=/api/auth</c> — transmitted only to the auth endpoints, never to resource APIs.</item>
    /// </list>
    /// A null/empty token (defensive) writes no cookie.
    /// </summary>
    private void IssueRefreshTokenCookie(string? refreshToken)
    {
        if (string.IsNullOrEmpty(refreshToken))
        {
            return;
        }

        Response.Cookies.Append(RefreshTokenCookieName, refreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Path = "/api/auth",
            IsEssential = true,
            Expires = DateTimeOffset.UtcNow.AddDays(_jwtSettings.RefreshTokenExpirationDays)
        });
    }

    /// <summary>
    /// Expires the httpOnly refresh-token cookie on logout. The attributes (Path/Secure/SameSite/HttpOnly)
    /// must match those used when the cookie was issued so the browser removes the correct cookie.
    /// </summary>
    private void ClearRefreshTokenCookie()
    {
        Response.Cookies.Delete(RefreshTokenCookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Path = "/api/auth"
        });
    }
}
