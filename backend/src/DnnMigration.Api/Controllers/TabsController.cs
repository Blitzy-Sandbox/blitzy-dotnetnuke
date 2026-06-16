using DnnMigration.Api.Authorization;
using DnnMigration.Application.Common;
using DnnMigration.Application.DTOs.Tab;
using DnnMigration.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DnnMigration.Api.Controllers;

/// <summary>
/// REST resource controller for Tabs (<c>/api/v1/tabs</c>). In DotNetNuke a "Tab" is a site Page, so this
/// controller exposes thin CRUD delegation over <see cref="ITabService"/> and returns the uniform
/// <c>{ data, meta }</c> success envelope.
/// </summary>
/// <remarks>
/// The controller performs no mapping or business logic of its own: <see cref="ITabService"/> already returns
/// DTOs, so the actions translate HTTP requests into service calls and wrap the results in
/// <see cref="ApiResponse"/>. Service exceptions are intentionally NOT caught here; they bubble to the global
/// <c>ExceptionHandlingMiddleware</c>, which emits RFC 7807 Problem Details. The only in-controller
/// short-circuits are a 400 when the required <c>portalId</c> filter is absent (on list/count/get-by-id/delete),
/// a 404 when a tab is not found, and a 400 when the route id and body id disagree on update. Tab identity is
/// portal-scoped in DNN, so <see cref="GetById"/> and <see cref="Delete"/> require <c>portalId</c> as a query
/// parameter. Tab deletion is a SOFT delete (the service/repository sets the deleted flag); list reads exclude
/// soft-deleted rows in the service/repository, not here.
/// </remarks>
// MIGRATION: Replaces the legacy TabController.vb surface (GetTab/GetTabs/GetTabsByParentId/GetTabCount/
// AddTab/UpdateTab/DeleteTab). Tab identity is portal-scoped, so GET-by-id and DELETE require portalId.
// Tab delete is a SOFT delete; reads exclude deleted rows in the service. The legacy tab move/copy/reorder and
// skin/container operations are intentionally NOT part of this resource surface. Documented in root
// MIGRATION_NOTES.md.
[ApiController]
[Authorize]
[Produces("application/json")]
[Route("api/v1/tabs")]
public sealed class TabsController : ControllerBase
{
    private readonly ITabService _tabService;

    /// <summary>Initializes the controller with the Tab application service.</summary>
    /// <param name="tabService">The Tab application service that the actions delegate to.</param>
    public TabsController(ITabService tabService)
    {
        _tabService = tabService;
    }

    /// <summary>
    /// Lists tabs for a portal (<c>?portalId=</c>), optionally filtered to the children of a single parent tab
    /// (<c>?portalId=&amp;parentId=</c>). The <c>portalId</c> filter is required because tabs have no global list.
    /// </summary>
    /// <param name="portalId">Identifier of the owning portal. Required (portal 0 is the valid default portal).</param>
    /// <param name="parentId">When supplied, returns only the child tabs of the given parent tab within the portal.</param>
    /// <param name="cancellationToken">Token bound by ASP.NET Core to <c>HttpContext.RequestAborted</c>.</param>
    /// <returns>
    /// <c>200 OK</c> with the <c>{ data, meta }</c> envelope, or <c>400 Bad Request</c> when <paramref name="portalId"/> is absent.
    /// </returns>
    [HttpGet]
    [Authorize(Policy = Permissions.View)]
    public async Task<IActionResult> Get(
        [FromQuery] int? portalId = null,
        [FromQuery] int? parentId = null,
        CancellationToken cancellationToken = default)
    {
        // MIGRATION: portal 0 is a valid DNN portal, so the guard tests presence (HasValue), NOT a positive value.
        if (!portalId.HasValue)
        {
            return Problem(statusCode: 400, title: "Missing filter", detail: "The portalId query parameter is required.");
        }

        // MIGRATION: legacy GetTabsByParentId(ParentId, PortalId) -> ?parentId=; GetTabs(PortalId) -> ?portalId=.
        if (parentId.HasValue)
        {
            var byParent = await _tabService.GetByParentAsync(parentId.Value, portalId.Value, cancellationToken);
            return Ok(ApiResponse.Success(byParent));
        }

        var byPortal = await _tabService.GetByPortalAsync(portalId.Value, cancellationToken);
        return Ok(ApiResponse.Success(byPortal));
    }

    /// <summary>Counts the tabs in a portal.</summary>
    /// <param name="portalId">Identifier of the owning portal. Required (portal 0 is the valid default portal).</param>
    /// <param name="cancellationToken">Token bound by ASP.NET Core to <c>HttpContext.RequestAborted</c>.</param>
    /// <returns>
    /// <c>200 OK</c> with the scalar count wrapped in the <c>{ data, meta }</c> envelope, or <c>400 Bad Request</c>
    /// when <paramref name="portalId"/> is absent. The literal <c>count</c> segment cannot collide with the
    /// <c>{id:int}</c> route because "count" is not an integer.
    /// </returns>
    [HttpGet("count")]
    [Authorize(Policy = Permissions.View)]
    public async Task<IActionResult> GetCount([FromQuery] int? portalId = null, CancellationToken cancellationToken = default)
    {
        if (!portalId.HasValue)
        {
            return Problem(statusCode: 400, title: "Missing filter", detail: "The portalId query parameter is required.");
        }

        // MIGRATION: legacy GetTabCount(portalId) -> scalar int wrapped in the uniform success envelope.
        var count = await _tabService.GetCountAsync(portalId.Value, cancellationToken);
        return Ok(ApiResponse.Success(count));
    }

