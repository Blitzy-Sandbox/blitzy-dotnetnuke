using DnnMigration.Application.Common;
using DnnMigration.Application.DTOs.Module;
using DnnMigration.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DnnMigration.Api.Controllers;

/// <summary>
/// REST resource controller for Modules (<c>/api/v1/modules</c>). Provides thin CRUD delegation over
/// <see cref="IModuleService"/> and returns the uniform <c>{ data, meta }</c> success envelope.
/// </summary>
/// <remarks>
/// The controller performs no mapping or business logic of its own: <see cref="IModuleService"/> already
/// returns DTOs, so the actions translate HTTP requests into service calls and wrap the results in
/// <see cref="ApiResponse"/>. Service exceptions are intentionally NOT caught here; they bubble to the
/// global <c>ExceptionHandlingMiddleware</c>, which emits RFC 7807 Problem Details. The only in-controller
/// short-circuits are a 404 when a module is not found, a 400 when neither list discriminator is supplied,
/// and a 400 when the route id and body id disagree. Module deletion is a SOFT delete (the service/repository
/// sets <c>IsDeleted</c>); list reads exclude soft-deleted rows in the service/repository, not here.
/// </remarks>
// MIGRATION: Replaces the legacy ModuleController.vb business surface (AddModule/GetModule/GetModules/
// GetTabModules/UpdateModule/DeleteModule). Postback/ViewState -> stateless REST. Module delete is a SOFT
// delete (IsDeleted); list reads exclude soft-deleted rows in the service/repository. The legacy module
// copy/move/settings operations are intentionally NOT part of this resource surface. Documented in root
// MIGRATION_NOTES.md.
[ApiController]
[Authorize]
[Produces("application/json")]
[Route("api/v1/modules")]
public sealed class ModulesController : ControllerBase
{
    private readonly IModuleService _moduleService;

    /// <summary>Initializes the controller with the Module application service.</summary>
    /// <param name="moduleService">The Module application service that the actions delegate to.</param>
    public ModulesController(IModuleService moduleService)
    {
        _moduleService = moduleService;
    }

    /// <summary>
    /// Lists modules scoped to a single tab (<c>?tabId=</c>) or to a whole portal (<c>?portalId=</c>).
    /// Exactly one discriminator is required because modules have no global list; <c>?tabId=</c> takes
    /// precedence when both are supplied.
    /// </summary>
    /// <param name="portalId">When supplied (and <paramref name="tabId"/> is not), returns every module in the portal.</param>
    /// <param name="tabId">When supplied, returns the modules placed on the given tab. Takes precedence over <paramref name="portalId"/>.</param>
    /// <param name="cancellationToken">Token bound by ASP.NET Core to <c>HttpContext.RequestAborted</c>.</param>
    /// <returns>
    /// <c>200 OK</c> with the <c>{ data, meta }</c> envelope, or <c>400 Bad Request</c> when neither
    /// <paramref name="tabId"/> nor <paramref name="portalId"/> is provided.
    /// </returns>
    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] int? portalId = null,
        [FromQuery] int? tabId = null,
        CancellationToken cancellationToken = default)
    {
        // MIGRATION: legacy GetTabModules(TabId) -> ?tabId=; GetModules(PortalID) -> ?portalId=.
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

    /// <summary>Gets a single module by its identifier.</summary>
    /// <param name="id">The module identifier.</param>
    /// <param name="cancellationToken">Token bound by ASP.NET Core to <c>HttpContext.RequestAborted</c>.</param>
    /// <returns><c>200 OK</c> with the module envelope, or <c>404 Not Found</c> when it does not exist.</returns>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken = default)
    {
        var module = await _moduleService.GetByIdAsync(id, cancellationToken);
        return module is null ? NotFound() : Ok(ApiResponse.Success(module));
    }

    /// <summary>Creates a module.</summary>
    /// <param name="request">The create request payload.</param>
    /// <param name="cancellationToken">Token bound by ASP.NET Core to <c>HttpContext.RequestAborted</c>.</param>
    /// <returns><c>201 Created</c> with a <c>Location</c> header pointing at <see cref="GetById"/> and the created module envelope.</returns>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateModuleDto request, CancellationToken cancellationToken = default)
    {
        var created = await _moduleService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.ModuleID }, ApiResponse.Success(created));
    }

    /// <summary>Updates a module.</summary>
    /// <param name="id">The module identifier taken from the route; must equal <see cref="UpdateModuleDto.ModuleID"/>.</param>
    /// <param name="request">The update request payload.</param>
    /// <param name="cancellationToken">Token bound by ASP.NET Core to <c>HttpContext.RequestAborted</c>.</param>
    /// <returns><c>200 OK</c> with the updated module envelope, or <c>400 Bad Request</c> when the ids disagree.</returns>
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

    /// <summary>Deletes a module (SOFT delete — the service/repository sets <c>IsDeleted</c>).</summary>
    /// <param name="id">The module identifier.</param>
    /// <param name="cancellationToken">Token bound by ASP.NET Core to <c>HttpContext.RequestAborted</c>.</param>
    /// <returns><c>204 No Content</c> on success.</returns>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken = default)
    {
        await _moduleService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
