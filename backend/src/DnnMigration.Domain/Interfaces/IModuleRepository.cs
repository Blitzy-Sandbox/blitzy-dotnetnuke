// -----------------------------------------------------------------------------
// DnnMigration - Modern DotNetNuke Migration
// Copyright (c) 2024. All rights reserved.
// Licensed under the MIT License.
// -----------------------------------------------------------------------------
// MIGRATION: Extracted from VB.NET DotNetNuke.Entities.Modules.ModuleController
// Source: Library/Components/Modules/ModuleController.vb
// Changes:
// - Converted VB.NET ModuleController methods to C# interface contract
// - GetModule → GetByIdAsync, GetModules → GetAllAsync, AddModule → AddAsync
// - UpdateModule → UpdateAsync, DeleteTabModule → DeleteTabModuleAsync
// - Added async/await pattern with Task return types
// - Added CancellationToken parameter to all async methods with default value
// - Used nullable reference type Module? for GetByIdAsync return
// - Used file-scoped namespace as per C# 12 standards
// - Added XML documentation comments
// - Included query methods: GetByTabIdAsync, GetByPortalIdAsync, GetModuleSettingsAsync
// - Added methods for tab-module relationships reflecting the source's TabModule concept
// -----------------------------------------------------------------------------

using DnnMigration.Domain.Entities;

namespace DnnMigration.Domain.Interfaces;

/// <summary>
/// Repository interface defining data access contract for <see cref="Module"/> entities.
/// </summary>
/// <remarks>
/// <para>
/// This interface defines the contract for module data access operations, enabling
/// the Infrastructure layer to implement module data access while maintaining
/// domain layer independence following the Repository Pattern.
/// </para>
/// <para>
/// MIGRATION: Extracted from VB.NET ModuleController.vb lifecycle operations including
/// GetModule, GetModules, AddModule, UpdateModule, DeleteTabModule, and related
/// settings management methods.
/// </para>
/// </remarks>
public interface IModuleRepository
{
    #region Core CRUD Operations

