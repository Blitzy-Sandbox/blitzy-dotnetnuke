using DnnMigration.Domain.Enums;

namespace DnnMigration.Application.DTOs.Module;

/// <summary>
/// Full read projection of a module, returned by <c>ModulesController</c>
/// (<c>GET /api/v1/modules</c> and <c>GET /api/v1/modules/{id}</c>). Forms the read side of the
/// anti-corruption boundary between the <c>Module</c> domain entity and the REST API; AutoMapper maps
/// <c>Module</c> -&gt; <see cref="ModuleDto"/> via <c>ModuleProfile</c>, so the property names and types
/// here mirror the entity exactly to permit a frictionless 1:1 mapping with no per-member configuration.
/// </summary>
// MIGRATION: Read projection of ModuleInfo.vb (DotNetNuke.Entities.Modules). Anti-corruption boundary
// between the Module domain entity and the REST API (ModulesController -> GET /api/v1/modules). Legacy
// Null.NullString sentinels -> nullable string; Null.NullDate -> DateTime?. The legacy
// ModulePermissionCollection is intentionally NOT exposed as a nav collection on this DTO. IsDeleted is
// surfaced for admin views only (Create/Update DTOs omit it).
public class ModuleDto
{
    public int ModuleID { get; set; }            // PK
    public int PortalID { get; set; }
    public int TabID { get; set; }
    public int TabModuleID { get; set; }
    public int ModuleDefID { get; set; }
    public int ModuleOrder { get; set; }
    public string? PaneName { get; set; }
    public string? ModuleTitle { get; set; }     // legacy XML name was "title"; keep the C# member name ModuleTitle
    public int CacheTime { get; set; }
    public string? Alignment { get; set; }
    public string? Color { get; set; }
    public string? Border { get; set; }
    public string? IconFile { get; set; }
    public bool AllTabs { get; set; }
    public VisibilityState Visibility { get; set; }   // Domain enum (Maximized=0, Minimized=1, None=2)
    public bool DisplayTitle { get; set; }
    public bool DisplayPrint { get; set; }
    public bool DisplaySyndicate { get; set; }
    public string? Header { get; set; }
    public string? Footer { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? ContainerSrc { get; set; }
    public bool InheritViewPermissions { get; set; }
    public int DesktopModuleID { get; set; }
    public string? FriendlyName { get; set; }
    public string? Description { get; set; }
    public string? Version { get; set; }
    public bool IsDeleted { get; set; }
}
