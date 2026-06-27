using FluentValidation;
using DnnMigration.Application.DTOs.Auth;

namespace DnnMigration.Application.Validators;

// MIGRATION (CP-final review - auth workflow parity): API-contract validation for the password-reset request derived
// from the DNN SendPassword.ascx.vb flow. The legacy Web Forms control required either a username or (when the portal
// RequiresUniqueEmail) an email; the single UsernameOrEmail field carries either, so presence is the only structural
// rule. PortalId enforces the multi-tenant scope (AAP 0.7.1). Synchronous-only (no data access): account resolution is
// performed in AuthService, which returns a generic non-enumerating response regardless of outcome.
public sealed class ForgotPasswordValidator : AbstractValidator<ForgotPasswordRequest>
{
    public ForgotPasswordValidator()
    {
        // MIGRATION: SendPassword required the account identifier (username or email) to be supplied.
        RuleFor(x => x.UsernameOrEmail)
            .NotEmpty().WithMessage("A username or email address is required.");

        // MIGRATION: Multi-tenant scope -- PortalId resolves the user within the correct portal.
        RuleFor(x => x.PortalId)
            .GreaterThanOrEqualTo(0);
    }
}
