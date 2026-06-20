using DnnMigration.Application.Common;
using DnnMigration.Application.DTOs.Module;
using DnnMigration.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DnnMigration.Api.Controllers;

/// <summary>REST resource controller for Modules (/api/v1/modules).</summary>
// MIGRATION: Replaces the legacy ModuleController.vb surface (AddModule/GetModule/GetModules/GetTabModules/
// UpdateModule/DeleteModule). Module delete is a SOFT delete (IsDeleted); list reads exclude soft-deleted
// rows in the service/repository. Documented in root MIGRATION_NOTES.md.
[ApiController]
[Authorize]
[Produces("application/json")]
[Route("api/v1/modules")]
public sealed class ModulesController : ControllerBase
{
    private readonly IModuleService _moduleService;

    public ModulesController(IModuleService moduleService)
    {
        _moduleService = moduleService;
    }

    /// <summary>List modules for a tab (?tabId=) or a portal (?portalId=). One is required.</summary>
    // MIGRATION: Modules are always scoped to a tab or portal in the legacy model (GetTabModules(TabId)/
    // GetModules(PortalID)); there is no global module list, so the LIST endpoint requires a discriminator.
    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] int? portalId = null,
        [FromQuery] int? tabId = null,
        CancellationToken cancellationToken = default)
    {
        if (tabId.HasValue)
        {
            var byTab = await _moduleService.GetByTabAsync(tabId.Value, cancellationToken);
            return Ok(ApiResponse.Success(byTab));
        }

        if (portalId.HasValue)
        {
            var byPortal = await _moduleService.GetByPortalAsync(portalId.Value, cancellationToken);
            return Ok(ApiResponse.Success(byPortal));
        }

        return Problem(statusCode: 400, title: "Missing filter", detail: "Either portalId or tabId query parameter is required.");
    }

    /// <summary>Get a single module by id.</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken = default)
    {
        var module = await _moduleService.GetByIdAsync(id, cancellationToken);
        // MIGRATION (M8/DEV-037): RFC 7807 ProblemDetails (application/problem+json) instead of a bare
        // NotFound(), per the AAP error contract and matching the Problem(...) convention used above.
        return module is null
            ? Problem(statusCode: StatusCodes.Status404NotFound, title: "Module not found", detail: $"No module exists with id {id}.")
            : Ok(ApiResponse.Success(module));
    }

    /// <summary>Create a module.</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateModuleDto request, CancellationToken cancellationToken = default)
    {
        var created = await _moduleService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.ModuleID }, ApiResponse.Success(created));
    }

    /// <summary>Update a module.</summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateModuleDto request, CancellationToken cancellationToken = default)
    {
        if (id != request.ModuleID)
        {
            return Problem(statusCode: 400, title: "Identifier mismatch", detail: "Route id does not match body ModuleID.");
        }

        var updated = await _moduleService.UpdateAsync(request, cancellationToken);
        return Ok(ApiResponse.Success(updated));
    }

    /// <summary>Delete a module (SOFT delete — sets IsDeleted).</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken = default)
    {
        await _moduleService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
