using Microsoft.AspNetCore.Authorization;

namespace DnnMigration.Api.Authorization;

/// <summary>
/// Authorization requirement carrying a permission key and the role names that satisfy it.
/// </summary>
/// <remarks>
/// MIGRATION (Finding CP4-1 / AAP §0.6.2): the data-only requirement consumed by
/// <see cref="PermissionAuthorizationHandler"/>. It expresses, for a single permission key
/// (<see cref="Permissions"/>), the set of role names that grant it. The composition root constructs one
/// requirement per registered policy from the same key→role mapping the frontend
/// <c>PERMISSION_ROLE_MAP</c> uses, keeping client UI gating and server enforcement in lock-step.
/// </remarks>
public sealed class PermissionRequirement : IAuthorizationRequirement
{
    /// <summary>
    /// Initializes a new <see cref="PermissionRequirement"/>.
    /// </summary>
    /// <param name="permission">The permission key being enforced (one of <see cref="Permissions"/>).</param>
    /// <param name="allowedRoles">The role names that satisfy the requirement (e.g. <c>Administrators</c>).</param>
    public PermissionRequirement(string permission, IReadOnlyList<string> allowedRoles)
    {
        Permission = permission;
        AllowedRoles = allowedRoles;
    }

    /// <summary>The permission key being enforced.</summary>
    public string Permission { get; }

    /// <summary>The role names that satisfy this requirement (membership in ANY grants access).</summary>
    public IReadOnlyList<string> AllowedRoles { get; }
}
