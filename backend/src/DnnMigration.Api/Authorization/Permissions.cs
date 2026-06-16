namespace DnnMigration.Api.Authorization;

// MIGRATION: server-side permission authorization. The legacy DNN model expressed
// authorization through PortalSecurity.vb's SecurityAccessLevel enum
// (Anonymous/View/Edit/Admin/Host) and the HasNecessaryPermission(...) overloads
// (Library/Components/Security/PortalSecurity.vb:L45, L469-L529). That role/permission
// model is re-expressed here as ASP.NET Core authorization policies whose names ARE the
// permission keys, evaluated by <see cref="PermissionAuthorizationHandler"/>. This is the
// design documented in MIGRATION_NOTES.md §3.3 / §7.3 and AAP §0.6.2: the permission keys
// VIEW / EDIT / DELETE / MANAGE_SETTINGS are honored, and an unknown permission key yields
// a 403 (the legacy ForbiddenException case — no dedicated type exists, so the failed
// authorization result is rendered as an RFC 7807 403 by
// <see cref="ProblemDetailsAuthorizationResultHandler"/>).

/// <summary>
/// Canonical permission keys recognised by the API's authorization layer.
/// </summary>
/// <remarks>
/// <para>
/// Each constant doubles as the policy name supplied to <c>[Authorize(Policy = ...)]</c> on
/// the resource controllers. <see cref="PermissionPolicyProvider"/> materialises a policy
/// carrying a <see cref="PermissionRequirement"/> for the requested name, and
/// <see cref="PermissionAuthorizationHandler"/> evaluates the authenticated principal against
/// it. Any policy name that is not one of <see cref="All"/> is treated as an unknown
/// permission and is never granted, which surfaces as a 403 rather than a 500.
/// </para>
/// <para>
/// The string values are intentionally the verbatim, upper-case keys shared with the Angular
/// client's <c>has-permission</c> directive / <c>permission.service.ts</c> (MIGRATION_NOTES.md
/// §7.3) so that the server and the UI gate on identical tokens.
/// </para>
/// </remarks>
public static class Permissions
{
    /// <summary>Permission to read / list a resource. Mapped from the legacy <c>SecurityAccessLevel.View</c>.</summary>
    public const string View = "VIEW";

    /// <summary>Permission to create or modify a resource. Mapped from the legacy <c>SecurityAccessLevel.Edit</c>.</summary>
    public const string Edit = "EDIT";

    /// <summary>Permission to delete a resource.</summary>
    public const string Delete = "DELETE";

    /// <summary>Permission to manage a resource's settings/configuration (the most privileged resource operation).</summary>
    public const string ManageSettings = "MANAGE_SETTINGS";

    /// <summary>
    /// The well-known DotNetNuke portal administrators role. A principal in this role (or a
    /// host super-user, see <see cref="SuperUserClaimType"/>) is granted every permission in
    /// <see cref="All"/>. Centralised here so the role name is defined in exactly one place.
    /// </summary>
    public const string AdministratorRole = "Administrators";

    /// <summary>
    /// The custom JWT claim type emitted by <c>JwtService</c> that flags a DNN host super-user
    /// (its value is <c>bool.ToString()</c>, i.e. <c>"True"</c>/<c>"False"</c>). A super-user is
    /// granted every permission regardless of role membership.
    /// </summary>
    public const string SuperUserClaimType = "IsSuperUser";

    /// <summary>
    /// The complete set of recognised permission keys. Membership in this set is the authoritative
    /// definition of a "known" permission: any policy name absent from it is an unknown permission
    /// and is denied (→ 403).
    /// </summary>
    public static readonly IReadOnlySet<string> All =
        new HashSet<string>(StringComparer.Ordinal) { View, Edit, Delete, ManageSettings };
}
