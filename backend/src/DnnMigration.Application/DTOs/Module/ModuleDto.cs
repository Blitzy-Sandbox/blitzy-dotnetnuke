using DnnMigration.Domain.Enums;

namespace DnnMigration.Application.DTOs.Module;

// MIGRATION: Read projection of ModuleInfo.vb (DotNetNuke.Entities.Modules). Anti-corruption boundary
// between the Module domain entity and the REST API (ModulesController -> GET /api/v1/modules). Legacy
// Null.NullString sentinels -> nullable string; Null.NullDate -> DateTime?. The legacy
// ModulePermissionCollection is intentionally NOT exposed as a nav collection on this DTO. IsDeleted is
// surfaced for admin views only (Create/Update DTOs omit it).

/// <summary>
/// Full read projection for a Module resource. This is the response shape returned by
/// <c>ModulesController</c> for <c>GET /api/v1/modules</c> and <c>GET /api/v1/modules/{id}</c>, forming
/// the read side of the anti-corruption boundary between the <c>Module</c> domain entity and the REST API.
/// AutoMapper maps <c>Module</c> -&gt; <see cref="ModuleDto"/> via the sibling <c>ModuleProfile</c>; the
/// property names and types here mirror the <c>Module</c> entity exactly so the mapping requires no
/// per-member configuration.
/// </summary>
public class ModuleDto
{
    /// <summary>Primary key of the module (ModuleID).</summary>
    public int ModuleID { get; set; } // PK

    /// <summary>Identifier of the portal that owns this module.</summary>
    public int PortalID { get; set; }

    /// <summary>Identifier of the tab (page) on which this module instance appears.</summary>
    public int TabID { get; set; }

    /// <summary>Identifier of the tab-module association row.</summary>
    public int TabModuleID { get; set; }

    /// <summary>Identifier of the module definition this instance is based on.</summary>
    public int ModuleDefID { get; set; }

    /// <summary>Ordinal position of the module within its pane.</summary>
    public int ModuleOrder { get; set; }

    /// <summary>Name of the skin pane the module is rendered into. Null when unset.</summary>
    public string? PaneName { get; set; }

    /// <summary>Display title of the module (legacy XML element name was "title").</summary>
    public string? ModuleTitle { get; set; }

    /// <summary>Output cache duration, in seconds.</summary>
    public int CacheTime { get; set; }

    /// <summary>Horizontal alignment of the module content. Null when unset.</summary>
    public string? Alignment { get; set; }

    /// <summary>Background color override. Null when unset.</summary>
    public string? Color { get; set; }

    /// <summary>Border width/style override. Null when unset.</summary>
    public string? Border { get; set; }

    /// <summary>Path to the module's icon file. Null when unset.</summary>
    public string? IconFile { get; set; }

    /// <summary>When true, the module is displayed on all tabs of the portal.</summary>
    public bool AllTabs { get; set; }

    /// <summary>Visibility state of the module (Maximized=0, Minimized=1, None=2).</summary>
    public VisibilityState Visibility { get; set; }

    /// <summary>When true, the module title is rendered in the container.</summary>
    public bool DisplayTitle { get; set; }

    /// <summary>When true, the print action is rendered for the module.</summary>
    public bool DisplayPrint { get; set; }

    /// <summary>When true, the syndication (RSS) action is rendered for the module.</summary>
    public bool DisplaySyndicate { get; set; }

    /// <summary>Custom HTML rendered above the module content. Null when unset.</summary>
    public string? Header { get; set; }

    /// <summary>Custom HTML rendered below the module content. Null when unset.</summary>
    public string? Footer { get; set; }

    /// <summary>Date from which the module becomes visible. Null when no start constraint.</summary>
    public DateTime? StartDate { get; set; }

    /// <summary>Date after which the module is no longer visible. Null when no end constraint.</summary>
    public DateTime? EndDate { get; set; }

    /// <summary>Path to the container skin used to render the module. Null when unset.</summary>
    public string? ContainerSrc { get; set; }

    /// <summary>When true, the module inherits view permissions from its tab.</summary>
    public bool InheritViewPermissions { get; set; }

    /// <summary>Identifier of the desktop module backing this instance.</summary>
    public int DesktopModuleID { get; set; }

    /// <summary>Friendly (human-readable) name of the desktop module. Null when unset.</summary>
    public string? FriendlyName { get; set; }

    /// <summary>Description of the desktop module. Null when unset.</summary>
    public string? Description { get; set; }

    /// <summary>Version string of the desktop module. Null when unset.</summary>
    public string? Version { get; set; }

    /// <summary>
    /// Soft-delete state. Exposed on this read DTO for admin views only; Create/Update DTOs omit it.
    /// </summary>
    public bool IsDeleted { get; set; }
}
