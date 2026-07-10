namespace DnnMigration.Application.DTOs;

/// <summary>
/// Request payload to update an existing module instance's settings.
/// Bound from the body of <c>PUT /api/modules/{id}</c>; the module id is
/// supplied by the route and is deliberately absent from the body, as is the
/// placement identity (<c>PortalID</c>/<c>TabID</c>/<c>ModuleDefID</c>), which
/// is never re-assigned through an update. Validation is applied externally by
/// <c>ModuleValidator</c> (FluentValidation), so this type carries no
/// attributes, logic, or data access — it is a pure data-transfer contract.
/// MIGRATION: editable subset of legacy ModuleInfo
/// (Library/Components/Modules/ModuleInfo.vb). The legacy VisibilityState enum
/// is mirrored as an <see cref="int"/> and the legacy Date fields are mirrored
/// as nullable <see cref="System.DateTime"/> to match the Module entity.
/// </summary>
public record UpdateModuleDto
{
    /// <summary>Display title shown for the module instance (legacy <c>ModuleTitle</c>).</summary>
    public string ModuleTitle { get; init; } = string.Empty;

    /// <summary>Layout pane the module is rendered in (legacy <c>PaneName</c>); optional.</summary>
    public string? PaneName { get; init; }

    /// <summary>Ordering position of the module within its pane (legacy <c>ModuleOrder</c>).</summary>
    public int ModuleOrder { get; init; }

    /// <summary>Output cache duration in seconds (legacy <c>CacheTime</c>).</summary>
    public int CacheTime { get; init; }

    /// <summary>Horizontal alignment of the module container (legacy <c>Alignment</c>); optional.</summary>
    public string? Alignment { get; init; }

    /// <summary>Background color of the module container (legacy <c>Color</c>); optional.</summary>
    public string? Color { get; init; }

    /// <summary>Border width of the module container (legacy <c>Border</c>); optional.</summary>
    public string? Border { get; init; }

    /// <summary>Path to the module icon file (legacy <c>IconFile</c>); optional.</summary>
    public string? IconFile { get; init; }

    /// <summary>Whether the module appears on all tabs/pages (legacy <c>AllTabs</c>).</summary>
    public bool AllTabs { get; init; }

    /// <summary>
    /// Visibility state of the module. MIGRATION: mirrors the legacy
    /// <c>VisibilityState</c> enum (Maximized/Minimized/None) as an integer.
    /// </summary>
    public int Visibility { get; init; }

    /// <summary>Custom header markup rendered above the module (legacy <c>Header</c>); optional.</summary>
    public string? Header { get; init; }

    /// <summary>Custom footer markup rendered below the module (legacy <c>Footer</c>); optional.</summary>
    public string? Footer { get; init; }

    /// <summary>Date on which the module becomes visible (legacy <c>StartDate</c>); optional.</summary>
    public DateTime? StartDate { get; init; }

    /// <summary>Date after which the module is no longer visible (legacy <c>EndDate</c>); optional.</summary>
    public DateTime? EndDate { get; init; }

    /// <summary>Path to the module container skin (legacy <c>ContainerSrc</c>); optional.</summary>
    public string? ContainerSrc { get; init; }

    /// <summary>Whether the module title is displayed (legacy <c>DisplayTitle</c>).</summary>
    public bool DisplayTitle { get; init; }

    /// <summary>Whether the print action is offered for the module (legacy <c>DisplayPrint</c>).</summary>
    public bool DisplayPrint { get; init; }

    /// <summary>Whether the syndication (RSS) action is offered (legacy <c>DisplaySyndicate</c>).</summary>
    public bool DisplaySyndicate { get; init; }

    /// <summary>Whether view permissions are inherited from the page (legacy <c>InheritViewPermissions</c>).</summary>
    public bool InheritViewPermissions { get; init; }
}
