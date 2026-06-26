using DnnMigration.Application.Interfaces;
using DnnMigration.Domain.Common;
using DnnMigration.Domain.Entities;
using DnnMigration.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DnnMigration.Infrastructure.Settings;

// MIGRATION (CP2 review — DependencyInjection #1 / Program.cs #4): concrete Infrastructure ADAPTER for the
// Application IPortalSettingsService port. Wired in Infrastructure/DependencyInjection.cs as Scoped so it shares
// the request's DnnDbContext.
//
// LEGACY FIDELITY: DNN 4.x has NO name/value "PortalSettings" table. PortalSettings.vb
// (GetSiteSettings/GetSiteSetting/UpdateSiteSetting, L923-981) resolved the portal's "Site Settings" module via
// ModuleController.GetModuleByDefinition(PortalId, "Site Settings").ModuleID and then read/wrote the
// [ModuleSettings] table for that module. This adapter replicates that indirection exactly: it resolves the
// Site-Settings module's ModuleID from _context.Modules (mapped to the legacy read view vw_Modules) by
// PortalId + FriendlyName, then reads/writes [ModuleSettings] rows keyed by that ModuleID.
//
// TRANSACTION SEMANTICS — verified against the call-sites:
//   * SetSettingAsync SELF-COMMITS. ModuleService.UpdateAsync performs its primary
//     IUnitOfWork.SaveChangesAsync BEFORE the settings block (L238) and its helper methods self-commit
//     (ResequenceTabAsync L417, PropagateDisplaySettingsAsync L451); there is NO guaranteed commit AFTER the
//     SetSettingAsync calls (L251/L254), so the adapter must persist its own change. Because the prior
//     operations already committed, the DbContext carries no unrelated pending changes at this point, so the
//     SaveChanges here flushes only the setting upsert.
//   * GetSettingAsync is a pure read; callers (e.g. UserService display-name formatting, L331) treat a
//     null/empty result as "not configured" and fall back to legacy when-empty behavior.
public sealed class PortalSettingsService : IPortalSettingsService
{
    // MIGRATION: the DesktopModule FriendlyName of the DNN administrative "Site Settings" module — the legacy
    // GetModuleByDefinition(PortalId, "Site Settings") definition name that anchors per-portal site settings.
    private const string SiteSettingsModuleName = "Site Settings";

    private readonly DnnDbContext _context;

    public PortalSettingsService(DnnDbContext context) => _context = context;

    /// <inheritdoc />
    public async Task<string?> GetSettingAsync(int portalId, string settingName, CancellationToken cancellationToken = default)
    {
        var moduleId = await ResolveSiteSettingsModuleIdAsync(portalId, cancellationToken);
        if (moduleId is null)
        {
            // MIGRATION: legacy parity — when the portal has no Site Settings module the setting is simply "not
            // configured"; callers fall back to their when-empty behavior rather than failing.
            return null;
        }

        var setting = await _context.Set<ModuleSetting>()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                ms => ms.ModuleId == moduleId.Value && ms.SettingName == settingName,
                cancellationToken);

        return setting?.SettingValue;
    }

    /// <inheritdoc />
    public async Task SetSettingAsync(int portalId, string settingName, string settingValue, CancellationToken cancellationToken = default)
    {
        var moduleId = await ResolveSiteSettingsModuleIdAsync(portalId, cancellationToken);
        if (moduleId is null)
        {
            // MIGRATION: a portal setting is physically a ModuleSettings row scoped to the Site Settings module;
            // without that module there is no valid anchor to persist to. Surface a clear domain error (translated
            // to RFC 7807 by the Api ExceptionHandlingMiddleware) instead of silently dropping the write.
            throw new DomainException(
                $"Cannot persist site setting '{settingName}' for portal {portalId}: the portal has no 'Site Settings' module to scope ModuleSettings to.");
        }

        // MIGRATION: create-or-update the ModuleSettings row (legacy UpdateSiteSetting upsert semantics).
        var existing = await _context.Set<ModuleSetting>()
            .FirstOrDefaultAsync(
                ms => ms.ModuleId == moduleId.Value && ms.SettingName == settingName,
                cancellationToken);

        if (existing is null)
        {
            await _context.Set<ModuleSetting>().AddAsync(
                new ModuleSetting
                {
                    ModuleId = moduleId.Value,
                    SettingName = settingName,
                    SettingValue = settingValue
                },
                cancellationToken);
        }
        else
        {
            existing.SettingValue = settingValue;
        }

        // SELF-COMMIT: the caller does not commit after this call (see class remarks).
        await _context.SaveChangesAsync(cancellationToken);
    }

    // MIGRATION: replicates ModuleController.GetModuleByDefinition(PortalId, "Site Settings").ModuleID. Resolves
    // against _context.Modules (the legacy read view vw_Modules) by PortalId + FriendlyName, excluding soft-deleted
    // modules. Returns null when the portal has no such module. vw_Modules can surface a module once per tab
    // placement, so the projection takes the first matching ModuleID (identical across placements).
    private async Task<int?> ResolveSiteSettingsModuleIdAsync(int portalId, CancellationToken cancellationToken)
    {
        return await _context.Modules
            .Where(m => m.PortalId == portalId
                        && m.FriendlyName == SiteSettingsModuleName
                        && !m.IsDeleted)
            .Select(m => m.ModuleId)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
