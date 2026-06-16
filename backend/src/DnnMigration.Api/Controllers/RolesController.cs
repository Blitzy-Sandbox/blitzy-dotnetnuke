using DnnMigration.Application.Common;
using DnnMigration.Application.DTOs.Role;
using DnnMigration.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace DnnMigration.Api.Controllers;

/// <summary>REST resource controller for Roles and user-role membership (/api/v1/roles).</summary>
// MIGRATION: Replaces the legacy RoleController.vb surface (AddRole/DeleteRole/GetRole/GetPortalRoles/
// UpdateRole + AddUserRole/DeleteUserRole/GetUserRoles/GetUsersInRole). Role delete is a HARD delete with
// transactional cascade (service concern). Assign-user-to-role is an admin UPSERT that returns 201 Created
// with the persisted UserRoleAssignmentDto; remove-user-from-role returns 204 (delete). GetRoleGroups is
// intentionally omitted (no RoleGroupDto in scope). Documented in root MIGRATION_NOTES.md (§6.2).
[ApiController]
[Authorize]
[Produces("application/json")]
[Route("api/v1/roles")]
public sealed class RolesController : ControllerBase
{
    private readonly IRoleService _roleService;

    public RolesController(IRoleService roleService)
    {
        _roleService = roleService;
    }

    /// <summary>List roles in a portal.</summary>
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] int? portalId = null, CancellationToken cancellationToken = default)
    {
        if (!portalId.HasValue)
        {
            return Problem(statusCode: 400, title: "Missing filter", detail: "The portalId query parameter is required.");
        }

        var roles = await _roleService.GetByPortalAsync(portalId.Value, cancellationToken);
        return Ok(ApiResponse.Success(roles));
    }

    /// <summary>Get a single role by id.</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken = default)
    {
        var role = await _roleService.GetByIdAsync(id, cancellationToken);
        return role is null ? NotFound() : Ok(ApiResponse.Success(role));
    }

    /// <summary>Create a role.</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRoleDto request, CancellationToken cancellationToken = default)
    {
        var created = await _roleService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.RoleID }, ApiResponse.Success(created));
    }

    /// <summary>Update a role.</summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateRoleDto request, CancellationToken cancellationToken = default)
    {
        if (id != request.RoleID)
        {
            return Problem(statusCode: 400, title: "Identifier mismatch", detail: "Route id does not match body RoleID.");
        }

        var updated = await _roleService.UpdateAsync(request, cancellationToken);
        return Ok(ApiResponse.Success(updated));
    }

    /// <summary>Delete a role (HARD delete with transactional cascade).</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken = default)
    {
        await _roleService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>List the users assigned to a role.</summary>
    [HttpGet("{roleId:int}/users")]
    public async Task<IActionResult> GetUsersInRole(int roleId, CancellationToken cancellationToken = default)
    {
        var users = await _roleService.GetUsersInRoleAsync(roleId, cancellationToken);
        return Ok(ApiResponse.Success(users));
    }

    /// <summary>List the roles a user belongs to.</summary>
    [HttpGet("user/{userId:int}")]
    public async Task<IActionResult> GetUserRoles(int userId, CancellationToken cancellationToken = default)
    {
        var roles = await _roleService.GetUserRolesAsync(userId, cancellationToken);
        return Ok(ApiResponse.Success(roles));
    }

    /// <summary>
    /// Assign a user to a role (admin UPSERT). The optional request body may carry the effective/expiry
    /// membership window (<see cref="AssignUserRoleDto"/>); the route <c>{roleId}</c>/<c>{userId}</c> are the
    /// canonical identity and override any UserID/RoleID supplied in the body. Returns <c>201 Created</c> with
    /// the persisted assignment so the caller sees the server-assigned UserRoleID and stored state.
    /// </summary>
    [HttpPost("{roleId:int}/users/{userId:int}")]
    public async Task<IActionResult> AddUserToRole(
        int roleId,
        int userId,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] AssignUserRoleDto? request = null,
        CancellationToken cancellationToken = default)
    {
        // MIGRATION: route ids are authoritative for the assignment identity; a body, when present, supplies only
        // the admin effective/expiry window. An absent body (EmptyBodyBehavior.Allow) yields a no-window assignment.
        request ??= new AssignUserRoleDto();
        request.RoleID = roleId;
        request.UserID = userId;

        var assignment = await _roleService.AddUserRoleAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetUserRoles), new { userId }, ApiResponse.Success(assignment));
    }

    /// <summary>Remove a user from a role.</summary>
    [HttpDelete("{roleId:int}/users/{userId:int}")]
    public async Task<IActionResult> RemoveUserFromRole(int roleId, int userId, CancellationToken cancellationToken = default)
    {
        await _roleService.RemoveUserRoleAsync(userId, roleId, cancellationToken);
        return NoContent();
    }
}
