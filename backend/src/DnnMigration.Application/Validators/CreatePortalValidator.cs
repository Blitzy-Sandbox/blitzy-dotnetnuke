using FluentValidation;
using DnnMigration.Application.DTOs.Portal;

namespace DnnMigration.Application.Validators;

// MIGRATION: Portal-create validation parity. Legacy rules come from
// Library/Components/Portal/PortalInfo.vb (no DataAnnotation validation attributes — XML serialization only)
// and Website/admin/Portal/SiteSettings.ascx.vb / Signup.ascx.vb (PortalName RequiredFieldValidator).
// MIGRATION: No MaxLength rules — the DNN core Portals table schema is not present in the in-scope
// install scripts, so legacy max-lengths are unknown; do NOT invent stricter limits (behavioral equivalence).
// MIGRATION: Duplicate-name / home-directory collision checks are stateful (DB) and handled in PortalService, not here.
public class CreatePortalValidator : AbstractValidator<CreatePortalDto>
{
    public CreatePortalValidator()
    {
        // MIGRATION: SiteSettings.ascx.vb / Signup.ascx.vb require a site (portal) name (RequiredFieldValidator).
        RuleFor(x => x.PortalName)
            .NotEmpty();

        // MIGRATION: PortalInfo.Email contact email — validate format only when supplied (legacy field optional at create).
        RuleFor(x => x.Email)
            .EmailAddress()
            .When(x => !string.IsNullOrEmpty(x.Email));
    }
}
