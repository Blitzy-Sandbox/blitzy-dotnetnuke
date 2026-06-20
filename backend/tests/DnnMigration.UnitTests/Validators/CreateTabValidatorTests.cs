using Xunit;
using FluentValidation.TestHelper;
using DnnMigration.Application.Validators;
using DnnMigration.Application.DTOs.Tab;

namespace DnnMigration.UnitTests.Validators;

// MIGRATION: Verifies CreateTabValidator parity with the legacy Website/admin/Tabs/**/*.ascx.vb controls:
//   TabName is required; RefreshInterval is optional (int?) and must be non-negative only when supplied
//   (the validator's When(x => x.RefreshInterval.HasValue) guard skips the rule for null).
// The legacy source-of-truth Library/Components/Tabs/TabInfo.vb carries no DataAnnotation validation
// attributes, so all validation semantics live in the FluentValidation rules exercised here. Tests use
// FluentValidation.TestHelper and default messages only (no custom WithMessage assertions), keeping the
// suite resilient to message wording while still pinning rule presence/absence per property.
public class CreateTabValidatorTests
{
    private readonly CreateTabValidator _validator = new();

    // ValidDto deliberately leaves RefreshInterval null: the When(HasValue) guard skips the
    // non-negative rule, so only TabName needs a value for a fully valid baseline.
    private static CreateTabDto ValidDto() => new()
    {
        TabName = "Home"
    };

    [Fact]
    public void Valid_Dto_Passes()
    {
        var result = _validator.TestValidate(ValidDto());
        result.ShouldNotHaveAnyValidationErrors();
    }

    // MIGRATION: tab/page admin requires a tab name (ManageTabs.ascx.vb page-name RequiredFieldValidator).
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void TabName_Missing_Fails(string? tabName)
    {
        var model = ValidDto();
        model.TabName = tabName;
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.TabName);
    }

    [Fact]
    public void TabName_Provided_NoError()
    {
        var model = ValidDto();
        model.TabName = "About Us";
        var result = _validator.TestValidate(model);
        result.ShouldNotHaveValidationErrorFor(x => x.TabName);
    }

    // MIGRATION: RefreshInterval is intentionally NOT range-validated — legacy ManageTabs.ascx.vb (L298-299)
    // stored it with no lower-bound check, so a negative interval is accepted (preserved as BUG-001).
    [Theory]
    [InlineData(-1)]
    [InlineData(-30)]
    public void RefreshInterval_Negative_NoError(int interval)
    {
        var model = ValidDto();
        model.RefreshInterval = interval;
        var result = _validator.TestValidate(model);
        result.ShouldNotHaveValidationErrorFor(x => x.RefreshInterval);
    }

    // MIGRATION: a null RefreshInterval is allowed (the When-guard skips the non-negative rule).
    [Fact]
    public void RefreshInterval_Null_NoError()
    {
        var model = ValidDto();
        model.RefreshInterval = null;
        var result = _validator.TestValidate(model);
        result.ShouldNotHaveValidationErrorFor(x => x.RefreshInterval);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(60)]
    public void RefreshInterval_NonNegative_NoError(int interval)
    {
        var model = ValidDto();
        model.RefreshInterval = interval;
        var result = _validator.TestValidate(model);
        result.ShouldNotHaveValidationErrorFor(x => x.RefreshInterval);
    }
}
