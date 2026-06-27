using DnnMigration.Api.Authorization;
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
// MIGRATION (CP2 review — API versioning): exposed BOTH at /api/v1/roles (AAP §0.1.2/§0.3.4 URL-path
// versioning NFR) AND at /api/roles (AAP §0.3.4 resource table + Gate 5 literal paths) via dual [Route]
// attributes (no external API-versioning package is available offline). Recorded in MIGRATION_NOTES.md.
// MIGRATION (CP2 review — authorization + tenant isolation): role administration requires the
// PortalAdministrator policy, and every action enforces that the client-supplied portalId matches the JWT
// "portalId" claim (EnforceTenant) so a portal admin can only manage roles in its own portal; host SuperUsers
// bypass the tenant check.
// MIGRATION: Result -> HTTP status is operation-based (single-read failure -> 404, write failure -> 400)
// because Domain.Common.Result has no error-category discriminator.
// MIGRATION: User-role ASSIGNMENT WRITE operations (assign/remove/update a user to/from a role) from the
    // legacy SecurityRoles.ascx.vb are now IMPLEMENTED via IRoleService.AssignUserRoleAsync /
    // RemoveUserRoleAsync / UpdateUserRoleAsync. Exposed as an assignment sub-resource: POST /api/roles/assignments
    // (assign), PUT /api/roles/assignments (recompute expiry / cancel), DELETE /api/roles/{roleId}/users/{userId}
    // (remove). The CanRemoveUserFromRole guard (the Administrators/Registered system roles) is enforced in RoleService.
