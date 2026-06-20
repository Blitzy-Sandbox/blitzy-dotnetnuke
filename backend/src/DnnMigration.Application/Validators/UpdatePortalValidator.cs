using FluentValidation;
using DnnMigration.Application.DTOs.Portal;

namespace DnnMigration.Application.Validators;

// MIGRATION: Portal-update validation parity (PortalController.vb UpdatePortalInfo). Rules from
// PortalInfo.vb (no validation attributes) + Website/admin/Portal/SiteSettings.ascx.vb (PortalName required).
// MIGRATION (CP1 message-parity correction): the authoritative DNN 4.9 schema IS known
// (DotNetNuke.Schema.SqlDataProvider: PortalName nvarchar(128), etc.); physical column lengths are enforced
// at the EF layer (PortalConfiguration HasMaxLength), NOT re-declared here, because the legacy SiteSettings
// validation tier used only a RequiredFieldValidator on the portal name (no server-side length validator).
// The error messages below reproduce the legacy ASCX validator text verbatim (DNN convention: trailing period).
// MIGRATION: Duplicate-name / home-directory collision checks are stateful and handled in PortalService.
public class UpdatePortalValidator : AbstractValidator<UpdatePortalDto>
{
    public UpdatePortalValidator()
    {
        // MIGRATION: update targets an existing portal - key must be valid (> 0).
        RuleFor(x => x.PortalID)
            .GreaterThan(0).WithMessage("A valid Portal is required.");

        // MIGRATION: SiteSettings.ascx.vb requires a site (portal) name.
        RuleFor(x => x.PortalName)
            .NotEmpty().WithMessage("Portal Name Is Required.");

        // MIGRATION: PortalInfo.Email format checked only when supplied.
        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("Email Address is invalid.")
            .When(x => !string.IsNullOrEmpty(x.Email));
    }
}
