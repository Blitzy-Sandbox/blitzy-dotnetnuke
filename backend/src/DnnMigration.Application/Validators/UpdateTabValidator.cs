using FluentValidation;
using DnnMigration.Application.DTOs.Tab;

namespace DnnMigration.Application.Validators;

// MIGRATION: Tab(page)-update validation parity (TabController.vb update path). Rules from
// Website/admin/Tabs/ManageTabs.ascx.vb (TabName required). TabInfo.vb has no validation attributes.
// MIGRATION: Reserved device-name, duplicate TabPath, and circular parent-reference checks
// are stateful and handled in TabService, not here.
public class UpdateTabValidator : AbstractValidator<UpdateTabDto>
{
    public UpdateTabValidator()
    {
        // MIGRATION: update targets an existing tab(page) — key must be valid (> 0).
        RuleFor(x => x.TabID)
            .GreaterThan(0);

        // MIGRATION: ManageTabs.ascx.vb page-name RequiredFieldValidator.
        RuleFor(x => x.TabName)
            .NotEmpty();

        // MIGRATION: TabInfo.RefreshInterval non-negative when supplied.
        RuleFor(x => x.RefreshInterval)
            .GreaterThanOrEqualTo(0)
            .When(x => x.RefreshInterval.HasValue);
    }
}
