namespace DnnMigration.Application.DTOs.User;

// MIGRATION: Read projection of UserInfo.vb + Membership/UserMembership.vb (composite flattened).
// SECURITY: deliberately OMITS Password / PasswordAnswer / PasswordQuestion (never expose in any read DTO).
public class UserDto
{
    public int UserID { get; set; }

    public int PortalID { get; set; }

    public int? AffiliateID { get; set; }

    public string? Username { get; set; }

    public string? DisplayName { get; set; }

    public string? Email { get; set; }

    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    // MIGRATION: legacy UserInfo.FullName = FirstName & " " & LastName (computed, read-only).
    public string FullName => $"{FirstName} {LastName}";

    public bool IsSuperUser { get; set; }

    // From membership (UserMembership.Approved, legacy default True).
    public bool Approved { get; set; }

    // MIGRATION: legacy UserInfo.Roles As String() public contract -> denormalized role-name array.
    public string[]? Roles { get; set; }

    public DateTime? CreatedDate { get; set; }

    public DateTime? LastLoginDate { get; set; }

    public DateTime? LastPasswordChangeDate { get; set; }

    public DateTime? LastActivityDate { get; set; }
}
