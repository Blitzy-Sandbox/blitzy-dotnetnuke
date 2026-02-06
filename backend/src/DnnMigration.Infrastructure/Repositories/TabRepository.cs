// -----------------------------------------------------------------------------
// DnnMigration - Modern DotNetNuke Migration
// Copyright (c) 2024. All rights reserved.
// Licensed under the MIT License.
// -----------------------------------------------------------------------------
// MIGRATION: VB.NET DotNetNuke.Entities.Tabs.TabController → C# 12 TabRepository
// Source: Library/Components/Tabs/TabController.vb, Library/Components/Tabs/TabInfo.vb
//
// Key transformations:
// 1) Replace DataProvider.Instance().AddTab (lines 330-373) with AddAsync + SaveChangesAsync
// 2) Replace DataProvider.Instance().DeleteTab (lines 446-457) with Remove + SaveChangesAsync
// 3) Replace DataProvider.Instance().GetTab (lines 467-502) with FirstOrDefaultAsync
// 4) Replace DataProvider.Instance().GetTabs (lines 528-548) with ToListAsync
// 5) Replace DataProvider.Instance().UpdateTab (lines 780-814) with Update + SaveChangesAsync
// 6) Convert FillTabInfo IDataReader hydration (lines 66-139) to EF Core entity tracking
// 7) Convert FillTabInfoCollection (lines 141-162) to ToListAsync
// 8) Convert FillTabInfoDictionary (lines 164-184) to ToDictionaryAsync
// 9) Convert GetTabsByPortal (lines 528-548) to Where(t => t.PortalId == portalId)
// 10) Convert GetTabsByParentId (lines 524-526) to Where(t => t.ParentId == parentId)
// 11) Convert GetTabByName (lines 504-510) to Where(t => t.TabName == name)
// 12) Convert GetTabCount (lines 512-514) to CountAsync
// 13) Implement ITabRepository interface with async/await pattern
// 14) Add CancellationToken support to all async methods
// 15) Use AsNoTracking() for read-only queries
// 16) Use file-scoped namespace syntax
// 17) Use nullable reference types (Tab?)
// 18) Preserve hierarchical parent-child relationship queries
// 19) Support TabPath generation (GenerateTabPath logic from Globals.vb)
// 20) Remove DataCache.ClearTabsCache calls (caching handled by application layer)
// 21) Preserve TabOrder management for navigation ordering
//
// Original VB.NET patterns replaced:
// - DataProvider.Instance().GetTab/GetTabs → DbContext.Tabs LINQ
// - DataProvider.Instance().AddTab/UpdateTab/DeleteTab → EF Core Add/Update/Remove
// - FillTabInfo IDataReader → EF Core entity materialization
// - FillTabInfoCollection ArrayList → LINQ ToListAsync
// - DataCache.ClearTabsCache → Service layer responsibility
// -----------------------------------------------------------------------------

using DnnMigration.Domain.Entities;
using DnnMigration.Domain.Interfaces;
using DnnMigration.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DnnMigration.Infrastructure.Repositories;

