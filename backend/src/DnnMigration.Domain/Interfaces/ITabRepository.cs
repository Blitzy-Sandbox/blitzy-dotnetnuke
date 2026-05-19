// -----------------------------------------------------------------------------
// DnnMigration - Modern DotNetNuke Migration
// Copyright (c) 2024. All rights reserved.
// Licensed under the MIT License.
// -----------------------------------------------------------------------------
// MIGRATION: VB.NET DotNetNuke.Entities.Tabs.TabController → C# 12 ITabRepository interface
// Source: Library/Components/Tabs/TabController.vb
// Changes:
// - Extracted interface from VB.NET TabController class
// - GetTab → GetByIdAsync with nullable return type
// - GetTabs/GetTabsByPortal → GetAllAsync/GetByPortalIdAsync with IEnumerable return
// - GetTabsByParentId → GetChildrenAsync for hierarchical navigation
// - GetTabByName → GetByNameAsync
// - GetTabByTabPath → GetByPathAsync
// - GetTabCount → GetTabCountAsync
// - AddTab → AddAsync returning the created entity
// - UpdateTab → UpdateAsync
// - DeleteTab → DeleteAsync
// - UpdatePortalTabOrder → MoveTabAsync for tab reordering
// - All methods use async/await with Task return types
// - All methods include CancellationToken with default value
// - Uses file-scoped namespace per C# 12 standards
// -----------------------------------------------------------------------------

using DnnMigration.Domain.Entities;

namespace DnnMigration.Domain.Interfaces;

