namespace DnnMigration.Application.DTOs.Module;

// MIGRATION: Inbound request for PUT /api/modules/{id} (AAP 0.3.4). Mutable class mirroring the editable module
// settings in Website/admin/Modules/ModuleSettings.ascx.vb (UpdateSettings, L343-385). The module id is route-bound
// (NOT in the body). PortalId (tenant) is fixed and not editable here; ModuleDefId/DesktopModuleId (module type) are
// fixed after creation. No business logic or validation here (FluentValidation lives in Application/Validators).
public class UpdateModuleRequest
{
    // MIGRATION: Tab (page) placement — may change (legacy MoveModule when the target tab differs).
    public int TabId { get; set; }

    public string? ModuleTitle { get; set; }

    public string? PaneName { get; set; }

    public int ModuleOrder { get; set; }

    public bool AllTabs { get; set; }

    // MIGRATION: Visibility as int. Legacy VisibilityState enum (Maximized=0, Minimized=1, None=2) was not migrated
    // to a C# enum; the value is an integer matching the DB column.
    public int Visibility { get; set; }

    public string? Alignment { get; set; }

    public string? Color { get; set; }

    public string? Border { get; set; }

    public string? IconFile { get; set; }

    public int CacheTime { get; set; }

    public string? Header { get; set; }

    public string? Footer { get; set; }

    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public string? ContainerSrc { get; set; }

    // MIGRATION: Defaults preserved from the legacy ModuleInfo constructor (DisplayTitle=True, DisplayPrint=True)
    // for behavioral parity (AAP 0.7.1) when the client omits them.
    public bool DisplayTitle { get; set; } = true;

    public bool DisplayPrint { get; set; } = true;

    public bool DisplaySyndicate { get; set; }

    public bool InheritViewPermissions { get; set; }
}
