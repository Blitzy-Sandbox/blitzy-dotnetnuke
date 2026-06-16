using FluentValidation;
using DnnMigration.Application.DTOs.Portal;

namespace DnnMigration.Application.Validators;

// MIGRATION: Portal-update validation parity (PortalController.vb UpdatePortalInfo). Rules from
// PortalInfo.vb (no validation attributes) + Website/admin/Portal/SiteSettings.ascx.vb (PortalName required).
// MIGRATION: No MaxLength — DNN core Portals schema absent from in-scope install scripts; none invented.
// MIGRATION: Duplicate-name / home-directory collision checks are stateful and handled in PortalService.
public class UpdatePortalValidator : AbstractValidator<UpdatePortalDto>
{
    public UpdatePortalValidator()
    {
        // MIGRATION: update targets an existing portal — key must be valid (> 0).
        RuleFor(x => x.PortalID)
            .GreaterThan(0);

        // MIGRATION: SiteSettings.ascx.vb requires a site (portal) name.
        RuleFor(x => x.PortalName)
            .NotEmpty();

        // MIGRATION: PortalInfo.Email format checked only when supplied.
        RuleFor(x => x.Email)
            .EmailAddress()
            .When(x => !string.IsNullOrEmpty(x.Email));
    }
}
