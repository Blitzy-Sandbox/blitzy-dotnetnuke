namespace DnnMigration.Application.DTOs.User;

// MIGRATION: Create input projected from UserInfo.vb + Membership/UserMembership.vb.
public class CreateUserDto
{
    public int PortalID { get; set; }

    public string? Username { get; set; }

    // MIGRATION/SECURITY: PLAINTEXT password, INPUT ONLY. Hashed via IPasswordHasher (BCrypt) in the
    // service layer before persistence; never stored as plaintext and never echoed back in any response DTO.
    // Required-ness is enforced by CreateUserValidator (FluentValidation) in the Validators/ folder.
    public string? Password { get; set; }

    public string? DisplayName { get; set; }

    public string? Email { get; set; }

    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public bool IsSuperUser { get; set; }

    public bool Approved { get; set; }
}
