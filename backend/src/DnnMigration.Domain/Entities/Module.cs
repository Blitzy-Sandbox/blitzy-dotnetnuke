using DnnMigration.Domain.Enums;

namespace DnnMigration.Domain.Entities;

// MIGRATION: Converted from VB.NET DotNetNuke.Entities.Modules.ModuleInfo (Library/Components/Modules/ModuleInfo.vb).
// Renamed ModuleInfo -> Module; XML attributes and IPropertyAccess removed; persistence-ignorant POCO.
// Scoped by PortalId (tenant) + TabId (page placement).
// MIGRATION: Dropped legacy runtime/presentation members (ContainerPath, PaneModuleIndex, PaneModuleCount,
// IsDefaultModule, AllModules), the computed feature flags (IsPortable/IsSearchable/IsUpgradeable + the GetFeature
// helper, derivable from SupportedFeatures), the Clone/Initialize helpers, and the IPropertyAccess implementation.
public class Module
{
    // MIGRATION: Multi-tenant discriminator. Legacy PortalID initialized to Null.NullInteger (-1) -> nullable int.
    public int? PortalId { get; set; }

    // MIGRATION: Page placement. Legacy TabID initialized to Null.NullInteger (-1) -> nullable int.
    public int? TabId { get; set; }

    public int? TabModuleId { get; set; }

    public int? ModuleId { get; set; }

    public int? ModuleDefId { get; set; }

    public int ModuleOrder { get; set; }

    public string? PaneName { get; set; }

    public string? ModuleTitle { get; set; }

    public int CacheTime { get; set; }

    public string? Alignment { get; set; }

    public string? Color { get; set; }

    public string? Border { get; set; }

    public string? IconFile { get; set; }

    public bool AllTabs { get; set; }

    // MIGRATION: Legacy Visibility was the inline enum VisibilityState (Maximized=0, Minimized=1, None=2).
    // The enum is not reintroduced in this phase; stored as int matching the integer DB column.
    public int Visibility { get; set; }

    public bool IsDeleted { get; set; }

    public string? Header { get; set; }

    public string? Footer { get; set; }

    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public string? ContainerSrc { get; set; }

    public bool DisplayTitle { get; set; } = true;

    public bool DisplayPrint { get; set; } = true;

    public bool DisplaySyndicate { get; set; }

    public bool InheritViewPermissions { get; set; }

    public int DesktopModuleId { get; set; }

    public string? FriendlyName { get; set; }

    public string? FolderName { get; set; }

    public string? Description { get; set; }

    public string? Version { get; set; }

    public bool IsPremium { get; set; }

    public bool IsAdmin { get; set; }

    public string? BusinessControllerClass { get; set; }

    public string? ModuleName { get; set; }

    public int SupportedFeatures { get; set; }

    public string? CompatibleVersions { get; set; }

    public string? Dependencies { get; set; }

    public string? Permissions { get; set; }

    public int DefaultCacheTime { get; set; }

    public int ModuleControlId { get; set; }

    public string? ControlSrc { get; set; }

    // MIGRATION: Typed with the migrated SecurityAccessLevel enum (DnnMigration.Domain.Enums). Default Anonymous (legacy ctor default).
    public SecurityAccessLevel ControlType { get; set; } = SecurityAccessLevel.Anonymous;

    public string? ControlTitle { get; set; }

    public string? HelpUrl { get; set; }

    public bool SupportsPartialRendering { get; set; }

    public string? AuthorizedEditRoles { get; set; }

    public string? AuthorizedViewRoles { get; set; }

    // MIGRATION: Legacy <XmlIgnore> AuthorizedRoles, commented "should be deprecated due to roles being abstracted".
    // Retained (real backing-field property) for behavioral parity; prefer AuthorizedEditRoles/AuthorizedViewRoles.
    public string? AuthorizedRoles { get; set; }

    // MIGRATION: Replaces legacy ModulePermissionCollection. Module is secured by its module permissions.
    public ICollection<ModulePermission> ModulePermissions { get; set; } = new List<ModulePermission>();
}
