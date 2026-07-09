using DnnMigration.Application.DTOs;
using DnnMigration.Application.Interfaces;
using DnnMigration.Domain.Common;
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
// MIGRATION (authorization — vertical gate): the caller must be an Administrator or a host
// (isSuperUser) to reach any tab action (AAP §0.6.4). Horizontal (per-portal) scoping is applied
// per action below via the ApiControllerBase guards.
[Authorize(Policy = "PortalAdministrator")]
// MIGRATION QA finding F: declare the response contract for OpenAPI/Swagger. Success bodies use the
// { data, meta } envelope (ApiResponse<T>); every error body is an RFC 7807 Problem Details payload
// produced centrally by the exception-handling middleware. 401 is declared once here because every
// action on this authorized resource returns it when the bearer token is missing or invalid; the
// per-action attributes below add the success shape plus the action-specific 400/403/404 responses.
// MIGRATION QA finding F: intentionally NO [Produces("application/json")] here. That attribute is an
// MVC result filter that would override the Content-Type of the [ApiController]-produced 400
// ValidationProblemDetails from "application/problem+json" to "application/json", breaking RFC 7807.
// The [ProducesResponseType] attributes alone supply the response schemas to Swagger.
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
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
    /// Returns a bounded, server-paginated collection of tabs, optionally filtered by parent tab or by
    /// owning portal.
    /// </summary>
    /// <param name="portalId">
    /// When supplied (and <paramref name="parentId"/> is not), restricts the result to the tabs
    /// owned by this portal.
    /// </param>
    /// <param name="parentId">
    /// When supplied, restricts the result to the immediate child tabs of this parent tab. Takes
    /// precedence over <paramref name="portalId"/> when both are provided.
    /// </param>
    /// <param name="page">
    /// Optional 1-based page number (default 1). Values below 1 are normalized to 1.
    /// </param>
    /// <param name="pageSize">
    /// Optional page size (default 50, hard maximum 200). Values above the maximum are clamped so a
    /// single request can never materialize every tab (R6 Issue 1 — unbounded lists).
    /// </param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns>
    /// HTTP 200 with the success envelope; <c>data</c> is the current page of tabs and <c>meta</c>
    /// carries <c>count</c>, <c>page</c>, <c>pageSize</c>, <c>totalCount</c>, and <c>totalPages</c>.
    /// </returns>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<TabDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int? portalId,
        [FromQuery] int? parentId,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        // MIGRATION (R6 Issue 1 — unbounded lists): the previous implementation fetched the ENTIRE tab
        // set (all tabs, or every tab in a portal) into memory before narrowing/returning it. The endpoint
        // is now bounded — page and pageSize are normalized (default 1/50, hard cap 200) and the data layer
        // applies Skip/Take + a COUNT so a single request can never stream an unbounded set.
        var paging = PaginationParameters.Normalize(page, pageSize);

        // MIGRATION (authorization — horizontal scoping): resolve the caller's portal scope up front.
        // A host (super) user is unrestricted; a non-host caller may not request another portal's tabs
        // explicitly (403) and is confined to its own portal on every branch below (AAP §0.6.4).
        var callerScopedPortalId = default(int?);
        if (!CallerIsSuperUser())
        {
            callerScopedPortalId = CallerPortalId();
            if (callerScopedPortalId is null) return ForbiddenProblem("The caller has no portal scope.");
            if (portalId.HasValue && portalId.Value != callerScopedPortalId.Value)
                return ForbiddenProblem($"The caller is not authorized to access resources owned by portal {portalId.Value}.");
        }

        // MIGRATION: the legacy "read tabs" surface exposed three distinct entry points
        // (GetTabsByParentId, GetTabs(PortalId), GetAllTabs). They are collapsed into a single REST
        // resource whose behaviour is selected by optional query-string filters — now every branch is
        // bounded by the same Skip/Take window and returns an honest totalCount.
        PagedResult<TabDto> result;

        if (callerScopedPortalId is int scopedPortalId)
        {
            // Non-host caller: always confined to its own portal.
            if (parentId.HasValue)
            {
                // MIGRATION: TabController.GetTabsByParentId (L524) — scoped. A tab hierarchy never crosses
                // portals, so the children of a parent all share the parent's PortalID. Verify the parent
                // belongs to the caller's portal first; a parent in another portal yields an empty,
                // zero-count page (rather than paging that portal's children and leaking their count).
                var parent = await _tabService.GetByIdAsync(parentId.Value, cancellationToken);
                result = parent is not null && parent.PortalID == scopedPortalId
                    ? await _tabService.GetByParentPagedAsync(parentId.Value, paging.Skip, paging.PageSize, cancellationToken)
                    : PagedResult<TabDto>.Empty;
            }
            else
            {
                // Both the explicit portalId (validated == own) and the unfiltered request collapse to the
                // caller's own portal — the legacy "all tabs then narrow to my portal" reduces to exactly this.
                result = await _tabService.GetByPortalPagedAsync(scopedPortalId, paging.Skip, paging.PageSize, cancellationToken);
            }
        }
        else if (parentId.HasValue)
        {
            // MIGRATION: TabController.GetTabsByParentId (L524). When BOTH filters are supplied,
            // parentId deliberately wins so the outcome stays simple and deterministic.
            result = await _tabService.GetByParentPagedAsync(parentId.Value, paging.Skip, paging.PageSize, cancellationToken);
        }
        else if (portalId.HasValue)
        {
            // MIGRATION: TabController.GetTabs(PortalId) (L516).
            result = await _tabService.GetByPortalPagedAsync(portalId.Value, paging.Skip, paging.PageSize, cancellationToken);
        }
        else
        {
            // MIGRATION: TabController.GetAllTabs (L463).
            result = await _tabService.GetPagedAsync(paging.Skip, paging.PageSize, cancellationToken);
        }

        return PagedEnvelope(result, paging);
    }

    /// <summary>
    /// Returns a single tab by its identifier.
    /// </summary>
    /// <param name="id">The unique identifier (<c>TabID</c>) of the tab to retrieve.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns>HTTP 200 with the tab envelope when found; otherwise HTTP 404.</returns>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<TabDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        // MIGRATION: TabController.GetTab (L467). A missing tab yields 404 (NotFound) rather than
        // the legacy null TabInfo return; the 404 body is produced centrally as RFC 7807 Problem
        // Details by the exception/status-code middleware.
        var tab = await _tabService.GetByIdAsync(id, cancellationToken);
        if (tab is null) return NotFound();

        // MIGRATION (authorization — horizontal scoping): a non-host caller may read a tab only when
        // it belongs to the caller's own portal (AAP §0.6.4).
        var denied = RequirePortalAccess(tab.PortalID);
        if (denied is not null) return denied;

        return OkEnvelope(tab);
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
    [ProducesResponseType(typeof(ApiResponse<TabDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create(
        [FromBody] CreateTabDto dto,
        CancellationToken cancellationToken)
    {
        // MIGRATION: TabController.AddTab (L326), which returned the new TabId. The service now
        // returns the created projection and the controller emits 201 Created with a Location
        // header pointing at the canonical GET-by-id action. [ApiController] auto-validates the
        // model and short-circuits with an RFC 7807 400 response before this body runs when the
        // payload is invalid, so no manual validation is required here.
        // MIGRATION (authorization — horizontal scoping): a non-host caller may create a tab only
        // within its own portal (the target portal travels in the DTO) (AAP §0.6.4).
        var denied = RequirePortalAccess(dto.PortalID);
        if (denied is not null) return denied;

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
    [ProducesResponseType(typeof(ApiResponse<TabDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] UpdateTabDto dto,
        CancellationToken cancellationToken)
    {
        // MIGRATION: TabController.UpdateTab (L780). A null result means the target tab does not
        // exist, which maps to 404 instead of the legacy silent no-op.
        // MIGRATION (authorization — horizontal scoping): confirm the target tab belongs to the
        // caller's portal before mutating it. The existing row is fetched first so a cross-portal
        // caller is rejected with 403 (not a silent no-op) and a missing row yields 404 (AAP §0.6.4).
        var existing = await _tabService.GetByIdAsync(id, cancellationToken);
        if (existing is null) return NotFound();
        var denied = RequirePortalAccess(existing.PortalID);
        if (denied is not null) return denied;

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
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        // MIGRATION: TabController.DeleteTab (L446). The service reports whether a row was removed;
        // true -> 204 No Content (empty body), false -> 404 Not Found.
        // MIGRATION (authorization — horizontal scoping): confirm the target tab belongs to the
        // caller's portal before deleting it (AAP §0.6.4). A missing row yields 404; a cross-portal
        // delete is rejected with 403.
        var existing = await _tabService.GetByIdAsync(id, cancellationToken);
        if (existing is null) return NotFound();
        var denied = RequirePortalAccess(existing.PortalID);
        if (denied is not null) return denied;

        var deleted = await _tabService.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}
