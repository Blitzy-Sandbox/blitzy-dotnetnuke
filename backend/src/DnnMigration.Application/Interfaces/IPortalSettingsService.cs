namespace DnnMigration.Application.Interfaces;

// MIGRATION: Application-layer PORT (abstraction) for reading and writing per-portal site settings, replacing
// the legacy DotNetNuke.Entities.Portals.PortalSettings static helpers GetPortalSettingsDictionary /
// UpdatePortalSetting / UpdateSiteSetting (Library/Components/Portal/PortalSettings.vb), which were backed by
// the PortalSettings table via the reflection-instantiated DataProvider. The legacy code read settings such as
// "Security_DisplayNameFormat" (applied by UserInfo.UpdateDisplayName) and wrote "defaultmoduleid" /
// "defaulttabid" (ModuleController.UpdateModule when a module is flagged the portal default).
//
// Clean/Onion: declared in the Application project (Application owns its abstractions, references Domain ONLY).
// The concrete adapter (mapping onto the PortalSettings table) lives in DnnMigration.Infrastructure and is
// wired in Infrastructure/DependencyInjection.cs; that adapter is DEFERRED to CP2 (Infrastructure is
// mid-pipeline). The port is introduced now so Application services can apply the legacy settings-driven rules
// (UserService display-name formatting — CP1 review UserService #6; ModuleService default-module persistence —
// part of CP1 review ModuleService #3 lifecycle) at their correct root cause instead of omitting them.
// Recorded in MIGRATION_NOTES.md.

/// <summary>
/// Abstraction over per-portal site settings (the legacy PortalSettings table). Settings are simple
/// name/value strings scoped to a portal (tenant). Used by the Application layer to apply legacy
/// settings-driven business rules (e.g. the display-name format, the portal default module/tab) without
/// taking a dependency on the persistence mechanism.
/// </summary>
public interface IPortalSettingsService
{
    /// <summary>
    /// Reads a single portal setting by name, or <c>null</c> when the setting is not present for the portal.
    /// Callers treat a <c>null</c> (or empty) result as "rule not configured" and fall back to legacy
    /// when-empty behavior.
    /// </summary>
    /// <param name="portalId">Multi-tenant discriminator: the portal the setting is scoped to.</param>
    /// <param name="settingName">The setting key (e.g. <c>"Security_DisplayNameFormat"</c>).</param>
    /// <param name="cancellationToken">Token to observe for cancellation.</param>
    /// <returns>The setting value, or <c>null</c> when not configured for the portal.</returns>
    Task<string?> GetSettingAsync(int portalId, string settingName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or updates a single portal setting (legacy <c>PortalSettings.UpdateSiteSetting</c>).
    /// </summary>
    /// <param name="portalId">Multi-tenant discriminator: the portal the setting is scoped to.</param>
    /// <param name="settingName">The setting key (e.g. <c>"defaultmoduleid"</c>, <c>"defaulttabid"</c>).</param>
    /// <param name="settingValue">The value to persist.</param>
    /// <param name="cancellationToken">Token to observe for cancellation.</param>
    Task SetSettingAsync(int portalId, string settingName, string settingValue, CancellationToken cancellationToken = default);
}
