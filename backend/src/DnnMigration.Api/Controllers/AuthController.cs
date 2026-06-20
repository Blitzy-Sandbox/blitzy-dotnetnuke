using System.Security.Claims;
using DnnMigration.Application.Common;
using DnnMigration.Application.DTOs.Auth;
using DnnMigration.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DnnMigration.Api.Controllers;

/// <summary>JWT authentication endpoints (/api/auth/{login,refresh,logout,me}).</summary>
// MIGRATION: Replaces legacy ASP.NET Forms Authentication + 56-bit DES (PortalSecurity.vb SignOut/Encrypt/
// Decrypt, IsInRole) with stateless JWT Bearer tokens + BCrypt. The API holds no session: identity is carried
// in JWT claims, so logout/me extract the user id from the authenticated ClaimsPrincipal and pass it to the
// service (the Application layer never touches HttpContext/ClaimsPrincipal). Documented in root MIGRATION_NOTES.md.
[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>Authenticate with credentials and receive access + refresh tokens.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request, CancellationToken cancellationToken = default)
    {
        var result = await _authService.LoginAsync(request, cancellationToken);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>Exchange a valid refresh token for a fresh access + refresh token pair.</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequestDto request, CancellationToken cancellationToken = default)
    {
        var result = await _authService.RefreshAsync(request, cancellationToken);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>
    /// Log the current user out. MIGRATION (m1/DEV-028): Phase 1 is STATELESS — the server keeps no
    /// refresh-token store, so this endpoint performs NO server-side token revocation; the client discards
    /// its stored tokens locally and the short-lived access token simply expires. (Server-side revocation
    /// against a persisted refresh-token store is a documented later-checkpoint option.)
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
}
