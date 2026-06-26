using System.Security.Claims;
using DnnMigration.Application.DTOs.Auth;
using DnnMigration.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DnnMigration.Api.Controllers;

// MIGRATION: Replaces the legacy DotNetNuke Web Forms authentication/password workflows
// Website/admin/Security/SendPassword.ascx.vb + Website/admin/Users/Password.ascx.vb; the underlying
// auth behavior originates in PortalSecurity.vb / UserMembership.vb. Forms auth + AspNetSqlMembershipProvider
// are replaced by JWT Bearer auth with refresh-token rotation (AAP 0.3.3 / 0.7.6). Thin endpoints delegate
// to IAuthService.
// MIGRATION: Routing uses api/[controller] (=> /api/auth) WITHOUT a /v1/ segment (Gate 5 + AAP
// resource-table parity). Recorded for MIGRATION_NOTES.md.
// MIGRATION: Login/refresh FAILURE maps to 400 (operation-based) because Domain.Common.Result carries no
// error-category discriminator; distinguishing invalid-credentials as 401 is a future ErrorType candidate.
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public sealed class AuthController(IAuthService authService) : ApiControllerBase
{
    /// <summary>POST /api/auth/login — authenticate and issue access/refresh tokens.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await authService.LoginAsync(request, HttpContext.RequestAborted);
        return HandleResult(result);
    }

    /// <summary>POST /api/auth/refresh — rotate the refresh token and issue a new access token.</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request)
    {
        var result = await authService.RefreshAsync(request, HttpContext.RequestAborted);
        return HandleResult(result);
    }

    /// <summary>POST /api/auth/logout — revoke the supplied refresh token.</summary>
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Logout([FromBody] RefreshRequest request)
    {
        var result = await authService.LogoutAsync(request, HttpContext.RequestAborted);
        return HandleDelete(result);
    }

    // MIGRATION: The current-user id is taken from the JWT subject (NameIdentifier) claim and the portal id from the
    // custom "portalId" claim (both issued by JwtService.GenerateAccessToken). CP1 review (AuthService #6) — /me is now
    // PORTAL-SCOPED: a missing/invalid subject OR portal claim yields 401 before any service call, and the lookup is
    // constrained to the token's portal so a principal cannot read a user outside its tenant (multi-tenant isolation,
    // AAP 0.7.1).
    /// <summary>GET /api/auth/me — the authenticated user's profile snapshot.</summary>
    [HttpGet("me")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCurrentUser()
    {
        string? subject = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(subject, out int userId))
        {
            return Unauthorized();
        }

        string? portal = User.FindFirstValue("portalId");
        if (!int.TryParse(portal, out int portalId))
        {
            return Unauthorized();
        }

        var result = await authService.GetCurrentUserAsync(portalId, userId, HttpContext.RequestAborted);
        return HandleGet(result);
    }
}
