using DnnMigration.Application.DTOs.User;

namespace DnnMigration.Application.DTOs.Auth;

// MIGRATION: NEW DTO with no legacy *Info.vb equivalent. Response for POST /api/auth/login and
// POST /api/auth/refresh. Replaces legacy ASP.NET Forms Authentication session cookies + DES tokens
// (Library/Components/Security/PortalSecurity.vb: SignOut L79, DES Encrypt/Decrypt L138-L215) with a
// stateless JWT Bearer access token plus a rotated refresh token.
// SECURITY: NEVER carries password material - only tokens, expiry, and the safe UserDto projection
// (UserDto itself exposes no password fields).
public class AuthResponseDto
{
    // The signed JWT Bearer access token (~60-minute lifetime per the Jwt settings).
    public string? AccessToken { get; set; }

    // The rotated refresh token; exchange it via POST /api/auth/refresh for a new access token.
    public string? RefreshToken { get; set; }

    // Absolute UTC instant at which AccessToken expires.
    public DateTime ExpiresAt { get; set; }

    // Seconds until AccessToken expires (relative lifetime; OAuth2-style convenience field).
    public int ExpiresIn { get; set; }

    // The authenticated user's safe read projection. SECURITY: UserDto carries no password fields.
    public UserDto? User { get; set; }
}
