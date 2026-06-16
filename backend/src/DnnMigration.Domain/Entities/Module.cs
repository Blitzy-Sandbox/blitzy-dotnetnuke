using DnnMigration.Domain.Enums;

namespace DnnMigration.Domain.Entities;

// MIGRATION: legacy ModuleInfo is a denormalized merge of Module/DesktopModule/ModuleDefinition/ModuleControl.
// The persisted columns are ported here; the EF configuration in Infrastructure maps them to the correct tables.
// IPropertyAccess (GetProperty/Cacheability), XmlIgnore runtime/computed helpers (ContainerPath,
// PaneModuleIndex/Count, IsDefaultModule, AllModules, IsPortable/IsSearchable/IsUpgradeable, AuthorizedRoles)
// are dropped.
//
// Source of truth: Library/Components/Modules/ModuleInfo.vb (VB class ModuleInfo : IPropertyAccess in
// Namespace DotNetNuke.Entities.Modules). Pure POCO aggregate-root entity for the Domain layer: ZERO
// framework dependencies (no EF/DataAnnotation attributes, no XML serialization attributes). Public-contract
// and domain semantics are preserved (AAP §0.7.1), including the IsDeleted soft-delete flag (AAP §0.4.1).
public class Module
{
    // ---- Core module columns (legacy Module table) ----

    // MIGRATION: legacy ctor seeded these identity/relationship keys with Null.NullInteger, but they are
    // required EF keys/FKs and are therefore kept non-nullable int (a missing key is invalid, not null).
    public int PortalID { get; set; }

    public int TabID { get; set; }

    public int TabModuleID { get; set; }

    public int ModuleID { get; set; }

    public int ModuleDefID { get; set; }

    public int ModuleOrder { get; set; }

    public string? PaneName { get; set; }

    public string? ModuleTitle { get; set; }

    public int CacheTime { get; set; }

    public string? Alignment { get; set; }

    public string? Color { get; set; }

    public string? Border { get; set; }

    public string? IconFile { get; set; }

    public bool AllTabs { get; set; }

    public VisibilityState Visibility { get; set; }

    // MIGRATION: IsDeleted soft-delete flag preserved verbatim (AAP §0.4.1); the ModuleService list filter relies on it.
    public bool IsDeleted { get; set; }

    public string? Header { get; set; }

    public string? Footer { get; set; }

    // MIGRATION: VB Date with Null.NullDate sentinel -> nullable DateTime (absence is represented as null).
    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public string? ContainerSrc { get; set; }

    // MIGRATION: legacy ctor defaulted DisplayTitle = True; preserved via initializer.
    public bool DisplayTitle { get; set; } = true;

    // MIGRATION: legacy ctor defaulted DisplayPrint = True; preserved via initializer.
    public bool DisplayPrint { get; set; } = true;

    // MIGRATION: legacy ctor defaulted DisplaySyndicate = False; preserved via initializer.
    public bool DisplaySyndicate { get; set; } = false;

    public bool InheritViewPermissions { get; set; }

    // ---- Desktop-module / module-definition columns carried on the legacy fat object ----

    public int DesktopModuleID { get; set; }

    public string? FriendlyName { get; set; }

    public string? FolderName { get; set; }

    public string? Description { get; set; }

    public string? Version { get; set; }

    public bool IsPremium { get; set; }

    public bool IsAdmin { get; set; }

    public string? BusinessControllerClass { get; set; }

    public string? ModuleName { get; set; }

    public int SupportedFeatures { get; set; }

    // ---- Module-control columns ----

    public int ModuleControlId { get; set; }

    public string? ControlSrc { get; set; }

    // MIGRATION: legacy ControlType was SecurityAccessLevel (DotNetNuke.Security), which is out of scope for Enums/.
    // Mapped to int here; the integer value preserves the legacy enum ordinal.
    public int ControlType { get; set; }

    public string? ControlTitle { get; set; }

    public string? HelpUrl { get; set; }

    public bool SupportsPartialRendering { get; set; }

    // ---- Navigation collection ----

    // MIGRATION: legacy ModulePermissions As Security.Permissions.ModulePermissionCollection -> EF navigation.
    // Legacy role-CSV projections (AuthorizedRoles/AuthorizedEditRoles/AuthorizedViewRoles) are replaced by
    // this ModulePermissions navigation as the authoritative persisted permission data.
    public ICollection<ModulePermission> ModulePermissions { get; set; } = new List<ModulePermission>();
}
