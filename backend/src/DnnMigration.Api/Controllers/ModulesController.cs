// -----------------------------------------------------------------------------
//  ModulesController.cs
//
//  MIGRATION: Thin ASP.NET Core 8 REST controller for module management, exposed
//  at /api/modules. It replaces the legacy DotNetNuke 4.x Web Forms module-admin
//  workflows (Website/admin/Modules/**/*.ascx.vb, e.g. ModuleSettings.ascx.vb) and
//  the data/business surface of Library/Components/Modules/ModuleController.vb.
//
//  The legacy static/Shared ModuleController methods are replaced by the injected
//  DnnMigration.Application.Interfaces.IModuleService (business rules live in the
//  Application service; data access lives behind a repository). This controller is
//  deliberately THIN: it performs no business logic, no data access, and no SQL.
//  It only binds HTTP requests to service calls and shapes the success responses
//  through the shared { data, meta } envelope on ApiControllerBase. Error/404
//  bodies are produced centrally as RFC 7807 Problem Details by the API's
//  exception-handling middleware; this controller never hand-formats error JSON.
//
//  Legacy VB (ModuleController.vb) -> REST mapping:
//    GetAllModules            (L871) -> GET    /api/modules
//    GetModules(PortalID)     (L915) -> GET    /api/modules?portalId={pid}
//    GetModule                (L885) -> GET    /api/modules/{id}
//    GetModuleByDefinition    (L955) -> GET    /api/modules/by-definition
//    AddModule                (L645) -> POST   /api/modules
//    UpdateModule             (L1095)-> PUT    /api/modules/{id}
//    DeleteModule             (L819) -> DELETE /api/modules/{id}
// -----------------------------------------------------------------------------

using DnnMigration.Application.DTOs;
using DnnMigration.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DnnMigration.Api.Controllers;

