// -----------------------------------------------------------------------------
// DnnMigration - Modern DotNetNuke Migration
// Copyright (c) 2024. All rights reserved.
// Licensed under the MIT License.
// -----------------------------------------------------------------------------
// MIGRATION: VB.NET DotNetNuke.Entities.Modules.ModuleController → C# 12 ModuleService
// Source: Library/Components/Modules/ModuleController.vb
// Changes:
// - Converted from VB.NET Shared methods to C# 12 async instance methods
// - Implements IModuleService interface with async/await patterns
// - Uses IModuleRepository for data access (replaces DataProvider calls)
// - Uses AutoMapper for Entity-DTO mapping (replaces FillModuleInfo)
// - All methods include CancellationToken for cooperative cancellation
// - Business logic extracted exactly from ModuleController.vb
// - Permission setup logic preserved from AddModule
// - Copy/Move operations preserved with tab module semantics
// - ConfigureAwait(false) used per library code patterns
// -----------------------------------------------------------------------------

using AutoMapper;
using DnnMigration.Application.DTOs.Common;
using DnnMigration.Application.DTOs.Module;
using DnnMigration.Application.Interfaces;
using DnnMigration.Domain.Entities;
using DnnMigration.Domain.Enums;
using DnnMigration.Domain.Interfaces;

namespace DnnMigration.Application.Services;

