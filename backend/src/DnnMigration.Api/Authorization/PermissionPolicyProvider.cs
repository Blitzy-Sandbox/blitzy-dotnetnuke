using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace DnnMigration.Api.Authorization;

/// <summary>
/// An <see cref="IAuthorizationPolicyProvider"/> that materialises a permission-based
/// <see cref="AuthorizationPolicy"/> on demand for any policy name that is not otherwise registered.
/// </summary>
/// <remarks>
/// <para>
/// Resource controllers opt in with <c>[Authorize(Policy = Permissions.View)]</c> (and the Edit/Delete/
/// ManageSettings variants). Rather than pre-registering one named policy per permission key in
/// <c>Program.cs</c>, this provider builds a policy carrying a <see cref="PermissionRequirement"/> for the
/// requested name the first time it is seen. This has two important consequences required by AAP §0.6.2 and
/// MIGRATION_NOTES.md §3.3:
/// </para>
/// <list type="number">
///   <item><description>A recognised key the caller lacks is denied by
///   <see cref="PermissionAuthorizationHandler"/> → 403.</description></item>
///   <item><description>An <em>unknown</em> permission key still resolves to a (permission) policy, so the
///   authorization middleware evaluates it and the handler denies it → 403, instead of the framework throwing
///   <see cref="InvalidOperationException"/> ("The AuthorizationPolicy named '…' was not found.") → 500.</description></item>
/// </list>
/// <para>
/// Genuinely registered named policies and the default/fallback policies are delegated to the wrapped
/// <see cref="DefaultAuthorizationPolicyProvider"/> so that bare <c>[Authorize]</c> (authentication-only) and
/// any future named policies continue to behave normally.
/// </para>
/// </remarks>
public sealed class PermissionPolicyProvider : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider _fallbackProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="PermissionPolicyProvider"/> class.
    /// </summary>
    /// <param name="options">The authorization options used to seed the wrapped default provider.</param>
    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    {
        // Reuse the framework default provider for the default policy, the fallback policy, and any
        // policies registered explicitly via AddAuthorization(o => o.AddPolicy(...)).
        _fallbackProvider = new DefaultAuthorizationPolicyProvider(options);
    }

    /// <summary>Gets the default policy applied to bare <c>[Authorize]</c> (authentication required).</summary>
    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _fallbackProvider.GetDefaultPolicyAsync();

    /// <summary>Gets the fallback policy applied when no authorization metadata is present (none by default).</summary>
    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _fallbackProvider.GetFallbackPolicyAsync();

    /// <summary>
    /// Resolves the policy for the given <paramref name="policyName"/>: an explicitly-registered policy when
    /// one exists, otherwise a dynamically-built permission policy carrying a
    /// <see cref="PermissionRequirement"/> for that name.
    /// </summary>
    /// <param name="policyName">The requested policy name (a permission key for resource actions).</param>
    /// <returns>The resolved authorization policy (never <see langword="null"/> for our usage).</returns>
    public async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        // Honour any explicitly-registered named policy first (forward-compatible; none are registered today).
        var existing = await _fallbackProvider.GetPolicyAsync(policyName);
        if (existing is not null)
        {
            return existing;
        }

        // Build a permission policy for the requested name. Unknown keys flow through here too and are
        // denied by the handler (→ 403) rather than triggering a "policy not found" exception (→ 500).
        return new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(policyName))
            .Build();
    }
}
