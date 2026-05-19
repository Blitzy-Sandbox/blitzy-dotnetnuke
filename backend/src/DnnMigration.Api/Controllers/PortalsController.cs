// -----------------------------------------------------------------------
// <copyright file="PortalsController.cs" company="DNN Migration Project">
// MIT License - DotNetNuke Migration to .NET 8
// </copyright>
// <summary>
// ASP.NET Core 8 REST API controller implementing portal (multi-tenant site) management endpoints.
// MIGRATION: Replaces legacy SiteSettings.ascx.vb, Signup.ascx.vb, and Portals.ascx.vb WebForms
// code-behind with RESTful API following BFF pattern for Angular frontend consumption.
// </summary>
// -----------------------------------------------------------------------

using DnnMigration.Application.DTOs.Common;
using DnnMigration.Application.DTOs.Portal;
using DnnMigration.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace DnnMigration.Api.Controllers;

/// <summary>
/// REST API controller for portal (multi-tenant site) management operations.
/// Provides endpoints for listing, retrieving, creating, updating, and deleting portals.
/// </summary>
/// <remarks>
/// <para>
/// MIGRATION: This controller replaces the following legacy DotNetNuke 4.x WebForms code-behind files:
/// <list type="bullet">
/// <item><description><c>Website/admin/Portal/SiteSettings.ascx.vb</c> - Portal configuration and update operations</description></item>
/// <item><description><c>Website/admin/Portal/Signup.ascx.vb</c> - Portal creation workflow (cmdUpdate_Click handler)</description></item>
/// <item><description><c>Website/admin/Portal/Portals.ascx.vb</c> - Portal listing with pagination</description></item>
/// </list>
/// </para>
/// <para>
/// API Endpoints:
/// <list type="table">
/// <listheader>
/// <term>HTTP Method</term>
/// <description>Endpoint</description>
/// </listheader>
/// <item><term>GET</term><description>/api/portals - List portals with pagination and optional name filter</description></item>
/// <item><term>GET</term><description>/api/portals/{id} - Get single portal by ID</description></item>
/// <item><term>POST</term><description>/api/portals - Create new portal (Administrators only)</description></item>
/// <item><term>PUT</term><description>/api/portals/{id} - Update existing portal (Administrators only)</description></item>
/// <item><term>DELETE</term><description>/api/portals/{id} - Delete portal (Administrators only)</description></item>
/// </list>
/// </para>
/// </remarks>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class PortalsController : ControllerBase
{
    private readonly IPortalService _portalService;
    private readonly ILogger<PortalsController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PortalsController"/> class.
    /// </summary>
    /// <param name="portalService">
    /// The portal service for business logic operations.
    /// MIGRATION: Replaces direct PortalController.vb calls from legacy SiteSettings.ascx.vb code.
    /// </param>
    /// <param name="logger">The logger instance for structured logging.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="portalService"/> or <paramref name="logger"/> is <c>null</c>.
    /// </exception>
    public PortalsController(IPortalService portalService, ILogger<PortalsController> logger)
    {
        _portalService = portalService ?? throw new ArgumentNullException(nameof(portalService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Retrieves a paginated list of portals with optional name filtering.
    /// </summary>
    /// <param name="pageIndex">
    /// The zero-based index of the page to retrieve. Default is 0 (first page).
    /// </param>
    /// <param name="pageSize">
    /// The maximum number of portals per page. Default is 10. Maximum allowed is 100.
    /// </param>
    /// <param name="nameFilter">
    /// Optional filter string to match against portal names. When provided, only portals
    /// containing this string in their name (case-insensitive) are returned.
    /// </param>
    /// <param name="cancellationToken">
    /// A cancellation token for cooperative cancellation of the asynchronous operation.
    /// </param>
    /// <returns>
    /// An <see cref="ActionResult{T}"/> containing a <see cref="PagedResult{T}"/> of 
    /// <see cref="PortalDto"/> objects representing the requested page of portals.
    /// </returns>
    /// <remarks>
    /// <para>
    /// MIGRATION: Derived from Portals.ascx.vb BindData method which calls
    /// GetPortalsByName for filtered results and GetPortals for unfiltered results.
    /// </para>
    /// <para>
    /// Example requests:
    /// <list type="bullet">
    /// <item><description>GET /api/portals - Returns first 10 portals</description></item>
    /// <item><description>GET /api/portals?pageIndex=1&amp;pageSize=20 - Returns second page of 20 portals</description></item>
    /// <item><description>GET /api/portals?nameFilter=demo - Returns portals with "demo" in their name</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <response code="200">Returns the paginated list of portals.</response>
    /// <response code="400">If the pagination parameters are invalid.</response>
    /// <response code="401">If the user is not authenticated.</response>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<PortalDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PagedResult<PortalDto>>> GetPortals(
        [FromQuery] int pageIndex = 0,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? nameFilter = null,
        CancellationToken cancellationToken = default)
    {
        // Validate pagination parameters
        if (pageIndex < 0)
        {
            _logger.LogWarning("Invalid pageIndex {PageIndex} provided for GetPortals request", pageIndex);
            return BadRequest(new { error = "PageIndex must be a non-negative integer." });
        }

        if (pageSize < 1 || pageSize > 100)
        {
            _logger.LogWarning("Invalid pageSize {PageSize} provided for GetPortals request", pageSize);
            return BadRequest(new { error = "PageSize must be between 1 and 100." });
        }

        _logger.LogInformation(
            "Retrieving portals: pageIndex={PageIndex}, pageSize={PageSize}, nameFilter={NameFilter}",
            pageIndex,
            pageSize,
            nameFilter ?? "(none)");

        PagedResult<PortalDto> result;

        // MIGRATION: If nameFilter is provided, use GetPortalsByNameAsync (derived from Portals.ascx.vb filter logic)
        // Otherwise, use GetPortalsAsync for all portals
        if (!string.IsNullOrWhiteSpace(nameFilter))
        {
            // Apply SQL LIKE pattern matching with wildcard prefix and suffix
            var searchPattern = $"%{nameFilter}%";
            result = await _portalService.GetPortalsByNameAsync(searchPattern, pageIndex, pageSize, cancellationToken);
            
            _logger.LogInformation(
                "Retrieved {Count} portals matching filter '{Filter}' (Page {PageIndex} of {TotalPages})",
                result.Items.Count,
                nameFilter,
                pageIndex,
                result.TotalPages);
        }
        else
        {
            result = await _portalService.GetPortalsAsync(pageIndex, pageSize, cancellationToken);
            
            _logger.LogInformation(
                "Retrieved {Count} portals (Page {PageIndex} of {TotalPages})",
                result.Items.Count,
                pageIndex,
                result.TotalPages);
        }

        return Ok(result);
    }

    /// <summary>
    /// Retrieves a single portal by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the portal to retrieve.</param>
    /// <param name="cancellationToken">
    /// A cancellation token for cooperative cancellation of the asynchronous operation.
    /// </param>
    /// <returns>
    /// An <see cref="ActionResult{T}"/> containing the <see cref="PortalDto"/> if found;
    /// otherwise, a 404 Not Found response.
    /// </returns>
    /// <remarks>
    /// <para>
    /// MIGRATION: Derived from SiteSettings.ascx.vb Page_Load which calls
    /// objPortalController.GetPortal(intPortalId) to load portal data for display.
    /// </para>
    /// <para>
    /// Example request: GET /api/portals/1
    /// </para>
    /// </remarks>
    /// <response code="200">Returns the portal with the specified ID.</response>
    /// <response code="401">If the user is not authenticated.</response>
    /// <response code="404">If no portal exists with the specified ID.</response>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(PortalDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PortalDto>> GetPortal(
        int id,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving portal with ID {PortalId}", id);

        var portal = await _portalService.GetPortalAsync(id, cancellationToken);

        if (portal is null)
        {
            _logger.LogWarning("Portal with ID {PortalId} was not found", id);
            return NotFound(new { error = $"Portal with ID {id} was not found." });
        }

        _logger.LogInformation("Successfully retrieved portal '{PortalName}' (ID: {PortalId})", portal.PortalName, id);
        return Ok(portal);
    }

    /// <summary>
    /// Creates a new portal with the specified configuration.
    /// </summary>
    /// <param name="request">
    /// A <see cref="CreatePortalRequest"/> containing the portal creation parameters including
    /// portal alias, title, administrator credentials, template selection, and other settings.
    /// </param>
    /// <param name="cancellationToken">
    /// A cancellation token for cooperative cancellation of the asynchronous operation.
    /// </param>
    /// <returns>
    /// An <see cref="ActionResult{T}"/> containing the newly created <see cref="PortalDto"/>
    /// with a 201 Created status code and a Location header pointing to the new resource.
    /// </returns>
    /// <remarks>
    /// <para>
    /// MIGRATION: Derived from Signup.ascx.vb cmdUpdate_Click handler (lines 153-329) which:
    /// <list type="number">
    /// <item><description>Validates the template file</description></item>
    /// <item><description>Validates the portal alias (name)</description></item>
    /// <item><description>Validates administrator password confirmation</description></item>
    /// <item><description>Calls PortalController.CreatePortal with all parameters</description></item>
    /// <item><description>Creates administrator user and sends notification email</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// This endpoint requires the "Administrators" role for authorization.
    /// </para>
    /// </remarks>
    /// <response code="201">Returns the newly created portal with Location header.</response>
    /// <response code="400">If the request data is invalid or fails validation.</response>
    /// <response code="401">If the user is not authenticated.</response>
    /// <response code="403">If the user does not have the Administrators role.</response>
    [HttpPost]
    [Authorize(Roles = "Administrators")]
    [ProducesResponseType(typeof(PortalDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PortalDto>> CreatePortal(
        [FromBody] CreatePortalRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            _logger.LogWarning("CreatePortal called with null request body");
            return BadRequest(new { error = "Request body is required." });
        }

        _logger.LogInformation(
            "Creating new portal with alias '{PortalAlias}' and title '{Title}'",
            request.PortalAlias,
            request.Title);

        try
        {
            // MIGRATION: Call service to create portal (encapsulates Signup.ascx.vb cmdUpdate_Click logic)
            // This includes: creating portal record, administrator user, default roles, template processing
            var createdPortal = await _portalService.CreatePortalAsync(request, cancellationToken);

            _logger.LogInformation(
                "Successfully created portal '{PortalName}' with ID {PortalId}",
                createdPortal.PortalName,
                createdPortal.PortalId);

            // Return 201 Created with Location header pointing to the new portal resource
            return CreatedAtAction(
                nameof(GetPortal),
                new { id = createdPortal.PortalId },
                createdPortal);
        }
        catch (InvalidOperationException ex)
        {
            // Business rule violation (e.g., duplicate alias, invalid template)
            _logger.LogWarning(
                ex,
                "Portal creation failed for alias '{PortalAlias}': {Message}",
                request.PortalAlias,
                ex.Message);
            
            return BadRequest(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            // Validation error in request data
            _logger.LogWarning(
                ex,
                "Invalid portal creation request for alias '{PortalAlias}': {Message}",
                request.PortalAlias,
                ex.Message);
            
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Updates an existing portal with the specified configuration changes.
    /// </summary>
    /// <param name="id">The unique identifier of the portal to update.</param>
    /// <param name="request">
    /// An <see cref="UpdatePortalRequest"/> containing the updated portal settings including
    /// branding, quotas, navigation tabs, and localization configuration.
    /// </param>
    /// <param name="cancellationToken">
    /// A cancellation token for cooperative cancellation of the asynchronous operation.
    /// </param>
    /// <returns>
    /// An <see cref="ActionResult{T}"/> containing the updated <see cref="PortalDto"/>
    /// if successful; otherwise, a 404 Not Found response.
    /// </returns>
    /// <remarks>
    /// <para>
    /// MIGRATION: Derived from SiteSettings.ascx.vb cmdUpdate_Click handler (lines 687-810) which:
    /// <list type="number">
    /// <item><description>Validates page form data</description></item>
    /// <item><description>Parses quota and date values from form fields</description></item>
    /// <item><description>Calls UpdatePortalInfo with 27 parameters</description></item>
    /// <item><description>Updates skin settings</description></item>
    /// <item><description>Updates portal site settings</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// This endpoint requires the "Administrators" role for authorization.
    /// </para>
    /// </remarks>
    /// <response code="200">Returns the updated portal.</response>
    /// <response code="400">If the request data is invalid.</response>
    /// <response code="401">If the user is not authenticated.</response>
    /// <response code="403">If the user does not have the Administrators role.</response>
    /// <response code="404">If no portal exists with the specified ID.</response>
    [HttpPut("{id:int}")]
    [Authorize(Roles = "Administrators")]
    [ProducesResponseType(typeof(PortalDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PortalDto>> UpdatePortal(
        int id,
        [FromBody] UpdatePortalRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            _logger.LogWarning("UpdatePortal called with null request body for portal ID {PortalId}", id);
            return BadRequest(new { error = "Request body is required." });
        }

        _logger.LogInformation(
            "Updating portal with ID {PortalId} to name '{PortalName}'",
            id,
            request.PortalName);

        try
        {
            // MIGRATION: Call service to update portal (encapsulates SiteSettings.ascx.vb cmdUpdate_Click logic)
            // This includes: updating portal record, clearing cache, updating site settings
            var updatedPortal = await _portalService.UpdatePortalAsync(id, request, cancellationToken);

            _logger.LogInformation(
                "Successfully updated portal '{PortalName}' (ID: {PortalId})",
                updatedPortal.PortalName,
                id);

            return Ok(updatedPortal);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            // Portal not found
            _logger.LogWarning("Portal with ID {PortalId} was not found for update", id);
            return NotFound(new { error = $"Portal with ID {id} was not found." });
        }
        catch (InvalidOperationException ex)
        {
            // Other business rule violation
            _logger.LogWarning(
                ex,
                "Portal update failed for ID {PortalId}: {Message}",
                id,
                ex.Message);
            
            return BadRequest(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            // Validation error in request data
            _logger.LogWarning(
                ex,
                "Invalid portal update request for ID {PortalId}: {Message}",
                id,
                ex.Message);
            
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Deletes a portal and all associated data.
    /// </summary>
    /// <param name="id">The unique identifier of the portal to delete.</param>
    /// <param name="cancellationToken">
    /// A cancellation token for cooperative cancellation of the asynchronous operation.
    /// </param>
    /// <returns>
    /// A 204 No Content response if deletion succeeds; otherwise, a 404 Not Found response
    /// or a 400 Bad Request if deletion is not allowed.
    /// </returns>
    /// <remarks>
    /// <para>
    /// MIGRATION: Derived from SiteSettings.ascx.vb cmdDelete_Click handler (lines 556-586) which:
    /// <list type="number">
    /// <item><description>Gets the portal info by ID</description></item>
    /// <item><description>Calls PortalController.DeletePortal with the portal and server path</description></item>
    /// <item><description>Logs the deletion event</description></item>
    /// <item><description>Redirects to host URL if deleting current portal</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// Deletion includes: removing custom resource files, deleting portal directories,
    /// removing database records, and clearing portal alias cache.
    /// </para>
    /// <para>
    /// This endpoint requires the "Administrators" role for authorization.
    /// Cannot delete the last remaining portal.
    /// </para>
    /// </remarks>
    /// <response code="204">Portal was successfully deleted.</response>
    /// <response code="400">If the portal cannot be deleted (e.g., last remaining portal).</response>
    /// <response code="401">If the user is not authenticated.</response>
    /// <response code="403">If the user does not have the Administrators role.</response>
    /// <response code="404">If no portal exists with the specified ID.</response>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Administrators")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeletePortal(
        int id,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Attempting to delete portal with ID {PortalId}", id);

        try
        {
            // MIGRATION: Call service to delete portal (encapsulates SiteSettings.ascx.vb cmdDelete_Click logic)
            // This includes: validating portal exists, checking it's not the last portal,
            // deleting files/directories, and removing database records
            await _portalService.DeletePortalAsync(id, cancellationToken);

            _logger.LogInformation("Successfully deleted portal with ID {PortalId}", id);

            return NoContent();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            // Portal not found
            _logger.LogWarning("Portal with ID {PortalId} was not found for deletion", id);
            return NotFound(new { error = $"Portal with ID {id} was not found." });
        }
        catch (InvalidOperationException ex)
        {
            // Business rule violation (e.g., cannot delete last portal)
            _logger.LogWarning(
                ex,
                "Portal deletion failed for ID {PortalId}: {Message}",
                id,
                ex.Message);
            
            return BadRequest(new { error = ex.Message });
        }
    }
}
