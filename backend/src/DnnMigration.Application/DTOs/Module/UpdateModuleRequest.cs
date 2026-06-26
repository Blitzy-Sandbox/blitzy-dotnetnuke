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

    // MIGRATION: CP1 review (ModuleService #3, CRITICAL) — module-permission grants reconciled on update. Legacy
    // ModuleController.UpdateModule (L1099-1118) diffed the supplied permissions against the stored set and, when
    // they differed, deleted all then re-added — SKIPPING any grant where InheritViewPermissions && PermissionKey =
    // "VIEW" (that VIEW grant is inherited from the tab, not stored on the module) and persisting only AllowAccess
    // grants. ModuleService applies that exact rule. Defaults to an empty list.
    public List<ModulePermissionDto> Permissions { get; set; } = new();

    // MIGRATION: CP1 review (ModuleService #3, CRITICAL) — legacy UpdateModule (L1132-1144): when AllModules was set,
    // THIS module's display settings (Alignment/Color/Border/IconFile/Visibility/ContainerSrc/DisplayTitle/
    // DisplayPrint/DisplaySyndicate) were propagated to every module on every (non-admin) tab in the portal. This is
    // a transient OPERATION flag (the legacy ModuleInfo.AllModules was a runtime member, dropped from the Domain
    // entity); ModuleService reads it to drive the propagation, it is not persisted on the module.
    public bool AllModules { get; set; }

    // MIGRATION: CP1 review (ModuleService #3, CRITICAL) — legacy UpdateModule (L1126-1130): when IsDefaultModule was
    // set, the portal "defaultmoduleid"/"defaulttabid" site settings were written (UpdateSiteSetting). Transient
    // OPERATION flag (the legacy ModuleInfo.IsDefaultModule runtime member was dropped from the Domain entity);
    // ModuleService reads it to write the portal settings via IPortalSettingsService, it is not persisted on the module.
    public bool IsDefaultModule { get; set; }
}
