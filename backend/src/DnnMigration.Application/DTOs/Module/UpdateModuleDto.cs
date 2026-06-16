using DnnMigration.Domain.Enums;

namespace DnnMigration.Application.DTOs.Module;

// MIGRATION: Update projection of ModuleInfo.vb. Carries ModuleID (target identity) plus the mutable
// module-instance settings (editable via the legacy ModuleSettings.ascx.vb screen). IsDeleted is excluded
// (soft-delete is service-managed). Immutable relationship keys (PortalID/TabID/ModuleDefID/DesktopModuleID/
// TabModuleID) and catalog fields (FriendlyName/Description/Version) are excluded. No permission nav collection.
public class UpdateModuleDto
{
    public int ModuleID { get; set; }
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
    public bool DisplayTitle { get; set; }
    public bool DisplayPrint { get; set; }
    public bool DisplaySyndicate { get; set; }
    public string? Header { get; set; }
    public string? Footer { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? ContainerSrc { get; set; }
    public bool InheritViewPermissions { get; set; }
}
