namespace DnnMigration.Application.DTOs.Auth;

// MIGRATION: NEW DTO with no legacy *Info.vb equivalent. Credentials for POST /api/auth/login.
// Part of the single sanctioned behavior change (AAP §0.6.2/§0.7.1): legacy ASP.NET Forms Authentication
// + 56-bit DES (Library/Components/Security/PortalSecurity.vb: SignOut L79, DES Encrypt/Decrypt L138-L215)
// is replaced by stateless JWT Bearer issuance + BCrypt password verification.
public class LoginRequestDto
{
    // Username or email address identifying the account (accepts either form).
    // Required: non-empty enforced by AuthService (no Auth FluentValidation validator is in AAP scope).
    public string? Username { get; set; }

    // MIGRATION/SECURITY: PLAINTEXT password, INPUT ONLY. Verified against the stored BCrypt hash via
    // IPasswordHasher (replacing legacy DES Encrypt/Decrypt). Never persisted as plaintext, never echoed
    // back in any response DTO.
    public string? Password { get; set; }
}
