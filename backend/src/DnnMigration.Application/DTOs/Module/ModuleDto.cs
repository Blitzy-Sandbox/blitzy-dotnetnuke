// -----------------------------------------------------------------------------
// DnnMigration - Module Data Transfer Object
// MIGRATION: Converted from DotNetNuke.Entities.Modules.ModuleInfo (VB.NET)
// Source: Library/Components/Modules/ModuleInfo.vb (lines 36-635)
// -----------------------------------------------------------------------------

namespace DnnMigration.Application.DTOs.Module;

/// <summary>
/// Data Transfer Object representing a module instance in API responses.
/// MIGRATION: Converted from VB.NET ModuleInfo entity with properties mapped
/// from the legacy DotNetNuke module system.
/// </summary>
/// <remarks>
/// This DTO is used by:
/// - GET /api/modules - Returns PagedResult&lt;ModuleDto&gt;
/// - GET /api/modules/{id} - Returns single ModuleDto
/// 
/// The record type provides immutability and value semantics appropriate for API responses.
/// </remarks>
public record ModuleDto
{
    #region Core Identity Properties

    /// <summary>
    /// Gets the unique identifier for this module instance.
    /// MIGRATION: From _ModuleID (line 46), VB Integer → C# int
    /// </summary>
    public int ModuleId { get; init; }

    /// <summary>
    /// Gets the tab-module relationship identifier, unique per tab placement.
    /// MIGRATION: From _TabModuleID (line 45), VB Integer → C# int
    /// </summary>
    public int TabModuleId { get; init; }

    /// <summary>
    /// Gets the identifier of the tab/page where this module is placed.
    /// MIGRATION: From _TabID (line 44), VB Integer → C# int
    /// </summary>
    public int TabId { get; init; }

    /// <summary>
    /// Gets the identifier of the portal that owns this module.
    /// MIGRATION: From _PortalID (line 43), VB Integer → C# int
    /// </summary>
    public int PortalId { get; init; }

    /// <summary>
    /// Gets the module definition identifier that defines this module type.
    /// MIGRATION: From _ModuleDefID (line 47), VB Integer → C# int
    /// </summary>
    public int ModuleDefId { get; init; }

    #endregion

    #region Placement and Display Properties

    /// <summary>
    /// Gets the display order of this module within its pane.
    /// MIGRATION: From _ModuleOrder (line 47), VB Integer → C# int
    /// </summary>
    public int ModuleOrder { get; init; }

    /// <summary>
    /// Gets the name of the pane where this module is rendered (e.g., "ContentPane", "LeftPane").
    /// MIGRATION: From _PaneName (line 49), VB String → C# string
    /// </summary>
    public string PaneName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the display title shown in the module header.
    /// MIGRATION: From _ModuleTitle (line 50), nullable
    /// </summary>
    public string? ModuleTitle { get; init; }

    /// <summary>
    /// Gets the horizontal alignment of the module content.
    /// MIGRATION: From _Alignment (line 54), nullable
    /// </summary>
    public string? Alignment { get; init; }

    /// <summary>
    /// Gets the background color for the module container.
    /// MIGRATION: From _Color (line 55), nullable
    /// </summary>
    public string? Color { get; init; }

    /// <summary>
    /// Gets the border style for the module container.
    /// MIGRATION: From _Border (line 56), nullable
    /// </summary>
    public string? Border { get; init; }

    /// <summary>
    /// Gets the path to the module's icon file.
    /// MIGRATION: From _IconFile (line 57), nullable
    /// </summary>
    public string? IconFile { get; init; }

    /// <summary>
    /// Gets the cache duration in seconds for this module's content.
    /// MIGRATION: From _CacheTime (line 52), VB Integer → C# int
    /// </summary>
    public int CacheTime { get; init; }

    #endregion

    #region Visibility and Scheduling Properties

    /// <summary>
    /// Gets the visibility state of the module.
    /// MIGRATION: From _Visibility (line 59), VB VisibilityState enum → int
    /// Values: 0 = Maximized (fully visible), 1 = Minimized (collapsed), 2 = None (hidden)
    /// </summary>
    public int Visibility { get; init; }

    /// <summary>
    /// Gets a value indicating whether this module has been soft-deleted.
    /// MIGRATION: From _IsDeleted (line 61), VB Boolean → C# bool
    /// </summary>
    public bool IsDeleted { get; init; }