/// <summary>
/// RESTful endpoints for managing module instances (<c>/api/modules</c>).
/// </summary>
/// <remarks>
/// <para>
/// MIGRATION: replaces the legacy DotNetNuke Web Forms module administration
/// screens (<c>Website/admin/Modules/**</c>) and the HTTP-facing role of
/// <c>Library/Components/Modules/ModuleController.vb</c>. Following the target
/// Clean/Onion architecture (AAP §0.3.3), all business rules and data access are
/// delegated to <see cref="IModuleService"/>; this controller only translates HTTP
/// requests into service calls and projects the results into the standard success
/// envelope.
/// </para>
/// <para>
/// Response shaping follows the conventions centralised on
/// <see cref="ApiControllerBase"/>: collection and single-item reads use
/// <c>OkEnvelope</c> (HTTP 200), creation uses <c>CreatedEnvelope</c> (HTTP 201
/// with a <c>Location</c> header), a successful delete returns
/// <see cref="ControllerBase.NoContent"/> (HTTP 204), and a missing resource
/// returns <see cref="ControllerBase.NotFound()"/> (HTTP 404). Error bodies are
/// emitted centrally as RFC 7807 Problem Details, never hand-formatted here.
/// </para>
/// <para>
/// The controller requires authentication (<see cref="AuthorizeAttribute"/>); the
/// legacy <c>SecurityAccessLevel</c>/Forms-Authentication checks are replaced by
/// the JWT bearer scheme configured in <c>Program.cs</c>. Every action is fully
/// asynchronous and forwards the request's <see cref="CancellationToken"/> so that
/// client disconnects abort in-flight work.
/// </para>
/// </remarks>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class ModulesController : ApiControllerBase
{
    private readonly IModuleService _moduleService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ModulesController"/> class.
    /// </summary>
    /// <param name="moduleService">
    /// The module application service that encapsulates all module business rules
    /// and data access. MIGRATION: replaces the legacy static/<c>Shared</c>
    /// <c>ModuleController</c> members with a dependency-injected instance
    /// collaborator, satisfying the built-in DI-container requirement (AAP §0.1.1).
    /// </param>
    public ModulesController(IModuleService moduleService)
    {
        _moduleService = moduleService;
    }

    /// <summary>
    /// Returns all modules, optionally filtered to a single portal.
    /// </summary>
    /// <param name="portalId">
    /// When supplied, restricts the result to modules owned by the given portal
    /// (<c>GET /api/modules?portalId={pid}</c>); when omitted, all modules are
    /// returned (<c>GET /api/modules</c>).
    /// </param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns>
    /// HTTP 200 with the standard envelope whose <c>data</c> is the module list and
    /// whose <c>meta</c> carries the item <c>count</c>.
    /// </returns>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int? portalId, CancellationToken cancellationToken)
    {
        var modules = portalId.HasValue
            ? await _moduleService.GetByPortalAsync(portalId.Value, cancellationToken)   // MIGRATION: ModuleController.GetModules(PortalID) L915
            : await _moduleService.GetAllAsync(cancellationToken);                        // MIGRATION: ModuleController.GetAllModules L871
        var list = modules.ToList();
        return OkEnvelope(list, new { count = list.Count });
    }

    /// <summary>
    /// Returns a single module by its identifier.
    /// </summary>
    /// <param name="id">Identifier of the module to retrieve.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns>HTTP 200 with the module envelope, or HTTP 404 if no module has the given id.</returns>
    // MIGRATION: ModuleController.GetModule L885. The legacy TabId/ignoreCache cache
    // plumbing is not part of the REST contract; the service resolves the module by id.
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var module = await _moduleService.GetByIdAsync(id, cancellationToken);
        return module is null ? NotFound() : OkEnvelope(module);
    }

    /// <summary>
    /// Returns the first module in a portal whose definition has the given friendly name.
    /// </summary>
    /// <param name="portalId">Identifier of the portal to search within.</param>
    /// <param name="friendlyName">Friendly name of the module definition to match.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns>HTTP 200 with the module envelope, or HTTP 404 if no match is found.</returns>
    // MIGRATION: ModuleController.GetModuleByDefinition(PortalId, FriendlyName) L955.
    [HttpGet("by-definition")]
    public async Task<IActionResult> GetByDefinition(
        [FromQuery] int portalId,
        [FromQuery] string friendlyName,
        CancellationToken cancellationToken)
    {
        var module = await _moduleService.GetByDefinitionAsync(portalId, friendlyName, cancellationToken);
        return module is null ? NotFound() : OkEnvelope(module);
    }

    /// <summary>
    /// Creates (places) a new module instance.
    /// </summary>
    /// <param name="dto">The module placement payload bound from the request body.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns>
    /// HTTP 201 with the created module envelope and a <c>Location</c> header
    /// pointing at <see cref="GetById"/>.
    /// </returns>
    // MIGRATION: ModuleController.AddModule L645 (which returned the new ModuleID);
    // here the created projection carries ModuleID for the Location header.
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateModuleDto dto, CancellationToken cancellationToken)
    {
        var created = await _moduleService.CreateAsync(dto, cancellationToken);
        return CreatedEnvelope(nameof(GetById), new { id = created.ModuleID }, created);
    }

    /// <summary>
    /// Updates an existing module instance's settings.
    /// </summary>
    /// <param name="id">Identifier of the module to update (from the route).</param>
    /// <param name="dto">The editable module settings bound from the request body.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns>HTTP 200 with the updated module envelope, or HTTP 404 if no module has the given id.</returns>
    // MIGRATION: ModuleController.UpdateModule L1095, which also backs the legacy
    // Website/admin/Modules/ModuleSettings.ascx.vb "update settings" save workflow.
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateModuleDto dto, CancellationToken cancellationToken)
    {
        var updated = await _moduleService.UpdateAsync(id, dto, cancellationToken);
        return updated is null ? NotFound() : OkEnvelope(updated);
    }

    /// <summary>
    /// Deletes a module instance.
    /// </summary>
    /// <param name="id">Identifier of the module to delete.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns>HTTP 204 when the module is deleted, or HTTP 404 if no module has the given id.</returns>
    // MIGRATION: ModuleController.DeleteModule L819. A 204 (No Content) has no body,
    // so it deliberately does not use the { data, meta } envelope helpers.
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var deleted = await _moduleService.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}
