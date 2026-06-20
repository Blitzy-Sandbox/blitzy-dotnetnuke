using DnnMigration.Application.Common;
using DnnMigration.Application.DTOs.Portal;
using DnnMigration.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DnnMigration.Api.Controllers;

/// <summary>REST resource controller for Portals (/api/v1/portals).</summary>
// MIGRATION: Replaces the Web Forms admin grid Website/admin/Portal/Portals.ascx.vb and the legacy
// PortalController.vb business surface (GetPortals/GetPortal/GetPortalsByName/CreatePortal/UpdatePortalInfo/
// DeletePortalInfo). Postback/ViewState -> stateless REST. Portal delete remains a HARD delete with
// transactional cascade (handled in the service/repository). Documented in root MIGRATION_NOTES.md.
[ApiController]
[Authorize]
[Produces("application/json")]
[Route("api/v1/portals")]
public sealed class PortalsController : ControllerBase
{
    private readonly IPortalService _portalService;

    public PortalsController(IPortalService portalService)
    {
        _portalService = portalService;
    }

    /// <summary>List portals; when ?query= is supplied, performs a paged name-prefix search.</summary>
    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] string? query = null,
        [FromQuery] int pageIndex = 0,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        // MIGRATION: legacy Portals.ascx.vb letter filter -> ?query= name-prefix search via GetByNameAsync.
        if (!string.IsNullOrWhiteSpace(query))
        {
            var page = await _portalService.GetByNameAsync(query, pageIndex, pageSize, cancellationToken);
            return Ok(ApiResponse.Success(page.Items, ApiResponseMeta.FromPage(page)));
        }

        var portals = await _portalService.GetAllAsync(cancellationToken);
        return Ok(ApiResponse.Success(portals));
    }

    /// <summary>Get a single portal by id.</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken = default)
    {
        var portal = await _portalService.GetByIdAsync(id, cancellationToken);
        // MIGRATION (M5/DEV-037): emit RFC 7807 ProblemDetails (application/problem+json) instead of a bare
        // NotFound(), matching the existing Problem(...) convention below and the AAP error contract; the future
        // ExceptionHandlingMiddleware will translate typed exceptions into the same shape.
        if (portal is null)
        {
            return Problem(statusCode: StatusCodes.Status404NotFound, title: "Portal not found", detail: $"No portal exists with id {id}.");
        }

        return Ok(ApiResponse.Success(portal));
    }

    /// <summary>Create a portal.</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePortalDto request, CancellationToken cancellationToken = default)
    {
        var created = await _portalService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.PortalID }, ApiResponse.Success(created));
    }

    /// <summary>Update a portal.</summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdatePortalDto request, CancellationToken cancellationToken = default)
    {
        if (id != request.PortalID)
        {
            return Problem(statusCode: 400, title: "Identifier mismatch", detail: "Route id does not match body PortalID.");
        }

        var updated = await _portalService.UpdateAsync(request, cancellationToken);
        return Ok(ApiResponse.Success(updated));
    }

    /// <summary>Delete a portal (HARD delete with transactional cascade).</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken = default)
    {
        await _portalService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
