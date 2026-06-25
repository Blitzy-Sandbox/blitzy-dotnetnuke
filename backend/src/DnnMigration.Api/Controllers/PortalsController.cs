using DnnMigration.Application.DTOs.Portal;
using DnnMigration.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DnnMigration.Api.Controllers;

// MIGRATION: Replaces the legacy DotNetNuke Web Forms portal administration code-behind
// Website/admin/Portal/Portals.ascx.vb (portal list/grid + delete) and SiteSettings.ascx.vb
// (portal create/edit settings form). ViewState/postback machinery is discarded; the workflows are
// re-expressed as thin JSON REST endpoints that delegate to IPortalService.
// MIGRATION: Routing uses api/[controller] (=> /api/portals) WITHOUT a /v1/ segment. The AAP NFR mentions
// /api/v1, but Gate 5 and the AAP resource table use /api/portals; gate compliance + contract parity win.
// Recorded for MIGRATION_NOTES.md.
// MIGRATION: Result -> HTTP status is operation-based (single-read failure -> 404, write failure -> 400)
// because Domain.Common.Result carries no error-category discriminator.
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public sealed class PortalsController(IPortalService portalService) : ApiControllerBase
{
    /// <summary>GET /api/portals?pageIndex=&amp;pageSize= — host-level paged list of portals.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] int pageIndex = 0, [FromQuery] int pageSize = 20)
    {
        (pageIndex, pageSize) = NormalizePaging(pageIndex, pageSize);
        var result = await portalService.GetAllAsync(pageIndex, pageSize, HttpContext.RequestAborted);
        return HandlePaged(result);
    }

    /// <summary>GET /api/portals/{id} — a single portal by id.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await portalService.GetByIdAsync(id, HttpContext.RequestAborted);
        return HandleGet(result);
    }

    /// <summary>POST /api/portals — create a portal.</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreatePortalRequest request)
    {
        var result = await portalService.CreateAsync(request, HttpContext.RequestAborted);
        return HandleCreated(result, nameof(GetById), created => new { id = created.PortalId });
    }

    /// <summary>PUT /api/portals/{id} — update a portal.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdatePortalRequest request)
    {
        var result = await portalService.UpdateAsync(id, request, HttpContext.RequestAborted);
        return HandleResult(result);
    }

    /// <summary>DELETE /api/portals/{id} — delete a portal.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await portalService.DeleteAsync(id, HttpContext.RequestAborted);
        return HandleDelete(result);
    }

    // MIGRATION: Exposes IPortalService.HasSpaceAvailableAsync (the legacy portal storage-quota check
    // surfaced in SiteSettings.ascx.vb). Returns the standard envelope { data: <bool>, meta: {} }.
    /// <summary>GET /api/portals/{id}/space?fileSizeBytes= — whether the portal has room for a file.</summary>
    [HttpGet("{id:int}/space")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> HasSpaceAvailable(int id, [FromQuery] long fileSizeBytes)
    {
        var result = await portalService.HasSpaceAvailableAsync(id, fileSizeBytes, HttpContext.RequestAborted);
        return HandleResult(result);
    }
}
