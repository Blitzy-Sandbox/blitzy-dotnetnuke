namespace DnnMigration.Application.DTOs.Auth;

// MIGRATION: New auth-contract DTO with no 1:1 legacy class. Carries the inbound login credentials
//            for POST /api/auth/login (AAP §0.3.4). Derived from the legacy login signature
//            UserController.UserLogin(portalId, Username, Password, VerificationCode, PortalName, IP,
//            ByRef loginStatus, CreatePersistentCookie) (Library/Components/Users/UserController.vb
//            L991) and the obsolete PortalSecurity.UserLogin DES flow (PortalSecurity.vb).
// MIGRATION: Login is PORTAL-SCOPED (DNN authenticates a user within a portal), so PortalId is part
//            of the request (AAP §0.7.1 multi-tenant isolation).
// MIGRATION: Plaintext Password is INBOUND ONLY — verified against the BCrypt hash in
//            Infrastructure/Identity (PasswordHasher), replacing the legacy DES Encrypt/Decrypt in
//            PortalSecurity.vb (AAP §0.7.6). It must NEVER appear on a response DTO and NEVER be
//            logged (structured logging excludes sensitive data, AAP §0.7.5).
// MIGRATION: Legacy server-derived parameters PortalName (resolved from PortalId), IP (from the HTTP
//            request) and authType (default "DNN" at UserController.vb L1111) are NOT client inputs
//            and are intentionally omitted — the AuthService/controller supply them.
public class LoginRequest
{
    // MIGRATION: UserController.UserLogin 'Username' parameter. Required.
    public string Username { get; set; } = string.Empty;

    // MIGRATION: UserController.UserLogin 'Password' parameter. Required. Plaintext INBOUND ONLY —
    //            never echoed on a response, never logged.
    public string Password { get; set; } = string.Empty;

    // MIGRATION: UserController.UserLogin 'portalId' parameter. Required tenant scope (AAP §0.7.1).
    public int PortalId { get; set; }

    // MIGRATION: Maps to the legacy 'CreatePersistentCookie' flag ("whether the login credentials
    //            should be persisted") — the "Remember Me" option. Optional; defaults to false.
    public bool RememberMe { get; set; }

    // MIGRATION: UserController.UserLogin 'VerificationCode' parameter ("verification code of the
    //            User attempting to log in"). Optional/nullable — only meaningful for portals that
    //            use verified registration.
    public string? VerificationCode { get; set; }
}
