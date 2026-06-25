using DnnMigration.Domain.Enums;

namespace DnnMigration.Application.DTOs.Module;

// MIGRATION: Read model for GET /api/modules and /api/modules/{id} (AAP 0.3.4). Projected from the Domain entity
// DnnMigration.Domain.Entities.Module (legacy Library/Components/Modules/ModuleInfo.vb). Immutable record carrying
// only scalar persistent fields. No business logic, no data access, no raw entity types exposed.
public record ModuleResponse
{
    // --- Identity / scoping keys (nullable, mirroring the entity; legacy Null.NullInteger sentinels) ---

    // MIGRATION: Multi-tenant discriminator (AAP 0.7.1). Legacy PortalID (Null.NullInteger) -> int?.
    public int? PortalId { get; init; }

    // MIGRATION: Page placement. Legacy TabID (Null.NullInteger) -> int?.
    public int? TabId { get; init; }

    public int? TabModuleId { get; init; }

    public int? ModuleId { get; init; }

    public int? ModuleDefId { get; init; }

    // --- Placement / display ---
    public int ModuleOrder { get; init; }

    public string? PaneName { get; init; }

    public string? ModuleTitle { get; init; }

    public int CacheTime { get; init; }

    public string? Alignment { get; init; }

    public string? Color { get; init; }

    public string? Border { get; init; }

    public string? IconFile { get; init; }

    public bool AllTabs { get; init; }

    // MIGRATION: Visibility kept as int. Legacy VisibilityState enum (Maximized=0, Minimized=1, None=2) was
    // intentionally NOT migrated to a C# enum (mirrors the Domain entity decision); the DB column is integer.
    public int Visibility { get; init; }

    public bool IsDeleted { get; init; }

    public string? Header { get; init; }

    public string? Footer { get; init; }

    public DateTime? StartDate { get; init; }

    public DateTime? EndDate { get; init; }

    public string? ContainerSrc { get; init; }

    public bool DisplayTitle { get; init; }

    public bool DisplayPrint { get; init; }

    public bool DisplaySyndicate { get; init; }

    public bool InheritViewPermissions { get; init; }

    // --- Desktop-module / definition metadata ---
    public int DesktopModuleId { get; init; }

    public string? FriendlyName { get; init; }

    public string? FolderName { get; init; }

    public string? Description { get; init; }

    public string? Version { get; init; }

    public bool IsPremium { get; init; }

    public bool IsAdmin { get; init; }

    public string? BusinessControllerClass { get; init; }

    public string? ModuleName { get; init; }

    public int SupportedFeatures { get; init; }

    public string? CompatibleVersions { get; init; }

    public string? Dependencies { get; init; }

    public string? Permissions { get; init; }

    public int DefaultCacheTime { get; init; }

    // --- Module-control metadata ---
    public int ModuleControlId { get; init; }

    public string? ControlSrc { get; init; }

    // MIGRATION: ControlType surfaced as the migrated SecurityAccessLevel enum (DnnMigration.Domain.Enums) for
    // type-safety (the folder spec permits referencing the Domain enum transitively). Legacy ModuleInfo.ControlType
    // was the same enum (ControlPanel=-3, SkinObject=-2, Anonymous=-1, View=0, Edit=1, Admin=2, Host=3).
    public SecurityAccessLevel ControlType { get; init; }

    public string? ControlTitle { get; init; }

    public string? HelpUrl { get; init; }

    public bool SupportsPartialRendering { get; init; }

    public string? AuthorizedEditRoles { get; init; }

    public string? AuthorizedViewRoles { get; init; }

    // MIGRATION: Legacy <XmlIgnore> AuthorizedRoles (commented "should be deprecated due to roles being abstracted").
    // Surfaced for behavioral parity; consumers should prefer AuthorizedEditRoles/AuthorizedViewRoles.
    public string? AuthorizedRoles { get; init; }

    // MIGRATION: ModulePermissions (entity ICollection<ModulePermission>) intentionally OMITTED from this response.
    // Exposing the raw entity collection would over-fetch (AAP 0.7.7) and leak a Domain entity type
    // (folder spec: "No raw entities exposed"). Module-permission management is a separate concern.
}
