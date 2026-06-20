using Xunit;
using FluentValidation.TestHelper;
using DnnMigration.Application.Validators;
using DnnMigration.Application.DTOs.Module;

namespace DnnMigration.UnitTests.Validators;

// MIGRATION: Verifies CreateModuleValidator parity with legacy DNN module-create validation.
// ModuleTitle is required (Website/admin/Modules/ModuleSettings.ascx.vb supplies a required title field);
// Library/Components/Modules/ModuleInfo.vb declares ModuleTitle/CacheTime with only XML-serialization
// attributes and NO DataAnnotation validation, so the FluentValidation rules under test are the parity
// layer. CacheTime is a non-negative cache duration (seconds). The Visibility (VisibilityState) property
// is not validated, so it is left at its default enum value; the Domain enums namespace is intentionally
// not imported here (it is never referenced by name).
public class CreateModuleValidatorTests
{
    private readonly CreateModuleValidator _validator = new();

    // MIGRATION: Minimal baseline that satisfies both rules (ModuleTitle non-empty, CacheTime >= 0).
    // All other properties keep their defaults; Visibility stays at its default enum value (not validated).
    private static CreateModuleDto ValidDto() => new()
    {
        ModuleTitle = "My Module",
        CacheTime = 0
    };

    [Fact]
    public void Valid_Dto_Passes()
    {
        var result = _validator.TestValidate(ValidDto());
        result.ShouldNotHaveAnyValidationErrors();
    }

    // MIGRATION: ModuleSettings.ascx.vb requires a module title.
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
        model.ModuleTitle = "Announcements";
        var result = _validator.TestValidate(model);
        result.ShouldNotHaveValidationErrorFor(x => x.ModuleTitle);
    }

    // MIGRATION: ModuleInfo.CacheTime is a non-negative cache duration (seconds).
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
