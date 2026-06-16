namespace DnnMigration.Application.DTOs.User;

// MIGRATION: Update input projected from UserInfo.vb + Membership/UserMembership.vb.
// Username EXCLUDED (legacy IsReadOnly(True) - read-only post-creation). Plaintext password EXCLUDED
// (password changes go through a dedicated flow). PortalID EXCLUDED (UserID is the global PK).
public class UpdateUserDto
{
    public int UserID { get; set; }

    public string? DisplayName { get; set; }

    public string? Email { get; set; }

    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public bool IsSuperUser { get; set; }

    public bool Approved { get; set; }
}
