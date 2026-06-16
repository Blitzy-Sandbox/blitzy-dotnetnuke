using FluentValidation;
using DnnMigration.Application.DTOs.Portal;

namespace DnnMigration.Application.Validators;

// MIGRATION: Portal-update validation parity (PortalController.vb UpdatePortalInfo). Rules from
// PortalInfo.vb (no validation attributes) + Website/admin/Portal/SiteSettings.ascx.vb (PortalName required).
// MIGRATION: No server-side MaxLength rules here. Physical Portals column lengths ARE known
// (DotNetNuke.Schema.SqlDataProvider) and are enforced in PortalConfiguration, but legacy
// SiteSettings.ascx applied only a client-side HTML maxlength (never a server validation error);
// reproducing it as a rule would change behavior.
// MIGRATION: Duplicate-name / home-directory collision checks are stateful and handled in PortalService.
public class UpdatePortalValidator : AbstractValidator<UpdatePortalDto>
{
    public UpdatePortalValidator()
    {
        // MIGRATION: update targets an existing portal — key must be valid (> 0).
        RuleFor(x => x.PortalID)
            .GreaterThan(0).WithMessage("A valid portal identifier is required.");

        // MIGRATION: SiteSettings.ascx.vb requires a site (portal) name.
        RuleFor(x => x.PortalName)
            .NotEmpty().WithMessage("Portal Name is required.");

        // MIGRATION: PortalInfo.Email format checked only when supplied.
        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("Enter a valid Email address")
            .When(x => !string.IsNullOrEmpty(x.Email));
    }
}
