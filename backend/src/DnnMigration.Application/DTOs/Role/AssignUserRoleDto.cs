namespace DnnMigration.Application.DTOs.Role;

/// <summary>
/// Command DTO for the administrative "assign a user to a role" operation, carrying the
/// admin-supplied effective/expiry membership window.
/// </summary>
// MIGRATION: ported from the ADMIN assignment path RoleController.AddUserRole(PortalID, UserId,
// RoleId, EffectiveDate, ExpiryDate) [RoleController.vb:L295-317] — the path the legacy
// SecurityRoles.ascx.vb "Add Role To User" screen invokes (it reads two date textboxes; a blank
// textbox becomes Null.NullDate, modeled here as a null DateTime?). That admin path persists ONLY
// the effective/expiry window via an upsert; it never sets IsTrialUsed or Subscribed. Those two
// flags belong exclusively to the SELF-SERVICE RoleController.UpdateUserRole(Cancel) computed-window
// path [RoleController.vb:L489-557], which is OUT OF SCOPE for this admin API, so they are NOT
// accepted here (an earlier CP2 revision wrongly carried them on the write DTO; corrected per the
// CP2 code-review role-assignment-contract finding — see MIGRATION_NOTES.md §6.2). The read side
// (UserRoleAssignmentDto) still surfaces the full persisted state, including IsTrialUsed/Subscribed.
// UserRoleID is NOT accepted here — it is server-assigned (identity) — and the User/Role navigations
// are resolved by the repository from UserID/RoleID.
public class AssignUserRoleDto
{
    /// <summary>Identifier of the user to assign.</summary>
    public int UserID { get; set; }

    /// <summary>Identifier of the role to assign.</summary>
    public int RoleID { get; set; }

    /// <summary>Date the assignment becomes effective; <c>null</c> when unbounded (legacy Null.NullDate).</summary>
    public DateTime? EffectiveDate { get; set; }

    /// <summary>Date the assignment expires; <c>null</c> when it does not expire (legacy Null.NullDate).</summary>
    public DateTime? ExpiryDate { get; set; }
}
