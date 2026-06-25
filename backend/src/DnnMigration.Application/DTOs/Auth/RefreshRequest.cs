namespace DnnMigration.Application.DTOs.Auth;

// MIGRATION: New auth-contract DTO with no 1:1 legacy class. Carries the refresh token for the
//            rotation endpoint POST /api/auth/refresh (AAP §0.3.4, §0.7.6 refresh-token rotation).
//            DNN had no refresh-token concept — it used a persistent Forms-auth cookie
//            (PortalSecurity.vb / UserController.UserLogin 'CreatePersistentCookie'); JWT
//            refresh-token rotation replaces it.
// MIGRATION: The token is an OPAQUE string — validation and rotation live in Infrastructure/Identity
//            (JwtService). This is a plain shape only.
// NOTE: If refresh tokens are delivered via an httpOnly cookie (AAP §0.7.6 preferred storage), the
//       endpoint may read the token from the cookie and this body becomes optional; the RefreshToken
//       property is retained as the default body contract per the folder requirements.
public record RefreshRequest
{
    public string RefreshToken { get; init; } = string.Empty;
}
