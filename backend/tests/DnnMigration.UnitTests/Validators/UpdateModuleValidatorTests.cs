using Xunit;
using FluentValidation.TestHelper;
using DnnMigration.Application.Validators;
using DnnMigration.Application.DTOs.Module;

namespace DnnMigration.UnitTests.Validators;

// MIGRATION: Verifies UpdateModuleValidator parity with the legacy DNN module-update path
// (Library/Components/Modules/ModuleController.vb). The source-of-truth value object
// Library/Components/Modules/ModuleInfo.vb carries NO DataAnnotation validation attributes
// (only XML serialization attributes — ModuleTitle is a backing-field String initialized to
// Null.NullString, CacheTime a backing-field Integer, ModuleID a backing-field Integer), so all
// validation semantics originate in the FluentValidation validator under test. Mirrors the three
// rules declared in DnnMigration.Application.Validators.UpdateModuleValidator:
//   * ModuleID    -> GreaterThan(0)            (an update targets an EXISTING module — key must be > 0).
//   * ModuleTitle -> NotEmpty (required)        (the legacy admin UI Website/admin/Modules/ModuleSettings.ascx.vb requires a title).
//   * CacheTime   -> GreaterThanOrEqualTo(0)    (a non-negative cache duration in seconds).
// Contributes to Validation Gate 2 (dotnet test) and must build clean under Gate 1 (--warnaserror).
public class UpdateModuleValidatorTests
{
    private readonly UpdateModuleValidator _validator = new();

    // MIGRATION: Minimal fully-valid baseline. The validator only constrains ModuleID (> 0),
    // ModuleTitle (required) and CacheTime (non-negative); all other UpdateModuleDto properties keep
    // their defaults — in particular Visibility stays at its default VisibilityState enum value (not
    // validated, so the enum type is never referenced and no DnnMigration.Domain.Enums using is needed).
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

    // MIGRATION: ModuleSettings.ascx.vb requires a module title. Both null and empty string violate
    // RuleFor(x => x.ModuleTitle).NotEmpty().
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

    // MIGRATION: ModuleInfo.CacheTime is a non-negative cache duration (seconds). A negative value
    // violates RuleFor(x => x.CacheTime).GreaterThanOrEqualTo(0).
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
