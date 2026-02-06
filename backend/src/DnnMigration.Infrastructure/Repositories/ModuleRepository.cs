// MIGRATION: VB.NET ModuleController.vb → C# 12 ModuleRepository implementation
// Replaces DotNetNuke.Entities.Modules.ModuleController with EF Core 8 repository pattern
// Original used DataProvider.Instance() with SqlHelper - now uses DnnDbContext with LINQ

using System.Runtime.CompilerServices;
using DnnMigration.Domain.Entities;
using DnnMigration.Domain.Interfaces;
using DnnMigration.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DnnMigration.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for Module entity CRUD operations.
/// Replaces legacy ModuleController.vb SqlDataProvider/SqlHelper patterns with EF Core LINQ queries.
/// </summary>
/// <remarks>
/// MIGRATION NOTES:
/// - DataProvider.Instance() calls replaced with DnnDbContext dependency injection
/// - FillModuleInfo IDataReader hydration replaced with EF Core entity tracking
/// - SqlHelper.ExecuteReader replaced with EF Core async LINQ queries
/// - CBO.FillObject calls replaced by EF Core materialization
/// - DataCache.ClearModuleCache removed (caching handled by application layer)
/// </remarks>
public class ModuleRepository : IModuleRepository
{
    private readonly DnnDbContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="ModuleRepository"/> class.
    /// </summary>
    /// <param name="context">The database context for EF Core operations.</param>
    /// <exception cref="ArgumentNullException">Thrown when context is null.</exception>
    public ModuleRepository(DnnDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    #region Core CRUD Operations

    /// <summary>
    /// Gets a module by its identifier and tab identifier.
    /// </summary>
    /// <param name="moduleId">The unique identifier of the module.</param>
    /// <param name="tabId">The identifier of the tab where the module is placed.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The module if found; otherwise, null.</returns>
    /// <remarks>
    /// MIGRATION: Replaces GetModule stored procedure call (ModuleController.vb lines ~200-250)
    /// Original: DataProvider.Instance().GetModule(moduleId, tabId)
    /// </remarks>
    public async Task<Module?> GetByIdAsync(
        int moduleId,
        int tabId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Modules
            .AsNoTracking()
            .Include(m => m.Portal)
            .Include(m => m.Tab)
            .Include(m => m.ModuleDefinition)
            .Include(m => m.ModulePermissions)
            .FirstOrDefaultAsync(m => m.ModuleId == moduleId && m.TabId == tabId, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Gets all modules in the system.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A collection of all modules.</returns>
    /// <remarks>
    /// MIGRATION: Replaces GetAllModules stored procedure call
    /// Original: DataProvider.Instance().GetAllModules()
    /// </remarks>
    public async Task<IEnumerable<Module>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Modules
            .AsNoTracking()
            .Include(m => m.Portal)
            .Include(m => m.Tab)
            .Include(m => m.ModuleDefinition)
            .OrderBy(m => m.PortalId)
            .ThenBy(m => m.TabId)
            .ThenBy(m => m.ModuleOrder)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Gets all modules for a specific portal.
    /// </summary>
    /// <param name="portalId">The portal identifier.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A collection of modules belonging to the portal.</returns>
    /// <remarks>
    /// MIGRATION: Replaces GetModules(PortalID) from ModuleController.vb
    /// Original: DataProvider.Instance().GetModules(portalId)
    /// </remarks>
    public async Task<IEnumerable<Module>> GetByPortalIdAsync(
        int portalId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Modules
            .AsNoTracking()
            .Include(m => m.Tab)
            .Include(m => m.ModuleDefinition)
            .Include(m => m.ModulePermissions)
            .Where(m => m.PortalId == portalId && !m.IsDeleted)
            .OrderBy(m => m.TabId)
            .ThenBy(m => m.ModuleOrder)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Gets all modules on a specific tab/page.
    /// </summary>
    /// <param name="tabId">The tab identifier.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A collection of modules on the tab.</returns>
    /// <remarks>
    /// MIGRATION: Replaces GetTabModules (ModuleController.vb lines 900-950)
    /// Original: DataProvider.Instance().GetTabModules(tabId)
    /// Converted: Where(m => m.TabId == tabId) LINQ
    /// </remarks>
    public async Task<IEnumerable<Module>> GetByTabIdAsync(
        int tabId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Modules
            .AsNoTracking()
            .Include(m => m.ModuleDefinition)
            .Include(m => m.ModulePermissions)
            .Where(m => m.TabId == tabId && !m.IsDeleted)
            .OrderBy(m => m.ModuleOrder)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Adds a new module to the database.
    /// </summary>
    /// <param name="module">The module entity to add.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The added module with generated identity.</returns>
    /// <remarks>
    /// MIGRATION: Replaces AddModule stored procedure call (ModuleController.vb lines 550-650)
    /// Original: DataProvider.Instance().AddModule(moduleId, portalId, moduleDefId, ...)
    /// Converted: AddAsync + SaveChangesAsync
    /// </remarks>
    public async Task<Module> AddAsync(Module module, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(module);

        // Validate required foreign key references
        if (module.PortalId <= 0)
        {
            throw new ArgumentException("PortalId must be specified.", nameof(module));
        }

        if (module.TabId <= 0)
        {
            throw new ArgumentException("TabId must be specified.", nameof(module));
        }

        if (module.ModuleDefId <= 0)
        {
            throw new ArgumentException("ModuleDefId must be specified.", nameof(module));
        }

        // Set defaults for new modules per legacy behavior
        if (string.IsNullOrEmpty(module.PaneName))
        {
            module.PaneName = "ContentPane";
        }

        // Calculate module order if not specified
        if (module.ModuleOrder <= 0)
        {
            var maxOrder = await _context.Modules
                .Where(m => m.TabId == module.TabId && m.PaneName == module.PaneName)
                .MaxAsync(m => (int?)m.ModuleOrder, cancellationToken)
                .ConfigureAwait(false);

            module.ModuleOrder = (maxOrder ?? 0) + 1;
        }

        await _context.Modules.AddAsync(module, cancellationToken).ConfigureAwait(false);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return module;
    }

    /// <summary>
    /// Updates an existing module in the database.
    /// </summary>
    /// <param name="module">The module entity with updated values.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <remarks>
    /// MIGRATION: Replaces UpdateModule stored procedure
    /// Original: DataProvider.Instance().UpdateModule(moduleId, ...)
    /// Converted: Update + SaveChangesAsync
    /// </remarks>
    public async Task UpdateAsync(Module module, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(module);

        var existingModule = await _context.Modules
            .FirstOrDefaultAsync(m => m.ModuleId == module.ModuleId, cancellationToken)
            .ConfigureAwait(false);

        if (existingModule is null)
        {
            throw new InvalidOperationException($"Module with ID {module.ModuleId} not found.");
        }

        // Update scalar properties
        existingModule.TabId = module.TabId;
        existingModule.TabModuleId = module.TabModuleId;
        existingModule.ModuleDefId = module.ModuleDefId;
        existingModule.ModuleOrder = module.ModuleOrder;
        existingModule.PaneName = module.PaneName;
        existingModule.ModuleTitle = module.ModuleTitle;
        existingModule.CacheTime = module.CacheTime;
        existingModule.Alignment = module.Alignment;
        existingModule.Color = module.Color;
        existingModule.Border = module.Border;
        existingModule.IconFile = module.IconFile;
        existingModule.AllTabs = module.AllTabs;
        existingModule.Visibility = module.Visibility;
        existingModule.IsDeleted = module.IsDeleted;
        existingModule.Header = module.Header;
        existingModule.Footer = module.Footer;
        existingModule.StartDate = module.StartDate;
        existingModule.EndDate = module.EndDate;
        existingModule.ContainerSrc = module.ContainerSrc;
        existingModule.DisplayTitle = module.DisplayTitle;
        existingModule.DisplayPrint = module.DisplayPrint;
        existingModule.DisplaySyndicate = module.DisplaySyndicate;
        existingModule.InheritViewPermissions = module.InheritViewPermissions;
        existingModule.IsDefaultModule = module.IsDefaultModule;
        existingModule.AllModules = module.AllModules;

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Deletes a module permanently from the database.
    /// </summary>
    /// <param name="moduleId">The module identifier to delete.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <remarks>
    /// MIGRATION: Replaces DeleteModule stored procedure call (ModuleController.vb lines 800-850)
    /// Original: DataProvider.Instance().DeleteModule(moduleId)
    /// Converted: Remove + SaveChangesAsync
    /// </remarks>
    public async Task DeleteAsync(int moduleId, CancellationToken cancellationToken = default)
    {
        var modules = await _context.Modules
            .Where(m => m.ModuleId == moduleId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (modules.Count == 0)
        {
            return; // Module already deleted or doesn't exist
        }

        // Delete module settings first
        await DeleteModuleSettingsAsync(moduleId, cancellationToken).ConfigureAwait(false);

        // Remove all module instances (across all tabs)
        _context.Modules.RemoveRange(modules);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    #endregion

    #region Query Methods

    /// <summary>
    /// Gets the first module instance with a given friendly name of the module definition.
    /// </summary>
    /// <param name="portalId">The identifier of the portal to search.</param>
    /// <param name="friendlyName">The friendly name of the module definition.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The first module matching the definition; otherwise, null.</returns>
    /// <remarks>
    /// MIGRATION: Replaces GetModuleByDefinition(PortalId, FriendlyName) from ModuleController.vb
    /// Original: DataProvider.Instance().GetModuleByDefinition(portalId, friendlyName)
    /// </remarks>
    public async Task<Module?> GetByDefinitionAsync(
        int portalId,
        string friendlyName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(friendlyName);

        return await _context.Modules
            .AsNoTracking()
            .Include(m => m.Portal)
            .Include(m => m.Tab)
            .Include(m => m.ModuleDefinition)
            .Where(m => m.PortalId == portalId && 
                        !m.IsDeleted && 
                        m.ModuleDefinition != null && 
                        m.ModuleDefinition.FriendlyName == friendlyName)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Gets all module instances with a given friendly name of the module definition.
    /// </summary>
    /// <param name="portalId">The identifier of the portal to search.</param>
    /// <param name="friendlyName">The friendly name of the module definition.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A collection of modules with the specified definition.</returns>
    /// <remarks>
    /// MIGRATION: Replaces GetModulesByDefinition(PortalId, FriendlyName) pattern
    /// </remarks>
    public async Task<IEnumerable<Module>> GetModulesByDefinitionAsync(
        int portalId,
        string friendlyName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(friendlyName);

        return await _context.Modules
            .AsNoTracking()
            .Include(m => m.Portal)
            .Include(m => m.Tab)
            .Include(m => m.ModuleDefinition)
            .Where(m => m.PortalId == portalId && 
                        !m.IsDeleted && 
                        m.ModuleDefinition != null && 
                        m.ModuleDefinition.FriendlyName == friendlyName)
            .OrderBy(m => m.TabId)
            .ThenBy(m => m.ModuleOrder)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Gets all tab-module references for a specific module.
    /// </summary>
    /// <param name="moduleId">The identifier of the module.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A collection of modules representing all tabs where the module is placed.</returns>
    /// <remarks>
    /// MIGRATION: Replaces GetModuleTabs(ModuleID) from ModuleController.vb
    /// A module can be placed on multiple tabs via AllTabs or manual placement.
    /// </remarks>
    public async Task<IEnumerable<Module>> GetModuleTabsAsync(
        int moduleId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Modules
            .AsNoTracking()
            .Include(m => m.Tab)
            .Where(m => m.ModuleId == moduleId && !m.IsDeleted)
            .OrderBy(m => m.TabId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Gets modules from a portal based on the AllTabs flag.
    /// </summary>
    /// <param name="portalId">The identifier of the portal.</param>
    /// <param name="allTabs">If true, returns modules shown on all tabs; otherwise, specific tabs only.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A collection of modules based on the AllTabs filter.</returns>
    /// <remarks>
    /// MIGRATION: Replaces GetAllTabsModules(PortalID, AllTabs) pattern
    /// Original: DataProvider.Instance().GetAllTabsModules(portalId, allTabs)
    /// </remarks>
    public async Task<IEnumerable<Module>> GetAllTabsModulesAsync(
        int portalId,
        bool allTabs,
        CancellationToken cancellationToken = default)
    {
        return await _context.Modules
            .AsNoTracking()
            .Include(m => m.Tab)
            .Include(m => m.ModuleDefinition)
            .Where(m => m.PortalId == portalId && m.AllTabs == allTabs && !m.IsDeleted)
            .OrderBy(m => m.ModuleOrder)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Gets all active modules that support search functionality for a portal.
    /// </summary>
    /// <param name="portalId">The identifier of the portal.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A collection of modules that support ISearchable.</returns>
    /// <remarks>
    /// MIGRATION: Replaces GetSearchModules(PortalId) from ModuleController.vb
    /// Original: DataProvider.Instance().GetSearchModules(portalId)
    /// Returns modules supporting search for indexing purposes.
    /// </remarks>
    public async Task<IEnumerable<Module>> GetSearchModulesAsync(
        int portalId,
        CancellationToken cancellationToken = default)
    {
        // MIGRATION: In legacy DNN, this checked for ISearchable interface on the module's business controller
        // For this migration, we return all active modules that could potentially be indexed
        // The application layer will determine which modules actually implement search
        return await _context.Modules
            .AsNoTracking()
            .Include(m => m.Tab)
            .Include(m => m.ModuleDefinition)
                .ThenInclude(md => md!.DesktopModule)
            .Where(m => m.PortalId == portalId && 
                        !m.IsDeleted && 
                        m.Tab != null && 
                        !m.Tab.IsDeleted &&
                        m.ModuleDefinition != null &&
                        m.ModuleDefinition.DesktopModule != null &&
                        !string.IsNullOrEmpty(m.ModuleDefinition.DesktopModule.BusinessControllerClass))
            .OrderBy(m => m.TabId)
            .ThenBy(m => m.ModuleOrder)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    #endregion

    #region Tab-Module Operations

    /// <summary>
    /// Deletes a module reference from a specific tab.
    /// </summary>
    /// <param name="tabId">The identifier of the tab.</param>
    /// <param name="moduleId">The identifier of the module.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <remarks>
    /// MIGRATION: Replaces DeleteTabModule(TabId, ModuleId) pattern
    /// If no other tab references exist, the module is soft-deleted.
    /// </remarks>
    public async Task DeleteTabModuleAsync(
        int tabId,
        int moduleId,
        CancellationToken cancellationToken = default)
    {
        var module = await _context.Modules
            .FirstOrDefaultAsync(m => m.ModuleId == moduleId && m.TabId == tabId, cancellationToken)
            .ConfigureAwait(false);

        if (module is null)
        {
            return;
        }

        // Check if module exists on other tabs
        var otherTabReferences = await _context.Modules
            .Where(m => m.ModuleId == moduleId && m.TabId != tabId && !m.IsDeleted)
            .AnyAsync(cancellationToken)
            .ConfigureAwait(false);

        if (otherTabReferences)
        {
            // Module exists on other tabs - just remove this tab reference
            _context.Modules.Remove(module);
        }
        else
        {
            // No other tab references - soft delete the module
            module.IsDeleted = true;
        }

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Copies a module from one tab to another, optionally including settings.
    /// </summary>
    /// <param name="moduleId">The identifier of the module to copy.</param>
    /// <param name="fromTabId">The identifier of the source tab.</param>
    /// <param name="toTabId">The identifier of the destination tab.</param>
    /// <param name="toPaneName">The name of the pane on the destination tab. Empty string uses same pane as source.</param>
    /// <param name="includeSettings">If true, tab-module settings are also copied.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <remarks>
    /// MIGRATION: Replaces CopyModule pattern from ModuleController.vb
    /// The module itself is not duplicated; only a new tab reference is created.
    /// </remarks>
    public async Task CopyModuleAsync(
        int moduleId,
        int fromTabId,
        int toTabId,
        string toPaneName,
        bool includeSettings,
        CancellationToken cancellationToken = default)
    {
        var sourceModule = await _context.Modules
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.ModuleId == moduleId && m.TabId == fromTabId, cancellationToken)
            .ConfigureAwait(false);

        if (sourceModule is null)
        {
            throw new InvalidOperationException($"Source module with ID {moduleId} not found on tab {fromTabId}.");
        }

        // Determine the pane name - use source pane if not specified
        var paneName = string.IsNullOrEmpty(toPaneName) ? sourceModule.PaneName : toPaneName;

        // Calculate the next module order for the target pane
        var maxOrder = await _context.Modules
            .Where(m => m.TabId == toTabId && m.PaneName == paneName)
            .MaxAsync(m => (int?)m.ModuleOrder, cancellationToken)
            .ConfigureAwait(false);

        // Create new tab-module reference (module ID stays the same)
        var newTabModule = new Module
        {
            ModuleId = sourceModule.ModuleId, // Same module ID - this is a reference, not a copy
            TabId = toTabId,
            PortalId = sourceModule.PortalId,
            ModuleDefId = sourceModule.ModuleDefId,
            ModuleOrder = (maxOrder ?? 0) + 1,
            PaneName = paneName,
            ModuleTitle = sourceModule.ModuleTitle,
            CacheTime = sourceModule.CacheTime,
            Alignment = sourceModule.Alignment,
            Color = sourceModule.Color,
            Border = sourceModule.Border,
            IconFile = sourceModule.IconFile,
            AllTabs = false, // Copied reference is not AllTabs
            Visibility = sourceModule.Visibility,
            IsDeleted = false,
            Header = sourceModule.Header,
            Footer = sourceModule.Footer,
            StartDate = sourceModule.StartDate,
            EndDate = sourceModule.EndDate,
            ContainerSrc = sourceModule.ContainerSrc,
            DisplayTitle = sourceModule.DisplayTitle,
            DisplayPrint = sourceModule.DisplayPrint,
            DisplaySyndicate = sourceModule.DisplaySyndicate,
            InheritViewPermissions = sourceModule.InheritViewPermissions,
            IsDefaultModule = false,
            AllModules = false
        };

        await _context.Modules.AddAsync(newTabModule, cancellationToken).ConfigureAwait(false);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Copy tab-module settings if requested
        if (includeSettings && sourceModule.TabModuleId > 0)
        {
            await CopyTabModuleSettingsAsync(sourceModule.TabModuleId, newTabModule.TabModuleId, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Moves a module from one tab to another, including all tab-module settings.
    /// </summary>
    /// <param name="moduleId">The identifier of the module to move.</param>
    /// <param name="fromTabId">The identifier of the source tab.</param>
    /// <param name="toTabId">The identifier of the destination tab.</param>
    /// <param name="toPaneName">The name of the pane on the destination tab.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <remarks>
    /// MIGRATION: Replaces MoveModule pattern
    /// Updates the module's TabId and recalculates module order
    /// </remarks>
    public async Task MoveModuleAsync(
        int moduleId,
        int fromTabId,
        int toTabId,
        string toPaneName,
        CancellationToken cancellationToken = default)
    {
        var module = await _context.Modules
            .FirstOrDefaultAsync(m => m.ModuleId == moduleId && m.TabId == fromTabId, cancellationToken)
            .ConfigureAwait(false);

        if (module is null)
        {
            throw new InvalidOperationException($"Module with ID {moduleId} not found on tab {fromTabId}.");
        }

        // Calculate the next module order for the target pane
        var maxOrder = await _context.Modules
            .Where(m => m.TabId == toTabId && m.PaneName == toPaneName)
            .MaxAsync(m => (int?)m.ModuleOrder, cancellationToken)
            .ConfigureAwait(false);

        module.TabId = toTabId;
        module.PaneName = toPaneName;
        module.ModuleOrder = (maxOrder ?? 0) + 1;

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Updates the position of a module within a pane on a page.
    /// </summary>
    /// <param name="tabId">The identifier of the tab.</param>
    /// <param name="moduleId">The identifier of the module.</param>
    /// <param name="moduleOrder">The new order position. Use -1 to add at the end of the pane.</param>
    /// <param name="paneName">The name of the pane.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <remarks>
    /// MIGRATION: Replaces UpdateModuleOrder(TabId, ModuleId, ModuleOrder, PaneName) pattern
    /// Original: DataProvider.Instance().UpdateModuleOrder(tabId, moduleId, moduleOrder, paneName)
    /// </remarks>
    public async Task UpdateModuleOrderAsync(
        int tabId,
        int moduleId,
        int moduleOrder,
        string paneName,
        CancellationToken cancellationToken = default)
    {
        var module = await _context.Modules
            .FirstOrDefaultAsync(m => m.ModuleId == moduleId && m.TabId == tabId, cancellationToken)
            .ConfigureAwait(false);

        if (module is null)
        {
            throw new InvalidOperationException($"Module with ID {moduleId} not found on tab {tabId}.");
        }

        var currentOrder = module.ModuleOrder;
        var currentPane = module.PaneName;

        // Handle -1 as "add to end"
        int newOrder;
        if (moduleOrder == -1)
        {
            var maxOrder = await _context.Modules
                .Where(m => m.TabId == tabId && m.PaneName == paneName && !m.IsDeleted)
                .MaxAsync(m => (int?)m.ModuleOrder, cancellationToken)
                .ConfigureAwait(false);
            newOrder = (maxOrder ?? 0) + 1;
        }
        else
        {
            newOrder = moduleOrder;
        }

        // If changing panes, just update pane and order
        if (currentPane != paneName)
        {
            module.PaneName = paneName;
            module.ModuleOrder = newOrder;
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        // Same pane - need to reorder other modules
        if (currentOrder == newOrder)
        {
            return; // No change needed
        }

        // Get all modules in the same pane
        var modulesInPane = await _context.Modules
            .Where(m => m.TabId == tabId && m.PaneName == paneName && !m.IsDeleted && m.ModuleId != moduleId)
            .OrderBy(m => m.ModuleOrder)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Reorder modules
        if (newOrder < currentOrder)
        {
            // Moving up - increment order of modules between new and current position
            foreach (var m in modulesInPane.Where(m => m.ModuleOrder >= newOrder && m.ModuleOrder < currentOrder))
            {
                m.ModuleOrder++;
            }
        }
        else
        {
            // Moving down - decrement order of modules between current and new position
            foreach (var m in modulesInPane.Where(m => m.ModuleOrder > currentOrder && m.ModuleOrder <= newOrder))
            {
                m.ModuleOrder--;
            }
        }

        module.ModuleOrder = newOrder;

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    #endregion

    #region Module Settings Operations

    /// <summary>
    /// Gets all settings for a module.
    /// </summary>
    /// <param name="moduleId">The module identifier.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A dictionary of setting names and values.</returns>
    /// <remarks>
    /// MIGRATION: Replaces GetModuleSettings from ModuleController.vb
    /// Uses raw SQL since ModuleSetting entity is not in the DbContext scope
    /// </remarks>
    public async Task<Dictionary<string, string>> GetModuleSettingsAsync(
        int moduleId,
        CancellationToken cancellationToken = default)
    {
        var settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // MIGRATION: Raw SQL query to ModuleSettings table
        // Original: DataProvider.Instance().GetModuleSettings(moduleId)
        var sql = @"SELECT SettingName, SettingValue 
                    FROM {0}ModuleSettings 
                    WHERE ModuleID = {1}";

        var formattedSql = FormattableStringFactory.Create(
            sql.Replace("{0}", GetDatabaseOwnerPrefix()).Replace("{1}", "{0}"),
            moduleId);

        try
        {
            var results = await _context.Database
                .SqlQuery<ModuleSettingRow>(formattedSql)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            foreach (var row in results)
            {
                if (!string.IsNullOrEmpty(row.SettingName))
                {
                    settings[row.SettingName] = row.SettingValue ?? string.Empty;
                }
            }
        }
        catch (Exception)
        {
            // Return empty dictionary if table doesn't exist or query fails
            // This maintains backward compatibility during migration
        }

        return settings;
    }

    /// <summary>
    /// Adds or updates a module setting value.
    /// </summary>
    /// <param name="moduleId">The module identifier.</param>
    /// <param name="settingName">The name of the setting.</param>
    /// <param name="settingValue">The value of the setting.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <remarks>
    /// MIGRATION: Replaces UpdateModuleSetting from ModuleController.vb
    /// Uses upsert pattern via raw SQL
    /// </remarks>
    public async Task UpdateModuleSettingAsync(
        int moduleId,
        string settingName,
        string settingValue,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(settingName);

        // MIGRATION: Uses MERGE (upsert) pattern for SQL Server
        // Original: DataProvider.Instance().UpdateModuleSetting(moduleId, settingName, settingValue)
        var sql = @"MERGE {0}ModuleSettings AS target
                    USING (SELECT @p0 AS ModuleID, @p1 AS SettingName, @p2 AS SettingValue) AS source
                    ON target.ModuleID = source.ModuleID AND target.SettingName = source.SettingName
                    WHEN MATCHED THEN
                        UPDATE SET SettingValue = source.SettingValue
                    WHEN NOT MATCHED THEN
                        INSERT (ModuleID, SettingName, SettingValue)
                        VALUES (source.ModuleID, source.SettingName, source.SettingValue);";

        var formattedSql = sql.Replace("{0}", GetDatabaseOwnerPrefix());

        await _context.Database
            .ExecuteSqlRawAsync(formattedSql, [moduleId, settingName, settingValue ?? string.Empty], cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Deletes a specific setting from a module.
    /// </summary>
    /// <param name="moduleId">The module identifier.</param>
    /// <param name="settingName">The name of the setting to delete.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <remarks>
    /// MIGRATION: Replaces DeleteModuleSetting pattern
    /// </remarks>
    public async Task DeleteModuleSettingAsync(
        int moduleId,
        string settingName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(settingName);

        var sql = @"DELETE FROM {0}ModuleSettings 
                    WHERE ModuleID = @p0 AND SettingName = @p1";

        var formattedSql = sql.Replace("{0}", GetDatabaseOwnerPrefix());

        await _context.Database
            .ExecuteSqlRawAsync(formattedSql, [moduleId, settingName], cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Deletes all settings from a module.
    /// </summary>
    /// <param name="moduleId">The module identifier.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <remarks>
    /// MIGRATION: Replaces DeleteModuleSettings pattern for cleanup
    /// </remarks>
    public async Task DeleteModuleSettingsAsync(
        int moduleId,
        CancellationToken cancellationToken = default)
    {
        var sql = @"DELETE FROM {0}ModuleSettings WHERE ModuleID = @p0";

        var formattedSql = sql.Replace("{0}", GetDatabaseOwnerPrefix());

        await _context.Database
            .ExecuteSqlRawAsync(formattedSql, [moduleId], cancellationToken)
            .ConfigureAwait(false);
    }

    #endregion

    #region Tab-Module Settings Operations

    /// <summary>
    /// Gets all settings for a tab-module instance.
    /// </summary>
    /// <param name="tabModuleId">The tab-module identifier.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A dictionary of setting names and values.</returns>
    /// <remarks>
    /// MIGRATION: Replaces GetTabModuleSettings pattern
    /// TabModuleSettings are tab-specific settings for a module instance
    /// </remarks>
    public async Task<Dictionary<string, string>> GetTabModuleSettingsAsync(
        int tabModuleId,
        CancellationToken cancellationToken = default)
    {
        var settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var sql = @"SELECT SettingName, SettingValue 
                    FROM {0}TabModuleSettings 
                    WHERE TabModuleID = {1}";

        var formattedSql = FormattableStringFactory.Create(
            sql.Replace("{0}", GetDatabaseOwnerPrefix()).Replace("{1}", "{0}"),
            tabModuleId);

        try
        {
            var results = await _context.Database
                .SqlQuery<ModuleSettingRow>(formattedSql)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            foreach (var row in results)
            {
                if (!string.IsNullOrEmpty(row.SettingName))
                {
                    settings[row.SettingName] = row.SettingValue ?? string.Empty;
                }
            }
        }
        catch (Exception)
        {
            // Return empty dictionary if table doesn't exist or query fails
        }

        return settings;
    }

    /// <summary>
    /// Adds or updates a tab-module setting value.
    /// </summary>
    /// <param name="tabModuleId">The tab-module identifier.</param>
    /// <param name="settingName">The name of the setting.</param>
    /// <param name="settingValue">The value of the setting.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <remarks>
    /// MIGRATION: Replaces UpdateTabModuleSetting pattern
    /// Uses upsert pattern via raw SQL for SQL Server
    /// </remarks>
    public async Task UpdateTabModuleSettingAsync(
        int tabModuleId,
        string settingName,
        string settingValue,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(settingName);

        var sql = @"MERGE {0}TabModuleSettings AS target
                    USING (SELECT @p0 AS TabModuleID, @p1 AS SettingName, @p2 AS SettingValue) AS source
                    ON target.TabModuleID = source.TabModuleID AND target.SettingName = source.SettingName
                    WHEN MATCHED THEN
                        UPDATE SET SettingValue = source.SettingValue
                    WHEN NOT MATCHED THEN
                        INSERT (TabModuleID, SettingName, SettingValue)
                        VALUES (source.TabModuleID, source.SettingName, source.SettingValue);";

        var formattedSql = sql.Replace("{0}", GetDatabaseOwnerPrefix());

        await _context.Database
            .ExecuteSqlRawAsync(formattedSql, [tabModuleId, settingName, settingValue ?? string.Empty], cancellationToken)
            .ConfigureAwait(false);
    }

    #endregion

    #region Private Helper Methods

    /// <summary>
    /// Gets the database owner prefix for SQL queries.
    /// </summary>
    /// <returns>The database owner prefix (default: "dbo.").</returns>
    /// <remarks>
    /// MIGRATION: Replaces DatabaseOwner and ObjectQualifier pattern from legacy code
    /// In the new architecture, we default to "dbo." schema
    /// </remarks>
    private static string GetDatabaseOwnerPrefix()
    {
        // Default to dbo schema - can be made configurable if needed
        return "dbo.";
    }

    /// <summary>
    /// Copies tab-module settings from one tab-module to another.
    /// </summary>
    /// <param name="sourceTabModuleId">The source tab-module identifier.</param>
    /// <param name="targetTabModuleId">The target tab-module identifier.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    private async Task CopyTabModuleSettingsAsync(
        int sourceTabModuleId,
        int targetTabModuleId,
        CancellationToken cancellationToken)
    {
        var settings = await GetTabModuleSettingsAsync(sourceTabModuleId, cancellationToken).ConfigureAwait(false);

        foreach (var setting in settings)
        {
            await UpdateTabModuleSettingAsync(targetTabModuleId, setting.Key, setting.Value, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    #endregion
}

/// <summary>
/// Internal DTO for reading module settings from raw SQL queries.
/// </summary>
/// <remarks>
/// Used by SqlQuery to materialize setting rows from ModuleSettings/TabModuleSettings tables.
/// </remarks>
internal sealed class ModuleSettingRow
{
    /// <summary>
    /// Gets or sets the setting name.
    /// </summary>
    public string? SettingName { get; set; }

    /// <summary>
    /// Gets or sets the setting value.
    /// </summary>
    public string? SettingValue { get; set; }
}
