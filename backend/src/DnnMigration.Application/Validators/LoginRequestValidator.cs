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
        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("Username Is Required.");

        // MIGRATION: Required login credential -- password must be supplied.
        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password Is Required.");

        // MIGRATION: Multi-tenant scope -- PortalId resolves the user within the correct portal.
        RuleFor(x => x.PortalId)
            .GreaterThanOrEqualTo(0);
    }
}
