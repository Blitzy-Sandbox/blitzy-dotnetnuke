// -----------------------------------------------------------------------------
// DnnMigration - Modern DotNetNuke Migration
// Copyright (c) 2024. All rights reserved.
// Licensed under the MIT License.
// -----------------------------------------------------------------------------
// MIGRATION: VB.NET DotNetNuke.Entities.Modules.ModuleInfo → C# 12 Module entity
// Source: Library/Components/Modules/ModuleInfo.vb
// Changes:
// - Converted from VB.NET Class to C# 12 class with file-scoped namespace
// - Removed IPropertyAccess implementation (token replacement handled by services)
// - Removed XmlElement attributes (EF Core uses Fluent API)
// - Removed denormalized properties from DesktopModule (FolderName, ModuleName, etc.)
// - Removed runtime-only properties (ContainerPath, PaneModuleIndex, PaneModuleCount)
// - Converted VB Date to C# DateTime?
// - Applied nullable reference types
// - Added navigation properties for Portal, Tab, ModuleDefinition
// - Added collection for ModulePermissions
// - Added XML documentation comments
// -----------------------------------------------------------------------------

using DnnMigration.Domain.Enums;

namespace DnnMigration.Domain.Entities;

/// <summary>
/// Represents a module instance placed on a tab within a portal.
/// </summary>
/// <remarks>
/// <para>
/// This entity maps to the Modules and TabModules tables. A module is an instance
/// of a <see cref="ModuleDefinition"/> placed on a specific <see cref="Tab"/> within
/// a <see cref="Portal"/>.
/// </para>
/// <para>
/// MIGRATION: Converted from VB.NET DotNetNuke.Entities.Modules.ModuleInfo.
/// Denormalized properties from DesktopModule have been removed - use navigation
/// properties to access related entity data.
/// </para>
/// </remarks>
public class Module
{
    /// <summary>
    /// Gets or sets the unique identifier for the module instance.
    /// </summary>
    /// <value>The primary key of the module record.</value>
    public int ModuleId { get; set; }

    /// <summary>
    /// Gets or sets the portal identifier that this module belongs to.
    /// </summary>
    /// <value>The foreign key to the Portal entity.</value>
    public int PortalId { get; set; }

    /// <summary>
    /// Gets or sets the tab identifier where this module is placed.
    /// </summary>
    /// <value>The foreign key to the Tab entity.</value>
    public int TabId { get; set; }

    /// <summary>
    /// Gets or sets the tab-module identifier for this specific placement.
    /// </summary>
    /// <value>The identifier linking the module to a specific tab placement.</value>
    /// <remarks>
    /// A module can be placed on multiple tabs, each with a different TabModuleId.
    /// </remarks>
    public int TabModuleId { get; set; }

    /// <summary>
    /// Gets or sets the module definition identifier.
    /// </summary>
    /// <value>The foreign key to the ModuleDefinition entity.</value>
    public int ModuleDefId { get; set; }

    /// <summary>
    /// Gets or sets the display order of the module within its pane.
    /// </summary>
    /// <value>The order in which the module appears in the pane.</value>
    public int ModuleOrder { get; set; }

    /// <summary>
    /// Gets or sets the name of the pane where the module is placed.
    /// </summary>
    /// <value>The pane name (e.g., "ContentPane", "LeftPane"). May be null.</value>
    public string? PaneName { get; set; }

    /// <summary>
    /// Gets or sets the title of the module.
    /// </summary>
    /// <value>The display title for the module. May be null.</value>
    public string? ModuleTitle { get; set; }

    /// <summary>
    /// Gets or sets the cache time in seconds.
    /// </summary>
    /// <value>The number of seconds to cache module output.</value>
    public int CacheTime { get; set; }

    /// <summary>
    /// Gets or sets the content alignment.
    /// </summary>
    /// <value>The alignment (e.g., "left", "center", "right"). May be null.</value>
    public string? Alignment { get; set; }

    /// <summary>
    /// Gets or sets the background color.
    /// </summary>
    /// <value>The background color value. May be null.</value>
    public string? Color { get; set; }

    /// <summary>
    /// Gets or sets the border style.
    /// </summary>
    /// <value>The border style value. May be null.</value>
    public string? Border { get; set; }

