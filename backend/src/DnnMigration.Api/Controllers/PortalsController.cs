using DnnMigration.Application.DTOs;
using DnnMigration.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DnnMigration.Api.Controllers;

/// <summary>
/// RESTful endpoints for managing portals (DotNetNuke "sites") under the <c>/api/portals</c> route.
/// </summary>
/// <remarks>
/// <para>
/// MIGRATION: replaces the legacy ASP.NET Web Forms portal-administration workflows
/// (<c>Website/admin/Portal/**/*.ascx.vb</c> — most notably <c>SiteSettings.ascx.vb</c> the
/// single-portal editor and <c>Portals.ascx.vb</c> the portal list/grid) together with the
/// data/business surface of <c>Library/Components/Portal/PortalController.vb</c>. The Web Forms
/// postback/ViewState model is eliminated: <c>Page_Load</c> data fetches become HTTP GETs and the
/// button-click handlers (<c>cmdUpdate_Click</c>, <c>cmdDelete_Click</c>) become PUT/DELETE requests
/// that return JSON only (AAP §0.6.3).
/// </para>
/// <para>
/// This is the CANONICAL thin-CRUD controller template for the sibling resource controllers. It is
/// intentionally THIN: it performs no business logic, no data access, no EF Core, and no SQL. Every
/// operation is delegated to the injected <see cref="IPortalService"/>, which owns the application
/// rules; the controller's sole responsibilities are HTTP concerns — model binding, routing,
/// status-code selection, and shaping successful results through the inherited
/// <c>{ "data": ..., "meta": ... }</c> envelope helpers exposed by <see cref="ApiControllerBase"/>
/// (AAP §0.7.1 "no business logic in API controllers"). Error responses (including 404 bodies) are
/// produced centrally as RFC 7807 Problem Details by the exception-handling middleware plus
/// <c>AddProblemDetails()</c>; this controller never hand-formats error JSON, and request
/// correlation IDs are attached by <c>CorrelationIdMiddleware</c> in the pipeline (not per action).
/// </para>
/// <para>
/// Legacy VB methods map to REST verbs as follows: <c>PortalController.GetPortals</c> (L1263) →
/// <c>GET /api/portals</c>; <c>GetPortal(PortalId)</c> (L1224) → <c>GET /api/portals/{id}</c>;
/// <c>CreatePortal</c> (L980) → <c>POST /api/portals</c>; <c>UpdatePortalInfo</c> (L1568) →
/// <c>PUT /api/portals/{id}</c>; and <c>DeletePortalInfo</c> (L1191) → <c>DELETE /api/portals/{id}</c>.
/// </para>
/// <para>
/// MIGRATION: the class carries <see cref="AuthorizeAttribute"/> so the whole resource is secured by
/// default. This replaces the legacy DotNetNuke <c>PortalSecurity.SecurityAccessLevel</c>
/// (View/Edit/Admin/Host) checks and DES/Forms-authentication gate; access is now proven by a JWT
/// bearer token validated by the ASP.NET Core authentication middleware (AAP §0.6.4).
/// </para>
/// </remarks>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class PortalsController : ApiControllerBase
{
    private readonly IPortalService _portalService;

    /// <summary>
    /// Initializes a new instance of the <see cref="PortalsController"/> class.
    /// </summary>
    /// <param name="portalService">The application service that implements every portal use case.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="portalService"/> is <see langword="null"/>.
    /// </exception>
    public PortalsController(IPortalService portalService)
    {
        // MIGRATION: the legacy PortalController.vb exposed Public Shared (static) members that
        // reached the database directly through SqlDataProvider.Instance(); the collaborator is now
        // supplied by the built-in DI container as an injected instance (no statics in application
        // code — AAP §0.6.1). Assigning the field here guarantees it is non-null (no CS8618).
        ArgumentNullException.ThrowIfNull(portalService);
        _portalService = portalService;
    }

    /// <summary>
    /// Returns the collection of all portals.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns>
    /// HTTP 200 with the success envelope; <c>data</c> is the portal list and <c>meta.count</c> is
    /// its size.
    /// </returns>
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        // MIGRATION: PortalController.GetPortals (L1263) / the Portals.ascx.vb BindData grid feed.
        var portals = await _portalService.GetAllAsync(cancellationToken);
        var list = portals.ToList();
        return OkEnvelope(list, new { count = list.Count });
    }

    /// <summary>
    /// Returns a single portal by its identifier.
    /// </summary>
    /// <param name="id">The unique identifier (<c>PortalID</c>) of the portal to retrieve.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns>HTTP 200 with the portal envelope when found; otherwise HTTP 404.</returns>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        // MIGRATION: PortalController.GetPortal (L1224) / SiteSettings.ascx.vb Page_Load (L232) read.
        // A missing portal yields 404 (NotFound) rather than the legacy null PortalInfo return; the
        // 404 body is produced centrally as RFC 7807 Problem Details by the status-code middleware.
        var portal = await _portalService.GetByIdAsync(id, cancellationToken);
        return portal is null ? NotFound() : OkEnvelope(portal);
    }

    /// <summary>
    /// Creates a new portal (and, per the payload, its optional initial administrator user).
    /// </summary>
    /// <param name="dto">The portal creation payload.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns>
    /// HTTP 201 with the created portal envelope and a <c>Location</c> header addressing
    /// <see cref="GetById"/>.
    /// </returns>
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreatePortalDto dto,
        CancellationToken cancellationToken)
    {
        // MIGRATION: PortalController.CreatePortal (L980), which returned the new PortalId. The
        // service now returns the created projection and the controller emits 201 Created with a
        // Location header pointing at the canonical GET-by-id action (route value key "id" matches
        // the "{id:int}" segment). [ApiController] auto-validates the model and short-circuits with
        // an RFC 7807 400 response (FluentValidation wired in Program.cs) before this body runs when
        // the payload is invalid, so no manual ModelState check is required here.
        var created = await _portalService.CreateAsync(dto, cancellationToken);
        return CreatedEnvelope(nameof(GetById), new { id = created.PortalID }, created);
    }

    /// <summary>
    /// Updates an existing portal's core settings.
    /// </summary>
    /// <param name="id">The identifier (<c>PortalID</c>) of the portal to update.</param>
    /// <param name="dto">The portal update payload.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns>HTTP 200 with the updated portal envelope when found; otherwise HTTP 404.</returns>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] UpdatePortalDto dto,
        CancellationToken cancellationToken)
    {
        // MIGRATION: PortalController.UpdatePortalInfo (L1568) / SiteSettings.ascx.vb
        // cmdUpdate_Click (L687). A null result means the target portal does not exist, which maps
        // to 404 instead of the legacy silent no-op on a missing row.
        var updated = await _portalService.UpdateAsync(id, dto, cancellationToken);
        return updated is null ? NotFound() : OkEnvelope(updated);
    }

    /// <summary>
    /// Permanently deletes a portal.
    /// </summary>
    /// <param name="id">The identifier (<c>PortalID</c>) of the portal to delete.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns>HTTP 204 when the portal was deleted; HTTP 404 when no portal with the given id exists.</returns>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        // MIGRATION: PortalController.DeletePortalInfo (L1191) / SiteSettings.ascx.vb
        // cmdDelete_Click (L556). The service reports whether a row was removed;
        // true -> 204 No Content (empty body), false -> 404 Not Found.
        var deleted = await _portalService.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}
