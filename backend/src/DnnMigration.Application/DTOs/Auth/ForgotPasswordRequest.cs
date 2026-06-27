namespace DnnMigration.Application.DTOs.Auth;

// MIGRATION (CP-final review - auth workflow parity): inbound DTO for POST /api/auth/forgot-password, replacing the
// legacy Website/admin/Security/SendPassword.ascx.vb "Password Reminder" postback. Login/reset are PORTAL-SCOPED
// (AAP 0.7.1), so PortalId is part of the request. SendPassword.GetUser() looked the user up by email (when the
// membership config RequiresUniqueEmail) or otherwise by username; the single UsernameOrEmail field carries either.
// SECURITY: the value is INBOUND ONLY and never echoed/logged; the endpoint returns an identical generic response
// whether or not a matching account exists (no account enumeration).
public class ForgotPasswordRequest
{
    // MIGRATION: SendPassword portal scope (PortalSettings.PortalId). Required tenant scope (AAP 0.7.1).
    public int PortalId { get; set; }

    // MIGRATION: SendPassword txtUsername / txtEmail - the account identifier (username, or email when the portal
    // requires a unique email). Required.
    public string UsernameOrEmail { get; set; } = string.Empty;
}
