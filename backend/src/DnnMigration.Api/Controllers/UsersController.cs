using DnnMigration.Application.Common;
using DnnMigration.Application.DTOs.User;
using DnnMigration.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DnnMigration.Api.Controllers;

/// <summary>REST resource controller for Users (/api/v1/users).</summary>
// MIGRATION: Replaces the legacy UserController.vb record-management surface (GetUser/GetUserByUsername/
// GetUsersByEmail/GetUsers paged/CreateUser/UpdateUser/DeleteUser). Authentication (UserLogin/ValidateUser)
// is intentionally excluded here and handled by AuthController/IAuthService (Forms Auth -> JWT). User delete
// is a HARD delete (CP3 correction): the DNN 4.9 dbo.Users table has no IsDeleted column, so the row (and its
// UserPortals membership) is removed, matching the legacy DeleteUser. Documented in root MIGRATION_NOTES.md
// §6.3 / D-014.
[ApiController]
[Authorize]
[Produces("application/json")]
[Route("api/v1/users")]
public sealed class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    /// <summary>List users in a portal (paged), or look up a single user by username or email within a portal.</summary>
    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] int? portalId = null,
        [FromQuery] string? username = null,
        [FromQuery] string? email = null,
        [FromQuery] int pageIndex = 0,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (!portalId.HasValue)
        {
            return Problem(statusCode: 400, title: "Missing filter", detail: "The portalId query parameter is required.");
        }

        if (!string.IsNullOrWhiteSpace(username))
        {
            var byUsername = await _userService.GetByUsernameAsync(portalId.Value, username, cancellationToken);
            return byUsername is null ? NotFound() : Ok(ApiResponse.Success(byUsername));
        }

        if (!string.IsNullOrWhiteSpace(email))
        {
            var byEmail = await _userService.GetByEmailAsync(portalId.Value, email, cancellationToken);
            return byEmail is null ? NotFound() : Ok(ApiResponse.Success(byEmail));
        }

        var page = await _userService.GetByPortalAsync(portalId.Value, pageIndex, pageSize, cancellationToken);
        return Ok(ApiResponse.Success(page.Items, ApiResponseMeta.FromPage(page)));
    }

    /// <summary>Get a single user by id.</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken = default)
    {
        var user = await _userService.GetByIdAsync(id, cancellationToken);
        return user is null ? NotFound() : Ok(ApiResponse.Success(user));
    }

    /// <summary>Create a user.</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUserDto request, CancellationToken cancellationToken = default)
    {
        var created = await _userService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.UserID }, ApiResponse.Success(created));
    }

    /// <summary>Update a user.</summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateUserDto request, CancellationToken cancellationToken = default)
    {
        if (id != request.UserID)
        {
            return Problem(statusCode: 400, title: "Identifier mismatch", detail: "Route id does not match body UserID.");
        }

        var updated = await _userService.UpdateAsync(request, cancellationToken);
        return Ok(ApiResponse.Success(updated));
    }

    /// <summary>Delete a user (HARD delete — removes the user row and its portal membership).</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken = default)
    {
        await _userService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
