namespace DnnMigration.Application.DTOs.User;

/// <summary>
/// Request body for the administrative "force password change on next login" toggle
/// (<c>POST /api/v1/users/{id}/force-password-change</c>).
/// </summary>
/// <remarks>
/// MIGRATION (CP-FINAL / Code-Review G5): reproduces the legacy DNN Membership admin workflow that set
/// <c>UserMembership.UpdatePassword</c> [Library/Components/Users/Membership/UserMembership.vb], which the
/// "Manage Users" / Membership admin surface exposed [Website/admin/Users/Membership.ascx.vb]. The flag maps
/// to the REAL persisted <c>dbo.Users.UpdatePassword</c> column (one of the table's 9 physical columns), so
/// this workflow is fully implementable WITHOUT a schema change (ADR-002 / AAP §0.2.2). A small dedicated DTO
/// is used (rather than a bare <see cref="bool"/> body) so the JSON contract is an explicit, self-describing
/// object <c>{ "require": true|false }</c>.
/// </remarks>
public sealed class ForcePasswordChangeRequestDto
{
    /// <summary>
    /// When <see langword="true"/>, the user must change their password at next login (sets
    /// <c>dbo.Users.UpdatePassword = 1</c>); when <see langword="false"/>, the requirement is cleared.
    /// </summary>
    public bool Require { get; set; }
}
