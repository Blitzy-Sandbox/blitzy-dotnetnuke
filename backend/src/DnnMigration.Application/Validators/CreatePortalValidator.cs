using FluentValidation;
using DnnMigration.Application.DTOs.Portal;

namespace DnnMigration.Application.Validators;

// MIGRATION: Portal-create validation parity. Legacy rules come from
// Library/Components/Portal/PortalInfo.vb (no DataAnnotation validation attributes - XML serialization only)
// and Website/admin/Portal/SiteSettings.ascx.vb / Signup.ascx.vb (PortalName RequiredFieldValidator).
// MIGRATION (CP1 message-parity correction): the authoritative DNN 4.9 schema IS known
// (Website/Providers/DataProviders/SqlDataProvider/DotNetNuke.Schema.SqlDataProvider: PortalName
// nvarchar(128), Description nvarchar(500), etc.). Those physical column lengths are enforced at the EF
// layer via HasMaxLength in PortalConfiguration, NOT re-declared here: the legacy SiteSettings/Signup
// validation tier used ONLY a RequiredFieldValidator on the portal name (no server-side length/format
// validator), so adding validation-tier MaxLength rules would over-constrain relative to legacy behavior
// (behavioral equivalence). The error messages below reproduce the legacy ASCX validator text verbatim
// (DNN Signup/SiteSettings convention: trailing period).
// MIGRATION: Duplicate-name / home-directory collision checks are stateful (DB) and handled in PortalService, not here.
public class CreatePortalValidator : AbstractValidator<CreatePortalDto>
{
    public CreatePortalValidator()
    {
        // MIGRATION: SiteSettings.ascx.vb / Signup.ascx.vb require a site (portal) name (RequiredFieldValidator).
        RuleFor(x => x.PortalName)
            .NotEmpty().WithMessage("Portal Name Is Required.");

        // MIGRATION: PortalInfo.Email contact email - validate format only when supplied (legacy field optional at create).
        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("Email Address is invalid.")
            .When(x => !string.IsNullOrEmpty(x.Email));
    }
}