/// <summary>
/// Repository interface defining data access contract for Tab (Page) entities.
/// </summary>
/// <remarks>
/// <para>
/// This interface abstracts data access operations for the Tab entity,
/// enabling the Infrastructure layer to implement data access while maintaining
/// domain layer independence. Tabs represent pages in the DNN portal navigation
/// hierarchy and support unlimited nesting through parent-child relationships.
/// </para>
/// <para>
/// MIGRATION: Extracted from legacy VB.NET TabController.vb CRUD operations:
/// <list type="bullet">
///   <item><description>GetTab → <see cref="GetByIdAsync"/></description></item>
///   <item><description>GetTabs → <see cref="GetAllAsync"/></description></item>
///   <item><description>GetTabsByPortal → <see cref="GetByPortalIdAsync"/></description></item>
///   <item><description>GetTabsByParentId → <see cref="GetChildrenAsync"/></description></item>
///   <item><description>GetTabByTabPath → <see cref="GetByPathAsync"/></description></item>
///   <item><description>GetTabByName → <see cref="GetByNameAsync"/></description></item>
///   <item><description>GetTabCount → <see cref="GetTabCountAsync"/></description></item>
///   <item><description>AddTab → <see cref="AddAsync"/></description></item>
///   <item><description>UpdateTab → <see cref="UpdateAsync"/></description></item>
///   <item><description>DeleteTab → <see cref="DeleteAsync"/></description></item>
///   <item><description>UpdatePortalTabOrder → <see cref="MoveTabAsync"/></description></item>
/// </list>
/// </para>
/// </remarks>
public interface ITabRepository
{
    /// <summary>
    /// Gets a tab by its unique identifier.
    /// </summary>
    /// <param name="tabId">The unique identifier of the tab.</param>
    /// <param name="cancellationToken">
    /// A token to monitor for cancellation requests, allowing cooperative cancellation
    /// of long-running database queries.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains
    /// the <see cref="Tab"/> entity if found; otherwise, <c>null</c>.
    /// </returns>
    /// <remarks>
    /// MIGRATION: Replaces VB.NET TabController.GetTab(ByVal TabId As Integer, ByVal PortalId As Integer, ByVal ignoreCache As Boolean).
    /// The original method retrieved from cache first when ignoreCache was false, then database if not cached.
    /// Cache management is now handled at the service layer.
    /// </remarks>
    /// <example>
    /// <code>
    /// var tab = await tabRepository.GetByIdAsync(42);
    /// if (tab is not null)
    /// {
    ///     Console.WriteLine($"Tab Name: {tab.TabName}");
    /// }
    /// </code>
    /// </example>
    Task<Tab?> GetByIdAsync(int tabId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all tabs in the system across all portals.
    /// </summary>
    /// <param name="cancellationToken">
    /// A token to monitor for cancellation requests, allowing cooperative cancellation
    /// of long-running database queries.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains
    /// an enumerable collection of all <see cref="Tab"/> entities.
    /// </returns>
    /// <remarks>
    /// MIGRATION: Replaces VB.NET TabController.GetAllTabs() which returned an ArrayList.
    /// The return type is now strongly-typed as IEnumerable&lt;Tab&gt;.
    /// </remarks>
    /// <example>
    /// <code>
    /// var allTabs = await tabRepository.GetAllAsync();
    /// foreach (var tab in allTabs)
    /// {
    ///     Console.WriteLine($"Tab: {tab.TabName} (Portal: {tab.PortalId})");
    /// }
    /// </code>
    /// </example>
    Task<IEnumerable<Tab>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all tabs belonging to a specific portal.
    /// </summary>
    /// <param name="portalId">The unique identifier of the portal.</param>
    /// <param name="cancellationToken">
    /// A token to monitor for cancellation requests, allowing cooperative cancellation
    /// of long-running database queries.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains
    /// an enumerable collection of <see cref="Tab"/> entities belonging to the specified portal.
    /// </returns>
    /// <remarks>
    /// MIGRATION: Replaces VB.NET TabController.GetTabsByPortal(ByVal PortalId As Integer) 
    /// which returned a Dictionary(Of Integer, TabInfo). Also consolidates GetTabs(ByVal PortalId As Integer)
    /// which converted the dictionary to an ArrayList.
    /// The return type is now strongly-typed as IEnumerable&lt;Tab&gt;.
    /// </remarks>
    /// <example>
    /// <code>
    /// var portalTabs = await tabRepository.GetByPortalIdAsync(1);
    /// foreach (var tab in portalTabs.OrderBy(t => t.TabOrder))
    /// {
    ///     Console.WriteLine($"Tab: {tab.TabName} (Order: {tab.TabOrder})");
    /// }
    /// </code>
    /// </example>
    Task<IEnumerable<Tab>> GetByPortalIdAsync(int portalId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all child tabs of a specified parent tab within a portal.
    /// </summary>
    /// <param name="parentId">
    /// The unique identifier of the parent tab. Use <c>-1</c> or <c>null</c> equivalent 
    /// to get root-level tabs (tabs with no parent).
    /// </param>
    /// <param name="portalId">The unique identifier of the portal.</param>
    /// <param name="cancellationToken">
    /// A token to monitor for cancellation requests, allowing cooperative cancellation
    /// of long-running database queries.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains
    /// an enumerable collection of child <see cref="Tab"/> entities.
    /// </returns>
    /// <remarks>
    /// <para>
    /// MIGRATION: Replaces VB.NET TabController.GetTabsByParentId(ByVal ParentId As Integer, ByVal PortalId As Integer).
    /// The original method filtered GetTabsByPortal by matching ParentId property.
    /// </para>
    /// <para>
    /// This method enables building hierarchical navigation trees by recursively
    /// retrieving child tabs at each level.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Get root-level tabs (no parent)
    /// var rootTabs = await tabRepository.GetChildrenAsync(-1, portalId);
    /// 
    /// // Get children of a specific tab
    /// var childTabs = await tabRepository.GetChildrenAsync(parentTabId, portalId);
    /// </code>
    /// </example>
    Task<IEnumerable<Tab>> GetChildrenAsync(int parentId, int portalId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a tab by its path within a portal.
    /// </summary>
    /// <param name="portalId">The unique identifier of the portal.</param>
    /// <param name="tabPath">
    /// The hierarchical path of the tab using double-slash separators
    /// (e.g., "//Home//About//Team").
    /// </param>
    /// <param name="cancellationToken">
    /// A token to monitor for cancellation requests, allowing cooperative cancellation
    /// of long-running database queries.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains
    /// the <see cref="Tab"/> entity if found; otherwise, <c>null</c>.
    /// </returns>
    /// <remarks>
    /// <para>
    /// MIGRATION: Replaces VB.NET TabController.GetTabByTabPath(ByVal portalId As Integer, ByVal tabPath As String)
    /// which was a shared/static method that looked up tabs in a cached dictionary keyed by "//{portalId}{tabPath}".
    /// </para>
    /// <para>
    /// Tab paths are hierarchical identifiers combining parent tab names with double-slash
    /// separators. For example, a "Team" page under "About" under "Home" would have 
    /// the path "//Home//About//Team".
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var tab = await tabRepository.GetByPathAsync(1, "//Home//About");
    /// if (tab is not null)
    /// {
    ///     Console.WriteLine($"Found tab: {tab.TabName}");
    /// }
    /// </code>
    /// </example>
    Task<Tab?> GetByPathAsync(int portalId, string tabPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a tab by its name within a portal.
    /// </summary>
    /// <param name="tabName">The name of the tab to find.</param>
    /// <param name="portalId">The unique identifier of the portal.</param>
    /// <param name="cancellationToken">
    /// A token to monitor for cancellation requests, allowing cooperative cancellation
    /// of long-running database queries.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains
    /// the <see cref="Tab"/> entity if found; otherwise, <c>null</c>.
    /// </returns>
    /// <remarks>
    /// <para>
    /// MIGRATION: Replaces VB.NET TabController.GetTabByName(ByVal TabName As String, ByVal PortalId As Integer).
    /// The original method called GetTabByNameAndParent with Integer.MinValue for ParentId,
    /// which meant it would return the first matching tab by name regardless of parent.
    /// </para>
    /// <para>
    /// Note: Tab names are not necessarily unique within a portal. If multiple tabs share
    /// the same name, this method returns the first match. Use <see cref="GetByPathAsync"/>
    /// for unambiguous tab identification.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var tab = await tabRepository.GetByNameAsync("About", 1);
    /// if (tab is not null)
    /// {
    ///     Console.WriteLine($"Found tab: {tab.TabName} at path {tab.TabPath}");
    /// }
    /// </code>
    /// </example>
    Task<Tab?> GetByNameAsync(string tabName, int portalId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the count of tabs in a specific portal.
    /// </summary>
    /// <param name="portalId">The unique identifier of the portal.</param>
    /// <param name="cancellationToken">
    /// A token to monitor for cancellation requests, allowing cooperative cancellation
    /// of long-running database queries.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains
    /// the total number of tabs in the portal.
    /// </returns>
    /// <remarks>
    /// MIGRATION: Replaces VB.NET TabController.GetTabCount(ByVal portalId As Integer)
    /// which called DataProvider.Instance().GetTabCount(portalId).
    /// </remarks>
    /// <example>
    /// <code>
    /// var tabCount = await tabRepository.GetTabCountAsync(portalId);
    /// Console.WriteLine($"Portal has {tabCount} tabs");
    /// </code>
    /// </example>
    Task<int> GetTabCountAsync(int portalId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new tab to the repository.
    /// </summary>
    /// <param name="tab">The tab entity to add.</param>
    /// <param name="cancellationToken">
    /// A token to monitor for cancellation requests, allowing cooperative cancellation
    /// of long-running database queries.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains
    /// the added <see cref="Tab"/> entity with its generated <see cref="Tab.TabId"/>.
    /// </returns>
    /// <remarks>
    /// <para>
    /// MIGRATION: Replaces VB.NET TabController.AddTab(ByVal objTab As TabInfo, ByVal AddAllTabsModules As Boolean).
    /// The original method:
    /// <list type="bullet">
    ///   <item><description>Generated TabPath using GenerateTabPath(ParentId, TabName)</description></item>
    ///   <item><description>Called DataProvider.Instance().AddTab with all tab properties</description></item>
    ///   <item><description>Added tab permissions from objTab.TabPermissions</description></item>
    ///   <item><description>Updated portal tab order for proper hierarchical positioning</description></item>
    ///   <item><description>Optionally copied "all tabs" modules to the new tab</description></item>
    ///   <item><description>Cleared tabs and portal dictionary caches</description></item>
    /// </list>
    /// TabPath generation and cache management are now handled at the service layer.
    /// Permission management may be handled separately via permission services.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var newTab = new Tab
    /// {
    ///     PortalId = 1,
    ///     TabName = "New Page",
    ///     IsVisible = true,
    ///     ParentId = null // Root level tab
    /// };
    /// var addedTab = await tabRepository.AddAsync(newTab);
    /// Console.WriteLine($"Created tab with ID: {addedTab.TabId}");
    /// </code>
    /// </example>
    Task<Tab> AddAsync(Tab tab, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing tab in the repository.
    /// </summary>
    /// <param name="tab">The tab entity with updated values.</param>
    /// <param name="cancellationToken">
    /// A token to monitor for cancellation requests, allowing cooperative cancellation
    /// of long-running database queries.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous operation.
    /// </returns>
    /// <remarks>
    /// <para>
    /// MIGRATION: Replaces VB.NET TabController.UpdateTab(ByVal objTab As TabInfo).
    /// The original method:
    /// <list type="bullet">
    ///   <item><description>Detected TabName or ParentId changes to determine if child TabPaths need updating</description></item>
    ///   <item><description>Called UpdatePortalTabOrder to maintain proper positioning</description></item>
    ///   <item><description>Called DataProvider.Instance().UpdateTab with all properties</description></item>
    ///   <item><description>Compared and updated tab permissions</description></item>
    ///   <item><description>Recursively updated child tab paths if needed via UpdateChildTabPath</description></item>
    ///   <item><description>Cleared the tabs cache</description></item>
    /// </list>
    /// TabPath cascade updates and cache management are now handled at the service layer.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var tab = await tabRepository.GetByIdAsync(42);
    /// if (tab is not null)
    /// {
    ///     tab.TabName = "Updated Name";
    ///     tab.Description = "Updated description";
    ///     await tabRepository.UpdateAsync(tab);
    /// }
    /// </code>
    /// </example>
    Task UpdateAsync(Tab tab, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a tab from the repository.
    /// </summary>
    /// <param name="tabId">The unique identifier of the tab to delete.</param>
    /// <param name="cancellationToken">
    /// A token to monitor for cancellation requests, allowing cooperative cancellation
    /// of long-running database queries.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous operation.
    /// </returns>
    /// <remarks>
    /// <para>
    /// MIGRATION: Replaces VB.NET TabController.DeleteTab(ByVal TabId As Integer, ByVal PortalId As Integer).
    /// The original method:
    /// <list type="bullet">
    ///   <item><description>Verified the tab has no children (parent tabs cannot be deleted)</description></item>
    ///   <item><description>Called DataProvider.Instance().DeleteTab(TabId)</description></item>
    ///   <item><description>Updated portal tab order to account for removed tab</description></item>
    ///   <item><description>Cleared tabs cache and portal dictionary cache</description></item>
    /// </list>
    /// Child validation and order updates are now handled at the service layer.
    /// </para>
    /// <para>
    /// Note: This performs a hard delete. The legacy DNN system also supported soft-delete
    /// by setting IsDeleted = true via UpdateTab. Consider using soft-delete at the
    /// service layer for recycle bin functionality.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Permanently delete a tab (ensure no children exist)
    /// await tabRepository.DeleteAsync(42);
    /// </code>
    /// </example>
    Task DeleteAsync(int tabId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves a tab to a new parent within the portal hierarchy.
    /// </summary>
    /// <param name="tabId">The unique identifier of the tab to move.</param>
    /// <param name="newParentId">
    /// The unique identifier of the new parent tab. Use <c>-1</c> to move to root level.
    /// </param>
    /// <param name="portalId">The unique identifier of the portal.</param>
    /// <param name="cancellationToken">
    /// A token to monitor for cancellation requests, allowing cooperative cancellation
    /// of long-running database queries.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous operation.
    /// </returns>
    /// <remarks>
    /// <para>
    /// MIGRATION: Replaces VB.NET TabController.UpdatePortalTabOrder(ByVal PortalId, ByVal TabId, ByVal NewParentId, ...).
    /// The original method was complex, handling:
    /// <list type="bullet">
    ///   <item><description>Creating temporary tab collections for reordering</description></item>
    ///   <item><description>Tracking old and new parent positions</description></item>
    ///   <item><description>Moving tabs with their children via the private MoveTab method</description></item>
    ///   <item><description>Updating Level property for the tab and all children</description></item>
    ///   <item><description>Recalculating TabOrder for all affected tabs</description></item>
    ///   <item><description>Separating admin/super tabs from desktop tabs in ordering</description></item>
    /// </list>
    /// The repository implementation should handle the database updates, while complex
    /// reordering logic may be coordinated at the service layer.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Move tab 42 under parent tab 10
    /// await tabRepository.MoveTabAsync(42, 10, portalId);
    /// 
    /// // Move tab 42 to root level
    /// await tabRepository.MoveTabAsync(42, -1, portalId);
    /// </code>
    /// </example>
    Task MoveTabAsync(int tabId, int newParentId, int portalId, CancellationToken cancellationToken = default);
}