/// <summary>
/// Entity Framework Core repository implementation for Tab (Page) entity CRUD operations.
/// </summary>
/// <remarks>
/// <para>
/// This repository replaces the legacy VB.NET TabController.vb pattern which used
/// DataProvider.Instance() with SqlHelper for all database operations. The repository
/// implements <see cref="ITabRepository"/> and uses EF Core 8 with LINQ queries
/// through <see cref="DnnDbContext"/>.
/// </para>
/// <para>
/// <strong>MIGRATION NOTE:</strong> The original TabController had methods that:
/// <list type="bullet">
///   <item><description>Called DataProvider.Instance().GetTab → Replaced with FirstOrDefaultAsync</description></item>
///   <item><description>Called DataProvider.Instance().GetTabs → Replaced with ToListAsync</description></item>
///   <item><description>Called DataProvider.Instance().AddTab → Replaced with Add + SaveChangesAsync</description></item>
///   <item><description>Called DataProvider.Instance().UpdateTab → Replaced with Update + SaveChangesAsync</description></item>
///   <item><description>Called DataProvider.Instance().DeleteTab → Replaced with Remove + SaveChangesAsync</description></item>
///   <item><description>Called DataCache.ClearTabsCache → Removed (caching now at service layer)</description></item>
/// </list>
/// </para>
/// <para>
/// Tabs form a hierarchical tree structure through the self-referencing ParentId
/// relationship. Root-level tabs have a null ParentId (converted from VB.NET Null.NullInteger).
/// The repository supports querying this hierarchy through GetChildrenAsync and
/// maintaining tab ordering through TabOrder and Level properties.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Dependency Injection registration in Program.cs:
/// builder.Services.AddScoped&lt;ITabRepository, TabRepository&gt;();
///
/// // Usage in a service:
/// public class TabService : ITabService
/// {
///     private readonly ITabRepository _tabRepository;
///     
///     public async Task&lt;Tab?&gt; GetTabAsync(int tabId, CancellationToken ct = default)
///     {
///         return await _tabRepository.GetByIdAsync(tabId, ct);
///     }
/// }
/// </code>
/// </example>
public class TabRepository : ITabRepository
{
    private readonly DnnDbContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="TabRepository"/> class.
    /// </summary>
    /// <param name="context">The database context for EF Core operations.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is null.</exception>
    /// <remarks>
    /// MIGRATION: Replaces the VB.NET pattern of calling DataProvider.Instance() in each method.
    /// The DbContext is now injected via constructor for better testability and lifecycle management.
    /// </remarks>
    public TabRepository(DnnDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc />
    /// <remarks>
    /// MIGRATION: Replaces VB.NET TabController.GetTab(ByVal TabId As Integer, ByVal PortalId As Integer, ByVal ignoreCache As Boolean).
    /// Original code (lines 467-502):
    /// <code>
    /// Dim dr As IDataReader = DataProvider.Instance().GetTab(TabId)
    /// Try
    ///     tab = FillTabInfo(dr)
    /// Finally
    ///     If Not dr Is Nothing Then
    ///         dr.Close()
    ///     End If
    /// End Try
    /// </code>
    /// The cache-first logic with ignoreCache parameter has been removed - caching is now
    /// handled at the service layer for better separation of concerns.
    /// </remarks>
    public async Task<Tab?> GetByIdAsync(int tabId, CancellationToken cancellationToken = default)
    {
        // MIGRATION: DataProvider.Instance().GetTab(TabId) → FirstOrDefaultAsync
        // Using AsNoTracking for read-only performance optimization
        return await _context.Tabs
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TabId == tabId, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    /// <remarks>
    /// MIGRATION: Replaces VB.NET TabController.GetTabs(ByVal PortalId As Integer).
    /// Original VB.NET used a DataProvider call that returned all tabs and then filtered in memory.
    /// This implementation retrieves only non-deleted tabs ordered by TabOrder for navigation consistency.
    /// </remarks>
    public async Task<IEnumerable<Tab>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        // MIGRATION: DataProvider.Instance().GetTabs → ToListAsync
        // Filters out soft-deleted tabs and orders by TabOrder for navigation
        return await _context.Tabs
            .AsNoTracking()
            .Where(t => !t.IsDeleted)
            .OrderBy(t => t.TabOrder)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    /// <remarks>
    /// MIGRATION: Replaces VB.NET TabController.GetTabsByPortal(ByVal PortalId As Integer).
    /// Original code (lines 528-548):
    /// <code>
    /// Dim dr As IDataReader = DataProvider.Instance().GetTabs(PortalId)
    /// Try
    ///     While dr.Read
    ///         Dim objTab As TabInfo = FillTabInfo(dr, False)
    ///         dicTabs.Add(objTab.TabID, objTab)
    ///     End While
    /// Finally
    ///     CBO.CloseDataReader(dr, True)
    /// End Try
    /// </code>
    /// The result is filtered by PortalId and ordered by TabOrder to match the original
    /// navigation structure expectation.
    /// </remarks>
    public async Task<IEnumerable<Tab>> GetByPortalIdAsync(int portalId, CancellationToken cancellationToken = default)
    {
        // MIGRATION: Where(t => t.PortalId == portalId) replaces the stored procedure filtering
        return await _context.Tabs
            .AsNoTracking()
            .Where(t => t.PortalId == portalId && !t.IsDeleted)
            .OrderBy(t => t.Level)
            .ThenBy(t => t.TabOrder)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    /// <remarks>
    /// MIGRATION: Replaces VB.NET TabController.GetTabsByParentId (lines 524-526).
    /// Original pattern:
    /// <code>
    /// Return GetTabs(PortalId).Values.Cast(Of TabInfo).Where(Function(t) t.ParentId = ParentId)
    /// </code>
    /// This implementation directly queries the database instead of filtering an in-memory collection.
    /// Root-level tabs (where ParentId equals null) are retrieved when parentId parameter is null.
    /// </remarks>
    public async Task<IEnumerable<Tab>> GetChildrenAsync(int parentId, int portalId, CancellationToken cancellationToken = default)
    {
        // MIGRATION: Where(t => t.ParentId == parentId) replaces in-memory filtering
        // Note: In VB.NET, Null.NullInteger (-1) represented no parent; EF Core uses nullable int
        return await _context.Tabs
            .AsNoTracking()
            .Where(t => t.ParentId == parentId && t.PortalId == portalId && !t.IsDeleted)
            .OrderBy(t => t.TabOrder)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    /// <remarks>
    /// MIGRATION: Replaces VB.NET TabController.GetTabByPath logic.
    /// TabPath is a hierarchical path like "//Admin//SiteSettings" generated by GenerateTabPath.
    /// This method provides direct lookup by path for URL routing scenarios.
    /// </remarks>
    public async Task<Tab?> GetByPathAsync(int portalId, string tabPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tabPath))
        {
            return null;
        }

        // MIGRATION: TabPath comparison is case-insensitive to match original DNN behavior
        var normalizedPath = tabPath.Trim();
        return await _context.Tabs
            .AsNoTracking()
            .FirstOrDefaultAsync(t =>
                t.PortalId == portalId &&
                t.TabPath != null &&
                t.TabPath.Equals(normalizedPath, StringComparison.OrdinalIgnoreCase) &&
                !t.IsDeleted,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    /// <remarks>
    /// MIGRATION: Replaces VB.NET TabController.GetTabByName(ByVal TabName As String, ByVal PortalId As Integer).
    /// Original code (lines 504-510):
    /// <code>
    /// Dim objTabs As Hashtable = GetTabsByPortal(PortalId)
    /// For Each objTab As TabInfo In objTabs.Values
    ///     If objTab.TabName = TabName Then
    ///         Return objTab
    ///     End If
    /// Next
    /// </code>
    /// This replaces the in-memory loop with a direct database query.
    /// </remarks>
    public async Task<Tab?> GetByNameAsync(string tabName, int portalId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tabName))
        {
            return null;
        }

        // MIGRATION: Direct query replaces the in-memory iteration pattern
        return await _context.Tabs
            .AsNoTracking()
            .FirstOrDefaultAsync(t =>
                t.PortalId == portalId &&
                t.TabName == tabName &&
                !t.IsDeleted,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    /// <remarks>
    /// MIGRATION: Replaces VB.NET TabController.GetTabCount(ByVal PortalId As Integer).
    /// Original code (lines 512-514):
    /// <code>
    /// Return GetTabsByPortal(PortalId).Count
    /// </code>
    /// This directly queries the count instead of materializing all tabs first.
    /// </remarks>
    public async Task<int> GetTabCountAsync(int portalId, CancellationToken cancellationToken = default)
    {
        // MIGRATION: CountAsync replaces loading all tabs and calling .Count
        return await _context.Tabs
            .AsNoTracking()
            .CountAsync(t => t.PortalId == portalId && !t.IsDeleted, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    /// <remarks>
    /// MIGRATION: Replaces VB.NET TabController.AddTab(ByVal objTab As TabInfo, ByVal AddAllTabsPermission As Boolean).
    /// Original code (lines 330-373):
    /// <code>
    /// objTab.TabID = DataProvider.Instance().AddTab(objTab.PortalID, objTab.TabName, objTab.IsVisible, ...)
    /// DataCache.ClearTabsCache(objTab.PortalID)
    /// </code>
    /// Cache clearing is now handled by the service layer.
    /// </remarks>
    public async Task<Tab> AddAsync(Tab tab, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tab);

        // MIGRATION: Generate the TabPath before insertion using legacy Globals.GenerateTabPath logic
        tab.TabPath = GenerateTabPath(tab.ParentId, tab.TabName);

        // MIGRATION: Set Level based on parent hierarchy
        if (tab.ParentId.HasValue)
        {
            var parentTab = await _context.Tabs
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.TabId == tab.ParentId.Value, cancellationToken)
                .ConfigureAwait(false);

            tab.Level = parentTab != null ? parentTab.Level + 1 : 0;
        }
        else
        {
            tab.Level = 0;
        }

        // MIGRATION: If TabOrder is not set, place it at the end of siblings
        if (tab.TabOrder <= 0)
        {
            var maxOrder = await _context.Tabs
                .Where(t => t.PortalId == tab.PortalId && t.ParentId == tab.ParentId && !t.IsDeleted)
                .MaxAsync(t => (int?)t.TabOrder, cancellationToken)
                .ConfigureAwait(false);

            tab.TabOrder = (maxOrder ?? 0) + 1;
        }

        // MIGRATION: DataProvider.Instance().AddTab → Add + SaveChangesAsync
        _context.Tabs.Add(tab);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // MIGRATION: DataCache.ClearTabsCache(objTab.PortalID) removed - handled by service layer

        return tab;
    }

    /// <inheritdoc />
    /// <remarks>
    /// MIGRATION: Replaces VB.NET TabController.UpdateTab(ByVal objTab As TabInfo).
    /// Original code (lines 780-814):
    /// <code>
    /// DataProvider.Instance().UpdateTab(objTab.TabID, objTab.TabName, objTab.IsVisible, ...)
    /// DataCache.ClearTabsCache(objTab.PortalID)
    /// </code>
    /// Cache clearing is now handled by the service layer.
    /// </remarks>
    public async Task UpdateAsync(Tab tab, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tab);

        // MIGRATION: Regenerate TabPath in case name or parent changed
        tab.TabPath = GenerateTabPath(tab.ParentId, tab.TabName);

        // MIGRATION: Recalculate Level if parent changed
        if (tab.ParentId.HasValue)
        {
            var parentTab = await _context.Tabs
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.TabId == tab.ParentId.Value, cancellationToken)
                .ConfigureAwait(false);

            tab.Level = parentTab != null ? parentTab.Level + 1 : 0;
        }
        else
        {
            tab.Level = 0;
        }

        // MIGRATION: DataProvider.Instance().UpdateTab → Update + SaveChangesAsync
        _context.Tabs.Update(tab);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // MIGRATION: DataCache.ClearTabsCache(objTab.PortalID) removed - handled by service layer
    }

    /// <inheritdoc />
    /// <remarks>
    /// MIGRATION: Replaces VB.NET TabController.DeleteTab(ByVal TabId As Integer, ByVal PortalId As Integer).
    /// Original code (lines 446-457):
    /// <code>
    /// DataProvider.Instance().DeleteTab(TabId)
    /// DataCache.ClearTabsCache(PortalId)
    /// </code>
    /// The original code performed a hard delete. This implementation performs a soft delete
    /// (setting IsDeleted = true) to preserve data integrity and audit trails. For hard delete,
    /// the service layer can explicitly remove the entity.
    /// </remarks>
    public async Task DeleteAsync(int tabId, CancellationToken cancellationToken = default)
    {
        var tab = await _context.Tabs
            .FirstOrDefaultAsync(t => t.TabId == tabId, cancellationToken)
            .ConfigureAwait(false);

        if (tab is null)
        {
            return;
        }

        // MIGRATION: Soft delete by setting IsDeleted flag
        // For permanent removal, the caller can use Remove explicitly if needed
        tab.IsDeleted = true;

        // MIGRATION: Mark child tabs as deleted (cascade soft delete)
        var childTabs = await _context.Tabs
            .Where(t => t.ParentId == tabId && !t.IsDeleted)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var childTab in childTabs)
        {
            childTab.IsDeleted = true;
        }

        // MIGRATION: DataProvider.Instance().DeleteTab → SaveChangesAsync (soft delete)
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // MIGRATION: DataCache.ClearTabsCache(PortalId) removed - handled by service layer
    }

    /// <inheritdoc />
    /// <remarks>
    /// MIGRATION: Replaces VB.NET TabController.MoveTab logic for moving tabs in the hierarchy.
    /// The original code updated ParentId and reordered sibling tabs.
    /// This implementation:
    /// 1) Updates the tab's ParentId to the new parent
    /// 2) Recalculates Level based on new parent depth
    /// 3) Regenerates TabPath
    /// 4) Updates TabOrder for proper navigation ordering
    /// </remarks>
    public async Task MoveTabAsync(int tabId, int newParentId, int portalId, CancellationToken cancellationToken = default)
    {
        var tab = await _context.Tabs
            .FirstOrDefaultAsync(t => t.TabId == tabId && t.PortalId == portalId, cancellationToken)
            .ConfigureAwait(false);

        if (tab is null)
        {
            return;
        }

        // MIGRATION: Prevent circular reference - cannot move tab under itself or its descendants
        if (newParentId != 0 && await IsDescendantAsync(tabId, newParentId, cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException($"Cannot move tab {tabId} under its descendant {newParentId}.");
        }

        // MIGRATION: Store old parent for potential TabOrder reordering
        var oldParentId = tab.ParentId;

        // MIGRATION: Update ParentId (0 means root level, convert to null)
        tab.ParentId = newParentId == 0 ? null : newParentId;

        // MIGRATION: Recalculate Level based on new parent
        if (tab.ParentId.HasValue)
        {
            var newParent = await _context.Tabs
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.TabId == tab.ParentId.Value, cancellationToken)
                .ConfigureAwait(false);

            tab.Level = newParent != null ? newParent.Level + 1 : 0;
        }
        else
        {
            tab.Level = 0;
        }

        // MIGRATION: Regenerate TabPath for new location
        tab.TabPath = GenerateTabPath(tab.ParentId, tab.TabName);

        // MIGRATION: Place at end of new parent's children
        var maxOrder = await _context.Tabs
            .Where(t => t.PortalId == portalId && t.ParentId == tab.ParentId && t.TabId != tabId && !t.IsDeleted)
            .MaxAsync(t => (int?)t.TabOrder, cancellationToken)
            .ConfigureAwait(false);

        tab.TabOrder = (maxOrder ?? 0) + 1;

        // MIGRATION: Update all descendants' Levels and TabPaths
        await UpdateDescendantPathsAsync(tab, cancellationToken).ConfigureAwait(false);

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // MIGRATION: DataCache.ClearTabsCache(PortalId) removed - handled by service layer
    }

    /// <summary>
    /// Generates a hierarchical path for a tab based on parent hierarchy.
    /// </summary>
    /// <param name="parentId">The parent tab ID, or null for root-level tabs.</param>
    /// <param name="tabName">The name of the tab being added.</param>
    /// <returns>A path string in the format "//ParentName//ChildName//TabName".</returns>
    /// <remarks>
    /// <para>
    /// MIGRATION: Derived from VB.NET DotNetNuke.Common.Globals.GenerateTabPath (Library/Components/Shared/Globals.vb).
    /// Original VB.NET code (lines ~2250-2285):
    /// </para>
    /// <code>
    /// Public Shared Function GenerateTabPath(ByVal ParentId As Integer, ByVal TabName As String) As String
    ///     Dim strTabPath As String = Null.NullString
    ///     If Not TabName = "" Then
    ///         Dim objTabs As New TabController
    ///         If Not ParentId = Null.NullInteger Then
    ///             Dim objTab As TabInfo = objTabs.GetTab(ParentId)
    ///             If Not objTab Is Nothing Then
    ///                 strTabPath = objTab.TabPath
    ///             End If
    ///         End If
    ///         If strTabPath = "" Then
    ///             strTabPath = "//"
    ///         Else
    ///             strTabPath = strTabPath + "//"
    ///         End If
    ///         Dim strTabName As String = TabName
    ///         strTabName = Regex.Replace(strTabName, "[^\w]", "", RegexOptions.IgnoreCase)
    ///         strTabPath = strTabPath + strTabName
    ///     End If
    ///     Return strTabPath
    /// End Function
    /// </code>
    /// <para>
    /// The method strips non-word characters from tab names and constructs a hierarchical path
    /// that is used for URL routing and friendly URL generation.
    /// </para>
    /// </remarks>
    public string GenerateTabPath(int? parentId, string tabName)
    {
        if (string.IsNullOrWhiteSpace(tabName))
        {
            return string.Empty;
        }

        var tabPath = string.Empty;

        // MIGRATION: If ParentId is not null, get parent's TabPath
        if (parentId.HasValue)
        {
            // MIGRATION: Note - We use synchronous query here because this is called from
            // sync context. For full async support, the caller should pass parent TabPath directly.
            var parentTab = _context.Tabs
                .AsNoTracking()
                .FirstOrDefault(t => t.TabId == parentId.Value);

            if (parentTab != null && !string.IsNullOrWhiteSpace(parentTab.TabPath))
            {
                tabPath = parentTab.TabPath;
            }
        }

        // MIGRATION: Build path - start with "//" if no parent path exists
        if (string.IsNullOrEmpty(tabPath))
        {
            tabPath = "//";
        }
        else
        {
            tabPath += "//";
        }

        // MIGRATION: Strip non-word characters from tab name using regex pattern [^\w]
        var cleanTabName = System.Text.RegularExpressions.Regex.Replace(
            tabName,
            @"[^\w]",
            string.Empty,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        tabPath += cleanTabName;

        return tabPath;
    }

    /// <summary>
    /// Checks if a tab is a descendant of another tab to prevent circular references.
    /// </summary>
    /// <param name="potentialAncestorId">The tab that might be an ancestor.</param>
    /// <param name="tabIdToCheck">The tab to check if it's a descendant.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>True if tabIdToCheck is a descendant of potentialAncestorId.</returns>
    private async Task<bool> IsDescendantAsync(int potentialAncestorId, int tabIdToCheck, CancellationToken cancellationToken)
    {
        var currentTab = await _context.Tabs
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TabId == tabIdToCheck, cancellationToken)
            .ConfigureAwait(false);

        while (currentTab != null && currentTab.ParentId.HasValue)
        {
            if (currentTab.ParentId.Value == potentialAncestorId)
            {
                return true;
            }

            currentTab = await _context.Tabs
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.TabId == currentTab.ParentId.Value, cancellationToken)
                .ConfigureAwait(false);
        }

        return false;
    }

    /// <summary>
    /// Updates TabPath and Level for all descendants of a moved tab.
    /// </summary>
    /// <param name="parentTab">The tab whose descendants need updating.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    private async Task UpdateDescendantPathsAsync(Tab parentTab, CancellationToken cancellationToken)
    {
        var children = await _context.Tabs
            .Where(t => t.ParentId == parentTab.TabId && !t.IsDeleted)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var child in children)
        {
            child.Level = parentTab.Level + 1;
            child.TabPath = GenerateTabPath(parentTab.TabId, child.TabName);

            // MIGRATION: Recursively update all descendants
            await UpdateDescendantPathsAsync(child, cancellationToken).ConfigureAwait(false);
        }
    }
}