    /// <summary>
    /// Gets or sets the icon file path.
    /// </summary>
    /// <value>The relative path to the module's icon. May be null.</value>
    public string? IconFile { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the module appears on all tabs.
    /// </summary>
    /// <value><c>true</c> if the module is on all tabs; otherwise, <c>false</c>.</value>
    public bool AllTabs { get; set; }

    /// <summary>
    /// Gets or sets the visibility state of the module.
    /// </summary>
    /// <value>The visibility state (Maximized, Minimized, or None).</value>
    public VisibilityState Visibility { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the module is deleted (soft delete).
    /// </summary>
    /// <value><c>true</c> if the module is deleted; otherwise, <c>false</c>.</value>
    public bool IsDeleted { get; set; }

    /// <summary>
    /// Gets or sets the header content for the module.
    /// </summary>
    /// <value>Custom HTML to display above the module content. May be null.</value>
    public string? Header { get; set; }

    /// <summary>
    /// Gets or sets the footer content for the module.
    /// </summary>
    /// <value>Custom HTML to display below the module content. May be null.</value>
    public string? Footer { get; set; }

    /// <summary>
    /// Gets or sets the start date for module visibility.
    /// </summary>
    /// <value>The date when the module becomes visible. May be null for immediate visibility.</value>
    public DateTime? StartDate { get; set; }

    /// <summary>
    /// Gets or sets the end date for module visibility.
    /// </summary>
    /// <value>The date when the module becomes hidden. May be null for no end date.</value>
    public DateTime? EndDate { get; set; }

    /// <summary>
    /// Gets or sets the container source path.
    /// </summary>
    /// <value>The path to the container template. May be null for default container.</value>
    public string? ContainerSrc { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to display the module title.
    /// </summary>
    /// <value><c>true</c> if the title should be displayed; otherwise, <c>false</c>.</value>
    public bool DisplayTitle { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to display the print button.
    /// </summary>
    /// <value><c>true</c> if the print button should be displayed; otherwise, <c>false</c>.</value>
    public bool DisplayPrint { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to display the syndicate (RSS) button.
    /// </summary>
    /// <value><c>true</c> if syndication should be enabled; otherwise, <c>false</c>.</value>
    public bool DisplaySyndicate { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether view permissions are inherited from the tab.
    /// </summary>
    /// <value><c>true</c> if permissions are inherited; otherwise, <c>false</c>.</value>
    public bool InheritViewPermissions { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this is a default module.
    /// </summary>
    /// <value><c>true</c> if this is a default module; otherwise, <c>false</c>.</value>
    public bool IsDefaultModule { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this module appears on all modules.
    /// </summary>
    /// <value><c>true</c> if this applies to all modules; otherwise, <c>false</c>.</value>
    public bool AllModules { get; set; }

    /// <summary>
    /// Gets or sets the authorized view roles (legacy property).
    /// </summary>
    /// <value>A semicolon-separated list of role IDs. May be null.</value>
    /// <remarks>
    /// MIGRATION: Legacy property. Consider using ModulePermissions instead.
    /// </remarks>
    public string? AuthorizedViewRoles { get; set; }

    /// <summary>
    /// Gets or sets the authorized edit roles (legacy property).
    /// </summary>
    /// <value>A semicolon-separated list of role IDs. May be null.</value>
    /// <remarks>
    /// MIGRATION: Legacy property. Consider using ModulePermissions instead.
    /// </remarks>
    public string? AuthorizedEditRoles { get; set; }

    // Navigation properties

    /// <summary>
    /// Gets or sets the portal that this module belongs to.
    /// </summary>
    /// <value>Navigation property to the <see cref="Portal"/> entity.</value>
    public virtual Portal? Portal { get; set; }

    /// <summary>
    /// Gets or sets the tab where this module is placed.
    /// </summary>
    /// <value>Navigation property to the <see cref="Tab"/> entity.</value>
    public virtual Tab? Tab { get; set; }

    /// <summary>
    /// Gets or sets the module definition for this module instance.
    /// </summary>
    /// <value>Navigation property to the <see cref="ModuleDefinition"/> entity.</value>
    public virtual ModuleDefinition? ModuleDefinition { get; set; }

    /// <summary>
    /// Gets or sets the collection of module permissions.
    /// </summary>
    /// <value>A collection of <see cref="ModulePermission"/> entities for this module.</value>
    public virtual ICollection<ModulePermission> ModulePermissions { get; set; } = new List<ModulePermission>();
}
