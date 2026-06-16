using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace DnnMigration.Api.Authorization;

/// <summary>
/// Evaluates a <see cref="PermissionRequirement"/> against the authenticated principal, deciding
/// whether the caller holds the required permission key.
/// </summary>
/// <remarks>
/// <para>
/// MIGRATION: this replaces the legacy <c>PortalSecurity.HasNecessaryPermission(...)</c> overloads
/// (Library/Components/Security/PortalSecurity.vb:L469-L529) that switched on the
/// <c>SecurityAccessLevel</c> enum. The in-scope resources (Portals, Modules, Users, Roles, Tabs) are
/// DNN <em>administrative</em> surfaces, so authorization is role-based and <strong>fail-closed</strong>
/// (MIGRATION_NOTES.md §3.3 / §7.3, AAP §0.6.2 "Admin-only endpoints — require Administrator role"):
/// </para>
/// <list type="bullet">
///   <item><description>A host super-user (the <see cref="Permissions.SuperUserClaimType"/> claim is
///   <c>true</c>) or a member of the <see cref="Permissions.AdministratorRole"/> role holds every
///   permission in <see cref="Permissions.All"/>.</description></item>
///   <item><description>Every other authenticated principal holds <em>no</em> permission, so a
///   recognised-but-ungranted key (e.g. an authenticated non-admin requesting <see cref="Permissions.View"/>)
///   leaves the requirement unmet → 403.</description></item>
///   <item><description>An <em>unknown</em> permission key (not in <see cref="Permissions.All"/>) is never
///   granted to anyone → 403.</description></item>
/// </list>
/// <para>
/// An unauthenticated caller leaves the requirement unmet, which the authorization middleware surfaces as
/// a challenge (→ 401) rather than a forbid (→ 403). Per-entity DNN permission-grant tables
/// (FolderPermission/ModulePermission/TabPermission) are intentionally out of scope for this checkpoint and
/// are documented as a carry-forward in MIGRATION_NOTES.md; resource-level authorization here is role-based.
/// </para>
/// </remarks>
public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    /// <summary>
    /// Makes the authorization decision for the supplied <paramref name="requirement"/>.
    /// </summary>
    /// <param name="context">The authorization context (carries the <see cref="ClaimsPrincipal"/>).</param>
    /// <param name="requirement">The permission requirement being evaluated.</param>
    /// <returns>A completed task; the decision is recorded on <paramref name="context"/>.</returns>
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var user = context.User;

        // Unauthenticated → leave the requirement unmet so the pipeline challenges (401), not forbids (403).
        if (user.Identity?.IsAuthenticated != true)
        {
            return Task.CompletedTask;
        }

        if (PrincipalHasPermission(user, requirement.Permission))
        {
            context.Succeed(requirement);
        }

        // Otherwise the requirement is left unmet → the principal is authenticated but unauthorized → 403.
        return Task.CompletedTask;
    }

    /// <summary>
    /// Determines whether the authenticated <paramref name="user"/> holds the given <paramref name="permission"/>.
    /// </summary>
    /// <param name="user">The authenticated principal.</param>
    /// <param name="permission">The requested permission key.</param>
    /// <returns><see langword="true"/> when the permission is granted; otherwise <see langword="false"/>.</returns>
    private static bool PrincipalHasPermission(ClaimsPrincipal user, string permission)
    {
        // An unknown permission key is never granted (fail-closed) → 403 for everyone, including admins.
        if (!Permissions.All.Contains(permission))
        {
            return false;
        }

        // Host super-users and portal administrators hold every recognised permission.
        return IsSuperUser(user) || user.IsInRole(Permissions.AdministratorRole);
    }

    /// <summary>
    /// Reads the <see cref="Permissions.SuperUserClaimType"/> claim and parses its boolean value.
    /// </summary>
    /// <param name="user">The authenticated principal.</param>
    /// <returns><see langword="true"/> when the principal is flagged as a DNN host super-user.</returns>
    private static bool IsSuperUser(ClaimsPrincipal user)
    {
        var claim = user.FindFirst(Permissions.SuperUserClaimType)?.Value;
        return bool.TryParse(claim, out var isSuperUser) && isSuperUser;
    }
}
