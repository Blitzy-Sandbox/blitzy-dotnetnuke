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
// MIGRATION: Routing uses api/[controller] (=> /api/modules) WITHOUT a /v1/ segment (Gate 5 + AAP
// resource-table parity). Recorded for MIGRATION_NOTES.md.
// MIGRATION: Result -> HTTP status is operation-based (single-read failure -> 404, write failure -> 400)
// because Domain.Common.Result has no error-category discriminator.
[ApiController]
[Route("api/[controller]")]
[Authorize]
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
        (pageIndex, pageSize) = NormalizePaging(pageIndex, pageSize);
        var result = await moduleService.GetByPortalAsync(portalId, pageIndex, pageSize, HttpContext.RequestAborted);
        return HandlePaged(result);
    }

    /// <summary>GET /api/modules/by-tab/{tabId} — all modules placed on a tab (page), unpaged.</summary>
    [HttpGet("by-tab/{tabId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetByTab(int tabId)
    {
        var result = await moduleService.GetByTabAsync(tabId, HttpContext.RequestAborted);
        return HandleList(result);
    }

    /// <summary>GET /api/modules/{id} — a single module by id.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await moduleService.GetByIdAsync(id, HttpContext.RequestAborted);
        return HandleGet(result);
    }

    /// <summary>POST /api/modules — create a module.</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateModuleRequest request)
    {
        var result = await moduleService.CreateAsync(request, HttpContext.RequestAborted);
        return HandleCreated(result, nameof(GetById), created => new { id = created.ModuleId });
    }

    /// <summary>PUT /api/modules/{id} — update a module.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateModuleRequest request)
    {
        var result = await moduleService.UpdateAsync(id, request, HttpContext.RequestAborted);
        return HandleResult(result);
    }

    /// <summary>DELETE /api/modules/{id} — delete a module.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await moduleService.DeleteAsync(id, HttpContext.RequestAborted);
        return HandleDelete(result);
    }
}
