using DnnMigration.Application.DTOs;
using DnnMigration.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DnnMigration.Api.Controllers;

/// <summary>
/// RESTful endpoints for managing tabs (DotNetNuke "pages") under the <c>/api/tabs</c> route.
/// </summary>
/// <remarks>
/// <para>
/// MIGRATION: replaces the legacy Web Forms tab/page administration workflows
/// (<c>Website/admin/Tabs/Tabs.ascx.vb</c> — the page list/management screen — and
/// <c>Website/admin/Tabs/ManageTabs.ascx.vb</c> — the single-page editor) together with the
/// data/business surface of <c>Library/Components/Tabs/TabController.vb</c>. In DotNetNuke a
/// "Tab" is a portal page; the terminology is preserved here for schema and domain fidelity
/// (AAP §0.7.1 "Data model fidelity").
/// </para>
/// <para>
/// This controller is intentionally THIN. It performs no business logic, no data access, no EF
/// Core, and no SQL. Every operation is delegated to the injected <see cref="ITabService"/>, which
/// owns the application rules; the controller's sole responsibilities are HTTP concerns — model
/// binding, routing, status-code selection, and shaping successful results through the inherited
/// <c>{ "data": ..., "meta": ... }</c> envelope helpers exposed by <see cref="ApiControllerBase"/>.
/// Error responses (including 404 bodies) are produced centrally as RFC 7807 Problem Details by the
/// exception-handling middleware; this controller never hand-formats error JSON.
/// </para>
/// <para>
/// The legacy VB methods map to REST verbs as follows: <c>GetAllTabs</c> → <c>GET /api/tabs</c>,
/// <c>GetTabs(PortalId)</c> → <c>GET /api/tabs?portalId={id}</c>, <c>GetTabsByParentId</c> →
/// <c>GET /api/tabs?parentId={id}</c>, <c>GetTab</c> → <c>GET /api/tabs/{id}</c>, <c>AddTab</c> →
/// <c>POST /api/tabs</c>, <c>UpdateTab</c> → <c>PUT /api/tabs/{id}</c>, and <c>DeleteTab</c> →
/// <c>DELETE /api/tabs/{id}</c>.
/// </para>
/// </remarks>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class TabsController : ApiControllerBase
{
    private readonly ITabService _tabService;

    /// <summary>
    /// Initializes a new instance of the <see cref="TabsController"/> class.
    /// </summary>
    /// <param name="tabService">The application service that implements every tab use case.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="tabService"/> is <see langword="null"/>.
    /// </exception>
    public TabsController(ITabService tabService)
    {
        // MIGRATION: the legacy TabController.vb used shared/instance members that reached the
        // database directly through DataProvider.Instance(); the collaborator is now supplied by
        // the built-in DI container (no statics in application code — AAP §0.6.1).
        ArgumentNullException.ThrowIfNull(tabService);
        _tabService = tabService;
    }

    /// <summary>
    /// Returns the collection of tabs, optionally filtered by parent tab or by owning portal.
    /// </summary>
    /// <param name="portalId">
    /// When supplied (and <paramref name="parentId"/> is not), restricts the result to the tabs
    /// owned by this portal.
    /// </param>
    /// <param name="parentId">
    /// When supplied, restricts the result to the immediate child tabs of this parent tab. Takes
    /// precedence over <paramref name="portalId"/> when both are provided.
    /// </param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns>
    /// HTTP 200 with the success envelope; <c>data</c> is the tab list and <c>meta.count</c> is its
    /// size.
    /// </returns>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int? portalId,
        [FromQuery] int? parentId,
        CancellationToken cancellationToken)
    {
        IEnumerable<TabDto> tabs;

        // MIGRATION: the legacy "read tabs" surface exposed three distinct entry points
        // (GetTabsByParentId, GetTabs(PortalId), GetAllTabs). They are collapsed into a single REST
        // resource whose behaviour is selected by optional query-string filters.
        if (parentId.HasValue)
        {
            // MIGRATION: TabController.GetTabsByParentId (L524). When BOTH filters are supplied,
            // parentId deliberately wins so the outcome stays simple and deterministic.
            tabs = await _tabService.GetByParentAsync(parentId.Value, cancellationToken);
        }
        else if (portalId.HasValue)
        {
            // MIGRATION: TabController.GetTabs(PortalId) (L516).
            tabs = await _tabService.GetByPortalAsync(portalId.Value, cancellationToken);
        }
        else
        {
            // MIGRATION: TabController.GetAllTabs (L463).
            tabs = await _tabService.GetAllAsync(cancellationToken);
        }

        var list = tabs.ToList();
        return OkEnvelope(list, new { count = list.Count });
    }

    /// <summary>
    /// Returns a single tab by its identifier.
    /// </summary>
    /// <param name="id">The unique identifier (<c>TabID</c>) of the tab to retrieve.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns>HTTP 200 with the tab envelope when found; otherwise HTTP 404.</returns>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        // MIGRATION: TabController.GetTab (L467). A missing tab yields 404 (NotFound) rather than
        // the legacy null TabInfo return; the 404 body is produced centrally as RFC 7807 Problem
        // Details by the exception/status-code middleware.
        var tab = await _tabService.GetByIdAsync(id, cancellationToken);
        return tab is null ? NotFound() : OkEnvelope(tab);
    }

    /// <summary>
    /// Creates a new tab.
    /// </summary>
    /// <param name="dto">The tab creation payload.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns>
    /// HTTP 201 with the created tab envelope and a <c>Location</c> header addressing
    /// <see cref="GetById"/>.
    /// </returns>
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateTabDto dto,
        CancellationToken cancellationToken)
    {
        // MIGRATION: TabController.AddTab (L326), which returned the new TabId. The service now
        // returns the created projection and the controller emits 201 Created with a Location
        // header pointing at the canonical GET-by-id action. [ApiController] auto-validates the
        // model and short-circuits with an RFC 7807 400 response before this body runs when the
        // payload is invalid, so no manual validation is required here.
        var created = await _tabService.CreateAsync(dto, cancellationToken);
        return CreatedEnvelope(nameof(GetById), new { id = created.TabID }, created);
    }

    /// <summary>
    /// Updates an existing tab.
    /// </summary>
    /// <param name="id">The identifier (<c>TabID</c>) of the tab to update.</param>
    /// <param name="dto">The tab update payload.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns>HTTP 200 with the updated tab envelope when found; otherwise HTTP 404.</returns>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] UpdateTabDto dto,
        CancellationToken cancellationToken)
    {
        // MIGRATION: TabController.UpdateTab (L780). A null result means the target tab does not
        // exist, which maps to 404 instead of the legacy silent no-op.
        var updated = await _tabService.UpdateAsync(id, dto, cancellationToken);
        return updated is null ? NotFound() : OkEnvelope(updated);
    }

    /// <summary>
    /// Permanently deletes a tab.
    /// </summary>
    /// <param name="id">The identifier (<c>TabID</c>) of the tab to delete.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns>HTTP 204 when the tab was deleted; HTTP 404 when no tab with the given id exists.</returns>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        // MIGRATION: TabController.DeleteTab (L446). The service reports whether a row was removed;
        // true -> 204 No Content (empty body), false -> 404 Not Found.
        var deleted = await _tabService.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}
