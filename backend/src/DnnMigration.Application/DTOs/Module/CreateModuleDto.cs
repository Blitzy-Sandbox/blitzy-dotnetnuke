using DnnMigration.Domain.Enums;

namespace DnnMigration.Application.DTOs.Module;

// MIGRATION: Creation projection of ModuleInfo.vb. Excludes ModuleID/TabModuleID (DB-generated keys) and
// IsDeleted (internal soft-delete state, service-managed). Catalog fields (FriendlyName/Description/Version)
// derive from DesktopModuleID and are not part of the creation contract. Legacy ctor defaults preserved:
// DisplayTitle=true, DisplayPrint=true, DisplaySyndicate=false. No permission nav collection.
public class CreateModuleDto
{
    public int PortalID { get; set; }
    public int TabID { get; set; }
    public int ModuleDefID { get; set; }
    public int DesktopModuleID { get; set; }
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
    public bool DisplayTitle { get; set; } = true;
    public bool DisplayPrint { get; set; } = true;
    public bool DisplaySyndicate { get; set; } = false;
    public string? Header { get; set; }
    public string? Footer { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? ContainerSrc { get; set; }
    public bool InheritViewPermissions { get; set; }
}
