// -----------------------------------------------------------------------------
// <copyright file="ModulesController.cs" company="DNN Migration Project">
//   Copyright (c) DNN Migration Project. All rights reserved.
//   Licensed under the MIT License. See LICENSE file in the project root.
// </copyright>
// <summary>
//   ASP.NET Core 8 REST API controller for module management operations.
//   MIGRATION: Replaces legacy ModuleSettings.ascx.vb, Export.ascx.vb, and Import.ascx.vb
//   WebForms code-behinds with RESTful API endpoints.
//   Source: Website/admin/Modules/ModuleSettings.ascx.vb
// </summary>
// -----------------------------------------------------------------------------

using DnnMigration.Application.DTOs.Common;
using DnnMigration.Application.DTOs.Module;
using DnnMigration.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace DnnMigration.Api.Controllers;

/// <summary>
/// REST API controller for module lifecycle management operations.
/// </summary>
/// <remarks>
/// <para>
/// This controller provides endpoints for managing DNN modules within the portal system.
/// All endpoints require authentication, with administrative operations requiring the
/// "Administrators" role.
/// </para>
/// <para>
/// MIGRATION: This controller replaces the following legacy WebForms code-behinds:
/// <list type="bullet">
///   <item><description>ModuleSettings.ascx.vb - Module configuration and update (cmdUpdate_Click)</description></item>
///   <item><description>Export.ascx.vb - Module content export functionality</description></item>
///   <item><description>Import.ascx.vb - Module content import functionality</description></item>
/// </list>
/// </para>
/// <para>
/// The controller follows the BFF (Backend-for-Frontend) pattern, providing a clean
/// REST API for Angular 19 SPA consumption per Section 0.1.3 target architecture.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Get paginated modules for a portal
/// GET /api/modules?portalId=1&amp;pageIndex=0&amp;pageSize=10
/// 
/// // Get a specific module
/// GET /api/modules/123
/// 
/// // Get all modules on a tab/page
/// GET /api/modules/tab/42
/// 
/// // Create a new module (Administrators only)
/// POST /api/modules
/// Content-Type: application/json
/// { "tabId": 42, "moduleDefId": 117, "paneName": "ContentPane", "moduleTitle": "My Module" }
/// 
/// // Update module settings (Administrators only)
/// PUT /api/modules/123
/// Content-Type: application/json
/// { "moduleTitle": "Updated Title", "cacheTime": 300 }
/// 
/// // Delete a module (Administrators only)
/// DELETE /api/modules/123
/// </code>
/// </example>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class ModulesController : ControllerBase
{
    #region Private Fields

    /// <summary>
    /// Module service for business logic operations.
    /// </summary>
    private readonly IModuleService _moduleService;

    /// <summary>
    /// Logger for structured logging of controller operations.
    /// </summary>
    private readonly ILogger<ModulesController> _logger;

    #endregion

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="ModulesController"/> class.
    /// </summary>
    /// <param name="moduleService">
    /// The module service instance for handling module CRUD operations.
    /// MIGRATION: Replaces direct ModuleController.vb calls from legacy code.
    /// </param>
    /// <param name="logger">
    /// The logger instance for structured logging.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="moduleService"/> or <paramref name="logger"/> is null.
    /// </exception>
    public ModulesController(
        IModuleService moduleService,
        ILogger<ModulesController> logger)
    {
        _moduleService = moduleService ?? throw new ArgumentNullException(nameof(moduleService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    #endregion

    #region GET Endpoints

    /// <summary>
    /// Retrieves a paginated list of modules for a specific portal.
    /// </summary>
    /// <param name="portalId">The portal identifier to filter modules by.</param>
    /// <param name="pageIndex">Zero-based index of the page to retrieve. Default is 0.</param>
    /// <param name="pageSize">Maximum number of modules per page. Default is 10.</param>
    /// <param name="cancellationToken">Token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A paginated result containing modules for the specified portal.
    /// </returns>
    /// <remarks>
    /// MIGRATION: Replaces legacy ModuleController.GetModules(PortalID) from ModuleController.vb
    /// at lines 915-930. The original method returned ArrayList; this endpoint returns
    /// a strongly-typed PagedResult for better API design.
    /// </remarks>
    /// <response code="200">Returns the paginated list of modules.</response>
    /// <response code="400">If the portalId is invalid or pagination parameters are out of range.</response>
    /// <response code="401">If the user is not authenticated.</response>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ModuleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PagedResult<ModuleDto>>> GetModules(
        [FromQuery] int portalId,
        [FromQuery] int pageIndex = 0,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Retrieving modules for portal {PortalId}, page {PageIndex}, size {PageSize}",
            portalId,
            pageIndex,
            pageSize);

        // Validate pagination parameters
        if (pageIndex < 0)
        {
            _logger.LogWarning("Invalid pageIndex: {PageIndex}", pageIndex);
            return BadRequest("Page index must be non-negative.");
        }

        if (pageSize <= 0 || pageSize > 100)
        {
            _logger.LogWarning("Invalid pageSize: {PageSize}", pageSize);
            return BadRequest("Page size must be between 1 and 100.");
        }

        var result = await _moduleService.GetModulesAsync(
            portalId,
            pageIndex,
            pageSize,
            cancellationToken);

        _logger.LogInformation(
            "Retrieved {Count} modules (total: {TotalCount}) for portal {PortalId}",
            result.Items.Count,
            result.TotalCount,
            portalId);

        return Ok(result);
    }

    /// <summary>
    /// Retrieves a single module by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the module to retrieve.</param>
    /// <param name="cancellationToken">Token to cancel the asynchronous operation.</param>
    /// <returns>
    /// The module if found; otherwise, a 404 Not Found response.
    /// </returns>
    /// <remarks>
    /// MIGRATION: Replaces legacy ModuleController.GetModule(ModuleId, TabId, ignoreCache)
    /// from ModuleController.vb at lines 885-905. The TabId parameter is not required
    /// in the new API as modules can be uniquely identified by ModuleId alone.
    /// </remarks>
    /// <response code="200">Returns the requested module.</response>
    /// <response code="401">If the user is not authenticated.</response>
    /// <response code="404">If the module with the specified ID was not found.</response>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ModuleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ModuleDto>> GetModule(
        int id,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving module with ID {ModuleId}", id);

        var module = await _moduleService.GetModuleAsync(id, cancellationToken);

        if (module is null)
        {
            _logger.LogWarning("Module with ID {ModuleId} not found", id);
            return NotFound($"Module with ID {id} was not found.");
        }

        _logger.LogInformation(
            "Retrieved module {ModuleId}: {ModuleTitle}",
            module.ModuleId,
            module.ModuleTitle);

        return Ok(module);
    }

    /// <summary>
    /// Retrieves all modules placed on a specific tab (page).
    /// </summary>
    /// <param name="tabId">The tab/page identifier to retrieve modules for.</param>
    /// <param name="cancellationToken">Token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A collection of modules placed on the specified tab.
    /// </returns>
    /// <remarks>
    /// <para>
    /// MIGRATION: Replaces legacy ModuleController.GetTabModules(TabId) from ModuleController.vb
    /// at lines 1044-1063. The original method returned Dictionary(Of Integer, ModuleInfo);
    /// this endpoint returns IEnumerable for simpler consumption in REST APIs.
    /// </para>
    /// <para>
    /// The legacy ModuleSettings.ascx.vb BindData method used GetModule(ModuleId, TabId, False)
    /// to retrieve modules for display. This endpoint provides an optimized way to
    /// retrieve all modules on a tab in a single request.
    /// </para>
    /// </remarks>
    /// <response code="200">Returns all modules on the specified tab.</response>
    /// <response code="401">If the user is not authenticated.</response>
    [HttpGet("tab/{tabId:int}")]
    [ProducesResponseType(typeof(IEnumerable<ModuleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IEnumerable<ModuleDto>>> GetModulesByTab(
        int tabId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving modules for tab {TabId}", tabId);

        var modules = await _moduleService.GetModulesByTabAsync(tabId, cancellationToken);

        // Convert to list for count logging
        var moduleList = modules.ToList();

        _logger.LogInformation(
            "Retrieved {Count} modules for tab {TabId}",
            moduleList.Count,
            tabId);

        return Ok(moduleList);
    }

    #endregion

    #region POST Endpoints

    /// <summary>
    /// Creates a new module instance on a tab/page.
    /// </summary>
    /// <param name="request">The module creation request containing all required fields.</param>
    /// <param name="cancellationToken">Token to cancel the asynchronous operation.</param>
    /// <returns>
    /// The newly created module with HTTP 201 Created status and location header.
    /// </returns>
    /// <remarks>
    /// <para>
    /// MIGRATION: Replaces the module creation workflow from ModuleController.AddModule
    /// at lines 645-682 of ModuleController.vb. The creation process involves:
    /// </para>
    /// <list type="number">
    ///   <item><description>Creating a Module record via DataProvider.Instance().AddModule</description></item>
    ///   <item><description>Setting module permissions if provided</description></item>
    ///   <item><description>Creating a TabModule record via DataProvider.Instance().AddTabModule</description></item>
    ///   <item><description>Positioning the module in the pane (at bottom if ModuleOrder is -1)</description></item>
    ///   <item><description>Clearing the module cache for the tab</description></item>
    /// </list>
    /// <para>
    /// This endpoint requires the "Administrators" role per Section 0.7.7 security rules.
    /// </para>
    /// </remarks>
    /// <response code="201">Returns the newly created module with location header.</response>
    /// <response code="400">If the request body is invalid or validation fails.</response>
    /// <response code="401">If the user is not authenticated.</response>
    /// <response code="403">If the user does not have the Administrators role.</response>
    [HttpPost]
    [Authorize(Roles = "Administrators")]
    [ProducesResponseType(typeof(ModuleDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ModuleDto>> CreateModule(
        [FromBody] CreateModuleRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Creating module on tab {TabId} with definition {ModuleDefId} in pane {PaneName}",
            request.TabId,
            request.ModuleDefId,
            request.PaneName);

        var module = await _moduleService.CreateModuleAsync(request, cancellationToken);

        _logger.LogInformation(
            "Created module {ModuleId}: {ModuleTitle} on tab {TabId}",
            module.ModuleId,
            module.ModuleTitle,
            module.TabId);

        // Return 201 Created with location header pointing to the new resource
        return CreatedAtAction(
            nameof(GetModule),
            new { id = module.ModuleId },
            module);
    }

    #endregion

    #region PUT Endpoints

    /// <summary>
    /// Updates an existing module's settings.
    /// </summary>
    /// <param name="id">The unique identifier of the module to update.</param>
    /// <param name="request">The update request containing fields to modify (nullable fields support partial updates).</param>
    /// <param name="cancellationToken">Token to cancel the asynchronous operation.</param>
    /// <returns>
    /// The updated module reflecting all changes.
    /// </returns>
    /// <remarks>
    /// <para>
    /// MIGRATION: Replaces the cmdUpdate_Click handler from ModuleSettings.ascx.vb
    /// at lines 326-427. The update logic follows the original VB.NET implementation:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>Line 344: objModule.ModuleTitle = txtTitle.Text → ModuleTitle property</description></item>
    ///   <item><description>Line 345: objModule.Alignment = cboAlign.SelectedItem.Value → Alignment property</description></item>
    ///   <item><description>Line 346: objModule.Color = txtColor.Text → Color property</description></item>
    ///   <item><description>Line 347: objModule.Border = txtBorder.Text → Border property</description></item>
    ///   <item><description>Line 348: objModule.IconFile = ctlIcon.Url → IconFile property</description></item>
    ///   <item><description>Lines 349-353: CacheTime parsing logic → CacheTime property</description></item>
    ///   <item><description>Lines 359-363: Visibility state mapping → Visibility property (0=Maximized, 1=Minimized, 2=None)</description></item>
    ///   <item><description>Lines 365-366: Header/Footer text → Header/Footer properties</description></item>
    ///   <item><description>Lines 367-376: StartDate/EndDate handling → StartDate/EndDate properties</description></item>
    ///   <item><description>Line 377: ContainerSrc assignment → ContainerSrc property</description></item>
    ///   <item><description>Lines 379-382: Display options → InheritViewPermissions, DisplayTitle, DisplayPrint, DisplaySyndicate</description></item>
    ///   <item><description>Lines 383-384: chkDefault/chkAllModules → IsDefaultModule, AllModules properties</description></item>
    ///   <item><description>Line 358: AllTabs change detection and processing</description></item>
    /// </list>
    /// <para>
    /// This endpoint requires the "Administrators" role per Section 0.7.7 security rules.
    /// </para>
    /// </remarks>
    /// <response code="200">Returns the updated module.</response>
    /// <response code="400">If the request body is invalid or validation fails.</response>
    /// <response code="401">If the user is not authenticated.</response>
    /// <response code="403">If the user does not have the Administrators role.</response>
    /// <response code="404">If the module with the specified ID was not found.</response>
    [HttpPut("{id:int}")]
    [Authorize(Roles = "Administrators")]
    [ProducesResponseType(typeof(ModuleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ModuleDto>> UpdateModule(
        int id,
        [FromBody] UpdateModuleRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating module {ModuleId}", id);

        try
        {
            var module = await _moduleService.UpdateModuleAsync(id, request, cancellationToken);

            _logger.LogInformation(
                "Updated module {ModuleId}: {ModuleTitle}",
                module.ModuleId,
                module.ModuleTitle);

            return Ok(module);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Module with ID {ModuleId} not found for update", id);
            return NotFound($"Module with ID {id} was not found.");
        }
    }

    #endregion

    #region DELETE Endpoints

    /// <summary>
    /// Permanently deletes a module instance from the database.
    /// </summary>
    /// <param name="id">The unique identifier of the module to delete.</param>
    /// <param name="cancellationToken">Token to cancel the asynchronous operation.</param>
    /// <returns>
    /// HTTP 204 No Content on successful deletion.
    /// </returns>
    /// <remarks>
    /// <para>
    /// MIGRATION: Replaces the cmdDelete_Click handler from ModuleSettings.ascx.vb
    /// at lines 300-312. The original VB.NET implementation called:
    /// </para>
    /// <code>
    /// Dim objModules As New ModuleController
    /// objModules.DeleteTabModule(TabId, ModuleId)
    /// </code>
    /// <para>
    /// Note: This performs a hard delete. For soft delete (moving to recycle bin),
    /// use UpdateModule to set IsDeleted = true instead.
    /// </para>
    /// <para>
    /// This endpoint requires the "Administrators" role per Section 0.7.7 security rules.
    /// </para>
    /// </remarks>
    /// <response code="204">Module was successfully deleted.</response>
    /// <response code="401">If the user is not authenticated.</response>
    /// <response code="403">If the user does not have the Administrators role.</response>
    /// <response code="404">If the module with the specified ID was not found.</response>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Administrators")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteModule(
        int id,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Deleting module {ModuleId}", id);

        try
        {
            await _moduleService.DeleteModuleAsync(id, cancellationToken);

            _logger.LogInformation("Deleted module {ModuleId}", id);

            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Module with ID {ModuleId} not found for deletion", id);
            return NotFound($"Module with ID {id} was not found.");
        }
    }

    #endregion
}
