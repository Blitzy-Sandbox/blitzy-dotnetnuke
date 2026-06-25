namespace DnnMigration.Application.DTOs.Module;

// MIGRATION: Inbound request for POST /api/modules (AAP 0.3.4). Mutable class. Editable fields derived from
// Website/admin/Modules/ModuleSettings.ascx.vb (UpdateSettings, L343-385) plus the scoping/identification fields
// needed to add a module. Server-generated keys (ModuleId, TabModuleId) are OMITTED. No business logic or
// validation here (FluentValidation lives in Application/Validators).
public class CreateModuleRequest
{
    // MIGRATION: Required multi-tenant scoping (AAP 0.7.1) — a module must belong to a portal. Elevated to a
    // non-nullable int in the request contract (the Domain entity stores int? for the legacy Null.NullInteger sentinel).
    public int PortalId { get; set; }

    // MIGRATION: Required page placement — the tab (page) the module is added to. Non-nullable in the request contract.
    public int TabId { get; set; }

    // Module-definition identification (which module type to instantiate).
    public int? ModuleDefId { get; set; }

    public int DesktopModuleId { get; set; }

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
