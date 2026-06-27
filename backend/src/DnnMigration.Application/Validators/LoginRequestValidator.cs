using FluentValidation;
using DnnMigration.Application.DTOs.Auth;

namespace DnnMigration.Application.Validators;

// MIGRATION: API-contract login validation derived from the DNN login flow (Website/admin/Security login controls,
// SendPassword.ascx.vb) and PortalSecurity.vb. The legacy Web Forms login did not use markup field validators -- it
// verified credentials server-side -- so this validator only enforces presence of the required login fields plus the
// multi-tenant PortalId scope. Behavioral parity (AAP 0.7.1). Synchronous-only validator (no repository/data access):
// credential verification (BCrypt), CAPTCHA/VerificationCode (conditional on Security_CaptchaLogin), and
// account-status checks are enforced in AuthService.
public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        // MIGRATION: Required login credential -- username must be supplied.
        // MIGRATION/SECURITY (QA Checkpoint F5 -- finding F-6): bound the username to the legacy Users.UserName
        // column width (nvarchar(100); see 01.00.00.SqlDataProvider). Without a cap, an oversized username (e.g.
        // 200KB) passed presence validation and only failed deep at the data layer as a generic 500. The cap
        // rejects it at the API boundary with a 400 ValidationProblemDetails (via AddFluentValidationAutoValidation,
        // before AuthService/the repository run). Behavioral parity is preserved: any legitimate 1..100-char
        // username is still accepted exactly as before.
        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("Username Is Required.")
            .MaximumLength(100);

        // MIGRATION: Required login credential -- password must be supplied.
        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password Is Required.");

        // MIGRATION: Multi-tenant scope -- PortalId resolves the user within the correct portal.
        RuleFor(x => x.PortalId)
            .GreaterThanOrEqualTo(0);
    }
}
