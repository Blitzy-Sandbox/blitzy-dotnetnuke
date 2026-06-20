namespace DnnMigration.Domain.Entities;

// MIGRATION: Ported from DesktopModuleInfo.vb (DotNetNuke.Entities.Modules). DesktopModuleSupportedFeature
// bit-flag enum and GetFeature/UpdateFeature logic dropped; IsUpgradeable/IsPortable/IsSearchable are plain
// auto-properties. EF mapping via Fluent config in Infrastructure.
//
// Pure POCO catalog record describing an installable module definition (the "desktop module").
// Zero framework dependencies (Clean Architecture inner ring): no usings, no attributes, no interfaces.
public class DesktopModule
{
    // Primary key (identity column DesktopModuleID in the legacy schema).
    public int DesktopModuleID { get; set; }

    // Unique module name token used as the programmatic key for the definition.
    public string? ModuleName { get; set; }

    // Human-friendly display name shown in administrative UIs.
    public string? FriendlyName { get; set; }

    // Free-text description of the module's purpose.
    public string? Description { get; set; }

    // Physical/virtual folder that hosts the module's controls and resources.
    public string? FolderName { get; set; }

    // Version string of the installed module definition.
    public string? Version { get; set; }

    // True when the module is a premium (licensed) module rather than a free/standard one.
    public bool IsPremium { get; set; }

    // True when the module is an administrative module.
    public bool IsAdmin { get; set; }

    // Fully-qualified name of the optional business controller class implementing
    // the module's portable/searchable/upgradeable behaviours.
    public string? BusinessControllerClass { get; set; }

    // Bit-flag integer aggregating the supported feature flags. In the legacy schema this single
    // integer column encoded IsPortable (1), IsSearchable (2) and IsUpgradeable (4).
    public int SupportedFeatures { get; set; }

    // MIGRATION: In DesktopModuleInfo.vb these three booleans were computed from the SupportedFeatures bit-flags
    // (DesktopModuleSupportedFeature) via GetFeature/UpdateFeature. They are flattened to plain auto-properties here;
    // the bit-flag derivation (if needed) is reconstituted in the service/repository layer. The
    // DesktopModuleSupportedFeature enum is out of scope (not added to Enums/).
    public bool IsUpgradeable { get; set; }
    public bool IsPortable { get; set; }
    public bool IsSearchable { get; set; }

    // Range of core framework versions this module is compatible with.
    public string? CompatibleVersions { get; set; }

    // Declared dependencies that must be present for this module to function.
    public string? Dependencies { get; set; }

    // Host permissions required to install/run this module.
    public string? Permissions { get; set; }
}
