namespace DnnMigration.Domain.Entities;

// MIGRATION (CP2 review — DependencyInjection #1 / Program.cs #4): name/value setting row backing the Application
// IPortalSettingsService port (Application/Interfaces/IPortalSettingsService.cs). Mapped Code-First to the
// EXISTING [ModuleSettings] table (DotNetNuke.Schema.SqlDataProvider) — the schema is NOT altered.
//
// LEGACY STORAGE TRUTH: DNN 4.x has NO name/value "PortalSettings" table. PortalSettings.vb
// (GetSiteSettings/GetSiteSetting/UpdateSiteSetting) resolved the portal's "Site Settings" module via
// ModuleController.GetModuleByDefinition(PortalId, "Site Settings").ModuleID and then read/wrote the
// [ModuleSettings] table for that module. The IPortalSettingsService adapter replicates that indirection, so
// portal site settings physically live as ModuleSettings rows keyed by the Site-Settings module's ModuleID.
//
// The [ModuleSettings] CREATE TABLE has exactly three columns — [ModuleID] int, [SettingName] nvarchar(50),
// [SettingValue] nvarchar(2000) — with the natural composite key (ModuleID, SettingName). This entity maps ONLY
// those physical columns (no audit columns exist on this table in the 4.x schema), so EF never emits SQL for a
// non-existent column.
public class ModuleSetting
{
    // MIGRATION: [ModuleSettings].[ModuleID] — part of the composite key. For portal site settings this is the
    // ModuleID of the portal's "Site Settings" module (the IPortalSettingsService adapter resolves it).
    public int ModuleId { get; set; }

    // MIGRATION: [ModuleSettings].[SettingName] nvarchar(50) — part of the composite key (e.g. "defaultmoduleid",
    // "defaulttabid", "Security_DisplayNameFormat").
    public string SettingName { get; set; } = string.Empty;

    // MIGRATION: [ModuleSettings].[SettingValue] nvarchar(2000).
    public string SettingValue { get; set; } = string.Empty;
}
