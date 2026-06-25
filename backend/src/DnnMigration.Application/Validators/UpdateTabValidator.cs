using FluentValidation;
using DnnMigration.Application.DTOs.Tab;

namespace DnnMigration.Application.Validators;

// MIGRATION: Transcribed from Website/admin/Tabs/managetabs.ascx (valTabName RequiredFieldValidator + textbox
// maxlength limits) and ManageTabs.ascx.vb for the edit/update path. Behavioral parity (AAP 0.7.1). TabInfo.vb
// carries only XmlElement attributes, so the rules originate from the markup. Synchronous-only validator (no
// repository/data access): duplicate tab-name detection (TabController.GetTabByName) is enforced in TabService via
// ITabRepository.GetByNameAsync. UpdateTabRequest carries no PortalId/TabId (identity/tenant scope taken from the
// route). The legacy start/end-date validators are DataTypeCheck only (N/A for DateTime?) with no EndDate>StartDate
// cross-check, so none is added here.
public sealed class UpdateTabValidator : AbstractValidator<UpdateTabRequest>
{
    public UpdateTabValidator()
    {
        // MIGRATION: managetabs.ascx valTabName RequiredFieldValidator (txtTabName, maxlength=50),
        // error text "Tab Name Is Required".
        RuleFor(x => x.TabName)
            .NotEmpty().WithMessage("Tab Name Is Required")
            .MaximumLength(50);

        // MIGRATION: managetabs.ascx txtTitle maxlength=200.
        RuleFor(x => x.Title)
            .MaximumLength(200);

        // MIGRATION: managetabs.ascx txtDescription maxlength=500.
        RuleFor(x => x.Description)
            .MaximumLength(500);

        // MIGRATION: managetabs.ascx txtKeyWords maxlength=500.
        RuleFor(x => x.KeyWords)
            .MaximumLength(500);

        // MIGRATION: managetabs.ascx txtHeadText (PageHeadText) maxlength=500.
        RuleFor(x => x.PageHeadText)
            .MaximumLength(500);
    }
}
