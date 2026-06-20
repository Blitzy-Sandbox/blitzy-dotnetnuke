using DnnMigration.Api.Authorization;
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
// is a HARD delete (DEV-039: the [Users] table has no IsDeleted column, so a soft delete is impossible
// without a schema change which ADR-002 forbids). Documented in root MIGRATION_NOTES.md.
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
    [Authorize(Policy = Permissions.View)]
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
            // MIGRATION (M6/DEV-037): RFC 7807 ProblemDetails (application/problem+json) instead of a bare
            // NotFound(), per the AAP error contract and matching the Problem(...) convention used above.
            return byUsername is null
                ? Problem(statusCode: StatusCodes.Status404NotFound, title: "User not found", detail: $"No user with username '{username}' exists in portal {portalId.Value}.")
                : Ok(ApiResponse.Success(byUsername));
        }

        if (!string.IsNullOrWhiteSpace(email))
        {
            var byEmail = await _userService.GetByEmailAsync(portalId.Value, email, cancellationToken);
            return byEmail is null
                ? Problem(statusCode: StatusCodes.Status404NotFound, title: "User not found", detail: $"No user with email '{email}' exists in portal {portalId.Value}.")
                : Ok(ApiResponse.Success(byEmail));
        }

        var page = await _userService.GetByPortalAsync(portalId.Value, pageIndex, pageSize, cancellationToken);
        return Ok(ApiResponse.Success(page.Items, ApiResponseMeta.FromPage(page)));
    }

    /// <summary>Get a single user by id.</summary>
    [Authorize(Policy = Permissions.View)]
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken = default)
    {
        var user = await _userService.GetByIdAsync(id, cancellationToken);
        return user is null
            ? Problem(statusCode: StatusCodes.Status404NotFound, title: "User not found", detail: $"No user exists with id {id}.")
            : Ok(ApiResponse.Success(user));
    }

    /// <summary>Create a user.</summary>
    [Authorize(Policy = Permissions.Edit)]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUserDto request, CancellationToken cancellationToken = default)
    {
        var created = await _userService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.UserID }, ApiResponse.Success(created));
    }

    /// <summary>Update a user.</summary>
    [Authorize(Policy = Permissions.Edit)]
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

    /// <summary>Delete a user (HARD delete; DEV-039).</summary>
    [Authorize(Policy = Permissions.Delete)]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken = default)
    {
        await _userService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>Flag a user to change their password on next login (sets the [Users].UpdatePassword column).</summary>
    // MIGRATION: reproduces the legacy admin "force password change" affordance (cmdPassword_Click in
    // Website/admin/Users/Membership.ascx.vb). The related aspnet_Membership transitions
    // (authorize/unauthorize/unlock) are deferred — those fields are EF-Ignore()d with no [Users] column
    // (ADR-002 / §0.6.2). A missing user surfaces as RFC 7807 404 via the exception middleware
    // (KeyNotFoundException). Documented in root MIGRATION_NOTES.md.
    [Authorize(Policy = Permissions.Edit)]
    [HttpPost("{id:int}/force-password-change")]
    public async Task<IActionResult> ForcePasswordChange(int id, CancellationToken cancellationToken = default)
    {
        var updated = await _userService.ForcePasswordChangeAsync(id, cancellationToken);
        return Ok(ApiResponse.Success(updated));
    }
}
