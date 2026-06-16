namespace DnnMigration.Application.DTOs.Role;

/// <summary>
/// Command DTO for assigning a user to a role together with the full legacy membership
/// metadata (effective/expiry window, trial usage, and subscription flag).
/// </summary>
// MIGRATION: ported from RoleController.AddUserRole / UserRoleInfo.vb. The legacy
// SecurityRoles.ascx.vb "Add Role To User" flow set the effective date, expiry date,
// trial-used flag, and subscription flag at assignment time; this command carries those same
// fields so the migrated API preserves assignment parity rather than accepting only
// (userId, roleId). UserRoleID is NOT accepted here — it is server-assigned (identity), and the
// User/Role navigations are resolved by the repository from UserID/RoleID.
public class AssignUserRoleDto
{
    /// <summary>Identifier of the user to assign.</summary>
    public int UserID { get; set; }

    /// <summary>Identifier of the role to assign.</summary>
    public int RoleID { get; set; }

    /// <summary>Date the assignment becomes effective; <c>null</c> when unbounded.</summary>
    public DateTime? EffectiveDate { get; set; }

    /// <summary>Date the assignment expires; <c>null</c> when it does not expire.</summary>
    public DateTime? ExpiryDate { get; set; }

    /// <summary>Whether the trial period for this role has been used.</summary>
    public bool IsTrialUsed { get; set; }

    /// <summary>Whether the user is subscribed to the role.</summary>
    public bool Subscribed { get; set; }
}
