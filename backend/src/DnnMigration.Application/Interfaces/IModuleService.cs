// -----------------------------------------------------------------------------
// <copyright file="IModuleService.cs" company="DNN Migration Project">
//   Copyright (c) DNN Migration Project. All rights reserved.
//   Licensed under the MIT License. See LICENSE file in the project root.
// </copyright>
// <summary>
//   Application service interface for module lifecycle operations.
//   MIGRATION: Derived from DotNetNuke.Entities.Modules.ModuleController (VB.NET)
//   Source: Library/Components/Modules/ModuleController.vb
// </summary>
// -----------------------------------------------------------------------------

using DnnMigration.Application.DTOs.Common;
using DnnMigration.Application.DTOs.Module;

namespace DnnMigration.Application.Interfaces;

/// <summary>
/// Defines the contract for module lifecycle management operations.
/// </summary>
/// <remarks>
/// <para>
/// This interface provides async methods for module CRUD operations as well as
/// module copy and move functionality. All methods return DTOs rather than domain
/// entities to maintain separation between the API and domain layers.
/// </para>
/// <para>
/// MIGRATION: Methods derived from ModuleController.vb operations:
/// <list type="bullet">
///   <item><description>GetModuleAsync → GetModule (lines 885-905)</description></item>
///   <item><description>GetModulesAsync → GetModules (lines 915-930)</description></item>
///   <item><description>GetModulesByTabAsync → GetTabModules (lines 1044-1063)</description></item>
///   <item><description>CreateModuleAsync → AddModule (lines 645-682)</description></item>
///   <item><description>UpdateModuleAsync → UpdateModule (lines 1095-1148)</description></item>
///   <item><description>DeleteModuleAsync → DeleteModule (lines 819-826)</description></item>
///   <item><description>CopyModuleAsync → CopyModule (lines 700-726)</description></item>
///   <item><description>MoveModuleAsync → MoveModule (lines 1078-1086)</description></item>
/// </list>
/// </para>
/// <para>
/// All methods include <see cref="CancellationToken"/> parameter per Section 0.7.2
/// requirement for cooperative cancellation support in I/O operations.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Register in DI container
/// builder.Services.AddScoped&lt;IModuleService, ModuleService&gt;();
/// 
/// // Use in controller
/// public class ModulesController : ControllerBase
/// {
///     private readonly IModuleService _moduleService;
///     
///     public ModulesController(IModuleService moduleService)
///     {
///         _moduleService = moduleService;
///     }
///     
///     [HttpGet("{id}")]
///     public async Task&lt;ActionResult&lt;ModuleDto&gt;&gt; GetModule(
///         int id, CancellationToken cancellationToken)
///     {
///         var module = await _moduleService.GetModuleAsync(id, cancellationToken);
///         return module is null ? NotFound() : Ok(module);
///     }
/// }
/// </code>
/// </example>
public interface IModuleService
{
    #region Module Retrieval Operations

