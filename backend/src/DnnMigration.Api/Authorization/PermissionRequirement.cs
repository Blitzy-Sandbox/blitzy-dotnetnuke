using Microsoft.AspNetCore.Authorization;

namespace DnnMigration.Api.Authorization;

/// <summary>
/// An <see cref="IAuthorizationRequirement"/> that carries a single permission key (one of
/// <see cref="Permissions.All"/>, or an unknown key) which the authenticated principal must
/// hold for authorization to succeed.
/// </summary>
/// <remarks>
/// Instances are produced on demand by <see cref="PermissionPolicyProvider"/> — one per distinct
/// <c>[Authorize(Policy = ...)]</c> name — and evaluated by
/// <see cref="PermissionAuthorizationHandler"/>. Keeping the required key on the requirement (rather
/// than registering a fixed policy per key in <c>Program.cs</c>) lets the provider handle every
/// permission key uniformly, including unknown keys, without a "policy not found" exception.
/// </remarks>
public sealed class PermissionRequirement : IAuthorizationRequirement
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PermissionRequirement"/> class.
    /// </summary>
    /// <param name="permission">The permission key the principal must hold (case-sensitive).</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="permission"/> is null or whitespace.</exception>
    public PermissionRequirement(string permission)
    {
        if (string.IsNullOrWhiteSpace(permission))
        {
            throw new ArgumentException("A permission key is required.", nameof(permission));
        }

        Permission = permission;
    }

    /// <summary>Gets the permission key required by this requirement.</summary>
    public string Permission { get; }
}
