namespace DnnMigration.Domain.Entities;

// MIGRATION: Ported from ModuleDefinitionInfo.vb (DotNetNuke.Entities.Modules.Definitions). Placed in the
// flat DnnMigration.Domain.Entities namespace (legacy ".Definitions" sub-namespace collapsed). The legacy
// VB constructor initialized DefaultCacheTime to 0; in C# the int default is already 0, so no initializer
// or explicit constructor is required. ModuleDefinitionController/ModuleDefinitionValidator are out of
// scope. EF mapping is supplied via Fluent configuration in the Infrastructure layer (this entity stays a
// pure, framework-free POCO with zero attributes, interfaces, or using directives).
public class ModuleDefinition
{
    // Primary key (legacy ModuleDefID).
    public int ModuleDefID { get; set; }

    // Human-readable definition name (legacy FriendlyName); nullable to mirror the legacy String field.
    public string? FriendlyName { get; set; }

    // Foreign key to the owning DesktopModule (legacy DesktopModuleID).
    public int DesktopModuleID { get; set; }

    // Transient module identifier used during import/installation flows (legacy TempModuleID).
    public int TempModuleID { get; set; }

    // Default output-cache duration in seconds (legacy DefaultCacheTime; legacy ctor default 0).
    public int DefaultCacheTime { get; set; }
}
