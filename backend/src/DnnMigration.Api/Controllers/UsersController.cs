using DnnMigration.Application.DTOs.User;
using DnnMigration.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace DnnMigration.Api.Controllers;

// MIGRATION: Replaces the legacy DotNetNuke Web Forms user administration code-behind
// Website/admin/Users/ManageUsers.ascx.vb + Users.ascx.vb (user list/grid) and User.ascx.vb
// (create/edit user). ViewState/postback is discarded; re-expressed as thin JSON REST endpoints
// delegating to IUserService.
// MIGRATION: Routing uses api/[controller] (=> /api/users) WITHOUT a /v1/ segment (Gate 5 + AAP
// resource-table parity). Recorded for MIGRATION_NOTES.md.
// MIGRATION: Result -> HTTP status is operation-based (single-read failure -> 404, write failure -> 400)
// because Domain.Common.Result has no error-category discriminator.
// MIGRATION: No profile endpoint is exposed — the legacy profile workflow (UserProfileDto) is out of
// scope for this phase per the AAP.
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public sealed class UsersController(IUserService userService) : ApiControllerBase
{
    // MIGRATION: User listing is always portal-scoped (multi-tenant isolation preserved from DNN's
    // PortalId discriminator) — portalId is a required query parameter; there is no host-wide user list.
    /// <summary>GET /api/users?portalId=&amp;pageIndex=&amp;pageSize= — paged users for a portal.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetByPortal(
        [FromQuery, BindRequired] int portalId,
        [FromQuery] int pageIndex = 0,
        [FromQuery] int pageSize = 20)
    {
        (pageIndex, pageSize) = NormalizePaging(pageIndex, pageSize);
        var result = await userService.GetByPortalAsync(portalId, pageIndex, pageSize, HttpContext.RequestAborted);
        return HandlePaged(result);
    }

    /// <summary>GET /api/users/{id} — a single user by id.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await userService.GetByIdAsync(id, HttpContext.RequestAborted);
        return HandleGet(result);
    }

    /// <summary>POST /api/users — create a user.</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest request)
    {
        var result = await userService.CreateAsync(request, HttpContext.RequestAborted);
        return HandleCreated(result, nameof(GetById), created => new { id = created.UserId });
    }

    /// <summary>PUT /api/users/{id} — update a user.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateUserRequest request)
    {
        var result = await userService.UpdateAsync(id, request, HttpContext.RequestAborted);
        return HandleResult(result);
    }

    /// <summary>DELETE /api/users/{id} — delete a user.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await userService.DeleteAsync(id, HttpContext.RequestAborted);
        return HandleDelete(result);
    }
}
