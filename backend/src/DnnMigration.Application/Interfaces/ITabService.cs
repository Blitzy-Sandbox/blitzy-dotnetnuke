// -----------------------------------------------------------------------------
// DnnMigration - Tab Service Interface
// MIGRATION: Interface derived from Library/Components/Tabs/TabController.vb
// Converted from VB.NET to C# 12 with async/await patterns and nullable types
// -----------------------------------------------------------------------------

using DnnMigration.Application.DTOs;

namespace DnnMigration.Application.Interfaces;

/// <summary>
/// Defines the contract for tab/page navigation management operations.
/// Provides async methods for tab CRUD operations and hierarchy management.
/// </summary>
/// <remarks>
/// MIGRATION: This interface is derived from DotNetNuke.Entities.Tabs.TabController (TabController.vb).
/// All methods are async and return DTOs rather than domain entities per Section 0.3.3 Service Layer Pattern.
/// Methods include CancellationToken parameters per Section 0.7.2 async/await patterns.
/// 
/// Original VB.NET methods mapped:
/// - GetTab → GetTabAsync
/// - GetTabs → GetTabsAsync
/// - GetTabsByParentId → GetTabsByParentIdAsync
/// - GetTabByName → GetTabByNameAsync
/// - GetTabCount → GetTabCountAsync
/// - AddTab → CreateTabAsync
/// - UpdateTab → UpdateTabAsync
/// - DeleteTab → DeleteTabAsync
/// - UpdateTabOrder → UpdateTabOrderAsync
/// - UpdatePortalTabOrder (move logic) → MoveTabAsync
/// </remarks>
public interface ITabService
{
    #region Read Operations

