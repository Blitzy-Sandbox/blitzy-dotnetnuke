using DnnMigration.Domain.Enums;

namespace DnnMigration.Domain.Entities;

// MIGRATION: legacy ModuleInfo is a denormalized merge of Module/DesktopModule/ModuleDefinition/ModuleControl.
// The persisted columns are ported here; the EF configuration in Infrastructure maps them to the correct tables.
// IPropertyAccess (GetProperty/Cacheability), XmlIgnore runtime/computed helpers (ContainerPath,
// PaneModuleIndex/Count, IsDefaultModule, AllModules, IsPortable/IsSearchable/IsUpgradeable, AuthorizedRoles)
// are dropped.

/// <summary>
/// Pure POCO Module aggregate-root entity — the C# port of the legacy DotNetNuke
/// <c>ModuleInfo</c> "fat" value object (<c>Library/Components/Modules/ModuleInfo.vb</c>,
/// VB <c>Namespace DotNetNuke.Entities.Modules</c>).
/// </summary>
/// <remarks>
/// <para>
/// The legacy <c>ModuleInfo</c> is a denormalized object that merges columns sourced from four
/// relational tables — Module, DesktopModule, ModuleDefinition, and ModuleControl. To preserve the
/// public contract and domain semantics exactly (AAP §0.7.1), those persisted columns are carried
/// verbatim on this single entity; the EF Core <c>IEntityTypeConfiguration&lt;Module&gt;</c> in the
/// Infrastructure layer is responsible for mapping each property to its correct table and column,
/// honoring ADR-002 (the existing DNN <c>4.9.0.85</c> schema is mapped unchanged).
/// </para>
/// <para>
/// Domain-layer purity is maintained: this type carries zero framework dependencies — no EF Core or
/// DataAnnotation attributes, no XML serialization attributes, and no interface implementations. The
/// legacy VB <c>Property Get/Set</c> backing-field style is converted to C# auto-properties; the
/// <c>IPropertyAccess</c> token-engine members (<c>GetProperty</c>/<c>Cacheability</c>), the
/// <c>XmlIgnore</c> runtime/computed helpers (<c>ContainerPath</c>, <c>PaneModuleIndex</c>,
/// <c>PaneModuleCount</c>, <c>IsDefaultModule</c>, <c>AllModules</c>, <c>IsPortable</c>,
/// <c>IsSearchable</c>, <c>IsUpgradeable</c>, <c>AuthorizedRoles</c>), and the <c>Clone</c>/helper
/// methods are intentionally dropped. The <c>IsDeleted</c> soft-delete flag is preserved because the
/// module list filter relies on it (AAP §0.4.1), and permission data is exposed through the
/// <see cref="ModulePermissions"/> navigation collection rather than the legacy role-CSV projections.
/// </para>
/// </remarks>
public class Module
{
    // ============================================================================================
    // Core module columns (legacy Modules / TabModules tables).
    // ============================================================================================

