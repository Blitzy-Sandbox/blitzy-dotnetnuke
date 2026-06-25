namespace DnnMigration.Application.DTOs.Auth;

// MIGRATION: New auth-contract DTO with no 1:1 legacy class. Represents the JWT token bundle
//            returned on a successful login. Replaces the legacy ASP.NET 2.0 Forms-authentication
//            ticket / AspNetSqlMembershipProvider session (Website/release.config) and the obsolete
//            PortalSecurity.UserLogin flow (Library/Components/Security/PortalSecurity.vb L631-632)
//            with short-lived JWT access tokens + refresh-token rotation (AAP §0.7.6).
// MIGRATION: Tokens are OPAQUE strings here — issuance, signing, validation and rotation live in
//            Infrastructure/Identity (JwtService). This DTO is a plain shape only.
// MIGRATION: The legacy UserLoginStatus enum (LOGIN_FAILURE / LOGIN_SUCCESS / LOGIN_SUPERUSER /
//            LOGIN_USERLOCKEDOUT / LOGIN_USERNOTAPPROVED / ... in Library/Components/Users/Membership/
//            UserLoginStatus.vb) is deliberately NOT modelled as a field. Login FAILURES surface as
//            RFC 7807 ProblemDetails via ExceptionHandlingMiddleware (AAP §0.7.5); a LoginResponse is
//            only produced for the SUCCESS case.
// MIGRATION: Contains NO password material (AAP §0.7.6 security rule).
public record LoginResponse
{
    // MIGRATION: Short-lived JWT access token (Bearer). 60-minute lifetime per AAP §0.7.6.
    public string AccessToken { get; init; } = string.Empty;

    // MIGRATION: Opaque refresh token enabling refresh-token rotation (AAP §0.7.6), replacing the
    //            legacy persistent Forms-auth cookie ('CreatePersistentCookie').
    public string RefreshToken { get; init; } = string.Empty;

    // OAuth2 token type; always "Bearer" for JWT bearer auth.
    public string TokenType { get; init; } = "Bearer";

    // Access-token lifetime in seconds (e.g. 3600 = 60 minutes, per AAP §0.7.6).
    public int ExpiresIn { get; init; }

    // Absolute UTC expiry of the access token (System.DateTime via ImplicitUsings).
    public DateTime ExpiresAt { get; init; }

    // MIGRATION: Optional snapshot of the authenticated user (projected from
    //            UserController.GetCurrentUserInfo()), returned on login to save a /api/auth/me
    //            round-trip. References CurrentUserDto rather than duplicating a lightweight summary
    //            (folder requirements permit either; the reference is chosen to stay DRY).
    public CurrentUserDto? User { get; init; }
}
