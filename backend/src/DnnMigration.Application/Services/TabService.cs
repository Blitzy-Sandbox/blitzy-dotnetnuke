// -----------------------------------------------------------------------------
// DnnMigration - Tab Service Implementation
// MIGRATION: Business logic extracted from Library/Components/Tabs/TabController.vb
// Converted from VB.NET to C# 12 with async/await patterns, nullable types,
// and modern dependency injection.
// -----------------------------------------------------------------------------
// Source VB.NET methods mapped:
// - GetTab(TabId, PortalId, ignoreCache) → GetTabAsync
// - GetTabs(PortalId) → GetTabsAsync
// - GetTabsByParentId(ParentId, PortalId) → GetTabsByParentIdAsync
// - GetTabByName(TabName, PortalId) → GetTabByNameAsync
// - GetTabCount(portalId) → GetTabCountAsync
// - AddTab(objTab, AddAllTabsModules) → CreateTabAsync
// - UpdateTab(objTab) → UpdateTabAsync
// - DeleteTab(TabId, PortalId) → DeleteTabAsync
// - UpdateTabOrder(objTab) → UpdateTabOrderAsync
// - UpdatePortalTabOrder (move logic) → MoveTabAsync
// -----------------------------------------------------------------------------

using AutoMapper;
using DnnMigration.Application.DTOs;
using DnnMigration.Application.Interfaces;
using DnnMigration.Domain.Entities;
using DnnMigration.Domain.Interfaces;

namespace DnnMigration.Application.Services;

/// <summary>
/// Application service implementing page/navigation management business logic.
/// Orchestrates tab (page) CRUD operations and hierarchical navigation between
/// the API layer and domain/repository layers.
/// </summary>
/// <remarks>
/// <para>
/// MIGRATION: This service class extracts business logic from the legacy VB.NET
/// TabController.vb class. The original controller combined data access and business
/// logic; this service follows Clean Architecture by delegating data access to
/// <see cref="ITabRepository"/> and providing business operation orchestration.
/// </para>
/// <para>
/// Key migration changes:
/// <list type="bullet">
///   <item><description>Synchronous methods converted to async with CancellationToken support</description></item>
///   <item><description>VB.NET Null class handling converted to C# nullable types</description></item>
///   <item><description>DataProvider.Instance() calls replaced with repository methods</description></item>
///   <item><description>DataCache.ClearTabsCache calls abstracted (cache invalidation handled at infrastructure layer)</description></item>
///   <item><description>AutoMapper used for entity-DTO mapping instead of manual property copying</description></item>
/// </list>
/// </para>
/// </remarks>
public class TabService : ITabService
{
    private readonly ITabRepository _tabRepository;
    private readonly IMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="TabService"/> class.
    /// </summary>
    /// <param name="tabRepository">The tab repository for data access operations.</param>
    /// <param name="mapper">The AutoMapper instance for entity-DTO mapping.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="tabRepository"/> or <paramref name="mapper"/> is null.
    /// </exception>
    public TabService(ITabRepository tabRepository, IMapper mapper)
    {
        _tabRepository = tabRepository ?? throw new ArgumentNullException(nameof(tabRepository));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }

    #region Read Operations

    /// <inheritdoc />
    /// <remarks>
    /// MIGRATION: Derived from TabController.GetTab(TabId, PortalId, ignoreCache) in TabController.vb.
    /// Original method checked cache first when ignoreCache was false, then queried database.
    /// Cache management is now handled at the infrastructure layer via EF Core caching.
    /// The PortalId parameter is used for validation to ensure multi-tenant isolation.
    /// </remarks>
    public async Task<TabDto?> GetTabAsync(
        int tabId, 
        int portalId, 
        CancellationToken cancellationToken = default)
    {
        // MIGRATION: Original VB.NET used Null.IsNull(PortalId) check for host tabs
        // In the migrated version, we retrieve the tab and validate portal ownership
        var tab = await _tabRepository.GetByIdAsync(tabId, cancellationToken)
            .ConfigureAwait(false);

        if (tab is null)
        {
            return null;
        }

        // MIGRATION: Preserve multi-tenant isolation by verifying portal ownership
        // Original logic: if tab belongs to different portal (and not a host tab), return null
        if (tab.PortalId != portalId)
        {
            return null;
        }

        return _mapper.Map<TabDto>(tab);
    }

