namespace DnnMigration.Application.DTOs.User;

// MIGRATION (Finding CP-FINAL-6 / DEV-067 — Membership workflow parity): read projection of a user's ASP.NET
// Membership state for the Website/admin/Users/Membership.ascx.vb workflow (approve / unauthorize / unlock /
// force-password-change). The approval and lockout state are sourced from the physical [aspnet_Membership]
// table (User.Approved / User.LockedOut are EF-Ignore()d), while UpdatePassword is the mapped [Users] column.
//
// This DTO is built MANUALLY by UserService (NOT via AutoMapper) so the entity<->DTO map set asserted by
// UserProfileTests.Configuration_IsValid is unaffected, and so the security contract that NO password material
// is ever exposed in a read DTO is preserved (this type carries no Password / PasswordAnswer / PasswordQuestion).
//
// SERIALIZATION: the property names are deliberately chosen so System.Text.Json's camelCase policy emits
// `approved` / `lockedOut` / `updatePassword` / `failedPasswordAttemptCount` / `lastLockoutDate` / `userID`,
// matching the EXISTING Angular `MembershipDto` model field names verbatim (no client-side remapping required).
public class MembershipDto
{
    /// <summary>The user this membership state belongs to (wire field <c>userID</c>).</summary>
    public int UserID { get; set; }

    /// <summary>
    /// Whether the membership is approved ([aspnet_Membership].IsApproved; wire field <c>approved</c>). Target
    /// of the authorize / unauthorize transitions.
    /// </summary>
    public bool Approved { get; set; }

    /// <summary>
    /// Whether the membership is locked out ([aspnet_Membership].IsLockedOut; wire field <c>lockedOut</c>).
    /// Cleared by the unlock transition.
    /// </summary>
    public bool LockedOut { get; set; }

    /// <summary>
    /// Whether the user must change their password on next login (mapped [Users].UpdatePassword; wire field
    /// <c>updatePassword</c>). Set by the force-password-change transition.
    /// </summary>
    public bool UpdatePassword { get; set; }

    /// <summary>
    /// Count of consecutive failed password attempts ([aspnet_Membership].FailedPasswordAttemptCount; wire
    /// field <c>failedPasswordAttemptCount</c>). Reset to zero by the unlock transition.
    /// </summary>
    public int FailedPasswordAttemptCount { get; set; }

    /// <summary>
    /// Timestamp of the most recent lockout ([aspnet_Membership].LastLockoutDate; wire field
    /// <c>lastLockoutDate</c>), or <c>null</c> when the account has never been locked out (the service maps the
    /// legacy "never locked out" sentinel — 1754-01-01 — and any earlier value to <c>null</c>).
    /// </summary>
    public DateTime? LastLockoutDate { get; set; }
}
