namespace DnnMigration.Domain.Entities;

// MIGRATION: Ported from ModuleDefinitionInfo.vb (legacy namespace DotNetNuke.Entities.Modules.Definitions).
// The legacy ".Definitions" sub-namespace is intentionally collapsed into the flat
// DnnMigration.Domain.Entities namespace. The legacy private backing fields + Property Get/Set blocks
// become C# auto-properties. The legacy parameterless constructor only assigned DefaultCacheTime = 0,
// which is already the default value for a C# int, so no field initializer or constructor is required.
// ModuleDefinitionController and ModuleDefinitionValidator remain out of scope.
// EF Core persistence mapping for this entity is supplied by Fluent configuration in the Infrastructure layer;
// this Domain entity stays a pure POCO with zero framework dependencies (no attributes, interfaces, or usings).
public class ModuleDefinition
{
    // Primary key. Legacy field: _ModuleDefID (Integer).
    public int ModuleDefID { get; set; }

    // Human-readable definition name. Legacy field: _FriendlyName (String);
    // a reference type, therefore nullable under nullable reference types.
    public string? FriendlyName { get; set; }

    // Foreign key to the owning DesktopModule. Legacy field: _DesktopModuleID (Integer).
    public int DesktopModuleID { get; set; }

    // Transient module identifier used during install/import flows. Legacy field: _TempModuleID (Integer).
    public int TempModuleID { get; set; }

    // Default output-cache duration (seconds). Legacy field: _DefaultCacheTime (Integer);
    // the legacy constructor initialized this to 0, which is already the C# int default.
    public int DefaultCacheTime { get; set; }
}
