namespace DnnMigration.Application.DTOs.Auth;

// MIGRATION (CP-final review - auth workflow parity): outbound DTO for POST /api/auth/forgot-password. SECURITY: the
// message is intentionally GENERIC and IDENTICAL regardless of whether a matching account was found, so the endpoint
// never reveals account existence (no enumeration). It carries no user data, no token, and no account status.
public record ForgotPasswordResponse
{
    // MIGRATION: a generic, non-enumerating confirmation message shown by the SPA forgot-password screen.
    public string Message { get; init; } = string.Empty;
}
