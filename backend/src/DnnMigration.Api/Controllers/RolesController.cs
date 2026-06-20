using DnnMigration.Application.Common;
using DnnMigration.Application.DTOs.Role;
using DnnMigration.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DnnMigration.Api.Controllers;

/// <summary>REST resource controller for Roles and user-role membership (/api/v1/roles).</summary>
// MIGRATION: Replaces the legacy RoleController.vb surface (AddRole/DeleteRole/GetRole/GetPortalRoles/
// UpdateRole + AddUserRole/DeleteUserRole/GetUserRoles/GetUsersInRole). Role delete is a HARD delete with
// transactional cascade (service concern). Membership mutations return 204 (no UserRoleDto exists).
// GetRoleGroups is intentionally omitted (no RoleGroupDto in scope). Documented in root MIGRATION_NOTES.md.
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
        // MIGRATION (M7/DEV-037): RFC 7807 ProblemDetails (application/problem+json) instead of a bare
        // NotFound(), per the AAP error contract and matching the Problem(...) convention used elsewhere here.
        return role is null
            ? Problem(statusCode: StatusCodes.Status404NotFound, title: "Role not found", detail: $"No role exists with id {id}.")
            : Ok(ApiResponse.Success(role));
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

    /// <summary>Assign a user to a role.</summary>
    [HttpPost("{roleId:int}/users/{userId:int}")]
    public async Task<IActionResult> AddUserToRole(int roleId, int userId, CancellationToken cancellationToken = default)
    {
        await _roleService.AddUserRoleAsync(userId, roleId, cancellationToken);
        return NoContent();
    }

    /// <summary>Remove a user from a role.</summary>
    [HttpDelete("{roleId:int}/users/{userId:int}")]
    public async Task<IActionResult> RemoveUserFromRole(int roleId, int userId, CancellationToken cancellationToken = default)
    {
        await _roleService.RemoveUserRoleAsync(userId, roleId, cancellationToken);
        return NoContent();
    }
}
