namespace DnnMigration.Application.DTOs;

/// <summary>
/// Request payload used to change an authenticated user's password.
/// </summary>
/// <remarks>
/// MIGRATION: Replaces the legacy DotNetNuke password-change path modelled by
/// <c>UserMembership</c> (<c>Library/Components/Users/Membership/UserMembership.vb</c>),
/// whose <c>Password</c> and <c>UpdatePassword</c> members drove credential updates
/// through the ADO.NET membership provider. In the modernized BFF architecture the
/// target user is resolved from the route/authentication context (JWT claims) rather
/// than being carried in the request body, so no user identifier appears here. This
/// type is intentionally kept separate from <c>UpdateUserDto</c> so that password
/// material never travels on a general profile-update payload. Validation (required
/// values, complexity rules, and old/new comparison) is applied via FluentValidation
/// at the application boundary rather than through data-annotation attributes on this
/// contract.
/// </remarks>
public record ChangePasswordDto
{
    /// <summary>
    /// The user's current password. Verified against the stored credential before the
    /// change is applied.
    /// </summary>
    public string OldPassword { get; init; } = string.Empty;

    /// <summary>
    /// The new password to set for the user.
    /// </summary>
    public string NewPassword { get; init; } = string.Empty;
}
