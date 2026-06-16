namespace DnnMigration.Domain.Entities;

// MIGRATION: Ported from DesktopModuleInfo.vb (DotNetNuke.Entities.Modules). The DesktopModuleSupportedFeature
// bit-flag enum and the GetFeature/UpdateFeature/SetFeature/ClearFeature helper logic are dropped;
// IsUpgradeable/IsPortable/IsSearchable are flattened to plain auto-properties (see the MIGRATION note on those
// members). EF mapping is supplied via Fluent configuration in the Infrastructure layer; this type remains a
// pure POCO with zero framework dependencies.
public class DesktopModule
{
    // Primary key (DesktopModuleID).
    public int DesktopModuleID { get; set; }

    public string? ModuleName { get; set; }

    public string? FriendlyName { get; set; }

    public string? Description { get; set; }

    public string? FolderName { get; set; }

    public string? Version { get; set; }

    public bool IsPremium { get; set; }

    public bool IsAdmin { get; set; }

    public string? BusinessControllerClass { get; set; }

    // Bit-flag integer. In legacy VB the three feature booleans below were derived from this value.
    public int SupportedFeatures { get; set; }

    // MIGRATION: In DesktopModuleInfo.vb these three booleans were computed from the SupportedFeatures bit-flags
    // (DesktopModuleSupportedFeature) via GetFeature/UpdateFeature. They are flattened to plain auto-properties here;
    // the bit-flag derivation (if needed) is reconstituted in the service/repository layer. The
    // DesktopModuleSupportedFeature enum is out of scope (not added to Enums/).
    public bool IsUpgradeable { get; set; }

    public bool IsPortable { get; set; }

    public bool IsSearchable { get; set; }

    public string? CompatibleVersions { get; set; }

    public string? Dependencies { get; set; }

    public string? Permissions { get; set; }
}