    /// <summary>
    /// Retrieves a tab by its identifier and portal identifier.
    /// </summary>
    /// <param name="tabId">The unique identifier of the tab.</param>
    /// <param name="portalId">The portal identifier for multi-tenant isolation.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// A task containing the tab DTO if found; otherwise, null.
    /// </returns>
    /// <remarks>
    /// MIGRATION: Derived from TabController.GetTab(TabId, PortalId, ignoreCache) in TabController.vb.
    /// Original method used cache lookup with fallback to database query via DataProvider.Instance().GetTab(TabId).
    /// The ignoreCache parameter is not exposed in service layer; caching is handled by infrastructure.
    /// </remarks>
    Task<TabDto?> GetTabAsync(int tabId, int portalId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all tabs for a specified portal.
    /// </summary>
    /// <param name="portalId">The portal identifier to filter tabs by.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// A task containing an enumerable collection of all tab DTOs for the portal.
    /// </returns>
    /// <remarks>
    /// MIGRATION: Derived from TabController.GetTabs(PortalId) in TabController.vb.
    /// Original method iterated GetTabsByPortal dictionary and returned ArrayList.
    /// Returns IEnumerable for modern collection semantics and LINQ compatibility.
    /// </remarks>
    Task<IEnumerable<TabDto>> GetTabsAsync(int portalId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all child tabs for a specified parent tab within a portal.
    /// </summary>
    /// <param name="parentId">The parent tab identifier. Use null or -1 for root-level tabs.</param>
    /// <param name="portalId">The portal identifier for multi-tenant isolation.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// A task containing an enumerable collection of child tab DTOs.
    /// </returns>
    /// <remarks>
    /// MIGRATION: Derived from TabController.GetTabsByParentId(ParentId, PortalId) in TabController.vb.
    /// Original method called private GetTabsByParent which filtered GetTabsByPortal by ParentId.
    /// Essential for building hierarchical navigation menus.
    /// </remarks>
    Task<IEnumerable<TabDto>> GetTabsByParentIdAsync(int? parentId, int portalId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a tab by its name within a specified portal.
    /// </summary>
    /// <param name="tabName">The display name of the tab to find.</param>
    /// <param name="portalId">The portal identifier for multi-tenant isolation.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// A task containing the tab DTO if found; otherwise, null.
    /// </returns>
    /// <remarks>
    /// MIGRATION: Derived from TabController.GetTabByName(TabName, PortalId) in TabController.vb.
    /// Original method called GetTabByNameAndParent with Integer.MinValue for ParentId.
    /// If multiple tabs share the same name, returns the first match.
    /// </remarks>
    Task<TabDto?> GetTabByNameAsync(string tabName, int portalId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the total count of tabs for a specified portal.
    /// </summary>
    /// <param name="portalId">The portal identifier to count tabs for.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// A task containing the total number of tabs in the portal.
    /// </returns>
    /// <remarks>
    /// MIGRATION: Derived from TabController.GetTabCount(portalId) in TabController.vb.
    /// Original method called DataProvider.Instance().GetTabCount(portalId).
    /// Useful for quota validation and dashboard statistics.
    /// </remarks>
    Task<int> GetTabCountAsync(int portalId, CancellationToken cancellationToken = default);

    #endregion

    #region Write Operations

    /// <summary>
    /// Creates a new tab in the navigation hierarchy.
    /// </summary>
    /// <param name="request">The tab creation request containing tab properties.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// A task containing the created tab DTO with assigned TabId.
    /// </returns>
    /// <remarks>
    /// MIGRATION: Derived from TabController.AddTab(objTab, AddAllTabsModules) in TabController.vb.
    /// Original method:
    /// 1. Generated TabPath via GenerateTabPath(ParentId, TabName)
    /// 2. Called DataProvider.Instance().AddTab with all properties
    /// 3. Added tab permissions
    /// 4. Updated portal tab order via UpdatePortalTabOrder
    /// 5. Optionally copied AllTabs modules
    /// 6. Cleared cache
    /// </remarks>
    Task<TabDto> CreateTabAsync(CreateTabRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing tab's properties.
    /// </summary>
    /// <param name="tabId">The unique identifier of the tab to update.</param>
    /// <param name="request">The tab update request containing properties to modify.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// A task containing the updated tab DTO.
    /// </returns>
    /// <remarks>
    /// MIGRATION: Derived from TabController.UpdateTab(objTab) in TabController.vb.
    /// Original method:
    /// 1. Checked if TabName or ParentId changed (triggers child path updates)
    /// 2. Called UpdatePortalTabOrder for position changes
    /// 3. Called DataProvider.Instance().UpdateTab with all properties
    /// 4. Updated tab permissions if changed
    /// 5. Updated child tab paths if parent/name changed
    /// 6. Cleared cache
    /// </remarks>
    Task<TabDto> UpdateTabAsync(int tabId, UpdateTabRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a tab from the navigation hierarchy.
    /// </summary>
    /// <param name="tabId">The unique identifier of the tab to delete.</param>
    /// <param name="portalId">The portal identifier for multi-tenant isolation.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// A task representing the asynchronous delete operation.
    /// </returns>
    /// <remarks>
    /// MIGRATION: Derived from TabController.DeleteTab(TabId, PortalId) in TabController.vb.
    /// Original method:
    /// 1. Checked that tab has no children (parent tabs cannot be deleted)
    /// 2. Called DataProvider.Instance().DeleteTab(TabId)
    /// 3. Called UpdatePortalTabOrder with -2 for deletion cleanup
    /// 4. Cleared cache
    /// 
    /// Note: The service implementation should enforce the no-children constraint.
    /// </remarks>
    Task DeleteTabAsync(int tabId, int portalId, CancellationToken cancellationToken = default);

    #endregion

    #region Hierarchy Operations

    /// <summary>
    /// Updates the display order of a tab within its parent level.
    /// </summary>
    /// <param name="tabId">The unique identifier of the tab to reorder.</param>
    /// <param name="parentId">The parent tab identifier. Use null or -1 for root-level tabs.</param>
    /// <param name="order">
    /// The order adjustment value:
    /// Use positive value (e.g., 1) to move down in order (after siblings).
    /// Use negative value (e.g., -1) to move up in order (before siblings).
    /// </param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// A task representing the asynchronous reorder operation.
    /// </returns>
    /// <remarks>
    /// MIGRATION: Derived from TabController.UpdateTabOrder(objTab) in TabController.vb.
    /// Original method called DataProvider.Instance().UpdateTabOrder(TabID, TabOrder, Level, ParentId, TabPath).
    /// The order change logic was handled in UpdatePortalTabOrder with Order parameter.
    /// This operation reorders tabs within the same parent without changing hierarchy level.
    /// </remarks>
    Task UpdateTabOrderAsync(int tabId, int? parentId, int order, CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves a tab to a different parent in the navigation hierarchy.
    /// </summary>
    /// <param name="tabId">The unique identifier of the tab to move.</param>
    /// <param name="newParentId">
    /// The new parent tab identifier. Use null or -1 to move to root level.
    /// </param>
    /// <param name="portalId">The portal identifier for multi-tenant isolation.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// A task representing the asynchronous move operation.
    /// </returns>
    /// <remarks>
    /// MIGRATION: Derived from TabController.UpdatePortalTabOrder parent change logic in TabController.vb.
    /// Original method handled parent changes when intOldParentId != NewParentId:
    /// 1. Located position of last child for new parent
    /// 2. Updated ParentId property
    /// 3. Called MoveTab to handle repositioning with level adjustment
    /// 4. Recalculated TabOrder for all affected tabs
    /// 5. Updated child tab paths
    /// 6. Cleared cache
    /// 
    /// Moving a tab also moves all its descendants.
    /// </remarks>
    Task MoveTabAsync(int tabId, int? newParentId, int portalId, CancellationToken cancellationToken = default);

    #endregion
}
