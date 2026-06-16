using DnnMigration.Application.Common;
using DnnMigration.Application.DTOs.Portal;
using DnnMigration.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DnnMigration.Api.Controllers;

/// <summary>
/// REST resource controller for Portals (<c>/api/v1/portals</c>). Provides thin CRUD delegation over
/// <see cref="IPortalService"/> and returns the uniform <c>{ data, meta }</c> success envelope.
/// </summary>
/// <remarks>
/// The controller performs no mapping or business logic of its own: <see cref="IPortalService"/> already
/// returns DTOs, so the actions translate HTTP requests into service calls and wrap the results in
/// <see cref="ApiResponse"/>. Service exceptions are intentionally NOT caught here; they bubble to the
/// global <c>ExceptionHandlingMiddleware</c>, which emits RFC 7807 Problem Details. The only in-controller
/// short-circuits are a 404 when a portal is not found and a 400 when the route id and body id disagree.
/// </remarks>
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

    /// <summary>Initializes the controller with the Portal application service.</summary>
    /// <param name="portalService">The Portal application service that the actions delegate to.</param>
    public PortalsController(IPortalService portalService)
    {
        _portalService = portalService;
    }

    /// <summary>
    /// Lists portals. When <paramref name="query"/> is supplied, performs a paged name-prefix search and
    /// returns a paged envelope; otherwise returns the full, unfiltered collection.
    /// </summary>
    /// <param name="query">Optional name-prefix filter (the legacy letter filter). When blank, all portals are returned.</param>
    /// <param name="pageIndex">Zero-based page index used only when <paramref name="query"/> is supplied.</param>
    /// <param name="pageSize">Maximum number of items per page used only when <paramref name="query"/> is supplied.</param>
    /// <param name="cancellationToken">Token bound by ASP.NET Core to <c>HttpContext.RequestAborted</c>.</param>
    /// <returns><c>200 OK</c> with the <c>{ data, meta }</c> envelope (paged when filtered).</returns>
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

    /// <summary>Gets a single portal by its identifier.</summary>
    /// <param name="id">The portal identifier.</param>
    /// <param name="cancellationToken">Token bound by ASP.NET Core to <c>HttpContext.RequestAborted</c>.</param>
    /// <returns><c>200 OK</c> with the portal envelope, or <c>404 Not Found</c> when it does not exist.</returns>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken = default)
    {
        var portal = await _portalService.GetByIdAsync(id, cancellationToken);
        return portal is null ? NotFound() : Ok(ApiResponse.Success(portal));
    }

    /// <summary>Creates a portal.</summary>
    /// <param name="request">The create request payload.</param>
    /// <param name="cancellationToken">Token bound by ASP.NET Core to <c>HttpContext.RequestAborted</c>.</param>
    /// <returns><c>201 Created</c> with a <c>Location</c> header pointing at <see cref="GetById"/> and the created portal envelope.</returns>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePortalDto request, CancellationToken cancellationToken = default)
    {
        var created = await _portalService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.PortalID }, ApiResponse.Success(created));
    }

    /// <summary>Updates a portal.</summary>
    /// <param name="id">The portal identifier taken from the route; must equal <see cref="UpdatePortalDto.PortalID"/>.</param>
    /// <param name="request">The update request payload.</param>
    /// <param name="cancellationToken">Token bound by ASP.NET Core to <c>HttpContext.RequestAborted</c>.</param>
    /// <returns><c>200 OK</c> with the updated portal envelope, or <c>400 Bad Request</c> when the ids disagree.</returns>
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

    /// <summary>Deletes a portal (HARD delete with transactional cascade performed by the service/repository).</summary>
    /// <param name="id">The portal identifier.</param>
    /// <param name="cancellationToken">Token bound by ASP.NET Core to <c>HttpContext.RequestAborted</c>.</param>
    /// <returns><c>204 No Content</c> on success.</returns>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken = default)
    {
        await _portalService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
