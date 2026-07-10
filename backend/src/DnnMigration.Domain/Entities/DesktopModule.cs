namespace DnnMigration.Domain.Entities;

/// <summary>
/// Desktop module registration entity.
/// </summary>
// MIGRATION: Converted from VB.NET DotNetNuke.Entities.Modules.DesktopModuleInfo
// (Library/Components/Modules/DesktopModuleInfo.vb, L36).
public class DesktopModule
{
    public int DesktopModuleID { get; set; }
    public string ModuleName { get; set; } = string.Empty;
    public string FriendlyName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string FolderName { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public bool IsPremium { get; set; }
    public bool IsAdmin { get; set; }
    public string BusinessControllerClass { get; set; } = string.Empty;
    // MIGRATION: the persisted feature bitmask (DesktopModuleInfo.vb `SupportedFeatures As Integer`,
    // L48/L146-152). This integer is the single source of truth for the feature flags below and is
    // what EF Core maps to the schema column; the boolean properties are computed over it.
    public int SupportedFeatures { get; set; }

    // MIGRATION: legacy DesktopModuleSupportedFeature bit flags (DesktopModuleInfo.vb L30-34).
    private const int FeaturePortable = 1;
    private const int FeatureSearchable = 2;
    private const int FeatureUpgradeable = 4;

    // MIGRATION: legacy sentinel Null.NullInteger (= -1) used by GetFeature to treat an unset
    // (-1) SupportedFeatures value as "no features". A default 0 mask yields all-false, matching
    // the legacy behaviour for a module with no declared features.
    private const int NullInteger = -1;

    // MIGRATION: IsPortable/IsSearchable/IsUpgradeable are NOT independent fields in the legacy
    // DesktopModuleInfo.vb — they are computed from and write through the SupportedFeatures bitmask
    // via GetFeature/UpdateFeature (L155-178). Preserving that here prevents the booleans from
    // drifting out of sync with the persisted SupportedFeatures integer and keeps EF/schema fidelity.
    public bool IsPortable
    {
        get => GetFeature(FeaturePortable);
        set => UpdateFeature(FeaturePortable, value);
    }

    public bool IsSearchable
    {
        get => GetFeature(FeatureSearchable);
        set => UpdateFeature(FeatureSearchable, value);
    }

    public bool IsUpgradeable
    {
        get => GetFeature(FeatureUpgradeable);
        set => UpdateFeature(FeatureUpgradeable, value);
    }

    public string CompatibleVersions { get; set; } = string.Empty;
    public string Dependencies { get; set; } = string.Empty;
    public string Permissions { get; set; } = string.Empty;

    // MIGRATION: faithful port of DesktopModuleInfo.vb GetFeature (L220-229):
    // `SupportedFeatures > Null.NullInteger AndAlso (SupportedFeatures And Feature) = Feature`.
    private bool GetFeature(int feature) =>
        SupportedFeatures > NullInteger && (SupportedFeatures & feature) == feature;

    // MIGRATION: faithful port of DesktopModuleInfo.vb UpdateFeature/SetFeature/ClearFeature
    // (L212-245): Set = `SupportedFeatures Or Feature`; Clear = `SupportedFeatures And (Not Feature)`.
    private void UpdateFeature(int feature, bool isSet)
    {
        if (isSet)
        {
            SupportedFeatures |= feature;
        }
        else
        {
            SupportedFeatures &= ~feature;
        }
    }
}
