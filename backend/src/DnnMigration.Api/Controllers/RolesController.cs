using DnnMigration.Application.DTOs.Role;
using DnnMigration.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace DnnMigration.Api.Controllers;

// MIGRATION: Replaces the legacy DotNetNuke Web Forms security/role administration code-behind
// Website/admin/Security/Roles.ascx.vb (role grid) + EditRoles.ascx.vb (create/edit role) and the
// read side of SecurityRoles.ascx.vb (user-role assignment view). ViewState/postback is discarded;
// re-expressed as thin JSON REST endpoints delegating to IRoleService.
// MIGRATION: Routing uses api/[controller] (=> /api/roles) WITHOUT a /v1/ segment (Gate 5 + AAP
// resource-table parity). Recorded for MIGRATION_NOTES.md.
// MIGRATION: Result -> HTTP status is operation-based (single-read failure -> 404, write failure -> 400)
// because Domain.Common.Result has no error-category discriminator.
// MIGRATION: User-role ASSIGNMENT WRITE operations (add/remove a user to/from a role) from the legacy
// SecurityRoles.ascx.vb are DEFERRED — there is no IRoleService write method, DTO, or AAP endpoint for
// them in this phase. Only the read-only GetUserRolesAsync lookup is exposed.
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public sealed class RolesController(IRoleService roleService) : ApiControllerBase
{
    // MIGRATION: Role listing is always portal-scoped (multi-tenant isolation preserved from DNN's
    // PortalId discriminator) — portalId is a required query parameter; there is no host-wide role list.
    /// <summary>GET /api/roles?portalId=&amp;pageIndex=&amp;pageSize= — paged roles for a portal.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetByPortal(
        [FromQuery, BindRequired] int portalId,
        [FromQuery] int pageIndex = 0,
        [FromQuery] int pageSize = 20)
    {
        (pageIndex, pageSize) = NormalizePaging(pageIndex, pageSize);
        var result = await roleService.GetByPortalAsync(portalId, pageIndex, pageSize, HttpContext.RequestAborted);
        return HandlePaged(result);
    }

    /// <summary>GET /api/roles/{id} — a single role by id.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await roleService.GetByIdAsync(id, HttpContext.RequestAborted);
        return HandleGet(result);
    }

    // MIGRATION: Read-only user-role lookup (the read side of SecurityRoles.ascx.vb). Returns the
    // unpaged { data: [...], meta: { count } } envelope.
    /// <summary>GET /api/roles/user/{userId} — the roles assigned to a user.</summary>
    [HttpGet("user/{userId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetUserRoles(int userId)
    {
        var result = await roleService.GetUserRolesAsync(userId, HttpContext.RequestAborted);
        return HandleList(result);
    }

    /// <summary>POST /api/roles — create a role.</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateRoleRequest request)
    {
        var result = await roleService.CreateAsync(request, HttpContext.RequestAborted);
        return HandleCreated(result, nameof(GetById), created => new { id = created.RoleId });
    }

    /// <summary>PUT /api/roles/{id} — update a role.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateRoleRequest request)
    {
        var result = await roleService.UpdateAsync(id, request, HttpContext.RequestAborted);
        return HandleResult(result);
    }

    /// <summary>DELETE /api/roles/{id} — delete a role.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await roleService.DeleteAsync(id, HttpContext.RequestAborted);
        return HandleDelete(result);
    }
}
