namespace DnnMigration.Application.DTOs;

/// <summary>
/// Read-only projection of a module instance returned by the module read
/// endpoints (<c>GET /api/modules</c> and <c>GET /api/modules/{id}</c>).
/// </summary>
/// <remarks>
/// MIGRATION: Mapped (via AutoMapper) from the Domain entity
/// <c>DnnMigration.Domain.Entities.Module</c>, which itself derives from the
/// legacy VB.NET <c>ModuleInfo</c> class
/// (<c>Library/Components/Modules/ModuleInfo.vb</c>). This DTO is a pure data
/// carrier: it contains no behavior, no data access, and no serialization
/// attributes. The response envelope (<c>{ "data": ..., "meta": ... }</c>) is
/// applied by the API layer, not by this type.
/// <para>
/// MIGRATION: <see cref="Visibility"/> is intentionally typed as
/// <see cref="int"/> rather than an enum. The legacy <c>VisibilityState</c>
/// enum (Maximized/Minimized/None) is preserved as an <see cref="int"/> on the
/// Domain entity, so this projection mirrors that type to keep the mapping
/// one-to-one. Legacy plumbing fields that are not part of the migration
/// surface are deliberately excluded from this projection.
/// </para>
/// </remarks>
public record ModuleDto
{
    /// <summary>Identifier of the portal (site) that owns this module instance.</summary>
    public int PortalID { get; init; }

    /// <summary>Identifier of the tab (page) the module is placed on.</summary>
    public int TabID { get; init; }

    /// <summary>Identifier of the tab-module placement (module-on-page instance).</summary>
    public int TabModuleID { get; init; }

    /// <summary>Identifier of the module instance.</summary>
    public int ModuleID { get; init; }

    /// <summary>Identifier of the module definition backing this instance.</summary>
    public int ModuleDefID { get; init; }

    /// <summary>Render order of the module within its pane.</summary>
    public int ModuleOrder { get; init; }

    /// <summary>Name of the layout pane the module is rendered in.</summary>
    public string PaneName { get; init; } = string.Empty;

    /// <summary>Display title of the module.</summary>
    public string ModuleTitle { get; init; } = string.Empty;

    /// <summary>Output cache duration, in seconds.</summary>
    public int CacheTime { get; init; }

    /// <summary>Horizontal alignment of the module content.</summary>
    public string Alignment { get; init; } = string.Empty;

    /// <summary>Background color applied to the module container.</summary>
    public string Color { get; init; } = string.Empty;

    /// <summary>Border width applied to the module container.</summary>
    public string Border { get; init; } = string.Empty;

    /// <summary>Path to the icon file associated with the module.</summary>
    public string IconFile { get; init; } = string.Empty;

    /// <summary>Indicates whether the module is displayed on all tabs.</summary>
    public bool AllTabs { get; init; }

    /// <summary>
    /// Visibility state of the module. MIGRATION: kept as <see cref="int"/> to
    /// mirror the legacy <c>VisibilityState</c> enum (0 = Maximized,
    /// 1 = Minimized, 2 = None) preserved on the Domain entity.
    /// </summary>
    public int Visibility { get; init; }

    /// <summary>Optional HTML rendered above the module content.</summary>
    public string Header { get; init; } = string.Empty;

    /// <summary>Optional HTML rendered below the module content.</summary>
    public string Footer { get; init; } = string.Empty;

    /// <summary>Date from which the module becomes visible.</summary>
    public DateTime StartDate { get; init; }

    /// <summary>Date after which the module is no longer visible.</summary>
    public DateTime EndDate { get; init; }

    /// <summary>Path to the container skin used to render the module.</summary>
    public string ContainerSrc { get; init; } = string.Empty;

    /// <summary>Indicates whether the module title is displayed.</summary>
    public bool DisplayTitle { get; init; }

    /// <summary>Indicates whether the print action is displayed.</summary>
    public bool DisplayPrint { get; init; }

    /// <summary>Indicates whether the syndicate (RSS) action is displayed.</summary>
    public bool DisplaySyndicate { get; init; }

    /// <summary>Indicates whether the module inherits view permissions from its tab.</summary>
    public bool InheritViewPermissions { get; init; }

    /// <summary>Identifier of the desktop module registration.</summary>
    public int DesktopModuleID { get; init; }

    /// <summary>Friendly name of the desktop module.</summary>
    public string FriendlyName { get; init; } = string.Empty;

    /// <summary>Folder name where the module's resources reside.</summary>
    public string FolderName { get; init; } = string.Empty;

    /// <summary>Description of the module.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Version string of the module.</summary>
    public string Version { get; init; } = string.Empty;

    /// <summary>Internal name of the module.</summary>
    public string ModuleName { get; init; } = string.Empty;

    /// <summary>Path to the module control (user control) that renders the module.</summary>
    public string ControlSrc { get; init; } = string.Empty;
}
