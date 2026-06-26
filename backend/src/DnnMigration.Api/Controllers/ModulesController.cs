using DnnMigration.Api.Authorization;
using DnnMigration.Application.DTOs.Module;
using DnnMigration.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace DnnMigration.Api.Controllers;

// MIGRATION: Replaces the legacy DotNetNuke Web Forms module administration code-behind
// Website/admin/Modules/ModuleSettings.ascx.vb (module settings + lifecycle). ViewState/postback is
// discarded; the workflow is re-expressed as thin JSON REST endpoints delegating to IModuleService.
// MIGRATION (CP2 review — API versioning): exposed BOTH at /api/v1/modules (AAP §0.1.2/§0.3.4 URL-path
// versioning NFR) AND at /api/modules (AAP §0.3.4 resource table + Gate 5 literal paths) via dual [Route]
// attributes (no external API-versioning package is available offline). Recorded in MIGRATION_NOTES.md.
// MIGRATION (CP2 review — authorization + tenant isolation): module administration requires the
// PortalAdministrator policy, and every action enforces that the client-supplied portalId matches the JWT
// "portalId" claim (EnforceTenant) so a portal admin can only manage modules in its own portal; host
// SuperUsers bypass the tenant check.
// MIGRATION: Result -> HTTP status is operation-based (single-read failure -> 404, write failure -> 400)
// because Domain.Common.Result has no error-category discriminator.
[ApiController]
[Route("api/[controller]")]
[Route("api/v1/[controller]")]
[Authorize(Policy = AuthorizationPolicies.PortalAdministrator)]
[Produces("application/json")]
public sealed class ModulesController(IModuleService moduleService) : ApiControllerBase
{
    // MIGRATION: Module listing is always portal-scoped (multi-tenant isolation preserved from DNN's
    // PortalId discriminator) — portalId is a required query parameter; there is no host-wide module list.
    /// <summary>GET /api/modules?portalId=&amp;pageIndex=&amp;pageSize= — paged modules for a portal.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetByPortal(
        [FromQuery, BindRequired] int portalId,
        [FromQuery] int pageIndex = 0,
        [FromQuery] int pageSize = 20)
    {
        var tenantDenied = EnforceTenant(portalId);
        if (tenantDenied is not null)
        {
            return tenantDenied;
        }

        (pageIndex, pageSize) = NormalizePaging(pageIndex, pageSize);
        var result = await moduleService.GetByPortalAsync(portalId, pageIndex, pageSize, HttpContext.RequestAborted);
        return HandlePaged(result);
    }

    // MIGRATION: CP1 review (IModuleService #1 / IModuleRepository #1) — a tab's modules are read portal-scoped
    // (multi-tenant isolation preserved from DNN's PortalId discriminator); portalId is a required query parameter.
    /// <summary>GET /api/modules/by-tab/{tabId}?portalId= — all modules placed on a tab (page), unpaged (portal-scoped).</summary>
    [HttpGet("by-tab/{tabId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetByTab([FromQuery, BindRequired] int portalId, int tabId)
    {
        var tenantDenied = EnforceTenant(portalId);
        if (tenantDenied is not null)
        {
            return tenantDenied;
        }

        var result = await moduleService.GetByTabAsync(portalId, tabId, HttpContext.RequestAborted);
        return HandleList(result);
    }

    // MIGRATION: CP1 review (IModuleService #1 / IModuleRepository #1) — a single-module read is portal-scoped so a
    // module from another portal can never be read through this tenant; portalId is a required query parameter.
    /// <summary>GET /api/modules/{id}?portalId= — a single module by id (portal-scoped).</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById([FromQuery, BindRequired] int portalId, int id)
    {
        var tenantDenied = EnforceTenant(portalId);
        if (tenantDenied is not null)
        {
            return tenantDenied;
        }

        var result = await moduleService.GetByIdAsync(portalId, id, HttpContext.RequestAborted);
        return HandleGet(result);
    }

    /// <summary>POST /api/modules — create a module.</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateModuleRequest request)
    {
        var tenantDenied = EnforceTenant(request.PortalId);
        if (tenantDenied is not null)
        {
            return tenantDenied;
        }

        var result = await moduleService.CreateAsync(request, HttpContext.RequestAborted);
        // MIGRATION: CP1 review — GetById is now portal-scoped, so the 201 Location route values must include
        // portalId (from the request body) alongside the new module id.
        return HandleCreated(result, nameof(GetById), created => new { id = created.ModuleId, portalId = request.PortalId });
    }

    // MIGRATION: CP1 review (IModuleService #1 / IModuleRepository #1) — update is portal-scoped so a module from
    // another portal can never be modified; portalId is a required query parameter.
    /// <summary>PUT /api/modules/{id}?portalId= — update a module (portal-scoped).</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update([FromQuery, BindRequired] int portalId, int id, [FromBody] UpdateModuleRequest request)
    {
        var tenantDenied = EnforceTenant(portalId);
        if (tenantDenied is not null)
        {
            return tenantDenied;
        }

        var result = await moduleService.UpdateAsync(portalId, id, request, HttpContext.RequestAborted);
        return HandleResult(result);
    }

    // MIGRATION: CP1 review (IModuleService #1 / IModuleRepository #1) — delete is portal-scoped so a module from
    // another portal can never be deleted; portalId is a required query parameter.
    /// <summary>DELETE /api/modules/{id}?portalId= — delete a module (portal-scoped).</summary>
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

        var result = await moduleService.DeleteAsync(portalId, id, HttpContext.RequestAborted);
        return HandleDelete(result);
    }
}
