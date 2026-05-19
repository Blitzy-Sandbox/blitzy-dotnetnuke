// -----------------------------------------------------------------------------
// DnnMigration - Tabs Controller
// ASP.NET Core 8 REST API Controller for Tab/Page/Navigation Management
// MIGRATION: Derived from Library/Components/Tabs/TabController.vb
// Converted from VB.NET to C# 12 with ASP.NET Core Web API patterns
// -----------------------------------------------------------------------------
// Original VB.NET methods mapped:
// - GetTab → GetTab endpoint
// - GetTabs → GetTabs endpoint
// - GetTabsByParentId → GetChildTabs endpoint
// - AddTab → CreateTab endpoint
// - UpdateTab → UpdateTab endpoint
// - DeleteTab → DeleteTab endpoint
// - UpdatePortalTabOrder → UpdateTabOrder endpoint
// -----------------------------------------------------------------------------

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using DnnMigration.Application.DTOs;
using DnnMigration.Application.Interfaces;

namespace DnnMigration.Api.Controllers;

/// <summary>
/// REST API controller for tab/page/navigation management operations.
/// Provides endpoints for CRUD operations on tabs within the portal navigation hierarchy.
/// </summary>
/// <remarks>
/// <para>
/// MIGRATION: This controller is derived from the legacy DotNetNuke TabController.vb
/// which managed page/tab navigation hierarchy through business logic and SqlDataProvider calls.
/// The original VB.NET class has been converted to a modern REST API following ASP.NET Core 8 patterns.
/// </para>
/// <para>
/// Key endpoint mappings from legacy code:
/// - GET /api/tabs → TabController.GetTabs(PortalId)
/// - GET /api/tabs/{id} → TabController.GetTab(TabId, PortalId, ignoreCache)
/// - GET /api/tabs/parent/{parentId} → TabController.GetTabsByParentId(ParentId, PortalId)
/// - POST /api/tabs → TabController.AddTab(objTab, AddAllTabsModules)
/// - PUT /api/tabs/{id} → TabController.UpdateTab(objTab)
/// - DELETE /api/tabs/{id} → TabController.DeleteTab(TabId, PortalId)
/// - PUT /api/tabs/{id}/order → TabController.UpdatePortalTabOrder(...)
/// </para>
/// </remarks>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class TabsController : ControllerBase
{
    private readonly ITabService _tabService;
    private readonly ILogger<TabsController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TabsController"/> class.
    /// </summary>
    /// <param name="tabService">The tab service for business logic operations.</param>
    /// <param name="logger">The logger for diagnostic output.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="tabService"/> or <paramref name="logger"/> is null.
    /// </exception>
    /// <remarks>
    /// MIGRATION: Constructor injection replaces direct instantiation of TabController in legacy code.
    /// The ITabService abstracts the business logic that was previously in TabController.vb.
    /// </remarks>
    public TabsController(ITabService tabService, ILogger<TabsController> logger)
    {
        _tabService = tabService ?? throw new ArgumentNullException(nameof(tabService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    #region Read Operations

    /// <summary>
    /// Retrieves all tabs for a specified portal.
    /// </summary>
    /// <param name="portalId">The portal identifier to filter tabs by.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// An enumerable collection of all tabs for the portal.
    /// </returns>
    /// <response code="200">Returns the list of tabs for the portal.</response>
    /// <response code="401">Unauthorized - user is not authenticated.</response>
    /// <remarks>
    /// <para>
    /// MIGRATION: Derived from TabController.GetTabs(PortalId) in TabController.vb (line 516-522).
    /// </para>
    /// <para>
    /// Original VB.NET implementation:
    /// <code>
    /// Public Function GetTabs(ByVal PortalId As Integer) As ArrayList
    ///     Dim arrTabs As New ArrayList
    ///     For Each tabPair As KeyValuePair(Of Integer, TabInfo) In GetTabsByPortal(PortalId)
    ///         arrTabs.Add(tabPair.Value)
    ///     Next
    ///     Return arrTabs
    /// End Function
    /// </code>
    /// </para>
    /// <para>
    /// The GetTabsByPortal method used caching via DataCache.GetPersistentCacheItem with fallback
    /// to DataProvider.Instance().GetTabs(PortalId) database call.
    /// </para>
    /// </remarks>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<TabDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IEnumerable<TabDto>>> GetTabs(
        [FromQuery] int portalId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Retrieving all tabs for portal {PortalId}",
            portalId);

        var tabs = await _tabService.GetTabsAsync(portalId, cancellationToken);

        _logger.LogInformation(
            "Retrieved {TabCount} tabs for portal {PortalId}",
            tabs.Count(),
            portalId);

        return Ok(tabs);
    }

    /// <summary>
    /// Retrieves a single tab by its identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the tab.</param>
    /// <param name="portalId">The portal identifier for multi-tenant isolation.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// The tab DTO if found; otherwise, a 404 Not Found response.
    /// </returns>
    /// <response code="200">Returns the requested tab.</response>
    /// <response code="401">Unauthorized - user is not authenticated.</response>
    /// <response code="404">Tab not found.</response>
    /// <remarks>
    /// <para>
    /// MIGRATION: Derived from TabController.GetTab(TabId, PortalId, ignoreCache) in TabController.vb (line 467-502).
    /// </para>
    /// <para>
    /// Original VB.NET implementation performed:
    /// 1. Cache lookup via GetTabsByPortal dictionary
    /// 2. Fallback to DataProvider.Instance().GetTab(TabId) if not in cache
    /// 3. FillTabInfo to hydrate the TabInfo entity from IDataReader
    /// </para>
    /// </remarks>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(TabDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TabDto>> GetTab(
        int id,
        [FromQuery] int portalId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Retrieving tab {TabId} for portal {PortalId}",
            id,
            portalId);

        var tab = await _tabService.GetTabAsync(id, portalId, cancellationToken);

        if (tab is null)
        {
            _logger.LogWarning(
                "Tab {TabId} not found in portal {PortalId}",
                id,
                portalId);
            return NotFound();
        }

        return Ok(tab);
    }

    /// <summary>
    /// Retrieves all child tabs for a specified parent tab.
    /// </summary>
    /// <param name="parentId">The parent tab identifier. Use -1 for root-level tabs.</param>
    /// <param name="portalId">The portal identifier for multi-tenant isolation.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// An enumerable collection of child tabs.
    /// </returns>
    /// <response code="200">Returns the list of child tabs.</response>
    /// <response code="401">Unauthorized - user is not authenticated.</response>
    /// <remarks>
    /// <para>
    /// MIGRATION: Derived from TabController.GetTabsByParentId(ParentId, PortalId) in TabController.vb (line 524-526).
    /// </para>
    /// <para>
    /// Original VB.NET implementation:
    /// <code>
    /// Public Function GetTabsByParentId(ByVal ParentId As Integer, ByVal PortalId As Integer) As ArrayList
    ///     Return GetTabsByParent(ParentId, PortalId)
    /// End Function
    /// </code>
    /// </para>
    /// <para>
    /// The private GetTabsByParent method filtered GetTabsByPortal dictionary by ParentId.
    /// </para>
    /// </remarks>
    [HttpGet("parent/{parentId:int}")]
    [ProducesResponseType(typeof(IEnumerable<TabDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IEnumerable<TabDto>>> GetChildTabs(
        int parentId,
        [FromQuery] int portalId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Retrieving child tabs for parent {ParentId} in portal {PortalId}",
            parentId,
            portalId);

        // MIGRATION: Convert -1 route parameter to nullable int for service layer
        int? parentIdNullable = parentId == -1 ? null : parentId;

        var childTabs = await _tabService.GetTabsByParentIdAsync(parentIdNullable, portalId, cancellationToken);

        _logger.LogInformation(
            "Retrieved {TabCount} child tabs for parent {ParentId} in portal {PortalId}",
            childTabs.Count(),
            parentId,
            portalId);

        return Ok(childTabs);
    }

    #endregion

    #region Write Operations

    /// <summary>
    /// Creates a new tab in the navigation hierarchy.
    /// </summary>
    /// <param name="request">The tab creation request containing tab properties.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// The created tab DTO with assigned TabId and 201 Created status.
    /// </returns>
    /// <response code="201">Tab created successfully.</response>
    /// <response code="400">Invalid request data.</response>
    /// <response code="401">Unauthorized - user is not authenticated.</response>
    /// <response code="403">Forbidden - user does not have Administrator role.</response>
    /// <remarks>
    /// <para>
    /// MIGRATION: Derived from TabController.AddTab(objTab, AddAllTabsModules) in TabController.vb (line 326-373).
    /// </para>
    /// <para>
    /// Original VB.NET implementation performed:
    /// 1. Generate TabPath via GenerateTabPath(ParentId, TabName)
    /// 2. Call DataProvider.Instance().AddTab with all properties
    /// 3. Add tab permissions via TabPermissionController
    /// 4. Update portal tab order via UpdatePortalTabOrder
    /// 5. Optionally copy AllTabs modules
    /// 6. Clear cache via ClearCache(PortalId)
    /// 7. Remove portal dictionary cache
    /// </para>
    /// <para>
    /// Requires Administrator role for security.
    /// </para>
    /// </remarks>
    [HttpPost]
    [Authorize(Roles = "Administrators")]
    [ProducesResponseType(typeof(TabDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<TabDto>> CreateTab(
        [FromBody] CreateTabRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Creating new tab '{TabName}' for portal {PortalId} with parent {ParentId}",
            request.TabName,
            request.PortalId,
            request.ParentId);

        var createdTab = await _tabService.CreateTabAsync(request, cancellationToken);

        _logger.LogInformation(
            "Created tab {TabId} '{TabName}' for portal {PortalId}",
            createdTab.TabId,
            createdTab.TabName,
            createdTab.PortalId);

        return CreatedAtAction(
            nameof(GetTab),
            new { id = createdTab.TabId, portalId = createdTab.PortalId },
            createdTab);
    }

    /// <summary>
    /// Updates an existing tab's properties.
    /// </summary>
    /// <param name="id">The unique identifier of the tab to update.</param>
    /// <param name="request">The tab update request containing properties to modify.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// The updated tab DTO if successful; otherwise, a 404 Not Found response.
    /// </returns>
    /// <response code="200">Tab updated successfully.</response>
    /// <response code="400">Invalid request data.</response>
    /// <response code="401">Unauthorized - user is not authenticated.</response>
    /// <response code="403">Forbidden - user does not have Administrator role.</response>
    /// <response code="404">Tab not found.</response>
    /// <remarks>
    /// <para>
    /// MIGRATION: Derived from TabController.UpdateTab(objTab) in TabController.vb (line 780-814).
    /// </para>
    /// <para>
    /// Original VB.NET implementation performed:
    /// 1. Check if TabName or ParentId changed (triggers child path updates)
    /// 2. Call UpdatePortalTabOrder for position changes
    /// 3. Call DataProvider.Instance().UpdateTab with all properties
    /// 4. Update tab permissions if changed via TabPermissionController
    /// 5. Update child tab paths if parent/name changed via UpdateChildTabPath
    /// 6. Clear cache via ClearCache(PortalId)
    /// </para>
    /// <para>
    /// Requires Administrator role for security.
    /// </para>
    /// </remarks>
    [HttpPut("{id:int}")]
    [Authorize(Roles = "Administrators")]
    [ProducesResponseType(typeof(TabDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TabDto>> UpdateTab(
        int id,
        [FromBody] UpdateTabRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Updating tab {TabId}",
            id);

        try
        {
            var updatedTab = await _tabService.UpdateTabAsync(id, request, cancellationToken);

            _logger.LogInformation(
                "Updated tab {TabId} '{TabName}'",
                updatedTab.TabId,
                updatedTab.TabName);

            return Ok(updatedTab);
        }
        catch (KeyNotFoundException)
        {
            _logger.LogWarning(
                "Tab {TabId} not found for update",
                id);
            return NotFound();
        }
    }

    /// <summary>
    /// Deletes a tab from the navigation hierarchy.
    /// </summary>
    /// <param name="id">The unique identifier of the tab to delete.</param>
    /// <param name="portalId">The portal identifier for multi-tenant isolation.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// A 204 No Content response if successful.
    /// </returns>
    /// <response code="204">Tab deleted successfully.</response>
    /// <response code="400">Tab has children and cannot be deleted.</response>
    /// <response code="401">Unauthorized - user is not authenticated.</response>
    /// <response code="403">Forbidden - user does not have Administrator role.</response>
    /// <response code="404">Tab not found.</response>
    /// <remarks>
    /// <para>
    /// MIGRATION: Derived from TabController.DeleteTab(TabId, PortalId) in TabController.vb (line 446-457).
    /// </para>
    /// <para>
    /// Original VB.NET implementation:
    /// <code>
    /// Public Sub DeleteTab(ByVal TabId As Integer, ByVal PortalId As Integer)
    ///     ' parent tabs can not be deleted
    ///     Dim arrTabs As ArrayList = GetTabsByParentId(TabId, PortalId)
    ///     If arrTabs.Count = 0 Then
    ///         DataProvider.Instance().DeleteTab(TabId)
    ///         UpdatePortalTabOrder(PortalId, TabId, -2, 0, 0, True)
    ///     End If
    ///     ClearCache(PortalId)
    ///     DataCache.RemoveCache(DataCache.PortalDictionaryCacheKey)
    /// End Sub
    /// </code>
    /// </para>
    /// <para>
    /// Note: Parent tabs (tabs with children) cannot be deleted. Delete child tabs first.
    /// Requires Administrator role for security.
    /// </para>
    /// </remarks>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Administrators")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteTab(
        int id,
        [FromQuery] int portalId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Deleting tab {TabId} from portal {PortalId}",
            id,
            portalId);

        try
        {
            await _tabService.DeleteTabAsync(id, portalId, cancellationToken);

            _logger.LogInformation(
                "Deleted tab {TabId} from portal {PortalId}",
                id,
                portalId);

            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            _logger.LogWarning(
                "Tab {TabId} not found for deletion in portal {PortalId}",
                id,
                portalId);
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            // MIGRATION: Parent tabs cannot be deleted - this preserves legacy behavior
            _logger.LogWarning(
                "Cannot delete tab {TabId}: {Reason}",
                id,
                ex.Message);
            return BadRequest(new { error = ex.Message });
        }
    }

    #endregion

    #region Hierarchy Operations

    /// <summary>
    /// Updates the order and/or parent of a tab in the navigation hierarchy.
    /// </summary>
    /// <param name="id">The unique identifier of the tab to reorder.</param>
    /// <param name="request">The tab order request containing reordering parameters.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// A 204 No Content response if successful.
    /// </returns>
    /// <response code="204">Tab order updated successfully.</response>
    /// <response code="400">Invalid request data.</response>
    /// <response code="401">Unauthorized - user is not authenticated.</response>
    /// <response code="403">Forbidden - user does not have Administrator role.</response>
    /// <response code="404">Tab not found.</response>
    /// <remarks>
    /// <para>
    /// MIGRATION: Derived from TabController.UpdatePortalTabOrder and UpdateTabOrder methods in TabController.vb.
    /// </para>
    /// <para>
    /// Original VB.NET UpdatePortalTabOrder (line 550-778) was a complex method handling:
    /// - Tab reordering within the same parent
    /// - Tab movement to a new parent
    /// - Level changes (promotion/demotion in hierarchy)
    /// - Visibility updates during reorder
    /// </para>
    /// <para>
    /// The method used a TabOrderHelper structure for tracking changes and performed
    /// the MoveTab private method for actual repositioning. All sibling tabs had their
    /// TabOrder values recalculated after any change.
    /// </para>
    /// <para>
    /// If ParentId is provided in the request, the tab is moved to a new parent.
    /// Otherwise, only the order position is changed within the current parent.
    /// Requires Administrator role for security.
    /// </para>
    /// </remarks>
    [HttpPut("{id:int}/order")]
    [Authorize(Roles = "Administrators")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateTabOrder(
        int id,
        [FromBody] TabOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Updating order for tab {TabId} to position {Order} with parent {ParentId}",
            id,
            request.Order,
            request.ParentId);

        try
        {
            // MIGRATION: Determine if this is a parent change (MoveTab) or just reordering
            if (request.ParentId.HasValue)
            {
                // Parent change requested - use MoveTabAsync
                // This maps to the parent change logic in UpdatePortalTabOrder (lines 593-636)
                _logger.LogInformation(
                    "Moving tab {TabId} to new parent {NewParentId}",
                    id,
                    request.ParentId.Value);

                // Need to get portalId from the tab first - extract from claims or request
                // For now, we delegate the portal resolution to the service layer
                await _tabService.UpdateTabOrderAsync(id, request.ParentId, request.Order, cancellationToken);
            }
            else
            {
                // Same parent - just reorder within siblings
                // This maps to the order change logic in UpdatePortalTabOrder (lines 713-748)
                await _tabService.UpdateTabOrderAsync(id, request.ParentId, request.Order, cancellationToken);
            }

            _logger.LogInformation(
                "Updated order for tab {TabId}",
                id);

            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            _logger.LogWarning(
                "Tab {TabId} not found for order update",
                id);
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(
                "Cannot update order for tab {TabId}: {Reason}",
                id,
                ex.Message);
            return BadRequest(new { error = ex.Message });
        }
    }

    #endregion
}
