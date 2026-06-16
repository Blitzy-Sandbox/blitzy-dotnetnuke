using FluentValidation;
using DnnMigration.Application.DTOs.Portal;

namespace DnnMigration.Application.Validators;

// MIGRATION: Portal-create validation parity. Legacy validation derives from
// Library/Components/Portal/PortalInfo.vb (which carries NO DataAnnotation validation
// attributes — only XML serialization) plus Website/admin/Portal/SiteSettings.ascx.vb /
// Signup.ascx.vb (PortalName RequiredFieldValidator).
// MIGRATION: No server-side MaxLength rules are imposed here for behavioral equivalence.
// Although the physical Portals column lengths ARE known (Website/Providers/DataProviders/
// SqlDataProvider/DotNetNuke.Schema.SqlDataProvider — e.g. PortalName nvarchar(128)) and are
// enforced at the persistence layer in PortalConfiguration, the legacy SiteSettings.ascx markup
// applied only a client-side HTML maxlength (UI truncation) and never raised a server-side
// validation error for over-length input. Reproducing that as a FluentValidation rule would
// change behavior, so it is intentionally omitted.
// MIGRATION: Duplicate-name / home-directory collision checks are stateful (DB) and handled
// in PortalService, not here.
/// <summary>
/// FluentValidation validator for <see cref="CreatePortalDto"/>. Reproduces the conservative,
/// legacy-faithful DNN portal-creation validation: a required portal (site) name and an
/// optional, format-checked contact email. Registered automatically by the API composition
/// root via <c>AddValidatorsFromAssembly(...)</c>, hence the type is public.
/// </summary>
public class CreatePortalValidator : AbstractValidator<CreatePortalDto>
{
    /// <summary>
    /// Configures the portal-creation validation rules that mirror legacy DNN behavior.
    /// </summary>
    public CreatePortalValidator()
    {
        // MIGRATION: SiteSettings.ascx.vb / Signup.ascx.vb require a site (portal) name (RequiredFieldValidator).
        RuleFor(x => x.PortalName)
            .NotEmpty().WithMessage("Portal Name is required.");

        // MIGRATION: PortalInfo.Email contact email — validate format only when supplied (legacy field optional at create).
        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("Enter a valid Email address")
            .When(x => !string.IsNullOrEmpty(x.Email));
    }
}