    /// <summary>
    /// Gets the date and time when this module becomes visible.
    /// MIGRATION: From _StartDate (line 64), VB Date → C# DateTime?
    /// Null indicates no scheduled start (immediately visible).
    /// </summary>
    public DateTime? StartDate { get; init; }

    /// <summary>
    /// Gets the date and time when this module becomes hidden.
    /// MIGRATION: From _EndDate (line 65), VB Date → C# DateTime?
    /// Null indicates no scheduled end (remains visible indefinitely).
    /// </summary>
    public DateTime? EndDate { get; init; }

    #endregion

    #region Display Options Properties

    /// <summary>
    /// Gets the path to the container skin source file.
    /// MIGRATION: From _ContainerSrc (line 66), nullable
    /// </summary>
    public string? ContainerSrc { get; init; }

    /// <summary>
    /// Gets a value indicating whether to display the module title.
    /// MIGRATION: From _DisplayTitle (line 67), VB Boolean → C# bool
    /// </summary>
    public bool DisplayTitle { get; init; }

    /// <summary>
    /// Gets a value indicating whether to display the print button.
    /// MIGRATION: From _DisplayPrint (line 68), VB Boolean → C# bool
    /// </summary>
    public bool DisplayPrint { get; init; }

    /// <summary>
    /// Gets a value indicating whether to display the RSS/syndication link.
    /// MIGRATION: From _DisplaySyndicate (line 69), VB Boolean → C# bool
    /// </summary>
    public bool DisplaySyndicate { get; init; }

    /// <summary>
    /// Gets the HTML content to display above the module content.
    /// MIGRATION: From _Header (line 62), nullable
    /// </summary>
    public string? Header { get; init; }

    /// <summary>
    /// Gets the HTML content to display below the module content.
    /// MIGRATION: From _Footer (line 63), nullable
    /// </summary>
    public string? Footer { get; init; }

    #endregion

    #region Permission and Behavior Properties

    /// <summary>
    /// Gets a value indicating whether view permissions are inherited from the parent tab.
    /// MIGRATION: From _InheritViewPermissions (line 70), VB Boolean → C# bool
    /// </summary>
    public bool InheritViewPermissions { get; init; }

    /// <summary>
    /// Gets a value indicating whether this module appears on all tabs in the portal.
    /// MIGRATION: From _AllTabs (line 58), VB Boolean → C# bool
    /// </summary>
    public bool AllTabs { get; init; }

    #endregion

    #region Module Definition Metadata Properties

    /// <summary>
    /// Gets the desktop module identifier for the parent module definition.
    /// MIGRATION: From _DesktopModuleID (line 72), VB Integer → C# int
    /// </summary>
    public int DesktopModuleId { get; init; }

    /// <summary>
    /// Gets the user-friendly display name of the module definition.
    /// MIGRATION: From _FriendlyName (line 74), nullable
    /// </summary>
    public string? FriendlyName { get; init; }

    /// <summary>
    /// Gets the internal system name of the module.
    /// MIGRATION: From _ModuleName (line 80), nullable
    /// </summary>
    public string? ModuleName { get; init; }

    /// <summary>
    /// Gets the folder name where module files are located.
    /// MIGRATION: From _FolderName (line 73), nullable
    /// </summary>
    public string? FolderName { get; init; }

    /// <summary>
    /// Gets the description of the module functionality.
    /// MIGRATION: From _Description (line 75), nullable
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets the version string of the module definition.
    /// MIGRATION: From _Version (line 76), nullable
    /// </summary>
    public string? Version { get; init; }

    #endregion

    #region Computed Feature Properties

    /// <summary>
    /// Gets a value indicating whether this module supports import/export functionality.
    /// MIGRATION: From IsPortable readonly property (lines 608-612)
    /// Computed from SupportedFeatures bitmask flag.
    /// </summary>
    public bool IsPortable { get; init; }

    /// <summary>
    /// Gets a value indicating whether this module's content is searchable.
    /// MIGRATION: From IsSearchable readonly property (lines 614-618)
    /// Computed from SupportedFeatures bitmask flag.
    /// </summary>
    public bool IsSearchable { get; init; }

    /// <summary>
    /// Gets a value indicating whether this module supports automatic upgrades.
    /// MIGRATION: From IsUpgradeable readonly property (lines 620-624)
    /// Computed from SupportedFeatures bitmask flag.
    /// </summary>
    public bool IsUpgradeable { get; init; }

    #endregion
}
