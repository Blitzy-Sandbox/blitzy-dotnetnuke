using FluentValidation;
using DnnMigration.Application.DTOs.Module;

namespace DnnMigration.Application.Validators;

// MIGRATION: Module-update validation parity (ModuleController.vb update path). Rules from
// ModuleInfo.vb (no validation attributes) + Website/admin/Modules/ModuleSettings.ascx.vb.
// MIGRATION: No MaxLength — DNN core Modules schema absent from in-scope install scripts; none invented.
public class UpdateModuleValidator : AbstractValidator<UpdateModuleDto>
{
    public UpdateModuleValidator()
    {
        // MIGRATION: update targets an existing module — key must be valid (> 0).
        RuleFor(x => x.ModuleID)
            .GreaterThan(0);

        // MIGRATION: ModuleSettings.ascx.vb requires a module title.
        RuleFor(x => x.ModuleTitle)
            .NotEmpty();

        // MIGRATION: non-negative cache duration (seconds).
        RuleFor(x => x.CacheTime)
            .GreaterThanOrEqualTo(0);
    }
}
