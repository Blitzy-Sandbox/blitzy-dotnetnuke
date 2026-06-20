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

    /// <summary>Log the current user out by revoking their refresh token.</summary>
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
}