    /// <summary>
    /// Gets or sets the identifier of the portal the module belongs to (foreign key). Required
    /// relationship key — kept non-nullable even though the legacy constructor seeded it with the
    /// <c>Null.NullInteger</c> sentinel.
    /// </summary>
    public int PortalID { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the tab (page) the module instance is placed on (foreign key).
    /// </summary>
    public int TabID { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the tab-module association row that links this module instance
    /// to a specific tab (foreign key).
    /// </summary>
    public int TabModuleID { get; set; }

    /// <summary>
    /// Gets or sets the unique identifier of the module (primary key).
    /// </summary>
    public int ModuleID { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the module definition this module is an instance of (foreign key).
    /// </summary>
    public int ModuleDefID { get; set; }

    /// <summary>
    /// Gets or sets the ordinal position of the module within its content pane.
    /// </summary>
    public int ModuleOrder { get; set; }

    /// <summary>
    /// Gets or sets the name of the content pane (layout zone) the module is rendered in.
    /// </summary>
    public string? PaneName { get; set; }

    /// <summary>
    /// Gets or sets the display title of the module instance.
    /// </summary>
    public string? ModuleTitle { get; set; }

    /// <summary>
    /// Gets or sets the output cache duration, in seconds, for the module.
    /// </summary>
    public int CacheTime { get; set; }

    /// <summary>
    /// Gets or sets the horizontal alignment of the module's content.
    /// </summary>
    public string? Alignment { get; set; }

    /// <summary>
    /// Gets or sets the background color applied to the module's container.
    /// </summary>
    public string? Color { get; set; }

    /// <summary>
    /// Gets or sets the border width applied to the module's container.
    /// </summary>
    public string? Border { get; set; }

    /// <summary>
    /// Gets or sets the relative path to the icon file displayed for the module.
    /// </summary>
    public string? IconFile { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the module is displayed on all tabs (pages) of the portal.
    /// </summary>
    public bool AllTabs { get; set; }

    /// <summary>
    /// Gets or sets the module's visibility state (maximized, minimized, or none).
    /// </summary>
    public VisibilityState Visibility { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the module is soft-deleted. Preserved from the legacy
    /// model; the module list query filters on this flag rather than physically removing rows.
    /// </summary>
    public bool IsDeleted { get; set; }

    /// <summary>
    /// Gets or sets the HTML rendered in the module's header region.
    /// </summary>
    public string? Header { get; set; }

    /// <summary>
    /// Gets or sets the HTML rendered in the module's footer region.
    /// </summary>
    public string? Footer { get; set; }

    /// <summary>
    /// Gets or sets the date from which the module becomes visible. Nullable — the legacy constructor
    /// seeded this with the <c>Null.NullDate</c> sentinel, mapped here to a null <see cref="DateTime"/>.
    /// </summary>
    public DateTime? StartDate { get; set; }

    /// <summary>
    /// Gets or sets the date after which the module is no longer visible. Nullable — the legacy
    /// constructor seeded this with the <c>Null.NullDate</c> sentinel, mapped here to a null
    /// <see cref="DateTime"/>.
    /// </summary>
    public DateTime? EndDate { get; set; }

    /// <summary>
    /// Gets or sets the relative path to the container skin that wraps the module.
    /// </summary>
    public string? ContainerSrc { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the module title is displayed. Defaults to
    /// <c>true</c>, matching the legacy constructor.
    /// </summary>
    public bool DisplayTitle { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the print action is displayed for the module. Defaults
    /// to <c>true</c>, matching the legacy constructor.
    /// </summary>
    public bool DisplayPrint { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the syndication (RSS) action is displayed for the
    /// module. Defaults to <c>false</c>, matching the legacy constructor.
    /// </summary>
    public bool DisplaySyndicate { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether the module inherits view permissions from its tab (page).
    /// </summary>
    public bool InheritViewPermissions { get; set; }

    // ============================================================================================
    // Desktop-module / module-definition columns carried on the legacy denormalized fat object.
    // EF Core maps these to the DesktopModules / ModuleDefinitions tables in the Infrastructure layer.
    // ============================================================================================

    /// <summary>
    /// Gets or sets the identifier of the desktop module (the installed module package) this module
    /// instance derives from (foreign key).
    /// </summary>
    public int DesktopModuleID { get; set; }

    /// <summary>
    /// Gets or sets the friendly display name of the desktop module.
    /// </summary>
    public string? FriendlyName { get; set; }

    /// <summary>
    /// Gets or sets the name of the folder containing the desktop module's resources.
    /// </summary>
    public string? FolderName { get; set; }

    /// <summary>
    /// Gets or sets the description of the desktop module.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the version string of the desktop module.
    /// </summary>
    public string? Version { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the desktop module is a premium (restricted-access) module.
    /// </summary>
    public bool IsPremium { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the desktop module is an administrative module.
    /// </summary>
    public bool IsAdmin { get; set; }

    /// <summary>
    /// Gets or sets the fully qualified name of the business-controller class that implements the
    /// desktop module's optional feature interfaces.
    /// </summary>
    public string? BusinessControllerClass { get; set; }

    /// <summary>
    /// Gets or sets the unique programmatic name of the desktop module.
    /// </summary>
    public string? ModuleName { get; set; }

    /// <summary>
    /// Gets or sets the bit flags describing which optional features the desktop module supports
    /// (the legacy <c>DesktopModuleSupportedFeature</c> bitmask: portability, search, upgradeability).
    /// </summary>
    public int SupportedFeatures { get; set; }

    // ============================================================================================
    // Module-control columns carried on the legacy denormalized fat object.
    // EF Core maps these to the ModuleControls table in the Infrastructure layer.
    // ============================================================================================

    /// <summary>
    /// Gets or sets the identifier of the module control (the UI control definition) associated with
    /// this module (foreign key).
    /// </summary>
    public int ModuleControlId { get; set; }

    /// <summary>
    /// Gets or sets the relative source path of the module control.
    /// </summary>
    public string? ControlSrc { get; set; }

    // MIGRATION: legacy ControlType was SecurityAccessLevel (DotNetNuke.Security), which is out of scope for Enums/.
    // Mapped to int here; the integer value preserves the legacy enum ordinal.
    /// <summary>
    /// Gets or sets the security access level required to render the module control. Stored as an
    /// <see cref="int"/> that preserves the ordinal of the legacy <c>SecurityAccessLevel</c> enum.
    /// </summary>
    public int ControlType { get; set; }

    /// <summary>
    /// Gets or sets the display title of the module control.
    /// </summary>
    public string? ControlTitle { get; set; }

    /// <summary>
    /// Gets or sets the URL of the help resource for the module control.
    /// </summary>
    public string? HelpUrl { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the module control supports partial (AJAX) rendering.
    /// </summary>
    public bool SupportsPartialRendering { get; set; }

    // ============================================================================================
    // Navigation collections.
    // ============================================================================================

    // MIGRATION: legacy role-CSV projections (AuthorizedRoles/AuthorizedEditRoles/AuthorizedViewRoles)
    // are replaced by the ModulePermissions navigation. The legacy VB type was
    // Security.Permissions.ModulePermissionCollection; it becomes an EF Core navigation collection.
    /// <summary>
    /// Gets or sets the collection of permission grants/denials that govern access to this module.
    /// Replaces the legacy <c>ModulePermissionCollection</c> and the role-CSV projections.
    /// </summary>
    public ICollection<ModulePermission> ModulePermissions { get; set; } = new List<ModulePermission>();
}
