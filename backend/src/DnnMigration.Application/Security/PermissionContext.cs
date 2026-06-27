using DnnMigration.Domain.Entities;

namespace DnnMigration.Application.Security;

// MIGRATION: Replaces the PortalSettings + ModuleInfo inputs the legacy
// PortalSecurity.HasNecessaryPermission(SecurityAccessLevel, PortalSettings, ModuleInfo, UserInfo) read
// (Library/Components/Security/PortalSecurity.vb L517-549). Each property below maps one-to-one onto the legacy
// expression the helper evaluated, so the migrated evaluator can reproduce the SAME authorization decision
// without depending on the heavyweight legacy PortalSettings / ModuleInfo objects:
//   AdministratorRoleName  <- PortalSettings.AdministratorRoleName        (drives isAdmin   = IsInRole)
//   PageAdministratorRoles <- PortalSettings.ActiveTab.AdministratorRoles (drives isPageEditor = IsInRoles)
//   AuthorizedViewRoles    <- ModuleConfiguration.AuthorizedViewRoles     (drives canViewModule = IsInRoles)
//   ModulePermissions      <- ModuleConfiguration.ModulePermissions       (drives canEditModule = HasModulePermission("EDIT"))
// All role inputs are DNN ";"-delimited permission strings (except AdministratorRoleName, a single role name),
// preserved verbatim so the evaluator's parsing matches the legacy behavior exactly.
public sealed record PermissionContext
{
    /// <summary>
    /// The portal's administrator role name (legacy <c>PortalSettings.AdministratorRoleName</c>), e.g.
    /// "Administrators". Evaluated with <c>IsInRole</c> to derive the legacy <c>isAdmin</c> flag.
    /// </summary>
    public string? AdministratorRoleName { get; init; }

    /// <summary>
    /// The ";"-delimited page (tab) administrator roles (legacy
    /// <c>PortalSettings.ActiveTab.AdministratorRoles</c>). Evaluated with <c>IsInRoles</c> to derive the legacy
    /// <c>isPageEditor</c> flag.
    /// </summary>
    public string? PageAdministratorRoles { get; init; }

    /// <summary>
    /// The ";"-delimited roles authorized to view the module (legacy
    /// <c>ModuleConfiguration.AuthorizedViewRoles</c>). Evaluated with <c>IsInRoles</c> to derive the legacy
    /// <c>canViewModule</c> flag.
    /// </summary>
    public string? AuthorizedViewRoles { get; init; }

    /// <summary>
    /// The module's permission grants (legacy <c>ModuleConfiguration.ModulePermissions</c>). The "EDIT" key is
    /// evaluated with <c>HasModulePermission</c> to derive the legacy <c>canEditModule</c> flag. Defaults to an
    /// empty collection (no module-level grants).
    /// </summary>
    public IReadOnlyCollection<ModulePermission> ModulePermissions { get; init; } = Array.Empty<ModulePermission>();
}
