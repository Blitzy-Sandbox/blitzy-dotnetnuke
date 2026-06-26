using DnnMigration.Api.Authorization;
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
// MIGRATION (CP2 review — API versioning): exposed BOTH at /api/v1/tabs (AAP §0.1.2/§0.3.4 URL-path
// versioning NFR) AND at /api/tabs (AAP §0.3.4 resource table + Gate 5 literal paths) via dual [Route]
// attributes (no external API-versioning package is available offline). Recorded in MIGRATION_NOTES.md.
// MIGRATION (CP2 review — authorization + tenant isolation): page (tab) administration requires the
// PortalAdministrator policy, and every action enforces that the client-supplied portalId matches the JWT
// "portalId" claim (EnforceTenant) so a portal admin can only manage pages in its own portal; host SuperUsers
// bypass the tenant check.
// MIGRATION: Result -> HTTP status is operation-based (single-read failure -> 404, write failure -> 400)
// because Domain.Common.Result has no error-category discriminator.
[ApiController]
[Route("api/[controller]")]
[Route("api/v1/[controller]")]
[Authorize(Policy = AuthorizationPolicies.PortalAdministrator)]
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
        var tenantDenied = EnforceTenant(portalId);
        if (tenantDenied is not null)
        {
            return tenantDenied;
        }

        var result = await tabService.GetByPortalAsync(portalId, HttpContext.RequestAborted);
        return HandleList(result);
    }

    // MIGRATION: CP1 review (ITabService #1 / TabService #3) — single-tab reads are PORTAL-SCOPED: portalId is a
    // required query parameter so a tab from another portal can never be read through the wrong tenant (multi-tenant
    // isolation preserved from DNN's PortalId discriminator, AAP §0.7.1).
    /// <summary>GET /api/tabs/{id}?portalId= — a single tab (page) by id, scoped to its portal.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById([FromQuery, BindRequired] int portalId, int id)
    {
        var tenantDenied = EnforceTenant(portalId);
        if (tenantDenied is not null)
        {
            return tenantDenied;
        }

        var result = await tabService.GetByIdAsync(portalId, id, HttpContext.RequestAborted);
        return HandleGet(result);
    }

    /// <summary>POST /api/tabs — create a tab (page).</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateTabRequest request)
    {
        var tenantDenied = EnforceTenant(request.PortalId);
        if (tenantDenied is not null)
        {
            return tenantDenied;
        }

        var result = await tabService.CreateAsync(request, HttpContext.RequestAborted);
        // MIGRATION: the created-resource Location points at the portal-scoped GetById, so the portalId query value
        // is carried in the route values alongside the id (CP1 review ITabService #1 multi-tenant contract).
        return HandleCreated(result, nameof(GetById), created => new { id = created.TabId, portalId = request.PortalId });
    }

    // MIGRATION: CP1 review (ITabService #1 / TabService #3) — PORTAL-SCOPED update: portalId enforces tenant
    // ownership before the tab is mutated (multi-tenant isolation, AAP §0.7.1).
    /// <summary>PUT /api/tabs/{id}?portalId= — update a tab (page), scoped to its portal.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update([FromQuery, BindRequired] int portalId, int id, [FromBody] UpdateTabRequest request)
    {
        var tenantDenied = EnforceTenant(portalId);
        if (tenantDenied is not null)
        {
            return tenantDenied;
        }

        var result = await tabService.UpdateAsync(portalId, id, request, HttpContext.RequestAborted);
        return HandleResult(result);
    }

    // MIGRATION: CP1 review (ITabService #1 / TabService #4) — PORTAL-SCOPED delete: portalId enforces tenant
    // ownership/authorization before the child-page guard and the delete run (multi-tenant isolation, AAP §0.7.1).
    /// <summary>DELETE /api/tabs/{id}?portalId= — delete a tab (page), scoped to its portal.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Delete([FromQuery, BindRequired] int portalId, int id)
    {
        var tenantDenied = EnforceTenant(portalId);
        if (tenantDenied is not null)
        {
            return tenantDenied;
        }

        var result = await tabService.DeleteAsync(portalId, id, HttpContext.RequestAborted);
        return HandleDelete(result);
    }
}
