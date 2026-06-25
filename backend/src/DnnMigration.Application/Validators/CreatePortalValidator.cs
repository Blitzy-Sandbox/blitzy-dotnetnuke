using FluentValidation;
using DnnMigration.Application.DTOs.Portal;

namespace DnnMigration.Application.Validators;

// MIGRATION: Transcribed from Website/admin/Portal/Signup.ascx (RequiredFieldValidators + textbox maxlength) and
// Signup.ascx.vb — the legacy portal-creation form. Behavioral parity (AAP 0.7.1): identical required-ness, length
// limits and error message strings. Synchronous-only validator (no repository/data access): the portal-admin
// bootstrap fields (FirstName/LastName/Username/Password/Confirm) and template selection are multi-entity concerns
// handled in PortalService/UserService, not here.
public sealed class CreatePortalValidator : AbstractValidator<CreatePortalRequest>
{
    public CreatePortalValidator()
    {
        // MIGRATION: Signup.ascx valPortalName RequiredFieldValidator on txtPortalName -> NotEmpty; txtPortalName
        // maxlength=128 -> MaximumLength(128). (RequiredFieldValidator did not trim; NotEmpty also rejects
        // whitespace-only input -- a deliberate, documented tightening.)
        RuleFor(x => x.PortalName)
            .NotEmpty().WithMessage("Portal Name Is Required.")
            .MaximumLength(128);

        // MIGRATION: Signup.ascx valEmail RequiredFieldValidator on txtEmail -> NotEmpty; txtEmail maxlength=100.
        // Legacy Signup had NO email-format RegularExpressionValidator, so none is added here (faithful parity).
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email Is Required.")
            .MaximumLength(100);

        // MIGRATION: Signup.ascx txtDescription maxlength=500 (no server-side message in legacy; length guard only).
        RuleFor(x => x.Description)
            .MaximumLength(500);

        // MIGRATION: Signup.ascx txtKeyWords maxlength=500.
        RuleFor(x => x.KeyWords)
            .MaximumLength(500);

        // MIGRATION: Signup.ascx txtHomeDirectory maxlength=100.
        RuleFor(x => x.HomeDirectory)
            .MaximumLength(100);
    }
}
