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
    public int SupportedFeatures { get; set; }
    public bool IsUpgradeable { get; set; }
    public bool IsPortable { get; set; }
    public bool IsSearchable { get; set; }
    public string CompatibleVersions { get; set; } = string.Empty;
    public string Dependencies { get; set; } = string.Empty;
    public string Permissions { get; set; } = string.Empty;
}