    /// <summary>Gets a single tab by its identifier within a portal.</summary>
    /// <param name="id">The tab identifier (TabID).</param>
    /// <param name="portalId">Identifier of the owning portal. Required because tab identity is portal-scoped.</param>
    /// <param name="cancellationToken">Token bound by ASP.NET Core to <c>HttpContext.RequestAborted</c>.</param>
    /// <returns>
    /// <c>200 OK</c> with the tab envelope, <c>404 Not Found</c> when it does not exist, or <c>400 Bad Request</c>
    /// when <paramref name="portalId"/> is absent.
    /// </returns>
    [HttpGet("{id:int}")]
    [Authorize(Policy = Permissions.View)]
    public async Task<IActionResult> GetById(int id, [FromQuery] int? portalId = null, CancellationToken cancellationToken = default)
    {
        if (!portalId.HasValue)
        {
            return Problem(statusCode: 400, title: "Missing filter", detail: "The portalId query parameter is required.");
        }

        // MIGRATION: legacy GetTab(TabId, PortalId) takes BOTH identifiers because a tab is scoped to its portal.
        var tab = await _tabService.GetByIdAsync(id, portalId.Value, cancellationToken);
        return tab is null ? NotFound() : Ok(ApiResponse.Success(tab));
    }

    /// <summary>Creates a tab.</summary>
    /// <param name="request">The create request payload.</param>
    /// <param name="cancellationToken">Token bound by ASP.NET Core to <c>HttpContext.RequestAborted</c>.</param>
    /// <returns>
    /// <c>201 Created</c> with a <c>Location</c> header pointing at <see cref="GetById"/> (carrying both the route
    /// <c>id</c> and the <c>portalId</c> query value so the header resolves) and the created tab envelope.
    /// </returns>
    [HttpPost]
    [Authorize(Policy = Permissions.Edit)]
    public async Task<IActionResult> Create([FromBody] CreateTabDto request, CancellationToken cancellationToken = default)
    {
        // MIGRATION: legacy AddTab(TabInfo) -> POST. The Location header must include portalId because GetById is
        // portal-scoped (GET /api/v1/tabs/{id}?portalId={portalId}).
        var created = await _tabService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.TabID, portalId = created.PortalID }, ApiResponse.Success(created));
    }

    /// <summary>Updates a tab.</summary>
    /// <param name="id">The tab identifier taken from the route; must equal <see cref="UpdateTabDto.TabID"/>.</param>
    /// <param name="request">The update request payload.</param>
    /// <param name="cancellationToken">Token bound by ASP.NET Core to <c>HttpContext.RequestAborted</c>.</param>
    /// <returns><c>200 OK</c> with the updated tab envelope, or <c>400 Bad Request</c> when the ids disagree.</returns>
    [HttpPut("{id:int}")]
    [Authorize(Policy = Permissions.Edit)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateTabDto request, CancellationToken cancellationToken = default)
    {
        if (id != request.TabID)
        {
            return Problem(statusCode: 400, title: "Identifier mismatch", detail: "Route id does not match body TabID.");
        }

        // MIGRATION: legacy UpdateTab(TabInfo) -> PUT.
        var updated = await _tabService.UpdateAsync(request, cancellationToken);
        return Ok(ApiResponse.Success(updated));
    }

    /// <summary>Deletes a tab within a portal (SOFT delete — the service/repository sets the deleted flag).</summary>
    /// <param name="id">The tab identifier (TabID).</param>
    /// <param name="portalId">Identifier of the owning portal. Required because tab identity is portal-scoped.</param>
    /// <param name="cancellationToken">Token bound by ASP.NET Core to <c>HttpContext.RequestAborted</c>.</param>
    /// <returns>
    /// <c>204 No Content</c> on success, or <c>400 Bad Request</c> when <paramref name="portalId"/> is absent.
    /// </returns>
    [HttpDelete("{id:int}")]
    [Authorize(Policy = Permissions.Delete)]
    public async Task<IActionResult> Delete(int id, [FromQuery] int? portalId = null, CancellationToken cancellationToken = default)
    {
        if (!portalId.HasValue)
        {
            return Problem(statusCode: 400, title: "Missing filter", detail: "The portalId query parameter is required.");
        }

        // MIGRATION: legacy DeleteTab(TabId, PortalId) is a SOFT delete and takes BOTH identifiers.
        await _tabService.DeleteAsync(id, portalId.Value, cancellationToken);
        return NoContent();
    }
}
