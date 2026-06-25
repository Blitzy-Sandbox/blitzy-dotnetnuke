using DnnMigration.Application.DTOs.Tab;
using DnnMigration.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace DnnMigration.Api.Controllers;

// MIGRATION: Tab (page) administration re-expressed as thin JSON REST endpoints delegating to ITabService.
// Behavior originates in the legacy DotNetNuke TabController.vb; the controller structure mirrors
// PortalsController (AAP 0.4.1) except the list is an UNPAGED portal page-tree. No Web Forms markup migrated.
// MIGRATION: Routing uses api/[controller] (=> /api/tabs) WITHOUT a /v1/ segment (Gate 5 + AAP
// resource-table parity). Recorded for MIGRATION_NOTES.md.
// MIGRATION: Result -> HTTP status is operation-based (single-read failure -> 404, write failure -> 400)
// because Domain.Common.Result has no error-category discriminator.
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public sealed class TabsController(ITabService tabService) : ApiControllerBase
{
    // MIGRATION: Tab listing is always portal-scoped (multi-tenant isolation preserved from DNN's
    // PortalId discriminator) and UNPAGED — it returns the portal's full page tree; portalId is required.
    /// <summary>GET /api/tabs?portalId= — the (unpaged) page tree for a portal.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetByPortal([FromQuery, BindRequired] int portalId)
    {
        var result = await tabService.GetByPortalAsync(portalId, HttpContext.RequestAborted);
        return HandleList(result);
    }

    /// <summary>GET /api/tabs/{id} — a single tab (page) by id.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await tabService.GetByIdAsync(id, HttpContext.RequestAborted);
        return HandleGet(result);
    }

    /// <summary>POST /api/tabs — create a tab (page).</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateTabRequest request)
    {
        var result = await tabService.CreateAsync(request, HttpContext.RequestAborted);
        return HandleCreated(result, nameof(GetById), created => new { id = created.TabId });
    }

    /// <summary>PUT /api/tabs/{id} — update a tab (page).</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateTabRequest request)
    {
        var result = await tabService.UpdateAsync(id, request, HttpContext.RequestAborted);
        return HandleResult(result);
    }

    /// <summary>DELETE /api/tabs/{id} — delete a tab (page).</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await tabService.DeleteAsync(id, HttpContext.RequestAborted);
        return HandleDelete(result);
    }
}
