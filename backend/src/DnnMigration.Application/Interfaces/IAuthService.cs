using DnnMigration.Application.DTOs.Auth;
using DnnMigration.Domain.Common;

namespace DnnMigration.Application.Interfaces;

/// <summary>
/// Service-layer contract for authentication. Consumed by AuthController via constructor
/// injection. The implementation validates credentials (BCrypt), issues and rotates JWT access
/// + refresh tokens, and projects the authenticated user. Replaces the legacy Forms-auth /
/// AspNetSqlMembershipProvider flow with JWT Bearer (AAP 0.3.3, 0.7.6).
/// </summary>
// MIGRATION: Source = Library/Components/Security/PortalSecurity.vb (UserLogin L632 -> UserController.UserLogin;
// SignOut L77) and the current-user projection from UserController.GetCurrentUserInfo. Library/Components/Users/
// Membership/UserMembership.vb is a membership DATA HOLDER (constructor-only, L67), not a controller. The legacy
// PortalSecurity DES Encrypt/Decrypt (L138/L175) is an Infrastructure/PasswordHasher (BCrypt) concern, NOT part of
// this contract. DTO-only contract — no raw Domain entities exposed (AAP 0.7.7).
public interface IAuthService
{
    // MIGRATION: Legacy PortalSecurity.UserLogin(Username, Password, PortalID, PortalName, IP, CreatePersistentCookie)
    // (PortalSecurity.vb L632). Authentication is PORTAL-SCOPED — the portal id is carried inside LoginRequest.PortalId.
    // Returns the JWT bundle (access + refresh token + current user). POST /api/auth/login.
    Task<Result<LoginResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);

    // MIGRATION: Refresh-token rotation — NO direct legacy equivalent (new to the JWT model, AAP 0.7.6). Accepts the
    // current refresh token and returns a fresh, rotated LoginResponse bundle. POST /api/auth/refresh.
    Task<Result<LoginResponse>> RefreshAsync(RefreshRequest request, CancellationToken cancellationToken = default);

    // MIGRATION: Legacy PortalSecurity.SignOut() (PortalSecurity.vb L77). Revokes the supplied refresh token
    // (rotation/blacklist). Non-generic Result (no payload). POST /api/auth/logout.
    Task<Result> LogoutAsync(RefreshRequest request, CancellationToken cancellationToken = default);

    // MIGRATION: Current-user projection from legacy UserController.GetCurrentUserInfo. Backs GET /api/auth/me.
    // CP1 review (AuthService #6 / IUserService #1) — PORTAL-SCOPED: both portalId and userId are resolved from the JWT
    // claims by the controller so the projection is tenant-consistent (multi-tenant isolation, AAP 0.7.1).
    Task<Result<CurrentUserDto>> GetCurrentUserAsync(int portalId, int userId, CancellationToken cancellationToken = default);

    // MIGRATION (CP-final review - auth workflow parity): legacy SendPassword.ascx.vb "Password Reminder" flow
    // (cmdSendPassword_Click). Portal-scoped (AAP 0.7.1): the user is resolved within request.PortalId. Returns an
    // IDENTICAL generic response whether or not a matching account exists (no account enumeration). With BCrypt
    // (one-way) the legacy "send the actual password" reminder is impossible by design, and email dispatch
    // (Services.Mail / Messaging) is OUT OF SCOPE per AAP 0.6.2; the in-scope work is validation + portal-scoped
    // lookup + the secure generic response. POST /api/auth/forgot-password (rate-limited).
    Task<Result<ForgotPasswordResponse>> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken = default);
}
