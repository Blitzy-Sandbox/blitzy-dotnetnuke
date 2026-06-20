using DnnMigration.Application.DTOs.Auth;
using DnnMigration.Application.DTOs.User;

namespace DnnMigration.Application.Interfaces;

/// <summary>
/// JWT authentication orchestration contract (the /api/auth/{login,refresh,logout,me} surface).
/// MIGRATION: replaces legacy Forms Authentication + DES (PortalSecurity.vb, UserController.UserLogin/
/// GetCurrentUserInfo) with stateless JWT Bearer tokens and BCrypt. Identity is carried in JWT claims;
/// LogoutAsync/GetCurrentUserAsync take the user id extracted by the controller from the authenticated
/// principal. Implemented by Application/Services/AuthService.cs (which depends on IJwtService,
/// IPasswordHasher, and IUserRepository).
/// </summary>
public interface IAuthService
{
    /// <summary>Validates credentials (BCrypt) and issues access + refresh tokens. Throws on invalid credentials.</summary>
    Task<AuthResponseDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>Rotates a valid refresh token, returning a fresh access + refresh token pair.</summary>
    Task<AuthResponseDto> RefreshAsync(RefreshTokenRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Acknowledges a logout. MIGRATION (m2/DEV-028): JWT is stateless and Phase 1 keeps NO server-side
    /// refresh-token store, so this performs NO server-side revocation — it is a no-op acknowledgement and
    /// the client is responsible for discarding its locally held access/refresh tokens. (Server-side
    /// revocation would require a persisted refresh-token store, which is out of Phase-1 scope.)
    /// </summary>
    Task LogoutAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>Returns the authenticated user's profile (/api/auth/me), or null if not found.</summary>
    Task<UserDto?> GetCurrentUserAsync(int userId, CancellationToken cancellationToken = default);
}
