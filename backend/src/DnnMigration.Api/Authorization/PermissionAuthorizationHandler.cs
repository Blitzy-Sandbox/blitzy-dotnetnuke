using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace DnnMigration.Api.Authorization;

/// <summary>
/// Authorization handler that grants a <see cref="PermissionRequirement"/> when the authenticated principal
/// is either a SuperUser (Host) or a member of one of the requirement's allowed roles.
/// </summary>
/// <remarks>
/// <para>
/// MIGRATION (Finding CP4-1 / AAP §0.6.2): replaces <c>PortalSecurity.HasNecessaryPermission</c>
/// (<c>Library/Components/Security/PortalSecurity.vb</c> L469-L535). The decision mirrors the frontend
/// <c>has-permission</c> directive EXACTLY so client UI gating and server enforcement never diverge:
/// </para>
/// <list type="bullet">
///   <item><description>
///     SuperUser short-circuit — a principal whose <c>IsSuperUser</c> JWT claim is <c>true</c> is granted
///     every permission, reproducing the legacy <c>If User.IsSuperUser Then blnAuthorized = True</c>
///     shortcut (PortalSecurity.vb L524-L526). The <c>IsSuperUser</c> claim is emitted by
///     <c>JwtService.GenerateAccessToken</c> as <c>user.IsSuperUser.ToString()</c> ("True"/"False").
///   </description></item>
///   <item><description>
///     Role match — otherwise the principal is granted when it holds ANY of the requirement's allowed roles
///     (the <c>Administrators</c> portal role for every key in this admin SPA). Role claims are emitted as
///     <c>ClaimTypes.Role</c> by <c>JwtService</c> and hydrated into the token by <c>AuthService</c>.
///   </description></item>
///   <item><description>
///     Fail-closed — when neither condition holds the handler does nothing; the requirement stays unmet, so
///     an authenticated-but-unauthorized request is rejected with <c>403 Forbidden</c> (the policy's
///     <c>RequireAuthenticatedUser</c> rejects anonymous callers with <c>401</c> first).
///   </description></item>
/// </list>
/// </remarks>
public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    /// <summary>
    /// The JWT claim type carrying the SuperUser (Host) flag. MUST match the literal emitted by
    /// <c>JwtService.GenerateAccessToken</c> (<c>new("IsSuperUser", user.IsSuperUser.ToString())</c>).
    /// </summary>
    public const string SuperUserClaimType = "IsSuperUser";

    /// <inheritdoc />
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        var user = context.User;

        // Defensive: an unauthenticated principal can never satisfy the requirement. The policy's
        // RequireAuthenticatedUser already enforces 401 for anonymous callers; this guard keeps the handler
        // correct if it is ever reused under a policy without that assertion.
        if (user.Identity is null || !user.Identity.IsAuthenticated)
        {
            return Task.CompletedTask;
        }

        // SuperUser (Host) short-circuit — mirrors the legacy IsSuperUser bypass.
        if (IsSuperUser(user))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // Role match — granted when the principal holds ANY of the allowed roles.
        foreach (var role in requirement.AllowedRoles)
        {
            if (user.IsInRole(role))
            {
                context.Succeed(requirement);
                return Task.CompletedTask;
            }
        }

        // Fail-closed: leave the requirement unmet -> 403 for an authenticated-but-unauthorized principal.
        return Task.CompletedTask;
    }

    /// <summary>
    /// Returns <c>true</c> when the principal carries an <c>IsSuperUser</c> claim whose value parses to
    /// <c>true</c> (case-insensitively, matching <see cref="bool.TryParse(string, out bool)"/>).
    /// </summary>
    private static bool IsSuperUser(ClaimsPrincipal user)
    {
        var claim = user.FindFirst(SuperUserClaimType);
        return claim is not null && bool.TryParse(claim.Value, out var isSuperUser) && isSuperUser;
    }
}
