namespace DnnMigration.Domain.Entities;

/// <summary>
/// Module definition registration entity.
/// </summary>
// MIGRATION: Converted from VB.NET DotNetNuke.Entities.Modules.ModuleDefinitionInfo
// (Library/Components/Modules/ModuleDefinitionInfo.vb, L30).
public class ModuleDefinition
{
    public int ModuleDefID { get; set; }
    public string FriendlyName { get; set; } = string.Empty;
    public int DesktopModuleID { get; set; }
    public int TempModuleID { get; set; }
    public int DefaultCacheTime { get; set; }
}
