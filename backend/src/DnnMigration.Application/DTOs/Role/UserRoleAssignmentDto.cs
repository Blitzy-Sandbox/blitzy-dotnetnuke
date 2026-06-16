namespace DnnMigration.Application.DTOs.Role;

/// <summary>
/// Read DTO for a single user-role assignment, exposing the per-assignment membership
/// metadata that governs the lifetime and billing state of the membership.
/// </summary>
// MIGRATION: ported from UserRoleInfo.vb (Library/Components/Users/UserRoleInfo.vb,
// DotNetNuke.Entities.Users). Surfaces the effective/expiry window, trial usage, and
// subscription flag that the legacy "Manage Roles" / SecurityRoles.ascx.vb screen
// round-tripped. Mirrors the UserRole JOIN entity: EffectiveDate/ExpiryDate are nullable
// (legacy Null date sentinels -> DateTime?), IsTrialUsed/Subscribed are booleans, and
// UserRoleID is the server-assigned identity key (read-only). The legacy denormalized
// FullName/Email display fields are intentionally NOT duplicated here; they are reached
// through the User aggregate, consistent with the UserRole entity's navigation design.
public class UserRoleAssignmentDto
{
    /// <summary>Server-assigned identity key of the user-role assignment.</summary>
    public int UserRoleID { get; set; }

    /// <summary>Identifier of the assigned user.</summary>
    public int UserID { get; set; }

    /// <summary>Identifier of the assigned role.</summary>
    public int RoleID { get; set; }

    /// <summary>Date the assignment becomes effective; <c>null</c> when unbounded.</summary>
    public DateTime? EffectiveDate { get; set; }

    /// <summary>Date the assignment expires; <c>null</c> when it does not expire.</summary>
    public DateTime? ExpiryDate { get; set; }

    /// <summary>Whether the trial period for this role has been used.</summary>
    public bool IsTrialUsed { get; set; }

    /// <summary>Whether the user is currently subscribed to the role.</summary>
    public bool Subscribed { get; set; }
}
