namespace DnnMigration.Domain.Entities;

// MIGRATION: NEW relational placement entity introduced to restore schema fidelity (ADR-002).
// In legacy DotNetNuke 4.9.0.85 a module's placement on a page lives in the physical [TabModules] table
// (TabID, ModuleID, ModuleOrder, PaneName, Visibility, ...), while module-definition data lives in the
// [Modules] table. The legacy ModuleInfo value object FLATTENED both tables into a single object, so CP2
// ModuleConfiguration Ignore()s the [TabModules]-sourced members on the Module entity (they are NOT
// physical [Modules] columns). This POCO maps the real [TabModules] table so the repository can JOIN
// [TabModules] -> [Modules] (reproducing the legacy ModuleController.GetTabModules) WITHOUT querying the
// ignored Module members, which would not translate against the preserved SQL Server schema.
// The legacy copyright header and Imports are dropped. EF Core persistence/column mapping is configured
// separately via Fluent API (IEntityTypeConfiguration<TabModule> in TabModuleConfiguration) in the
// Infrastructure layer; this Domain type intentionally carries zero framework dependencies.

/// <summary>
/// Pure POCO entity for the DotNetNuke <c>TabModules</c> table, which records the placement of a
/// <see cref="Module"/> on a <see cref="Tab"/> (page): its ordering, pane, visibility and display options.
/// </summary>
/// <remarks>
/// This type maps the physical placement table that the legacy flattened <c>ModuleInfo</c> denormalized
/// onto the module object. It carries data only and has no framework dependencies, so the Domain layer
/// remains the dependency-free inner ring of the Clean Architecture solution. The repository projects these
/// placement columns onto the corresponding (CP2-ignored) <see cref="Module"/> members so the returned
/// graph reproduces the legacy denormalized surface consumed by the Module DTO.
/// </remarks>
public class TabModule
{
    /// <summary>
    /// Gets or sets the unique identifier of the tab-module placement (primary key, physical column
    /// <c>TabModuleID</c>).
    /// </summary>
    public int TabModuleID { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the tab (page) the module is placed on (foreign key to
    /// <see cref="Tab"/>).
    /// </summary>
    public int TabID { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the placed module (foreign key to <see cref="Module"/>).
    /// </summary>
    public int ModuleID { get; set; }

    /// <summary>
    /// Gets or sets the name of the skin pane the module renders in.
    /// </summary>
    public string? PaneName { get; set; }

    /// <summary>
    /// Gets or sets the render order of the module within its pane.
    /// </summary>
    public int ModuleOrder { get; set; }

    /// <summary>
    /// Gets or sets the output-cache duration, in seconds.
    /// </summary>
    public int CacheTime { get; set; }

    /// <summary>
    /// Gets or sets the module alignment within its pane.
    /// </summary>
    public string? Alignment { get; set; }

    /// <summary>
    /// Gets or sets the module background color.
    /// </summary>
    public string? Color { get; set; }

    /// <summary>
    /// Gets or sets the module border width.
    /// </summary>
    public string? Border { get; set; }

    /// <summary>
    /// Gets or sets the path of the module icon file.
    /// </summary>
    public string? IconFile { get; set; }

    /// <summary>
    /// Gets or sets the raw visibility state as stored in the physical <c>[TabModules].[Visibility]</c>
    /// integer column. The repository converts this to the
    /// <see cref="DnnMigration.Domain.Enums.VisibilityState"/> enum when projecting onto
    /// <see cref="Module.Visibility"/>; it is kept as a raw <see cref="int"/> here so this POCO is a faithful
    /// mirror of the physical column type.
    /// </summary>
    public int Visibility { get; set; }

    /// <summary>
    /// Gets or sets the path of the module container (skin) source.
    /// </summary>
    public string? ContainerSrc { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the module title is displayed.
    /// </summary>
    public bool DisplayTitle { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the print affordance is displayed.
    /// </summary>
    public bool DisplayPrint { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the syndicate affordance is displayed.
    /// </summary>
    public bool DisplaySyndicate { get; set; }
}
