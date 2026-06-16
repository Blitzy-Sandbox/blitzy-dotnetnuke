using FluentValidation;
using DnnMigration.Application.DTOs.Module;

namespace DnnMigration.Application.Validators;

// MIGRATION: Module-create validation parity. Rules from Library/Components/Modules/ModuleInfo.vb
// (no validation attributes — only XML serialization; has VisibilityState enum and IsDeleted soft-delete flag)
// and Website/admin/Modules/ModuleSettings.ascx.vb (ModuleTitle field).
// MIGRATION: No MaxLength — DNN core Modules schema absent from in-scope install scripts; none invented.
public class CreateModuleValidator : AbstractValidator<CreateModuleDto>
{
    public CreateModuleValidator()
    {
        // MIGRATION: ModuleSettings.ascx.vb requires a module title.
        RuleFor(x => x.ModuleTitle)
            .NotEmpty();

        // MIGRATION: ModuleInfo.CacheTime is a non-negative cache duration (seconds).
        RuleFor(x => x.CacheTime)
            .GreaterThanOrEqualTo(0);
    }
}