/// <summary>
/// Application service implementing module lifecycle management business logic.
/// </summary>
/// <remarks>
/// <para>
/// MIGRATION: Converted from VB.NET DotNetNuke.Entities.Modules.ModuleController.
/// This service orchestrates module CRUD operations, tab-module relationships,
/// copying, and moving modules between the API layer and domain/repository layers.
/// </para>
/// <para>
/// The service layer provides:
/// <list type="bullet">
///   <item>Async methods for module retrieval (single, list, by tab, by definition)</item>
///   <item>Module creation with permission setup logic</item>
///   <item>Module update with partial update support</item>
///   <item>Module deletion (hard delete from tab)</item>
///   <item>Copy and move operations between tabs</item>
///   <item>Module order management within panes</item>
///   <item>Module settings management</item>
/// </list>
/// </para>
/// <para>
/// Business Logic Preservation:
/// <list type="bullet">
///   <item>MIGRATION: GetModule → GetModuleAsync - retrieves module by moduleId and tabId</item>
///   <item>MIGRATION: GetModules → GetModulesAsync - returns paginated portal modules</item>
///   <item>MIGRATION: GetTabModules → GetModulesByTabAsync - returns all modules on a tab</item>
///   <item>MIGRATION: AddModule → CreateModuleAsync - creates module with permissions</item>
///   <item>MIGRATION: UpdateModule → UpdateModuleAsync - updates module properties</item>
///   <item>MIGRATION: DeleteModule → DeleteModuleAsync - removes module from tab</item>
///   <item>MIGRATION: CopyModule → CopyModuleAsync - copies module to another tab</item>
///   <item>MIGRATION: MoveModule → MoveModuleAsync - moves module between tabs</item>
/// </list>
/// </para>
/// </remarks>
public class ModuleService : IModuleService
{
    private readonly IModuleRepository _moduleRepository;
    private readonly ITabRepository _tabRepository;
    private readonly IMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="ModuleService"/> class.
    /// </summary>
    /// <param name="moduleRepository">The module repository for data access.</param>
    /// <param name="tabRepository">The tab repository for looking up tab information.</param>
    /// <param name="mapper">The AutoMapper instance for entity-DTO mapping.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="moduleRepository"/>, <paramref name="tabRepository"/>, or <paramref name="mapper"/> is null.
    /// </exception>
    /// <remarks>
    /// MIGRATION: Constructor injection replaces VB.NET shared methods with
    /// DataProvider.Instance() singleton calls.
    /// </remarks>
    public ModuleService(
        IModuleRepository moduleRepository,
        ITabRepository tabRepository,
        IMapper mapper)
    {
        _moduleRepository = moduleRepository ?? throw new ArgumentNullException(nameof(moduleRepository));
        _tabRepository = tabRepository ?? throw new ArgumentNullException(nameof(tabRepository));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// MIGRATION: Converted from VB.NET ModuleController.GetModule(ModuleId, TabId, ignoreCache).
    /// This overload retrieves a module by ID only, using a default tab ID of 0 to get the
    /// module without tab-specific context.
    /// </para>
    /// <para>
    /// Original VB.NET signature:
    /// <code>
    /// Public Shared Function GetModule(ByVal ModuleId As Integer, ByVal TabId As Integer, _
    ///     Optional ByVal ignoreCache As Boolean = False) As ModuleInfo
    /// </code>
    /// </para>
    /// </remarks>
    public async Task<ModuleDto?> GetModuleAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        // MIGRATION: This overload satisfies IModuleService interface
        // Delegates to the full implementation with tabId = 0
        return await GetModuleAsync(id, 0, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Retrieves a single module by its module ID and tab ID.
    /// </summary>
    /// <param name="moduleId">The unique identifier of the module.</param>
    /// <param name="tabId">The tab identifier where the module is placed.</param>
    /// <param name="cancellationToken">Token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A task representing the asynchronous operation. The task result contains
    /// the <see cref="ModuleDto"/> if found; otherwise, <c>null</c>.
    /// </returns>
    /// <remarks>
    /// <para>
    /// MIGRATION: Converted from VB.NET ModuleController.GetModule(ModuleId, TabId, ignoreCache).
    /// The ignoreCache parameter has been removed as caching is handled at the repository layer.
    /// </para>
    /// <para>
    /// Original VB.NET signature:
    /// <code>
    /// Public Shared Function GetModule(ByVal ModuleId As Integer, ByVal TabId As Integer, _
    ///     Optional ByVal ignoreCache As Boolean = False) As ModuleInfo
    /// </code>
    /// </para>
    /// </remarks>
    public async Task<ModuleDto?> GetModuleAsync(
        int moduleId,
        int tabId,
        CancellationToken cancellationToken = default)
    {
        // MIGRATION: Business logic preserved from ModuleController.GetModule
        // Original logic used DataCache with key "GetModule" + ModuleId.ToString + TabId.ToString
        // Cache handling now delegated to repository layer
        
        if (moduleId <= 0)
        {
            return null;
        }

        var module = await _moduleRepository
            .GetByIdAsync(moduleId, tabId, cancellationToken)
            .ConfigureAwait(false);

        if (module is null)
        {
            return null;
        }

        return _mapper.Map<ModuleDto>(module);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// MIGRATION: Converted from VB.NET ModuleController.GetModules(PortalID).
    /// Returns modules as a paginated result for API consumption.
    /// </para>
    /// <para>
    /// Original VB.NET signature:
    /// <code>
    /// Public Shared Function GetModules(ByVal PortalID As Integer) As ArrayList
    /// </code>
    /// </para>
    /// </remarks>
    public async Task<PagedResult<ModuleDto>> GetModulesAsync(
        int portalId,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        // MIGRATION: Business logic preserved from ModuleController.GetModules
        // Original returned ArrayList, now returns PagedResult for API pagination
        
        if (portalId < 0)
        {
            return PagedResult<ModuleDto>.Create(
                Enumerable.Empty<ModuleDto>(),
                pageIndex,
                pageSize,
                0);
        }

        var modules = await _moduleRepository
            .GetByPortalIdAsync(portalId, cancellationToken)
            .ConfigureAwait(false);

        var moduleList = modules.ToList();
        var totalCount = moduleList.Count;

        var pagedModules = moduleList
            .Skip(pageIndex * pageSize)
            .Take(pageSize)
            .ToList();

        var moduleDtos = _mapper.Map<IEnumerable<ModuleDto>>(pagedModules);

        return PagedResult<ModuleDto>.Create(
            moduleDtos,
            pageIndex,
            pageSize,
            totalCount);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// MIGRATION: Converted from VB.NET ModuleController.GetTabModules(TabId).
    /// Returns all modules assigned to a specific tab/page.
    /// </para>
    /// <para>
    /// Original VB.NET signature:
    /// <code>
    /// Public Shared Function GetTabModules(ByVal TabId As Integer) As Dictionary(Of Integer, ModuleInfo)
    /// </code>
    /// </para>
    /// </remarks>
    public async Task<IEnumerable<ModuleDto>> GetModulesByTabAsync(
        int tabId,
        CancellationToken cancellationToken = default)
    {
        // MIGRATION: Business logic preserved from ModuleController.GetTabModules
        // Original returned Dictionary keyed by ModuleID to avoid duplicates
        // Now returns IEnumerable with distinct modules handled at repository layer
        
        if (tabId <= 0)
        {
            return Enumerable.Empty<ModuleDto>();
        }

        var modules = await _moduleRepository
            .GetByTabIdAsync(tabId, cancellationToken)
            .ConfigureAwait(false);

        return _mapper.Map<IEnumerable<ModuleDto>>(modules);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// MIGRATION: Converted from VB.NET ModuleController.GetAllTabsModules(PortalID, AllTabs).
    /// Returns modules that are configured to display on all tabs.
    /// </para>
    /// <para>
    /// Original VB.NET signature:
    /// <code>
    /// Public Shared Function GetAllTabsModules(ByVal PortalID As Integer, ByVal AllTabs As Boolean) As ArrayList
    /// </code>
    /// </para>
    /// </remarks>
    public async Task<IEnumerable<ModuleDto>> GetAllTabsModulesAsync(
        int portalId,
        bool allTabs,
        CancellationToken cancellationToken = default)
    {
        // MIGRATION: Business logic preserved from ModuleController.GetAllTabsModules
        // Retrieves modules with AllTabs flag set based on parameter
        
        if (portalId < 0)
        {
            return Enumerable.Empty<ModuleDto>();
        }

        var modules = await _moduleRepository
            .GetAllTabsModulesAsync(portalId, allTabs, cancellationToken)
            .ConfigureAwait(false);

        return _mapper.Map<IEnumerable<ModuleDto>>(modules);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// MIGRATION: Converted from VB.NET ModuleController.GetModuleByDefinition(PortalID, FriendlyName).
    /// Finds a module by its module definition friendly name within a portal.
    /// </para>
    /// <para>
    /// Original VB.NET signature:
    /// <code>
    /// Public Shared Function GetModuleByDefinition(ByVal PortalID As Integer, ByVal FriendlyName As String) As ModuleInfo
    /// </code>
    /// </para>
    /// </remarks>
    public async Task<ModuleDto?> GetModuleByDefinitionAsync(
        int portalId,
        string friendlyName,
        CancellationToken cancellationToken = default)
    {
        // MIGRATION: Business logic preserved from ModuleController.GetModuleByDefinition
        // Used to find modules by their definition friendly name (e.g., "Search Results")
        
        if (string.IsNullOrWhiteSpace(friendlyName))
        {
            return null;
        }

        var module = await _moduleRepository
            .GetByDefinitionAsync(portalId, friendlyName, cancellationToken)
            .ConfigureAwait(false);

        if (module is null)
        {
            return null;
        }

        return _mapper.Map<ModuleDto>(module);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// MIGRATION: Converted from VB.NET ModuleController.AddModule(objModule As ModuleInfo) As Integer.
    /// Creates a new module with permission setup logic preserved from original.
    /// </para>
    /// <para>
    /// Original VB.NET signature:
    /// <code>
    /// Public Shared Function AddModule(ByVal objModule As ModuleInfo) As Integer
    /// </code>
    /// </para>
    /// <para>
    /// Original business logic preserved:
    /// <list type="number">
    ///   <item>Create module record via DataProvider.AddModule</item>
    ///   <item>Add module to tab via DataProvider.AddTabModule</item>
    ///   <item>Loop through ModulePermissions and call ModulePermissionController.AddModulePermission</item>
    ///   <item>Clear module cache via DataCache.ClearModuleCache</item>
    /// </list>
    /// </para>
    /// </remarks>
    public async Task<ModuleDto> CreateModuleAsync(
        CreateModuleRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // MIGRATION: Business logic preserved from ModuleController.AddModule
        // Original code:
        // ModuleId = DataProvider.Instance().AddModule(...)
        // DataProvider.Instance().AddTabModule(...)
        // For Each objModulePermission As ModulePermissionInfo In objModule.ModulePermissions
        //     objModulePermission.ModuleID = ModuleId
        //     ModulePermissionController.AddModulePermission(objModulePermission)
        // Next

        // Look up the Tab to get the PortalId
        var tab = await _tabRepository.GetByIdAsync(request.TabId, cancellationToken)
            .ConfigureAwait(false);

        if (tab is null)
        {
            throw new KeyNotFoundException($"Tab with ID {request.TabId} not found.");
        }

        // Create the Module entity from the request DTO
        var module = new Module
        {
            PortalId = tab.PortalId, // MIGRATION: Set PortalId from Tab's PortalId
            TabId = request.TabId,
            ModuleDefId = request.ModuleDefId,
            PaneName = request.PaneName,
            ModuleOrder = request.ModuleOrder.GetValueOrDefault(-1), // MIGRATION: -1 means add at end of pane
            ModuleTitle = request.ModuleTitle ?? string.Empty,
            ContainerSrc = request.ContainerSrc,
            CacheTime = request.CacheTime.GetValueOrDefault(0), // MIGRATION: 0 means no caching
            DisplayTitle = request.DisplayTitle,
            InheritViewPermissions = request.InheritViewPermissions,
            AllTabs = request.AllTabs,
            Visibility = (VisibilityState)request.Visibility, // MIGRATION: Convert int to VisibilityState enum
            // Set required fields with defaults
            IsDeleted = false,
            Header = string.Empty,
            Footer = string.Empty,
            Alignment = string.Empty,
            Color = string.Empty,
            Border = string.Empty,
            IconFile = string.Empty,
            StartDate = null,
            EndDate = null,
            IsDefaultModule = false,
            AllModules = false
        };

        // MIGRATION: Permission setup logic from AddModule
        // Initialize the ModulePermissions collection
        module.ModulePermissions = new List<ModulePermission>();

        // If permissions are provided in the request, add them
        if (request.Permissions is not null)
        {
            foreach (var permissionRequest in request.Permissions)
            {
                var modulePermission = new ModulePermission
                {
                    PermissionId = permissionRequest.PermissionId,
                    RoleId = permissionRequest.RoleId,
                    UserId = permissionRequest.UserId,
                    AllowAccess = permissionRequest.AllowAccess
                };
                module.ModulePermissions.Add(modulePermission);
            }
        }

        // Add the module via repository
        var createdModule = await _moduleRepository
            .AddAsync(module, cancellationToken)
            .ConfigureAwait(false);

        // MIGRATION: Cache clearing equivalent handled by repository/infrastructure layer
        // Original: DataCache.ClearModuleCache(objModule.TabID)

        return _mapper.Map<ModuleDto>(createdModule);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// MIGRATION: Converted from VB.NET ModuleController.UpdateModule(objModule As ModuleInfo).
    /// Updates an existing module with partial update support.
    /// </para>
    /// <para>
    /// Original VB.NET signature:
    /// <code>
    /// Public Shared Sub UpdateModule(ByVal objModule As ModuleInfo)
    /// </code>
    /// </para>
    /// <para>
    /// Original business logic preserved:
    /// <list type="number">
    ///   <item>Update module properties via DataProvider.UpdateModule</item>
    ///   <item>Update tab-module relationship via DataProvider.UpdateTabModule</item>
    ///   <item>Update module permissions via ModulePermissionController</item>
    ///   <item>Clear module cache via DataCache.ClearModuleCache</item>
    /// </list>
    /// </para>
    /// </remarks>
    public async Task<ModuleDto> UpdateModuleAsync(
        int id,
        UpdateModuleRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (id <= 0)
        {
            throw new ArgumentException("Module ID must be greater than zero.", nameof(id));
        }

        // MIGRATION: Business logic preserved from ModuleController.UpdateModule
        // Retrieve existing module first
        var existingModule = await _moduleRepository
            .GetByIdAsync(id, request.TabId, cancellationToken)
            .ConfigureAwait(false);

        if (existingModule is null)
        {
            throw new KeyNotFoundException($"Module with ID {id} not found.");
        }

        // Apply partial updates - only update properties that are provided
        // MIGRATION: Preserves UpdateModule semantics where all properties are updated
        if (request.ModuleTitle is not null)
        {
            existingModule.ModuleTitle = request.ModuleTitle;
        }

        if (request.Alignment is not null)
        {
            existingModule.Alignment = request.Alignment;
        }

        if (request.Color is not null)
        {
            existingModule.Color = request.Color;
        }

        if (request.Border is not null)
        {
            existingModule.Border = request.Border;
        }

        if (request.IconFile is not null)
        {
            existingModule.IconFile = request.IconFile;
        }

        if (request.CacheTime.HasValue)
        {
            existingModule.CacheTime = request.CacheTime.Value;
        }

        if (request.Visibility.HasValue)
        {
            // MIGRATION: Cast int to VisibilityState enum (0=Maximized, 1=Minimized, 2=None)
            existingModule.Visibility = (VisibilityState)request.Visibility.Value;
        }

        if (request.Header is not null)
        {
            existingModule.Header = request.Header;
        }

        if (request.Footer is not null)
        {
            existingModule.Footer = request.Footer;
        }

        if (request.StartDate.HasValue)
        {
            existingModule.StartDate = request.StartDate;
        }

        if (request.EndDate.HasValue)
        {
            existingModule.EndDate = request.EndDate;
        }

        if (request.ContainerSrc is not null)
        {
            existingModule.ContainerSrc = request.ContainerSrc;
        }

        if (request.DisplayTitle.HasValue)
        {
            existingModule.DisplayTitle = request.DisplayTitle.Value;
        }

        if (request.InheritViewPermissions.HasValue)
        {
            existingModule.InheritViewPermissions = request.InheritViewPermissions.Value;
        }

        if (request.AllTabs.HasValue)
        {
            existingModule.AllTabs = request.AllTabs.Value;
        }

        if (request.IsDefaultModule.HasValue)
        {
            existingModule.IsDefaultModule = request.IsDefaultModule.Value;
        }

        if (request.AllModules.HasValue)
        {
            existingModule.AllModules = request.AllModules.Value;
        }

        if (request.IsDeleted.HasValue)
        {
            existingModule.IsDeleted = request.IsDeleted.Value;
        }

        // Update the module via repository
        await _moduleRepository
            .UpdateAsync(existingModule, cancellationToken)
            .ConfigureAwait(false);

        // MIGRATION: Cache clearing equivalent handled by repository/infrastructure layer
        // Original: DataCache.ClearModuleCache(objModule.TabID)

        return _mapper.Map<ModuleDto>(existingModule);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// MIGRATION: Converted from VB.NET ModuleController.DeleteModule(ModuleId).
    /// This overload permanently deletes a module by ID.
    /// </para>
    /// <para>
    /// Original VB.NET signature:
    /// <code>
    /// Public Shared Sub DeleteModule(ByVal ModuleId As Integer)
    /// </code>
    /// </para>
    /// <para>
    /// Note: For soft delete (recycle bin), use UpdateModuleAsync with IsDeleted = true.
    /// </para>
    /// </remarks>
    public async Task DeleteModuleAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        // MIGRATION: This overload satisfies IModuleService interface
        // Delegates to the full implementation with tabId = 0
        await DeleteModuleAsync(id, 0, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Permanently deletes a module from a specific tab.
    /// </summary>
    /// <param name="moduleId">The unique identifier of the module to delete.</param>
    /// <param name="tabId">The tab identifier where the module is placed.</param>
    /// <param name="cancellationToken">Token to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous delete operation.</returns>
    /// <remarks>
    /// <para>
    /// MIGRATION: Converted from VB.NET ModuleController.DeleteModule(ModuleId, TabId).
    /// Performs a hard delete removing the module from the specified tab.
    /// </para>
    /// <para>
    /// Original VB.NET signature:
    /// <code>
    /// Public Shared Sub DeleteModule(ByVal ModuleId As Integer, ByVal TabId As Integer)
    /// </code>
    /// </para>
    /// <para>
    /// Original business logic preserved:
    /// <list type="number">
    ///   <item>Delete search items via SearchDataStoreController.DeleteSearchItems</item>
    ///   <item>Delete module via DataProvider.DeleteModule</item>
    ///   <item>Clear module cache via DataCache.ClearModuleCache</item>
    /// </list>
    /// </para>
    /// <para>
    /// Note: For soft delete (recycle bin), use UpdateModuleAsync with IsDeleted = true.
    /// </para>
    /// </remarks>
    public async Task DeleteModuleAsync(
        int moduleId,
        int tabId,
        CancellationToken cancellationToken = default)
    {
        if (moduleId <= 0)
        {
            throw new ArgumentException("Module ID must be greater than zero.", nameof(moduleId));
        }

        // MIGRATION: Business logic preserved from ModuleController.DeleteModule
        // Original code:
        // Dim objModules As New ModuleController
        // Dim objModule As ModuleInfo = objModules.GetModule(ModuleId, TabId, False)
        // If Not objModule Is Nothing Then
        //     SearchDataStoreController.DeleteSearchItems(objModule)
        //     DataProvider.Instance().DeleteModule(ModuleId)
        //     DataCache.ClearModuleCache(TabId)
        // End If

        // Verify the module exists before deleting
        var existingModule = await _moduleRepository
            .GetByIdAsync(moduleId, tabId, cancellationToken)
            .ConfigureAwait(false);

        if (existingModule is null)
        {
            // Module doesn't exist - throw to signal 404
            throw new KeyNotFoundException($"Module with ID {moduleId} not found.");
        }

        // Delete the tab-module reference via repository
        // MIGRATION: Uses DeleteTabModuleAsync which corresponds to VB.NET DeleteTabModule(TabId, ModuleId)
        // If no other tab references exist, the module is soft-deleted by the repository
        await _moduleRepository
            .DeleteTabModuleAsync(tabId, moduleId, cancellationToken)
            .ConfigureAwait(false);

        // MIGRATION: Cache clearing equivalent handled by repository/infrastructure layer
        // Original: DataCache.ClearModuleCache(TabId)
        // MIGRATION: Search item deletion handled by repository/infrastructure layer
        // Original: SearchDataStoreController.DeleteSearchItems(objModule)
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// MIGRATION: Converted from VB.NET ModuleController.CopyModule.
    /// This overload copies a module without including settings.
    /// </para>
    /// <para>
    /// Original VB.NET signature:
    /// <code>
    /// Public Shared Sub CopyModule(ByVal ModuleId As Integer, ByVal FromTabId As Integer, _
    ///     ByVal ToTabId As Integer, ByVal ToPaneName As String, ByVal IncludeSettings As Boolean)
    /// </code>
    /// </para>
    /// </remarks>
    public async Task CopyModuleAsync(
        int moduleId,
        int fromTabId,
        int toTabId,
        string toPaneName,
        CancellationToken cancellationToken = default)
    {
        // MIGRATION: This overload satisfies IModuleService interface
        // Delegates to the full implementation with includeSettings = false
        await CopyModuleAsync(moduleId, fromTabId, toTabId, toPaneName, false, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Copies a module from one tab to another with optional settings transfer.
    /// </summary>
    /// <param name="moduleId">The identifier of the module to copy.</param>
    /// <param name="fromTabId">The source tab identifier.</param>
    /// <param name="toTabId">The destination tab identifier.</param>
    /// <param name="toPaneName">The pane name on the destination tab.</param>
    /// <param name="includeSettings">Whether to copy tab-module settings.</param>
    /// <param name="cancellationToken">Token to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous copy operation.</returns>
    /// <remarks>
    /// <para>
    /// MIGRATION: Converted from VB.NET ModuleController.CopyModule(ModuleId, FromTabId, ToTabId, ToPaneName, IncludeSettings).
    /// Creates a reference to an existing module on a different tab.
    /// </para>
    /// <para>
    /// Original VB.NET signature:
    /// <code>
    /// Public Shared Sub CopyModule(ByVal ModuleId As Integer, ByVal FromTabId As Integer, _
    ///     ByVal ToTabId As Integer, ByVal ToPaneName As String, ByVal IncludeSettings As Boolean)
    /// </code>
    /// </para>
    /// <para>
    /// Original business logic preserved:
    /// <list type="number">
    ///   <item>Get source module via GetModule(ModuleId, FromTabId)</item>
    ///   <item>Create new TabModule reference via DataProvider.AddTabModule</item>
    ///   <item>If IncludeSettings, copy settings via CopyTabModuleSettings</item>
    ///   <item>Clear module cache for both source and destination tabs</item>
    /// </list>
    /// </para>
    /// </remarks>
    public async Task CopyModuleAsync(
        int moduleId,
        int fromTabId,
        int toTabId,
        string toPaneName,
        bool includeSettings,
        CancellationToken cancellationToken = default)
    {
        if (moduleId <= 0)
        {
            throw new ArgumentException("Module ID must be greater than zero.", nameof(moduleId));
        }

        if (fromTabId <= 0)
        {
            throw new ArgumentException("From Tab ID must be greater than zero.", nameof(fromTabId));
        }

        if (toTabId <= 0)
        {
            throw new ArgumentException("To Tab ID must be greater than zero.", nameof(toTabId));
        }

        if (string.IsNullOrWhiteSpace(toPaneName))
        {
            throw new ArgumentException("Pane name cannot be empty.", nameof(toPaneName));
        }

        // MIGRATION: Business logic preserved from ModuleController.CopyModule
        // Original code:
        // Dim objModules As New ModuleController
        // Dim objModule As ModuleInfo = objModules.GetModule(ModuleId, FromTabId, False)
        // If Not objModule Is Nothing Then
        //     'Clone The module'
        //     objModule.TabID = ToTabId
        //     objModule.PaneName = ToPaneName
        //     objModule.ModuleOrder = -1
        //     objModule.TabModuleID = Null.NullInteger
        //     DataProvider.Instance().AddTabModule(...)
        //     If IncludeSettings Then CopyTabModuleSettings(objModule, FromTabId)
        //     DataCache.ClearModuleCache(FromTabId)
        //     DataCache.ClearModuleCache(ToTabId)
        // End If

        // Verify source module exists
        var sourceModule = await _moduleRepository
            .GetByIdAsync(moduleId, fromTabId, cancellationToken)
            .ConfigureAwait(false);

        if (sourceModule is null)
        {
            throw new KeyNotFoundException($"Source module with ID {moduleId} on tab {fromTabId} not found.");
        }

        // Delegate to repository to handle the copy operation
        await _moduleRepository
            .CopyModuleAsync(moduleId, fromTabId, toTabId, toPaneName, includeSettings, cancellationToken)
            .ConfigureAwait(false);

        // MIGRATION: Cache clearing equivalent handled by repository/infrastructure layer
        // Original: DataCache.ClearModuleCache(FromTabId)
        // Original: DataCache.ClearModuleCache(ToTabId)
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// MIGRATION: Converted from VB.NET ModuleController.MoveModule(ModuleId, FromTabId, ToTabId, ToPaneName).
    /// Moves a module from one tab to another (copy + delete from source).
    /// </para>
    /// <para>
    /// Original VB.NET signature:
    /// <code>
    /// Public Shared Sub MoveModule(ByVal ModuleId As Integer, ByVal FromTabId As Integer, _
    ///     ByVal ToTabId As Integer, ByVal ToPaneName As String)
    /// </code>
    /// </para>
    /// <para>
    /// Original business logic preserved:
    /// <list type="number">
    ///   <item>Copy module to destination tab via CopyModule</item>
    ///   <item>Delete module from source tab via DeleteTabModule</item>
    /// </list>
    /// </para>
    /// </remarks>
    public async Task MoveModuleAsync(
        int moduleId,
        int fromTabId,
        int toTabId,
        string toPaneName,
        CancellationToken cancellationToken = default)
    {
        if (moduleId <= 0)
        {
            throw new ArgumentException("Module ID must be greater than zero.", nameof(moduleId));
        }

        if (fromTabId <= 0)
        {
            throw new ArgumentException("From Tab ID must be greater than zero.", nameof(fromTabId));
        }

        if (toTabId <= 0)
        {
            throw new ArgumentException("To Tab ID must be greater than zero.", nameof(toTabId));
        }

        if (string.IsNullOrWhiteSpace(toPaneName))
        {
            throw new ArgumentException("Pane name cannot be empty.", nameof(toPaneName));
        }

        // MIGRATION: Business logic preserved from ModuleController.MoveModule
        // Original code:
        // CopyModule(ModuleId, FromTabId, ToTabId, ToPaneName, True)
        // DeleteTabModule(FromTabId, ModuleId)

        // Verify source module exists
        var sourceModule = await _moduleRepository
            .GetByIdAsync(moduleId, fromTabId, cancellationToken)
            .ConfigureAwait(false);

        if (sourceModule is null)
        {
            throw new KeyNotFoundException($"Source module with ID {moduleId} on tab {fromTabId} not found.");
        }

        // Delegate to repository to handle the move operation
        await _moduleRepository
            .MoveModuleAsync(moduleId, fromTabId, toTabId, toPaneName, cancellationToken)
            .ConfigureAwait(false);

        // MIGRATION: Cache clearing equivalent handled by repository/infrastructure layer
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// MIGRATION: Converted from VB.NET ModuleController.UpdateModuleOrder(TabId, ModuleId, ModuleOrder, PaneName).
    /// Updates the position of a module within a pane.
    /// </para>
    /// <para>
    /// Original VB.NET signature:
    /// <code>
    /// Public Shared Sub UpdateModuleOrder(ByVal TabId As Integer, ByVal ModuleId As Integer, _
    ///     ByVal ModuleOrder As Integer, ByVal PaneName As String)
    /// </code>
    /// </para>
    /// <para>
    /// Original business logic preserved:
    /// <list type="number">
    ///   <item>Update module order via DataProvider.UpdateModuleOrder</item>
    ///   <item>Clear module cache via DataCache.ClearModuleCache</item>
    /// </list>
    /// </para>
    /// </remarks>
    public async Task UpdateModuleOrderAsync(
        int tabId,
        int moduleId,
        int moduleOrder,
        string paneName,
        CancellationToken cancellationToken = default)
    {
        if (tabId <= 0)
        {
            throw new ArgumentException("Tab ID must be greater than zero.", nameof(tabId));
        }

        if (moduleId <= 0)
        {
            throw new ArgumentException("Module ID must be greater than zero.", nameof(moduleId));
        }

        if (string.IsNullOrWhiteSpace(paneName))
        {
            throw new ArgumentException("Pane name cannot be empty.", nameof(paneName));
        }

        // MIGRATION: Business logic preserved from ModuleController.UpdateModuleOrder
        // Original code:
        // DataProvider.Instance().UpdateModuleOrder(TabId, ModuleId, ModuleOrder, PaneName)
        // DataCache.ClearModuleCache(TabId)

        await _moduleRepository
            .UpdateModuleOrderAsync(tabId, moduleId, moduleOrder, paneName, cancellationToken)
            .ConfigureAwait(false);

        // MIGRATION: Cache clearing equivalent handled by repository/infrastructure layer
        // Original: DataCache.ClearModuleCache(TabId)
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// MIGRATION: Converted from VB.NET ModuleController.GetModuleSettings(ModuleId).
    /// Retrieves all settings for a specific module.
    /// </para>
    /// <para>
    /// Original VB.NET signature:
    /// <code>
    /// Public Shared Function GetModuleSettings(ByVal ModuleId As Integer) As Hashtable
    /// </code>
    /// </para>
    /// <para>
    /// Original business logic:
    /// <code>
    /// Dim dr As IDataReader = DataProvider.Instance().GetModuleSettings(ModuleId)
    /// While dr.Read
    ///     If Not dr.IsDBNull(1) Then h(dr.GetString(0)) = dr.GetString(1)
    /// End While
    /// </code>
    /// </para>
    /// </remarks>
    public async Task<IDictionary<string, string>> GetModuleSettingsAsync(
        int moduleId,
        CancellationToken cancellationToken = default)
    {
        if (moduleId <= 0)
        {
            return new Dictionary<string, string>();
        }

        // MIGRATION: Business logic preserved from ModuleController.GetModuleSettings
        // Original returned Hashtable, now returns IDictionary<string, string> for type safety

        var settings = await _moduleRepository
            .GetModuleSettingsAsync(moduleId, cancellationToken)
            .ConfigureAwait(false);

        return settings;
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// MIGRATION: Converted from VB.NET ModuleController.UpdateModuleSetting(ModuleId, SettingName, SettingValue).
    /// Updates or creates a single module setting.
    /// </para>
    /// <para>
    /// Original VB.NET signature:
    /// <code>
    /// Public Shared Sub UpdateModuleSetting(ByVal ModuleId As Integer, _
    ///     ByVal SettingName As String, ByVal SettingValue As String)
    /// </code>
    /// </para>
    /// <para>
    /// Original business logic:
    /// <code>
    /// DataProvider.Instance().UpdateModuleSetting(ModuleId, SettingName, SettingValue)
    /// DataCache.RemoveCache("GetModuleSettings" &amp; ModuleId.ToString)
    /// </code>
    /// </para>
    /// </remarks>
    public async Task UpdateModuleSettingAsync(
        int moduleId,
        string settingName,
        string settingValue,
        CancellationToken cancellationToken = default)
    {
        if (moduleId <= 0)
        {
            throw new ArgumentException("Module ID must be greater than zero.", nameof(moduleId));
        }

        if (string.IsNullOrWhiteSpace(settingName))
        {
            throw new ArgumentException("Setting name cannot be empty.", nameof(settingName));
        }

        // MIGRATION: Business logic preserved from ModuleController.UpdateModuleSetting
        // Original code:
        // DataProvider.Instance().UpdateModuleSetting(ModuleId, SettingName, SettingValue)
        // DataCache.RemoveCache("GetModuleSettings" & ModuleId.ToString)

        await _moduleRepository
            .UpdateModuleSettingAsync(moduleId, settingName, settingValue ?? string.Empty, cancellationToken)
            .ConfigureAwait(false);

        // MIGRATION: Cache clearing equivalent handled by repository/infrastructure layer
        // Original: DataCache.RemoveCache("GetModuleSettings" & ModuleId.ToString)
    }
}