using DnnMigration.Application.Common;
using DnnMigration.Application.DTOs.Tab;
using DnnMigration.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DnnMigration.Api.Controllers;

/// <summary>REST resource controller for Tabs/pages (/api/v1/tabs).</summary>
// MIGRATION: Replaces the legacy TabController.vb surface (GetTab/GetTabs/GetTabsByParentId/GetTabCount/
// AddTab/UpdateTab/DeleteTab). Tab identity is portal-scoped, so GET-by-id and DELETE require portalId.
// Tab delete is a SOFT delete; reads exclude deleted rows in the service. Documented in root MIGRATION_NOTES.md.
[ApiController]
[Authorize]
[Produces("application/json")]
[Route("api/v1/tabs")]
public sealed class TabsController : ControllerBase
{
    private readonly ITabService _tabService;

    public TabsController(ITabService tabService)
    {
        _tabService = tabService;
    }

    /// <summary>List tabs for a portal, optionally filtered by parent tab.</summary>
    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] int? portalId = null,
        [FromQuery] int? parentId = null,
        CancellationToken cancellationToken = default)
    {
        if (!portalId.HasValue)
        {
            return Problem(statusCode: 400, title: "Missing filter", detail: "The portalId query parameter is required.");
        }

        if (parentId.HasValue)
        {
            var byParent = await _tabService.GetByParentAsync(parentId.Value, portalId.Value, cancellationToken);
            return Ok(ApiResponse.Success(byParent));
        }

        var byPortal = await _tabService.GetByPortalAsync(portalId.Value, cancellationToken);
        return Ok(ApiResponse.Success(byPortal));
    }

    /// <summary>Count the tabs in a portal.</summary>
    [HttpGet("count")]
    public async Task<IActionResult> GetCount([FromQuery] int? portalId = null, CancellationToken cancellationToken = default)
    {
        if (!portalId.HasValue)
        {
            return Problem(statusCode: 400, title: "Missing filter", detail: "The portalId query parameter is required.");
        }

        var count = await _tabService.GetCountAsync(portalId.Value, cancellationToken);
        return Ok(ApiResponse.Success(count));
    }

    /// <summary>Get a single tab by id within a portal.</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, [FromQuery] int? portalId = null, CancellationToken cancellationToken = default)
    {
        if (!portalId.HasValue)
        {
            return Problem(statusCode: 400, title: "Missing filter", detail: "The portalId query parameter is required.");
        }

        var tab = await _tabService.GetByIdAsync(id, portalId.Value, cancellationToken);
        return tab is null ? NotFound() : Ok(ApiResponse.Success(tab));
    }

    /// <summary>Create a tab.</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTabDto request, CancellationToken cancellationToken = default)
    {
        var created = await _tabService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.TabID, portalId = created.PortalID }, ApiResponse.Success(created));
    }

    /// <summary>Update a tab.</summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateTabDto request, CancellationToken cancellationToken = default)
    {
        if (id != request.TabID)
        {
            return Problem(statusCode: 400, title: "Identifier mismatch", detail: "Route id does not match body TabID.");
        }

        var updated = await _tabService.UpdateAsync(request, cancellationToken);
        return Ok(ApiResponse.Success(updated));
    }

    /// <summary>Delete a tab within a portal (SOFT delete).</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, [FromQuery] int? portalId = null, CancellationToken cancellationToken = default)
    {
        if (!portalId.HasValue)
        {
            return Problem(statusCode: 400, title: "Missing filter", detail: "The portalId query parameter is required.");
        }

        await _tabService.DeleteAsync(id, portalId.Value, cancellationToken);
        return NoContent();
    }
}
