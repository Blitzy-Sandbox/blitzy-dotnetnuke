using DnnMigration.Domain.Enums;

namespace DnnMigration.Domain.Entities;

/// <summary>
/// Module (content module instance) entity.
/// </summary>
// MIGRATION: Converted from VB.NET DotNetNuke.Entities.Modules.ModuleInfo
// (Library/Components/Modules/ModuleInfo.vb, L36, XmlRoot "module"). The IPropertyAccess
// implementation (GetProperty/Cacheability) and all XmlIgnore render/runtime properties are dropped;
// only persisted data/state properties are retained. VB `Date` -> `DateTime`.
public class Module
{
    public int PortalID { get; set; }
    public int TabID { get; set; }
    public int TabModuleID { get; set; }
    public int ModuleID { get; set; }
    public int ModuleDefID { get; set; }
    public int ModuleOrder { get; set; }
    public string PaneName { get; set; } = string.Empty;

    // MIGRATION: legacy XML element "title" (property name ModuleTitle differs from element name)
    public string ModuleTitle { get; set; } = string.Empty;

    public int CacheTime { get; set; }
    public string Alignment { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public string Border { get; set; } = string.Empty;
    public string IconFile { get; set; } = string.Empty;
    public bool AllTabs { get; set; }

    // MIGRATION: legacy nested enum VisibilityState (Maximized=0, Minimized=1, None=2). Not one of
    // the three in-scope Domain enums; kept as int to avoid introducing an out-of-scope enum type.
    public int Visibility { get; set; }

    public bool IsDeleted { get; set; }
    public string Header { get; set; } = string.Empty;
    public string Footer { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string ContainerSrc { get; set; } = string.Empty;
    public bool DisplayTitle { get; set; }
    public bool DisplayPrint { get; set; }
    public bool DisplaySyndicate { get; set; }
    public bool InheritViewPermissions { get; set; }

    // MIGRATION: legacy ModulePermissionCollection -> ICollection<ModulePermission>
    public ICollection<ModulePermission> ModulePermissions { get; set; } = new List<ModulePermission>();

    public int DesktopModuleID { get; set; }
    public string FriendlyName { get; set; } = string.Empty;
    public string FolderName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public bool IsPremium { get; set; }
    public bool IsAdmin { get; set; }
    public string BusinessControllerClass { get; set; } = string.Empty;
    public string ModuleName { get; set; } = string.Empty;
    public int SupportedFeatures { get; set; }
    public string CompatibleVersions { get; set; } = string.Empty;
    public string Dependencies { get; set; } = string.Empty;
    public string Permissions { get; set; } = string.Empty;
    public int DefaultCacheTime { get; set; }
    public int ModuleControlId { get; set; }
    public string ControlSrc { get; set; } = string.Empty;

    // MIGRATION: legacy Integer-backed "controltype"; strongly typed to SecurityAccessLevel enum.
    public SecurityAccessLevel ControlType { get; set; }

    public string ControlTitle { get; set; } = string.Empty;
    public string HelpUrl { get; set; } = string.Empty;
    public bool SupportsPartialRendering { get; set; }
    public string AuthorizedEditRoles { get; set; } = string.Empty;
    public string AuthorizedViewRoles { get; set; } = string.Empty;

    // MIGRATION: The following legacy XmlIgnore render/runtime/computed members are intentionally
    // omitted because they hold presentation/runtime state rather than persisted data:
    // ContainerPath, PaneModuleIndex, PaneModuleCount, IsDefaultModule, AllModules, and the read-only
    // feature flags IsPortable/IsSearchable/IsUpgradeable (derived from SupportedFeatures via the
    // legacy GetFeature helper), plus the deprecated AuthorizedRoles. The IPropertyAccess token
    // members (GetProperty/Cacheability) and the Clone()/Initialize() helpers are likewise dropped;
    // the Domain layer retains persisted data only.
}
