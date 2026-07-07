namespace DnnMigration.Application.DTOs;

/// <summary>
/// Request payload used to create (place) a module instance on a tab.
/// Bound from the body of <c>POST /api/modules</c>, validated by
/// <c>ModuleValidator</c> (FluentValidation) and projected onto the
/// <c>Module</c> domain entity by AutoMapper (<c>MappingProfile</c>).
/// </summary>
/// <remarks>
/// MIGRATION: This is the writable/placement subset of the legacy VB.NET
/// <c>ModuleInfo</c> class (Library/Components/Modules/ModuleInfo.vb) that the
/// legacy Module Settings screen (Website/admin/Modules/**) allowed an editor
/// to supply when adding a module to a page. Read-only / server-derived fields
/// of <c>ModuleInfo</c> (ModuleID, TabModuleID, IsDeleted, DesktopModuleID,
/// FriendlyName, Version, permission collections, and so on) are intentionally
/// excluded from the create contract.
///
/// Type-mirroring with the <c>Module</c> entity is preserved verbatim:
/// the legacy <c>VisibilityState</c> enum is surfaced as an <see cref="int"/>
/// (<c>Visibility</c>); the legacy VB <c>Date</c> schedule fields become
/// nullable <see cref="System.DateTime"/> (<c>StartDate</c>/<c>EndDate</c>)
/// because a display schedule is optional at creation time.
///
/// This is a pure data-transfer contract: it carries no behaviour, no data
/// access, and no validation attributes (validation is expressed in
/// FluentValidation) and it is not wrapped in the { data, meta } envelope.
/// </remarks>
public record CreateModuleDto
{
    /// <summary>Identifier of the portal that owns the module instance.</summary>
    public int PortalID { get; init; }

    /// <summary>Identifier of the tab (page) on which the module is placed.</summary>
    public int TabID { get; init; }

    /// <summary>Identifier of the module definition being instantiated.</summary>
    public int ModuleDefID { get; init; }

    /// <summary>Display title shown for the module instance.</summary>
    public string ModuleTitle { get; init; } = string.Empty;

    /// <summary>Name of the content pane the module is placed into.</summary>
    public string PaneName { get; init; } = string.Empty;

    /// <summary>Sort order of the module within its pane.</summary>
    public int ModuleOrder { get; init; }

    /// <summary>Output cache duration, in seconds.</summary>
    public int CacheTime { get; init; }

    /// <summary>Optional horizontal alignment token for the container.</summary>
    public string? Alignment { get; init; }

    /// <summary>Optional background color for the container.</summary>
    public string? Color { get; init; }

    /// <summary>Optional border width/style for the container.</summary>
    public string? Border { get; init; }

    /// <summary>Optional relative path to the module's icon file.</summary>
    public string? IconFile { get; init; }

    /// <summary>When <see langword="true"/>, the module is displayed on all tabs.</summary>
    public bool AllTabs { get; init; }

    /// <summary>
    /// Visibility state of the module container.
    /// MIGRATION: mirrors the legacy <c>VisibilityState</c> enum as an
    /// <see cref="int"/> (0 = Maximized, 1 = Minimized, 2 = None).
    /// </summary>
    public int Visibility { get; init; }

    /// <summary>Optional custom header markup rendered above the module content.</summary>
    public string? Header { get; init; }

    /// <summary>Optional custom footer markup rendered below the module content.</summary>
    public string? Footer { get; init; }

    /// <summary>
    /// Optional date on which the module starts being displayed.
    /// MIGRATION: legacy VB <c>Date</c> becomes a nullable
    /// <see cref="System.DateTime"/>; a schedule is optional on create.
    /// </summary>
    public DateTime? StartDate { get; init; }

    /// <summary>
    /// Optional date on which the module stops being displayed.
    /// MIGRATION: legacy VB <c>Date</c> becomes a nullable
    /// <see cref="System.DateTime"/>; a schedule is optional on create.
    /// </summary>
    public DateTime? EndDate { get; init; }

    /// <summary>Optional relative path to the container skin (.ascx) source.</summary>
    public string? ContainerSrc { get; init; }

    /// <summary>Whether the module title is rendered in the container.</summary>
    public bool DisplayTitle { get; init; }

    /// <summary>Whether the print action is shown for the module.</summary>
    public bool DisplayPrint { get; init; }

    /// <summary>Whether the RSS/syndication action is shown for the module.</summary>
    public bool DisplaySyndicate { get; init; }

    /// <summary>Whether the module instance inherits view permissions from its tab.</summary>
    public bool InheritViewPermissions { get; init; }
}