[ApiController]
[Route("api/[controller]")]
[Route("api/v1/[controller]")]
[Authorize(Policy = AuthorizationPolicies.PortalAdministrator)]
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
        var tenantDenied = EnforceTenant(portalId);
        if (tenantDenied is not null)
        {
            return tenantDenied;
        }

        (pageIndex, pageSize) = NormalizePaging(pageIndex, pageSize);
        var result = await roleService.GetByPortalAsync(portalId, pageIndex, pageSize, HttpContext.RequestAborted);
        return HandlePaged(result);
    }

    // MIGRATION: CP1 review (IRoleService #1 / IRoleRepository #1) — a single-role read is portal-scoped
    // (multi-tenant isolation preserved from DNN's PortalId discriminator); portalId is a required query
    // parameter so a role from another portal can never be read through this tenant.
    /// <summary>GET /api/roles/{id}?portalId= — a single role by id (portal-scoped).</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById([FromQuery, BindRequired] int portalId, int id)
    {
        var tenantDenied = EnforceTenant(portalId);
        if (tenantDenied is not null)
        {
            return tenantDenied;
        }

        var result = await roleService.GetByIdAsync(portalId, id, HttpContext.RequestAborted);
        return HandleGet(result);
    }

    // MIGRATION: Read-only user-role lookup (the read side of SecurityRoles.ascx.vb). Returns the
    // unpaged { data: [...], meta: { count } } envelope.
    // MIGRATION: CP1 review (IRoleService #1 / IRoleRepository #1) — the legacy GetUserRoles query carried
    // PortalId; portalId is a required query parameter here so user-role reads stay portal-scoped.
    /// <summary>GET /api/roles/user/{userId}?portalId= — the roles assigned to a user (portal-scoped).</summary>
    [HttpGet("user/{userId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetUserRoles([FromQuery, BindRequired] int portalId, int userId)
    {
        var tenantDenied = EnforceTenant(portalId);
        if (tenantDenied is not null)
        {
            return tenantDenied;
        }

        var result = await roleService.GetUserRolesAsync(portalId, userId, HttpContext.RequestAborted);
        return HandleList(result);
    }

    /// <summary>POST /api/roles — create a role.</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateRoleRequest request)
    {
        var tenantDenied = EnforceTenant(request.PortalId);
        if (tenantDenied is not null)
        {
            return tenantDenied;
        }

        var result = await roleService.CreateAsync(request, HttpContext.RequestAborted);
        // MIGRATION: CP1 review — GetById is now portal-scoped, so the 201 Location route values must include
        // portalId (from the request body) alongside the new role id.
        return HandleCreated(result, nameof(GetById), created => new { id = created.RoleId, portalId = request.PortalId });
    }

    // MIGRATION: CP1 review (IRoleService #1 / RoleService #5, #6) — update is portal-scoped so a role from
    // another portal can never be modified, and the service enforces the Administrators/Registered system-role
    // guard. portalId is a required query parameter.
    /// <summary>PUT /api/roles/{id}?portalId= — update a role (portal-scoped).</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update([FromQuery, BindRequired] int portalId, int id, [FromBody] UpdateRoleRequest request)
    {
        var tenantDenied = EnforceTenant(portalId);
        if (tenantDenied is not null)
        {
            return tenantDenied;
        }

        var result = await roleService.UpdateAsync(portalId, id, request, HttpContext.RequestAborted);
        return HandleResult(result);
    }

    // MIGRATION: CP1 review (IRoleService #1 / RoleService #5, #6) — delete is portal-scoped so a role from
    // another portal can never be deleted, and the service enforces the system-role guard. portalId required.
    /// <summary>DELETE /api/roles/{id}?portalId= — delete a role (portal-scoped).</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Delete([FromQuery, BindRequired] int portalId, int id)
    {
        var tenantDenied = EnforceTenant(portalId);
        if (tenantDenied is not null)
        {
            return tenantDenied;
        }

        var result = await roleService.DeleteAsync(portalId, id, HttpContext.RequestAborted);
        return HandleDelete(result);
    }

    // MIGRATION: RoleController.AddUserRole (RoleController.vb L277/L295) — assign a user to a role. Self-contained
    // request (PortalId/UserId/RoleId + optional Effective/Expiry dates); EnforceTenant matches request.PortalId
    // against the JWT "portalId" claim so a portal admin can only assign within its own portal. 201 Created with a
    // Location header pointing at the user's role list (GetUserRoles).
    /// <summary>POST /api/roles/assignments — assign a user to a role.</summary>
    [HttpPost("assignments")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AssignUserRole([FromBody] AssignUserRoleRequest request)
    {
        var tenantDenied = EnforceTenant(request.PortalId);
        if (tenantDenied is not null)
        {
            return tenantDenied;
        }

        var result = await roleService.AssignUserRoleAsync(request, HttpContext.RequestAborted);
        return HandleCreated(result, nameof(GetUserRoles), created => new { userId = created.UserId, portalId = request.PortalId });
    }

    // MIGRATION: RoleController.UpdateUserRole (RoleController.vb L472/L489) — recompute the assignment's expiry from
    // the role's trial/billing schedule (N/O/D/W/M/Y), or on Cancel expire (paid + trial-used) / remove it.
    // EnforceTenant on request.PortalId.
    /// <summary>PUT /api/roles/assignments — update (or cancel) a user-role assignment.</summary>
    [HttpPut("assignments")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateUserRole([FromBody] UpdateUserRoleRequest request)
    {
        var tenantDenied = EnforceTenant(request.PortalId);
        if (tenantDenied is not null)
        {
            return tenantDenied;
        }

        var result = await roleService.UpdateUserRoleAsync(request, HttpContext.RequestAborted);
        return HandleResult(result);
    }

    // MIGRATION: RoleController.DeleteUserRole (RoleController.vb L330) + CanRemoveUserFromRole guard (L741/L764) —
    // remove a user from a role. portalId is a required query parameter (EnforceTenant). The system-role guard (the
    // portal Administrator cannot leave the Administrators role; no user can leave the Registered Users role) is
    // enforced in RoleService; a guard-blocked removal returns 400, a successful/no-op removal returns 204.
    /// <summary>DELETE /api/roles/{roleId}/users/{userId}?portalId= — remove a user from a role.</summary>
    [HttpDelete("{roleId:int}/users/{userId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RemoveUserRole([FromQuery, BindRequired] int portalId, int roleId, int userId)
    {
        var tenantDenied = EnforceTenant(portalId);
        if (tenantDenied is not null)
        {
            return tenantDenied;
        }

        var result = await roleService.RemoveUserRoleAsync(portalId, userId, roleId, HttpContext.RequestAborted);
        return HandleDelete(result);
    }
}