    /// <inheritdoc />
    /// <remarks>
    /// MIGRATION: Derived from TabController.GetTabs(PortalId) in TabController.vb.
    /// Original method iterated GetTabsByPortal dictionary and returned ArrayList.
    /// Returns IEnumerable for modern collection semantics and deferred execution.
    /// </remarks>
    public async Task<IEnumerable<TabDto>> GetTabsAsync(
        int portalId, 
        CancellationToken cancellationToken = default)
    {
        var tabs = await _tabRepository.GetByPortalIdAsync(portalId, cancellationToken)
            .ConfigureAwait(false);

        // MIGRATION: Original VB.NET sorted by TabOrder in the GetTabsByPortal cached dictionary
        // Preserve ordering behavior by sorting the results
        return tabs
            .OrderBy(t => t.TabOrder)
            .Select(tab => _mapper.Map<TabDto>(tab))
            .ToList();
    }

    /// <inheritdoc />
    /// <remarks>
    /// MIGRATION: Derived from TabController.GetTabsByParentId(ParentId, PortalId) in TabController.vb.
    /// Original method called private GetTabsByParent which filtered GetTabsByPortal by ParentId.
    /// Essential for building hierarchical navigation menus.
    /// </remarks>
    public async Task<IEnumerable<TabDto>> GetTabsByParentIdAsync(
        int? parentId, 
        int portalId, 
        CancellationToken cancellationToken = default)
    {
        // MIGRATION: VB.NET used -1 or Null.NullInteger for root level tabs
        // Convert null to -1 for repository compatibility
        var effectiveParentId = parentId ?? -1;

        var tabs = await _tabRepository.GetChildrenAsync(effectiveParentId, portalId, cancellationToken)
            .ConfigureAwait(false);

        // MIGRATION: Preserve ordering behavior - tabs should be ordered by TabOrder
        return tabs
            .OrderBy(t => t.TabOrder)
            .Select(tab => _mapper.Map<TabDto>(tab))
            .ToList();
    }

