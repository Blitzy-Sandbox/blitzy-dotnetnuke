using FluentValidation;
using DnnMigration.Application.DTOs.Portal;

namespace DnnMigration.Application.Validators;

// MIGRATION: Portal-create validation parity. Legacy validation derives from
// Library/Components/Portal/PortalInfo.vb (which carries NO DataAnnotation validation
// attributes — only XML serialization) plus Website/admin/Portal/SiteSettings.ascx.vb /
// Signup.ascx.vb (PortalName RequiredFieldValidator).
// MIGRATION: No MaxLength rules — the DNN core Portals table schema is not present in the
// in-scope install scripts, so legacy max-lengths are unknown; do NOT invent stricter limits
// (behavioral equivalence). The legacy .ascx markup carries only an HTML maxlength on the
// input (client-side truncation), which never raised a server validation error, so porting it
// as a rule would change behavior.
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
            .NotEmpty();

        // MIGRATION: PortalInfo.Email contact email — validate format only when supplied (legacy field optional at create).
        RuleFor(x => x.Email)
            .EmailAddress()
            .When(x => !string.IsNullOrEmpty(x.Email));
    }
}
