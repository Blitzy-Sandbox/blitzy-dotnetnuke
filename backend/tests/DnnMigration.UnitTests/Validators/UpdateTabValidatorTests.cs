using Xunit;
using FluentValidation.TestHelper;
using DnnMigration.Application.Validators;
using DnnMigration.Application.DTOs.Tab;

namespace DnnMigration.UnitTests.Validators;

// MIGRATION: Verifies UpdateTabValidator parity (TabController.vb update path + Website/admin/Tabs controls):
//   TabID must be a valid existing key (>0); TabName required; RefreshInterval optional (int?) and non-negative
//   only when supplied (When HasValue). TabInfo.vb has no DataAnnotation validation attributes.
public class UpdateTabValidatorTests
{
    private readonly UpdateTabValidator _validator = new();

    private static UpdateTabDto ValidDto() => new()
    {
        TabID = 1,
        TabName = "Home"
    };

    [Fact]
    public void Valid_Dto_Passes()
    {
        var result = _validator.TestValidate(ValidDto());
        result.ShouldNotHaveAnyValidationErrors();
    }

    // MIGRATION: update targets an existing tab - key must be valid (> 0).
    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void TabID_NotPositive_Fails(int tabId)
    {
        var model = ValidDto();
        model.TabID = tabId;
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.TabID);
    }

    [Fact]
    public void TabID_Positive_NoError()
    {
        var model = ValidDto();
        model.TabID = 13;
        var result = _validator.TestValidate(model);
        result.ShouldNotHaveValidationErrorFor(x => x.TabID);
    }

    // MIGRATION: ManageTabs.ascx.vb page-name RequiredFieldValidator - TabName required.
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
        model.TabName = "Contact";
        var result = _validator.TestValidate(model);
        result.ShouldNotHaveValidationErrorFor(x => x.TabName);
    }

    // MIGRATION: TabInfo.RefreshInterval must be non-negative when supplied.
    [Theory]
    [InlineData(-1)]
    [InlineData(-30)]
    public void RefreshInterval_Negative_Fails(int interval)
    {
        var model = ValidDto();
        model.RefreshInterval = interval;
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.RefreshInterval);
    }

    // MIGRATION: a null RefreshInterval is allowed (When-guard skips the rule).
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
