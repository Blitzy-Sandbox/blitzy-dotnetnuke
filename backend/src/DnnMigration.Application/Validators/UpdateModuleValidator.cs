using FluentValidation;
using DnnMigration.Application.DTOs.Module;

namespace DnnMigration.Application.Validators;

// MIGRATION: Transcribed from Website/admin/Modules/ModuleSettings.ascx (valBorder CompareValidator) and
// ModuleSettings.ascx.vb for the edit/update path. Behavioral parity (AAP 0.7.1). Synchronous-only validator
// (no repository/data access): duplicate module-definition checks are enforced in ModuleService. UpdateModuleRequest
// carries no PortalId (tenant scope is immutable on update / taken from the route), so no PortalId rule is present.
// The legacy start/end-date validators are DataTypeCheck only (N/A for DateTime?) with no EndDate>StartDate
// cross-check, so none is added here.
public sealed class UpdateModuleValidator : AbstractValidator<UpdateModuleRequest>
{
    public UpdateModuleValidator()
    {
        // MIGRATION: API contract -- TabId is the required page placement (legacy from the admin tab context).
        RuleFor(x => x.TabId)
            .GreaterThanOrEqualTo(0);

        // MIGRATION: ModuleSettings.ascx valBorder CompareValidator (Integer DataTypeCheck) on txtBorder (maxlength=1),
        // error text "Invalid Border (must be a number between 0 and 9)". CompareValidator passes on empty, so the
        // rule applies only when Border is supplied. The pattern ^[0-9]$ enforces a single 0-9 digit (subsuming the
        // legacy maxlength=1) and yields exactly one error message matching the legacy text.
        RuleFor(x => x.Border)
            .Matches(@"^[0-9]$").WithMessage("Invalid Border (must be a number between 0 and 9)")
            .When(x => !string.IsNullOrEmpty(x.Border));
    }
}
