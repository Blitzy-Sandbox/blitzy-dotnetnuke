namespace DnnMigration.Api.Authorization;

/// <summary>
/// Canonical permission-policy names enforced by the API authorization layer.
/// </summary>
/// <remarks>
/// MIGRATION (Finding CP4-1 / AAP §0.6.2): replaces the legacy
/// <c>PortalSecurity.HasNecessaryPermission</c> tiers
/// (<c>Library/Components/Security/PortalSecurity.vb</c> L469-L535). The four keys are the EXACT mirror of
/// the frontend <c>PermissionKey</c> union and <c>PERMISSION_ROLE_MAP</c>
/// (<c>frontend/src/app/shared/directives/has-permission/has-permission.directive.ts</c>). The string
/// VALUES (<c>VIEW</c>/<c>EDIT</c>/<c>DELETE</c>/<c>MANAGE_SETTINGS</c>) are also the registered ASP.NET Core
/// authorization-policy names, so a controller annotates an action with, e.g.,
/// <c>[Authorize(Policy = Permissions.Edit)]</c>. The server is the AUTHORITATIVE enforcement point; the
/// frontend directive performs UI gating only.
/// </remarks>
public static class Permissions
{
    /// <summary>Read access to a resource (mapped to HTTP GET actions).</summary>
    public const string View = "VIEW";

    /// <summary>Create/modify access to a resource (mapped to HTTP POST/PUT actions).</summary>
    public const string Edit = "EDIT";

    /// <summary>Delete access to a resource (mapped to HTTP DELETE actions).</summary>
    public const string Delete = "DELETE";

    /// <summary>
    /// Manage-settings access. Registered for parity with the frontend permission set; reserved for
    /// configuration/settings actions and applied by controllers that expose such operations.
    /// </summary>
    public const string ManageSettings = "MANAGE_SETTINGS";

    /// <summary>All permission-policy names, used by the composition root to register one policy per key.</summary>
    public static IReadOnlyList<string> All { get; } = new[] { View, Edit, Delete, ManageSettings };
}

/// <summary>
/// Well-known portal role names referenced by the authorization layer.
/// </summary>
/// <remarks>
/// MIGRATION (Finding CP4-1): for this administrative SPA every gated affordance is an administrative
/// action, so each permission key maps to the canonical DNN portal <c>Administrators</c> security role
/// (<c>Portal.AdministratorRoleName</c>) — identical to the frontend <c>PERMISSION_ROLE_MAP</c>. SuperUsers
/// (Hosts) are granted independently of this role via the <c>IsSuperUser</c> JWT claim, mirroring the legacy
/// <c>If User.IsSuperUser Then blnAuthorized = True</c> shortcut (PortalSecurity.vb L524-L526).
/// </remarks>
public static class AuthorizationRoles
{
    /// <summary>The DNN portal administrators role granted all administrative permission keys.</summary>
    public const string Administrators = "Administrators";
}
