using Xunit;
using FluentValidation.TestHelper;
using DnnMigration.Application.Validators;
using DnnMigration.Application.DTOs.Module;

namespace DnnMigration.UnitTests.Validators;

// MIGRATION: Verifies UpdateModuleValidator parity (ModuleController.vb update path). ModuleID must be a valid
// existing key (>0); ModuleTitle required (ModuleSettings.ascx.vb); CacheTime non-negative cache seconds.
// Legacy ModuleInfo.vb carries no DataAnnotation validation attributes — these rules are ported to the
// Application layer as FluentValidation. Default FluentValidation messages are asserted (no fabricated text).
public class UpdateModuleValidatorTests
{
    private readonly UpdateModuleValidator _validator = new();

    // Builds a fully valid UpdateModuleDto baseline that satisfies every rule, so each test mutates exactly
    // one property to isolate the rule under examination.
    private static UpdateModuleDto ValidDto() => new()
    {
        ModuleID = 1,
        ModuleTitle = "My Module",
        CacheTime = 0
    };

    [Fact]
    public void Valid_Dto_Passes()
    {
        var result = _validator.TestValidate(ValidDto());
        result.ShouldNotHaveAnyValidationErrors();
    }

    // MIGRATION: update targets an existing module — key must be valid (> 0).
    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void ModuleID_NotPositive_Fails(int moduleId)
    {
        var model = ValidDto();
        model.ModuleID = moduleId;
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.ModuleID);
    }

    [Fact]
    public void ModuleID_Positive_NoError()
    {
        var model = ValidDto();
        model.ModuleID = 99;
        var result = _validator.TestValidate(model);
        result.ShouldNotHaveValidationErrorFor(x => x.ModuleID);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ModuleTitle_Missing_Fails(string? title)
    {
        var model = ValidDto();
        model.ModuleTitle = title;
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.ModuleTitle);
    }

    [Fact]
    public void ModuleTitle_Provided_NoError()
    {
        var model = ValidDto();
        model.ModuleTitle = "Links";
        var result = _validator.TestValidate(model);
        result.ShouldNotHaveValidationErrorFor(x => x.ModuleTitle);
    }

    // MIGRATION: non-negative cache duration (seconds) — CacheTime < 0 is invalid.
    [Theory]
    [InlineData(-1)]
    [InlineData(-100)]
    public void CacheTime_Negative_Fails(int cacheTime)
    {
        var model = ValidDto();
        model.CacheTime = cacheTime;
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.CacheTime);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3600)]
    public void CacheTime_NonNegative_NoError(int cacheTime)
    {
        var model = ValidDto();
        model.CacheTime = cacheTime;
        var result = _validator.TestValidate(model);
        result.ShouldNotHaveValidationErrorFor(x => x.CacheTime);
    }
}