    /// <summary>
    /// Retrieves a single module by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the module to retrieve.</param>
    /// <param name="cancellationToken">Token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains
    /// the <see cref="ModuleDto"/> if found; otherwise, <c>null</c>.
    /// </returns>
    /// <remarks>
    /// MIGRATION: Derived from ModuleController.GetModule(ModuleId, TabId, ignoreCache)
    /// at lines 885-905 of ModuleController.vb. The TabId parameter is not required
    /// in the new API as modules can be uniquely identified by ModuleId alone.
    /// 
    /// Original VB.NET:
    /// <code>
    /// Public Function GetModule(ByVal ModuleId As Integer, ByVal TabId As Integer, ByVal ignoreCache As Boolean) As ModuleInfo
    /// </code>
    /// </remarks>
    /// <example>
    /// <code>
    /// var module = await moduleService.GetModuleAsync(123, cancellationToken);
    /// if (module is not null)
    /// {
    ///     Console.WriteLine($"Module: {module.ModuleTitle}");
    /// }
    /// </code>
    /// </example>
    Task<ModuleDto?> GetModuleAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a paginated list of modules for a specific portal.
    /// </summary>
    /// <param name="portalId">The portal identifier to filter modules by.</param>
    /// <param name="pageIndex">Zero-based index of the page to retrieve.</param>
    /// <param name="pageSize">Maximum number of modules per page.</param>
    /// <param name="cancellationToken">Token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains
    /// a <see cref="PagedResult{T}"/> of <see cref="ModuleDto"/> objects with pagination metadata.
    /// </returns>
    /// <remarks>
    /// MIGRATION: Derived from ModuleController.GetModules(PortalID) at lines 915-930
    /// of ModuleController.vb. The original method returned ArrayList; this method
    /// returns a strongly-typed PagedResult for better API design.
    /// 
    /// Original VB.NET:
    /// <code>
    /// Public Function GetModules(ByVal PortalID As Integer) As ArrayList
    /// </code>
    /// </remarks>
    /// <example>
    /// <code>
    /// var pagedResult = await moduleService.GetModulesAsync(1, 0, 20, cancellationToken);
    /// Console.WriteLine($"Total modules: {pagedResult.TotalCount}");
    /// foreach (var module in pagedResult.Items)
    /// {
    ///     Console.WriteLine($"- {module.ModuleTitle}");
    /// }
    /// </code>
    /// </example>
    Task<PagedResult<ModuleDto>> GetModulesAsync(
        int portalId,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all modules placed on a specific tab (page).
    /// </summary>
    /// <param name="tabId">The tab/page identifier to retrieve modules for.</param>
    /// <param name="cancellationToken">Token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains
    /// an <see cref="IEnumerable{T}"/> of <see cref="ModuleDto"/> objects for the tab.
    /// </returns>
    /// <remarks>
    /// MIGRATION: Derived from ModuleController.GetTabModules(TabId) at lines 1044-1063
    /// of ModuleController.vb. The original method returned Dictionary(Of Integer, ModuleInfo);
    /// this method returns IEnumerable for simpler consumption in REST APIs.
    /// 
    /// Original VB.NET:
    /// <code>
    /// Public Function GetTabModules(ByVal TabId As Integer) As Dictionary(Of Integer, ModuleInfo)
    /// </code>
    /// </remarks>
    /// <example>
    /// <code>
    /// var modules = await moduleService.GetModulesByTabAsync(42, cancellationToken);
    /// foreach (var module in modules)
    /// {
    ///     Console.WriteLine($"Module {module.ModuleId}: {module.ModuleTitle} in pane {module.PaneName}");
    /// }
    /// </code>
    /// </example>
    Task<IEnumerable<ModuleDto>> GetModulesByTabAsync(
        int tabId,
        CancellationToken cancellationToken = default);

    #endregion

    #region Module CRUD Operations

    /// <summary>
    /// Creates a new module instance on a tab/page.
    /// </summary>
    /// <param name="request">The module creation request containing all required fields.</param>
    /// <param name="cancellationToken">Token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains
    /// the newly created <see cref="ModuleDto"/> with its assigned identifier.
    /// </returns>
    /// <remarks>
    /// <para>
    /// MIGRATION: Derived from ModuleController.AddModule(ModuleInfo) at lines 645-682
    /// of ModuleController.vb. The creation process involves:
    /// </para>
    /// <list type="number">
    ///   <item><description>Creating a Module record via DataProvider.Instance().AddModule</description></item>
    ///   <item><description>Setting module permissions if provided</description></item>
    ///   <item><description>Creating a TabModule record via DataProvider.Instance().AddTabModule</description></item>
    ///   <item><description>Positioning the module in the pane (at bottom if ModuleOrder is -1)</description></item>
    ///   <item><description>Clearing the module cache for the tab</description></item>
    /// </list>
    /// 
    /// Original VB.NET:
    /// <code>
    /// Public Function AddModule(ByVal objModule As ModuleInfo) As Integer
    /// </code>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is null.</exception>
    /// <example>
    /// <code>
    /// var request = new CreateModuleRequest
    /// {
    ///     TabId = 42,
    ///     ModuleDefId = 117,
    ///     PaneName = "ContentPane",
    ///     ModuleTitle = "My New Module",
    ///     DisplayTitle = true
    /// };
    /// var module = await moduleService.CreateModuleAsync(request, cancellationToken);
    /// Console.WriteLine($"Created module with ID: {module.ModuleId}");
    /// </code>
    /// </example>
    Task<ModuleDto> CreateModuleAsync(
        CreateModuleRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing module's settings.
    /// </summary>
    /// <param name="id">The unique identifier of the module to update.</param>
    /// <param name="request">The update request containing fields to modify (nullable fields support partial updates).</param>
    /// <param name="cancellationToken">Token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains
    /// the updated <see cref="ModuleDto"/> reflecting all changes.
    /// </returns>
    /// <remarks>
    /// <para>
    /// MIGRATION: Derived from ModuleController.UpdateModule(ModuleInfo) at lines 1095-1148
    /// of ModuleController.vb. The update process involves:
    /// </para>
    /// <list type="number">
    ///   <item><description>Updating Module record via DataProvider.Instance().UpdateModule</description></item>
    ///   <item><description>Syncing module permissions if changed</description></item>
    ///   <item><description>Updating TabModule record via DataProvider.Instance().UpdateTabModule</description></item>
    ///   <item><description>Updating module order in pane if changed</description></item>
    ///   <item><description>Optionally applying settings to all modules if AllModules is true</description></item>
    ///   <item><description>Clearing the module cache</description></item>
    /// </list>
    /// 
    /// Original VB.NET:
    /// <code>
    /// Public Sub UpdateModule(ByVal objModule As ModuleInfo)
    /// </code>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is null.</exception>
    /// <exception cref="KeyNotFoundException">Thrown when the module with specified <paramref name="id"/> does not exist.</exception>
    /// <example>
    /// <code>
    /// var request = new UpdateModuleRequest
    /// {
    ///     ModuleTitle = "Updated Title",
    ///     CacheTime = 300,
    ///     DisplayTitle = true
    /// };
    /// var module = await moduleService.UpdateModuleAsync(123, request, cancellationToken);
    /// </code>
    /// </example>
    Task<ModuleDto> UpdateModuleAsync(
        int id,
        UpdateModuleRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Permanently deletes a module instance from the database.
    /// </summary>
    /// <param name="id">The unique identifier of the module to delete.</param>
    /// <param name="cancellationToken">Token to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous delete operation.</returns>
    /// <remarks>
    /// <para>
    /// MIGRATION: Derived from ModuleController.DeleteModule(ModuleId) at lines 819-826
    /// of ModuleController.vb. This permanently removes the module and associated
    /// search items from the database.
    /// </para>
    /// <para>
    /// Note: For soft delete (moving to recycle bin), use UpdateModuleAsync to set
    /// IsDeleted = true instead. This method performs a hard delete.
    /// </para>
    /// 
    /// Original VB.NET:
    /// <code>
    /// Public Sub DeleteModule(ByVal ModuleId As Integer)
    ///     DataProvider.Instance().DeleteModule(ModuleId)
    ///     DataProvider.Instance().DeleteSearchItems(ModuleId)
    /// End Sub
    /// </code>
    /// </remarks>
    /// <exception cref="KeyNotFoundException">Thrown when the module with specified <paramref name="id"/> does not exist.</exception>
    /// <example>
    /// <code>
    /// await moduleService.DeleteModuleAsync(123, cancellationToken);
    /// </code>
    /// </example>
    Task DeleteModuleAsync(int id, CancellationToken cancellationToken = default);

    #endregion

    #region Module Copy and Move Operations

    /// <summary>
    /// Copies a module from one tab to another, creating a shared instance.
    /// </summary>
    /// <param name="moduleId">The identifier of the module to copy.</param>
    /// <param name="fromTabId">The source tab identifier where the module currently exists.</param>
    /// <param name="toTabId">The destination tab identifier where the module will be copied.</param>
    /// <param name="toPaneName">
    /// The name of the pane on the destination tab where the module will be placed.
    /// If empty or null, the module retains its original pane name.
    /// </param>
    /// <param name="cancellationToken">Token to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous copy operation.</returns>
    /// <remarks>
    /// <para>
    /// MIGRATION: Derived from ModuleController.CopyModule at lines 700-726 of ModuleController.vb.
    /// The copy operation creates a shared reference to the same module on multiple tabs.
    /// Changes to module content affect all instances, but tab-specific settings (like
    /// position and container) are independent.
    /// </para>
    /// <para>
    /// The copy process:
    /// </para>
    /// <list type="number">
    ///   <item><description>Retrieves the source module from the fromTab</description></item>
    ///   <item><description>If toPaneName is empty, uses the source module's pane name</description></item>
    ///   <item><description>Adds a new TabModule record linking to the same Module</description></item>
    ///   <item><description>Optionally copies TabModuleSettings if includeSettings is true</description></item>
    ///   <item><description>Clears the cache for both source and destination tabs</description></item>
    /// </list>
    /// 
    /// Original VB.NET:
    /// <code>
    /// Public Sub CopyModule(ByVal moduleId As Integer, ByVal fromTabId As Integer, 
    ///     ByVal toTabId As Integer, ByVal toPaneName As String, ByVal includeSettings As Boolean)
    /// </code>
    /// </remarks>
    /// <exception cref="KeyNotFoundException">Thrown when the module or tabs do not exist.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the module already exists on the destination tab.</exception>
    /// <example>
    /// <code>
    /// // Copy module 123 from tab 10 to tab 20, placing it in the RightPane
    /// await moduleService.CopyModuleAsync(123, 10, 20, "RightPane", cancellationToken);
    /// </code>
    /// </example>
    Task CopyModuleAsync(
        int moduleId,
        int fromTabId,
        int toTabId,
        string toPaneName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves a module from one tab to another, removing it from the source tab.
    /// </summary>
    /// <param name="moduleId">The identifier of the module to move.</param>
    /// <param name="fromTabId">The source tab identifier where the module currently exists.</param>
    /// <param name="toTabId">The destination tab identifier where the module will be moved.</param>
    /// <param name="toPaneName">
    /// The name of the pane on the destination tab where the module will be placed.
    /// If empty or null, the module retains its original pane name.
    /// </param>
    /// <param name="cancellationToken">Token to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous move operation.</returns>
    /// <remarks>
    /// <para>
    /// MIGRATION: Derived from ModuleController.MoveModule at lines 1078-1086 of ModuleController.vb.
    /// The move operation is implemented as a copy followed by delete from the source tab.
    /// </para>
    /// <para>
    /// The move process:
    /// </para>
    /// <list type="number">
    ///   <item><description>Copies the module to the destination tab (including TabModuleSettings)</description></item>
    ///   <item><description>Deletes the TabModule reference from the source tab</description></item>
    /// </list>
    /// 
    /// Original VB.NET:
    /// <code>
    /// Public Sub MoveModule(ByVal moduleId As Integer, ByVal fromTabId As Integer, 
    ///     ByVal toTabId As Integer, ByVal toPaneName As String)
    ///     CopyModule(moduleId, fromTabId, toTabId, toPaneName, True)
    ///     DeleteTabModule(fromTabId, moduleId)
    /// End Sub
    /// </code>
    /// </remarks>
    /// <exception cref="KeyNotFoundException">Thrown when the module or tabs do not exist.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the module already exists on the destination tab.</exception>
    /// <example>
    /// <code>
    /// // Move module 123 from tab 10 to tab 20, keeping it in the same pane
    /// await moduleService.MoveModuleAsync(123, 10, 20, "", cancellationToken);
    /// </code>
    /// </example>
    Task MoveModuleAsync(
        int moduleId,
        int fromTabId,
        int toTabId,
        string toPaneName,
        CancellationToken cancellationToken = default);

    #endregion
}
