namespace DnnMigration.Application.DTOs.Auth;

// MIGRATION: NEW DTO with no legacy *Info.vb equivalent. Payload for POST /api/auth/refresh - exchanges a
// valid rotated refresh token for a new JWT access token. Part of the JWT replacement for legacy Forms
// Authentication (Library/Components/Security/PortalSecurity.vb); tokens are stateless so the server keeps
// no session.
public class RefreshTokenRequestDto
{
    // The rotated refresh token previously issued in AuthResponseDto.
    // Required: non-empty enforced by AuthService.
    public string? RefreshToken { get; set; }
}