    /// <summary>
    /// Gets a module by its identifier and tab identifier.
    /// </summary>
    /// <param name="moduleId">The unique identifier of the module.</param>
    /// <param name="tabId">The identifier of the tab where the module is placed.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A task representing the asynchronous operation. The task result contains
    /// the <see cref="Module"/> entity if found; otherwise, <c>null</c>.
    /// </returns>
    /// <remarks>
    /// MIGRATION: Corresponds to VB.NET ModuleController.GetModule(ModuleId, TabId, ignoreCache)
    /// Original stored procedure: dbo.GetModule
    /// Parameters: @ModuleID int, @TabID int
    /// </remarks>
    Task<Module?> GetByIdAsync(int moduleId, int tabId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all modules in the system.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A task representing the asynchronous operation. The task result contains
    /// a collection of all <see cref="Module"/> entities.
    /// </returns>
    /// <remarks>
    /// MIGRATION: Corresponds to VB.NET ModuleController.GetAllModules()
    /// Original stored procedure: dbo.GetAllModules
    /// Created for upgrade purposes to retrieve all modules across all portals.
    /// </remarks>
    Task<IEnumerable<Module>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all modules belonging to a specific portal.
    /// </summary>
    /// <param name="portalId">The identifier of the portal.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A task representing the asynchronous operation. The task result contains
    /// a collection of <see cref="Module"/> entities for the specified portal.
    /// </returns>
    /// <remarks>
    /// MIGRATION: Corresponds to VB.NET ModuleController.GetModules(PortalID)
    /// Original stored procedure: dbo.GetModules
    /// Parameters: @PortalID int
    /// </remarks>
    Task<IEnumerable<Module>> GetByPortalIdAsync(int portalId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all modules placed on a specific tab.
    /// </summary>
    /// <param name="tabId">The identifier of the tab.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A task representing the asynchronous operation. The task result contains
    /// a collection of <see cref="Module"/> entities for the specified tab.
    /// </returns>
    /// <remarks>
    /// MIGRATION: Corresponds to VB.NET ModuleController.GetTabModules(TabId)
    /// Original stored procedure: dbo.GetTabModules
    /// Parameters: @TabID int
    /// </remarks>
    Task<IEnumerable<Module>> GetByTabIdAsync(int tabId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new module to the data store.
    /// </summary>
    /// <param name="module">The module entity to add.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A task representing the asynchronous operation. The task result contains
    /// the added <see cref="Module"/> entity with its generated identifier.
    /// </returns>
    /// <remarks>
    /// MIGRATION: Corresponds to VB.NET ModuleController.AddModule(ModuleInfo)
    /// Original stored procedures: dbo.AddModule, dbo.AddTabModule
    /// The implementation should handle both module creation and tab-module linking.
    /// </remarks>
    Task<Module> AddAsync(Module module, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing module in the data store.
    /// </summary>
    /// <param name="module">The module entity with updated values.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// MIGRATION: Corresponds to VB.NET ModuleController.UpdateModule(ModuleInfo)
    /// Original stored procedures: dbo.UpdateModule, dbo.UpdateTabModule
    /// The implementation should update both module and tab-module records.
    /// </remarks>
    Task UpdateAsync(Module module, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a module instance permanently from the data store.
    /// </summary>
    /// <param name="moduleId">The identifier of the module to delete.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// MIGRATION: Corresponds to VB.NET ModuleController.DeleteModule(ModuleId)
    /// Original stored procedure: dbo.DeleteModule
    /// Parameters: @ModuleID int
    /// This permanently removes the module and all its search items.
    /// </remarks>
    Task DeleteAsync(int moduleId, CancellationToken cancellationToken = default);

    #endregion

    #region Query Methods

    /// <summary>
    /// Gets the first module instance with a given friendly name of the module definition.
    /// </summary>
    /// <param name="portalId">The identifier of the portal to search.</param>
    /// <param name="friendlyName">The friendly name of the module definition.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A task representing the asynchronous operation. The task result contains
    /// the first <see cref="Module"/> entity matching the definition; otherwise, <c>null</c>.
    /// </returns>
    /// <remarks>
    /// MIGRATION: Corresponds to VB.NET ModuleController.GetModuleByDefinition(PortalId, FriendlyName)
    /// Original stored procedure: dbo.GetModuleByDefinition
    /// Preferably used for admin and host modules.
    /// </remarks>
    Task<Module?> GetByDefinitionAsync(int portalId, string friendlyName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all module instances with a given friendly name of the module definition.
    /// </summary>
    /// <param name="portalId">The identifier of the portal to search.</param>
    /// <param name="friendlyName">The friendly name of the module definition.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A task representing the asynchronous operation. The task result contains
    /// a collection of <see cref="Module"/> entities matching the definition.
    /// </returns>
    /// <remarks>
    /// MIGRATION: Corresponds to VB.NET ModuleController.GetModulesByDefinition(PortalId, FriendlyName)
    /// Original stored procedure: dbo.GetModuleByDefinition
    /// Returns all instances, not just the first match.
    /// </remarks>
    Task<IEnumerable<Module>> GetModulesByDefinitionAsync(int portalId, string friendlyName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all tab-module references for a specific module.
    /// </summary>
    /// <param name="moduleId">The identifier of the module.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A task representing the asynchronous operation. The task result contains
    /// a collection of <see cref="Module"/> entities representing all tabs where
    /// the module is placed.
    /// </returns>
    /// <remarks>
    /// MIGRATION: Corresponds to VB.NET ModuleController.GetModuleTabs(ModuleID)
    /// Original stored procedure: dbo.GetModule with TabID = Null.NullInteger
    /// A module can be placed on multiple tabs, each represented by a different TabModuleId.
    /// </remarks>
    Task<IEnumerable<Module>> GetModuleTabsAsync(int moduleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets modules from a portal that are either shown on all tabs or on specific tabs.
    /// </summary>
    /// <param name="portalId">The identifier of the portal.</param>
    /// <param name="allTabs">
    /// If <c>true</c>, returns modules shown on all tabs;
    /// if <c>false</c>, returns modules shown on specific tabs only.
    /// </param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A task representing the asynchronous operation. The task result contains
    /// a collection of <see cref="Module"/> entities based on the AllTabs filter.
    /// </returns>
    /// <remarks>
    /// MIGRATION: Corresponds to VB.NET ModuleController.GetAllTabsModules(PortalID, AllTabs)
    /// Original stored procedure: dbo.GetAllTabsModules
    /// Parameters: @PortalID int, @AllTabs bit
    /// </remarks>
    Task<IEnumerable<Module>> GetAllTabsModulesAsync(int portalId, bool allTabs, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all active modules that support search functionality for a portal.
    /// </summary>
    /// <param name="portalId">The identifier of the portal.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A task representing the asynchronous operation. The task result contains
    /// a collection of <see cref="Module"/> entities that support ISearchable.
    /// </returns>
    /// <remarks>
    /// MIGRATION: Corresponds to VB.NET ModuleController.GetSearchModules(PortalId)
    /// Original stored procedure: dbo.GetSearchModules
    /// Returns modules supporting search for indexing purposes.
    /// </remarks>
    Task<IEnumerable<Module>> GetSearchModulesAsync(int portalId, CancellationToken cancellationToken = default);

    #endregion

    #region Tab-Module Operations

    /// <summary>
    /// Deletes a module reference from a specific tab.
    /// </summary>
    /// <param name="tabId">The identifier of the tab.</param>
    /// <param name="moduleId">The identifier of the module.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// MIGRATION: Corresponds to VB.NET ModuleController.DeleteTabModule(TabId, ModuleId)
    /// Original stored procedure: dbo.DeleteTabModule
    /// Parameters: @TabID int, @ModuleID int
    /// If no other tab references exist, the module is soft-deleted.
    /// </remarks>
    Task DeleteTabModuleAsync(int tabId, int moduleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Copies a module from one tab to another, optionally including settings.
    /// </summary>
    /// <param name="moduleId">The identifier of the module to copy.</param>
    /// <param name="fromTabId">The identifier of the source tab.</param>
    /// <param name="toTabId">The identifier of the destination tab.</param>
    /// <param name="toPaneName">The name of the pane on the destination tab. Empty string uses same pane as source.</param>
    /// <param name="includeSettings">If <c>true</c>, tab-module settings are also copied.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// MIGRATION: Corresponds to VB.NET ModuleController.CopyModule(moduleId, fromTabId, toTabId, toPaneName, includeSettings)
    /// Original stored procedure: dbo.AddTabModule (creates new tab-module reference)
    /// The module itself is not duplicated; only a new tab reference is created.
    /// </remarks>
    Task CopyModuleAsync(int moduleId, int fromTabId, int toTabId, string toPaneName, bool includeSettings, CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves a module from one tab to another, including all tab-module settings.
    /// </summary>
    /// <param name="moduleId">The identifier of the module to move.</param>
    /// <param name="fromTabId">The identifier of the source tab.</param>
    /// <param name="toTabId">The identifier of the destination tab.</param>
    /// <param name="toPaneName">The name of the pane on the destination tab.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// MIGRATION: Corresponds to VB.NET ModuleController.MoveModule(moduleId, fromTabId, toTabId, toPaneName)
    /// This operation copies the module to the new tab and removes it from the source tab.
    /// </remarks>
    Task MoveModuleAsync(int moduleId, int fromTabId, int toTabId, string toPaneName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the position of a module within a pane on a page.
    /// </summary>
    /// <param name="tabId">The identifier of the tab.</param>
    /// <param name="moduleId">The identifier of the module.</param>
    /// <param name="moduleOrder">The new order position. Use -1 to add at the end of the pane.</param>
    /// <param name="paneName">The name of the pane.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// MIGRATION: Corresponds to VB.NET ModuleController.UpdateModuleOrder(TabId, ModuleId, ModuleOrder, PaneName)
    /// Original stored procedure: dbo.UpdateModuleOrder
    /// Parameters: @TabID int, @ModuleID int, @ModuleOrder int, @PaneName nvarchar
    /// </remarks>
    Task UpdateModuleOrderAsync(int tabId, int moduleId, int moduleOrder, string paneName, CancellationToken cancellationToken = default);

    #endregion

    #region Module Settings Operations

    /// <summary>
    /// Gets all settings for a module.
    /// </summary>
    /// <param name="moduleId">The identifier of the module.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A task representing the asynchronous operation. The task result contains
    /// a dictionary of setting names and values.
    /// </returns>
    /// <remarks>
    /// MIGRATION: Corresponds to VB.NET ModuleController.GetModuleSettings(ModuleId)
    /// Original stored procedure: dbo.GetModuleSettings
    /// Parameters: @ModuleID int
    /// TabModuleSettings are not included; use GetTabModuleSettingsAsync for those.
    /// </remarks>
    Task<Dictionary<string, string>> GetModuleSettingsAsync(int moduleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds or updates a module setting value.
    /// </summary>
    /// <param name="moduleId">The identifier of the module.</param>
    /// <param name="settingName">The name of the setting.</param>
    /// <param name="settingValue">The value of the setting.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// MIGRATION: Corresponds to VB.NET ModuleController.UpdateModuleSetting(ModuleId, SettingName, SettingValue)
    /// Original stored procedures: dbo.GetModuleSetting, dbo.AddModuleSetting, dbo.UpdateModuleSetting
    /// The implementation should upsert (insert or update) the setting.
    /// </remarks>
    Task UpdateModuleSettingAsync(int moduleId, string settingName, string settingValue, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a specific setting from a module.
    /// </summary>
    /// <param name="moduleId">The identifier of the module.</param>
    /// <param name="settingName">The name of the setting to delete.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// MIGRATION: Corresponds to VB.NET ModuleController.DeleteModuleSetting(ModuleId, SettingName)
    /// Original stored procedure: dbo.DeleteModuleSetting
    /// Parameters: @ModuleID int, @SettingName nvarchar
    /// </remarks>
    Task DeleteModuleSettingAsync(int moduleId, string settingName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all settings from a module.
    /// </summary>
    /// <param name="moduleId">The identifier of the module.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// MIGRATION: Corresponds to VB.NET ModuleController.DeleteModuleSettings(ModuleId)
    /// Original stored procedure: dbo.DeleteModuleSettings
    /// Parameters: @ModuleID int
    /// </remarks>
    Task DeleteModuleSettingsAsync(int moduleId, CancellationToken cancellationToken = default);

    #endregion

    #region Tab-Module Settings Operations

    /// <summary>
    /// Gets all settings for a tab-module instance.
    /// </summary>
    /// <param name="tabModuleId">The identifier of the tab-module.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A task representing the asynchronous operation. The task result contains
    /// a dictionary of setting names and values.
    /// </returns>
    /// <remarks>
    /// MIGRATION: Corresponds to VB.NET ModuleController.GetTabModuleSettings(TabModuleId)
    /// Original stored procedure: dbo.GetTabModuleSettings
    /// Parameters: @TabModuleID int
    /// ModuleSettings are not included; these are settings specific to a tab placement.
    /// </remarks>
    Task<Dictionary<string, string>> GetTabModuleSettingsAsync(int tabModuleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds or updates a tab-module setting value.
    /// </summary>
    /// <param name="tabModuleId">The identifier of the tab-module.</param>
    /// <param name="settingName">The name of the setting.</param>
    /// <param name="settingValue">The value of the setting.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// MIGRATION: Corresponds to VB.NET ModuleController.UpdateTabModuleSetting(TabModuleId, SettingName, SettingValue)
    /// Original stored procedures: dbo.GetTabModuleSetting, dbo.AddTabModuleSetting, dbo.UpdateTabModuleSetting
    /// The implementation should upsert (insert or update) the setting.
    /// </remarks>
    Task UpdateTabModuleSettingAsync(int tabModuleId, string settingName, string settingValue, CancellationToken cancellationToken = default);

    #endregion
}
