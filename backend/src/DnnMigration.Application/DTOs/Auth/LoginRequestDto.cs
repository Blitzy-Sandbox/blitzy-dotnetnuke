using System.ComponentModel.DataAnnotations;

namespace DnnMigration.Application.DTOs.Auth;

// MIGRATION: NEW DTO with no legacy *Info.vb equivalent. Credentials for POST /api/auth/login.
// Part of the single sanctioned behavior change (AAP §0.6.2/§0.7.1): legacy ASP.NET Forms Authentication
// + 56-bit DES (Library/Components/Security/PortalSecurity.vb: SignOut L79, DES Encrypt/Decrypt L138-L215)
// is replaced by stateless JWT Bearer issuance + BCrypt password verification.
public class LoginRequestDto
{
    // Username or email address identifying the account (accepts either form).
    // Required: non-empty enforced by AuthService (no Auth FluentValidation validator is in AAP scope).
    // MIGRATION (Finding F5 DoS defense-in-depth, AAP §0.7.2 non-functional security): cap the length as a
    // memory-pressure guard. [ApiController] auto-validation rejects an over-length credential with a clean
    // RFC 7807 400 at model binding BEFORE the BCrypt lookup, so a multi-megabyte "username" can no longer be
    // buffered and processed. 256 is generous (it accommodates an email-as-username) and never rejects a
    // legitimate credential. Recorded as DEV-073 in root MIGRATION_NOTES.md.
    [StringLength(256, ErrorMessage = "The Username must not exceed 256 characters.")]
    public string? Username { get; set; }

    // MIGRATION/SECURITY: PLAINTEXT password, INPUT ONLY. Verified against the stored BCrypt hash via
    // IPasswordHasher (replacing legacy DES Encrypt/Decrypt). Never persisted as plaintext, never echoed
    // back in any response DTO.
    // MIGRATION (Finding F5 DoS defense-in-depth): same memory-pressure cap as Username. 256 far exceeds any
    // legitimate passphrase (BCrypt itself only consumes the first 72 bytes) while bounding abusive payloads.
    [StringLength(256, ErrorMessage = "The Password must not exceed 256 characters.")]
    public string? Password { get; set; }
}