    /// <inheritdoc />
    /// <remarks>
    /// MIGRATION: Derived from TabController.GetTabByName(TabName, PortalId) in TabController.vb.
    /// Original method called GetTabByNameAndParent with Integer.MinValue for ParentId.
    /// If multiple tabs share the same name, returns the first match.
    /// </remarks>
    public async Task<TabDto?> GetTabByNameAsync(
        string tabName, 
        int portalId, 
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tabName))
        {
            return null;
        }

        var tab = await _tabRepository.GetByNameAsync(tabName, portalId, cancellationToken)
            .ConfigureAwait(false);

        if (tab is null)
        {
            return null;
        }

        return _mapper.Map<TabDto>(tab);
    }

    /// <inheritdoc />
    /// <remarks>
    /// MIGRATION: Derived from TabController.GetTabCount(portalId) in TabController.vb.
    /// Original method called DataProvider.Instance().GetTabCount(portalId).
    /// Useful for quota validation and dashboard statistics.
    /// </remarks>
    public async Task<int> GetTabCountAsync(
        int portalId, 
        CancellationToken cancellationToken = default)
    {
        return await _tabRepository.GetTabCountAsync(portalId, cancellationToken)
            .ConfigureAwait(false);
    }

    #endregion

    #region Write Operations

    /// <inheritdoc />
    /// <remarks>
    /// MIGRATION: Derived from TabController.AddTab(objTab, AddAllTabsModules) in TabController.vb.
    /// Original method (lines 326-373):
    /// 1. Generated TabPath via GenerateTabPath(ParentId, TabName)
    /// 2. Called DataProvider.Instance().AddTab with all properties
    /// 3. Added tab permissions
    /// 4. Updated portal tab order via UpdatePortalTabOrder
    /// 5. Optionally copied AllTabs modules
    /// 6. Cleared cache
    /// 
    /// Note: AllTabs modules copy and permission management are handled separately.
    /// </remarks>
    public async Task<TabDto> CreateTabAsync(
        CreateTabRequest request, 
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // MIGRATION: Generate TabPath as in original VB.NET GenerateTabPath method
        var tabPath = await GenerateTabPathAsync(request.ParentId, request.TabName, request.PortalId, cancellationToken)
            .ConfigureAwait(false);

        // MIGRATION: Calculate level based on parent
        var level = await CalculateTabLevelAsync(request.ParentId, cancellationToken)
            .ConfigureAwait(false);

        // MIGRATION: Calculate initial TabOrder - place at end of siblings
        var tabOrder = await CalculateNewTabOrderAsync(request.ParentId, request.PortalId, cancellationToken)
            .ConfigureAwait(false);

        // Create new Tab entity from request
        var tab = new Tab
        {
            PortalId = request.PortalId,
            TabName = request.TabName,
            ParentId = request.ParentId,
            TabPath = tabPath,
            Level = level,
            TabOrder = tabOrder,
            IsVisible = request.IsVisible,
            DisableLink = request.DisableLink,
            Title = request.Title,
            Description = request.Description,
            KeyWords = request.KeyWords,
            IconFile = request.IconFile,
            Url = request.Url,
            SkinSrc = request.SkinSrc,
            ContainerSrc = request.ContainerSrc,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            RefreshInterval = request.RefreshInterval,
            PageHeadText = request.PageHeadText,
            IsSecure = request.IsSecure,
            IsDeleted = false,
            HasChildren = false
        };

        var createdTab = await _tabRepository.AddAsync(tab, cancellationToken)
            .ConfigureAwait(false);

        // MIGRATION: Update parent's HasChildren flag if applicable
        if (request.ParentId.HasValue && request.ParentId.Value > 0)
        {
            await UpdateParentHasChildrenFlagAsync(request.ParentId.Value, true, cancellationToken)
                .ConfigureAwait(false);
        }

        return _mapper.Map<TabDto>(createdTab);
    }

    /// <inheritdoc />
    /// <remarks>
    /// MIGRATION: Derived from TabController.UpdateTab(objTab) in TabController.vb (lines 780-814).
    /// Original method:
    /// 1. Checked if TabName or ParentId changed (triggers child path updates)
    /// 2. Called UpdatePortalTabOrder for position changes
    /// 3. Called DataProvider.Instance().UpdateTab with all properties
    /// 4. Updated tab permissions if changed
    /// 5. Updated child tab paths if parent/name changed
    /// 6. Cleared cache
    /// </remarks>
    public async Task<TabDto> UpdateTabAsync(
        int tabId, 
        UpdateTabRequest request, 
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var existingTab = await _tabRepository.GetByIdAsync(tabId, cancellationToken)
            .ConfigureAwait(false);

        if (existingTab is null)
        {
            throw new InvalidOperationException($"Tab with ID {tabId} not found.");
        }

        // MIGRATION: Track if TabName or ParentId changed for child path updates
        var originalTabName = existingTab.TabName;
        var originalParentId = existingTab.ParentId;
        var needsChildPathUpdate = false;

        // Apply partial updates (only update non-null properties)
        if (request.TabName is not null)
        {
            existingTab.TabName = request.TabName;
            needsChildPathUpdate = true;
        }

        if (request.ParentId.HasValue)
        {
            // MIGRATION: Handle parent change
            if (existingTab.ParentId != request.ParentId)
            {
                needsChildPathUpdate = true;
                existingTab.ParentId = request.ParentId;
                existingTab.Level = await CalculateTabLevelAsync(request.ParentId, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        if (request.TabOrder.HasValue)
        {
            existingTab.TabOrder = request.TabOrder.Value;
        }

        if (request.Title is not null)
        {
            existingTab.Title = request.Title;
        }

        if (request.Description is not null)
        {
            existingTab.Description = request.Description;
        }

        if (request.KeyWords is not null)
        {
            existingTab.KeyWords = request.KeyWords;
        }

        if (request.IconFile is not null)
        {
            existingTab.IconFile = request.IconFile;
        }

        if (request.IsVisible.HasValue)
        {
            existingTab.IsVisible = request.IsVisible.Value;
        }

        if (request.DisableLink.HasValue)
        {
            existingTab.DisableLink = request.DisableLink.Value;
        }

        if (request.Url is not null)
        {
            existingTab.Url = request.Url;
        }

        if (request.SkinSrc is not null)
        {
            existingTab.SkinSrc = request.SkinSrc;
        }

        if (request.ContainerSrc is not null)
        {
            existingTab.ContainerSrc = request.ContainerSrc;
        }

        if (request.StartDate.HasValue)
        {
            existingTab.StartDate = request.StartDate.Value;
        }

        if (request.EndDate.HasValue)
        {
            existingTab.EndDate = request.EndDate.Value;
        }

        if (request.RefreshInterval.HasValue)
        {
            existingTab.RefreshInterval = request.RefreshInterval.Value;
        }

        if (request.PageHeadText is not null)
        {
            existingTab.PageHeadText = request.PageHeadText;
        }

        if (request.IsSecure.HasValue)
        {
            existingTab.IsSecure = request.IsSecure.Value;
        }

        // MIGRATION: Regenerate TabPath if TabName or ParentId changed
        if (needsChildPathUpdate)
        {
            existingTab.TabPath = await GenerateTabPathAsync(
                existingTab.ParentId, 
                existingTab.TabName, 
                existingTab.PortalId, 
                cancellationToken).ConfigureAwait(false);

            // MIGRATION: Update child tab paths recursively
            // Original VB.NET called UpdateChildTabPath(objTab.TabID, objTab.PortalID)
            await UpdateChildTabPathsAsync(existingTab.TabId, existingTab.PortalId, cancellationToken)
                .ConfigureAwait(false);
        }

        await _tabRepository.UpdateAsync(existingTab, cancellationToken)
            .ConfigureAwait(false);

        // MIGRATION: Update HasChildren flags if parent changed
        if (originalParentId != existingTab.ParentId)
        {
            // Update old parent's HasChildren flag
            if (originalParentId.HasValue && originalParentId.Value > 0)
            {
                await RecalculateParentHasChildrenFlagAsync(originalParentId.Value, existingTab.PortalId, cancellationToken)
                    .ConfigureAwait(false);
            }

            // Update new parent's HasChildren flag
            if (existingTab.ParentId.HasValue && existingTab.ParentId.Value > 0)
            {
                await UpdateParentHasChildrenFlagAsync(existingTab.ParentId.Value, true, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        return _mapper.Map<TabDto>(existingTab);
    }

    /// <inheritdoc />
    /// <remarks>
    /// MIGRATION: Derived from TabController.DeleteTab(TabId, PortalId) in TabController.vb (lines 446-457).
    /// Original method:
    /// 1. Checked that tab has no children (parent tabs cannot be deleted)
    /// 2. Called DataProvider.Instance().DeleteTab(TabId)
    /// 3. Called UpdatePortalTabOrder with -2 for deletion cleanup
    /// 4. Cleared cache
    /// 
    /// The service implementation enforces the no-children constraint.
    /// </remarks>
    public async Task DeleteTabAsync(
        int tabId, 
        int portalId, 
        CancellationToken cancellationToken = default)
    {
        var tab = await _tabRepository.GetByIdAsync(tabId, cancellationToken)
            .ConfigureAwait(false);

        if (tab is null)
        {
            throw new InvalidOperationException($"Tab with ID {tabId} not found.");
        }

        // MIGRATION: Verify portal ownership for multi-tenant security
        if (tab.PortalId != portalId)
        {
            throw new InvalidOperationException($"Tab with ID {tabId} does not belong to portal {portalId}.");
        }

        // MIGRATION: Preserve original constraint - parent tabs cannot be deleted
        // Original VB.NET: If arrTabs.Count = 0 Then (checking GetTabsByParentId)
        var children = await _tabRepository.GetChildrenAsync(tabId, portalId, cancellationToken)
            .ConfigureAwait(false);

        if (children.Any())
        {
            throw new InvalidOperationException($"Cannot delete tab with ID {tabId} because it has child tabs. Delete or move child tabs first.");
        }

        var parentId = tab.ParentId;

        await _tabRepository.DeleteAsync(tabId, cancellationToken)
            .ConfigureAwait(false);

        // MIGRATION: Update parent's HasChildren flag if applicable
        if (parentId.HasValue && parentId.Value > 0)
        {
            await RecalculateParentHasChildrenFlagAsync(parentId.Value, portalId, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    #endregion

    #region Hierarchy Operations

    /// <inheritdoc />
    /// <remarks>
    /// MIGRATION: Derived from TabController.UpdateTabOrder(objTab) in TabController.vb (lines 816-819).
    /// Original method called DataProvider.Instance().UpdateTabOrder(TabID, TabOrder, Level, ParentId, TabPath).
    /// The order change logic was handled in UpdatePortalTabOrder with Order parameter.
    /// This operation reorders tabs within the same parent without changing hierarchy level.
    /// </remarks>
    public async Task UpdateTabOrderAsync(
        int tabId, 
        int? parentId, 
        int order, 
        CancellationToken cancellationToken = default)
    {
        var tab = await _tabRepository.GetByIdAsync(tabId, cancellationToken)
            .ConfigureAwait(false);

        if (tab is null)
        {
            throw new InvalidOperationException($"Tab with ID {tabId} not found.");
        }

        // MIGRATION: Get sibling tabs to find the swap target
        var effectiveParentId = parentId ?? tab.ParentId ?? -1;
        var siblings = (await _tabRepository.GetChildrenAsync(effectiveParentId, tab.PortalId, cancellationToken)
            .ConfigureAwait(false))
            .Where(t => !t.IsDeleted)
            .OrderBy(t => t.TabOrder)
            .ToList();

        var currentIndex = siblings.FindIndex(t => t.TabId == tabId);
        if (currentIndex == -1)
        {
            throw new InvalidOperationException($"Tab with ID {tabId} not found among siblings.");
        }

        // MIGRATION: Original VB.NET used Order parameter: -1 to move up, 1 to move down
        var targetIndex = currentIndex + order;

        // Bounds check
        if (targetIndex < 0 || targetIndex >= siblings.Count)
        {
            // Already at the boundary, nothing to do
            return;
        }

        // Swap TabOrder values with target tab
        var targetTab = siblings[targetIndex];
        var tempOrder = tab.TabOrder;
        tab.TabOrder = targetTab.TabOrder;
        targetTab.TabOrder = tempOrder;

        // Update both tabs
        await _tabRepository.UpdateAsync(tab, cancellationToken)
            .ConfigureAwait(false);
        await _tabRepository.UpdateAsync(targetTab, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    /// <remarks>
    /// MIGRATION: Derived from TabController.UpdatePortalTabOrder parent change logic in TabController.vb (lines 550-778).
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
    public async Task MoveTabAsync(
        int tabId, 
        int? newParentId, 
        int portalId, 
        CancellationToken cancellationToken = default)
    {
        var tab = await _tabRepository.GetByIdAsync(tabId, cancellationToken)
            .ConfigureAwait(false);

        if (tab is null)
        {
            throw new InvalidOperationException($"Tab with ID {tabId} not found.");
        }

        // MIGRATION: Verify portal ownership for multi-tenant security
        if (tab.PortalId != portalId)
        {
            throw new InvalidOperationException($"Tab with ID {tabId} does not belong to portal {portalId}.");
        }

        // MIGRATION: Prevent moving tab to become its own descendant (circular reference)
        if (newParentId.HasValue && newParentId.Value > 0)
        {
            if (await IsDescendantOfAsync(newParentId.Value, tabId, portalId, cancellationToken)
                .ConfigureAwait(false))
            {
                throw new InvalidOperationException($"Cannot move tab {tabId} under its own descendant {newParentId}.");
            }
        }

        var oldParentId = tab.ParentId;

        // MIGRATION: VB.NET used -1 for root level, convert null to -1 for consistency
        var effectiveNewParentId = newParentId ?? -1;

        // Use repository's MoveTabAsync which handles the complex reordering
        await _tabRepository.MoveTabAsync(tabId, effectiveNewParentId, portalId, cancellationToken)
            .ConfigureAwait(false);

        // MIGRATION: Update child tab paths recursively after move
        await UpdateChildTabPathsAsync(tabId, portalId, cancellationToken)
            .ConfigureAwait(false);

        // MIGRATION: Update HasChildren flags for old and new parents
        if (oldParentId.HasValue && oldParentId.Value > 0)
        {
            await RecalculateParentHasChildrenFlagAsync(oldParentId.Value, portalId, cancellationToken)
                .ConfigureAwait(false);
        }

        if (newParentId.HasValue && newParentId.Value > 0)
        {
            await UpdateParentHasChildrenFlagAsync(newParentId.Value, true, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    #endregion

    #region Private Helper Methods

    /// <summary>
    /// Generates the tab path based on parent hierarchy and tab name.
    /// </summary>
    /// <param name="parentId">The parent tab identifier.</param>
    /// <param name="tabName">The name of the tab.</param>
    /// <param name="portalId">The portal identifier.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The generated tab path in format "//ParentPath//TabName".</returns>
    /// <remarks>
    /// MIGRATION: Derived from TabController.GenerateTabPath(ParentId, TabName) in TabController.vb.
    /// Original method was a shared/static function that concatenated parent tab path with new tab name.
    /// </remarks>
    private async Task<string> GenerateTabPathAsync(
        int? parentId, 
        string tabName, 
        int portalId,
        CancellationToken cancellationToken)
    {
        // MIGRATION: Original VB.NET format used "//" as separator
        // Format: //TabName for root tabs, //ParentPath//TabName for child tabs
        if (!parentId.HasValue || parentId.Value <= 0)
        {
            // Root level tab
            return $"//{tabName}";
        }

        // Get parent tab to retrieve its path
        var parentTab = await _tabRepository.GetByIdAsync(parentId.Value, cancellationToken)
            .ConfigureAwait(false);

        if (parentTab is null || string.IsNullOrEmpty(parentTab.TabPath))
        {
            // Fallback to root if parent not found
            return $"//{tabName}";
        }

        return $"{parentTab.TabPath}//{tabName}";
    }

    /// <summary>
    /// Calculates the hierarchical level for a tab based on its parent.
    /// </summary>
    /// <param name="parentId">The parent tab identifier.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The level (0 for root, parent.Level + 1 for children).</returns>
    /// <remarks>
    /// MIGRATION: Level calculation was implicit in VB.NET UpdatePortalTabOrder method.
    /// Level is 0 for root tabs and increments for each level of nesting.
    /// </remarks>
    private async Task<int> CalculateTabLevelAsync(
        int? parentId, 
        CancellationToken cancellationToken)
    {
        if (!parentId.HasValue || parentId.Value <= 0)
        {
            return 0;
        }

        var parentTab = await _tabRepository.GetByIdAsync(parentId.Value, cancellationToken)
            .ConfigureAwait(false);

        return parentTab is null ? 0 : parentTab.Level + 1;
    }

    /// <summary>
    /// Calculates the tab order for a new tab, placing it at the end of siblings.
    /// </summary>
    /// <param name="parentId">The parent tab identifier.</param>
    /// <param name="portalId">The portal identifier.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The calculated tab order value.</returns>
    /// <remarks>
    /// MIGRATION: Derived from VB.NET AddTab logic where new tabs were placed after existing siblings.
    /// Original used (arrTabs.Count * 2) - 1 formula for host tabs.
    /// </remarks>
    private async Task<int> CalculateNewTabOrderAsync(
        int? parentId, 
        int portalId,
        CancellationToken cancellationToken)
    {
        var effectiveParentId = parentId ?? -1;
        var siblings = await _tabRepository.GetChildrenAsync(effectiveParentId, portalId, cancellationToken)
            .ConfigureAwait(false);

        var maxOrder = siblings.Any() 
            ? siblings.Max(t => t.TabOrder) 
            : 0;

        // MIGRATION: Original VB.NET used increments of 2 for tab ordering
        return maxOrder + 2;
    }

    /// <summary>
    /// Updates the HasChildren flag for a parent tab.
    /// </summary>
    /// <param name="parentId">The parent tab identifier.</param>
    /// <param name="hasChildren">The new HasChildren value.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    private async Task UpdateParentHasChildrenFlagAsync(
        int parentId, 
        bool hasChildren,
        CancellationToken cancellationToken)
    {
        var parentTab = await _tabRepository.GetByIdAsync(parentId, cancellationToken)
            .ConfigureAwait(false);

        if (parentTab is not null && parentTab.HasChildren != hasChildren)
        {
            parentTab.HasChildren = hasChildren;
            await _tabRepository.UpdateAsync(parentTab, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Recalculates the HasChildren flag for a parent tab based on actual children.
    /// </summary>
    /// <param name="parentId">The parent tab identifier.</param>
    /// <param name="portalId">The portal identifier.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    private async Task RecalculateParentHasChildrenFlagAsync(
        int parentId, 
        int portalId,
        CancellationToken cancellationToken)
    {
        var children = await _tabRepository.GetChildrenAsync(parentId, portalId, cancellationToken)
            .ConfigureAwait(false);

        var hasChildren = children.Any(c => !c.IsDeleted);

        await UpdateParentHasChildrenFlagAsync(parentId, hasChildren, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Updates the TabPath for all child tabs of a given parent recursively.
    /// </summary>
    /// <param name="parentTabId">The parent tab identifier.</param>
    /// <param name="portalId">The portal identifier.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <remarks>
    /// MIGRATION: Derived from TabController.UpdateChildTabPath(intTabid, portalId) in TabController.vb (lines 306-320).
    /// Original method recursively updated child tab paths when parent TabName or ParentId changed.
    /// </remarks>
    private async Task UpdateChildTabPathsAsync(
        int parentTabId, 
        int portalId,
        CancellationToken cancellationToken)
    {
        var parentTab = await _tabRepository.GetByIdAsync(parentTabId, cancellationToken)
            .ConfigureAwait(false);

        if (parentTab is null)
        {
            return;
        }

        var children = await _tabRepository.GetChildrenAsync(parentTabId, portalId, cancellationToken)
            .ConfigureAwait(false);

        foreach (var child in children)
        {
            // MIGRATION: Original VB.NET GenerateTabPath(objtab.ParentId, objtab.TabName)
            var newTabPath = $"{parentTab.TabPath}//{child.TabName}";

            if (child.TabPath != newTabPath)
            {
                child.TabPath = newTabPath;
                await _tabRepository.UpdateAsync(child, cancellationToken)
                    .ConfigureAwait(false);

                // Recursively update grandchildren
                await UpdateChildTabPathsAsync(child.TabId, portalId, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Checks if a tab is a descendant of another tab.
    /// </summary>
    /// <param name="tabId">The tab to check.</param>
    /// <param name="potentialAncestorId">The potential ancestor tab identifier.</param>
    /// <param name="portalId">The portal identifier.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>True if tabId is a descendant of potentialAncestorId.</returns>
    /// <remarks>
    /// MIGRATION: This validation prevents circular references when moving tabs.
    /// Not present in original VB.NET but added for data integrity.
    /// </remarks>
    private async Task<bool> IsDescendantOfAsync(
        int tabId, 
        int potentialAncestorId,
        int portalId,
        CancellationToken cancellationToken)
    {
        var tab = await _tabRepository.GetByIdAsync(tabId, cancellationToken)
            .ConfigureAwait(false);

        if (tab is null)
        {
            return false;
        }

        // If this tab is the potential ancestor, return true
        if (tab.TabId == potentialAncestorId)
        {
            return true;
        }

        // Check parent recursively
        if (!tab.ParentId.HasValue || tab.ParentId.Value <= 0)
        {
            return false;
        }

        return await IsDescendantOfAsync(tab.ParentId.Value, potentialAncestorId, portalId, cancellationToken)
            .ConfigureAwait(false);
    }

    #endregion
}
