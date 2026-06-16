using FluentValidation;
using DnnMigration.Application.DTOs.Tab;

namespace DnnMigration.Application.Validators;

/// <summary>
/// FluentValidation validator for <see cref="UpdateTabDto"/>. Enforces the field-level rules
/// required to update an existing tab (in DNN a "Tab" is a site page), reproducing the legacy
/// DNN tab/page-update validation so the migrated API preserves behavioral parity with the
/// original Web Forms administration screen (Website/admin/Tabs/ManageTabs.ascx.vb).
/// </summary>
/// <remarks>
/// Stateless, structural validation only. Stateful checks (reserved device-name, duplicate
/// <c>TabPath</c>, and circular parent-reference) are intentionally NOT performed here; they are
/// enforced in the service layer (TabService) where repository access is available.
/// </remarks>
// MIGRATION: Tab(page)-update validation parity (TabController.vb update path). Rules from
// Website/admin/Tabs/ManageTabs.ascx.vb (TabName required). TabInfo.vb has no validation attributes.
// MIGRATION: Reserved device-name, duplicate TabPath, and circular parent-reference checks
// are stateful and handled in TabService, not here.
public class UpdateTabValidator : AbstractValidator<UpdateTabDto>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateTabValidator"/> class and registers
    /// the field-level rules for updating a tab (page).
    /// </summary>
    public UpdateTabValidator()
    {
        // MIGRATION: update targets an existing tab(page) — key must be valid (> 0).
        RuleFor(x => x.TabID)
            .GreaterThan(0).WithMessage("A valid tab identifier is required.");

        // MIGRATION: ManageTabs.ascx.vb page-name RequiredFieldValidator (message from ManageTabs.ascx).
        RuleFor(x => x.TabName)
            .NotEmpty().WithMessage("Tab Name Is Required");

        // MIGRATION: TabInfo.RefreshInterval non-negative when supplied.
        RuleFor(x => x.RefreshInterval)
            .GreaterThanOrEqualTo(0).WithMessage("Refresh Interval must be greater than or equal to zero.")
            .When(x => x.RefreshInterval.HasValue);
    }
}
