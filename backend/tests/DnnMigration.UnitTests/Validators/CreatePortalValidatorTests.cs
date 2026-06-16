using Xunit;
using FluentValidation.TestHelper;
using DnnMigration.Application.Validators;
using DnnMigration.Application.DTOs.Portal;

namespace DnnMigration.UnitTests.Validators;

// MIGRATION: Verifies CreatePortalValidator parity with legacy portal-create rules. PortalName required
// (Website/admin/Portal/SiteSettings.ascx.vb / Signup.ascx.vb RequiredFieldValidator); PortalInfo.vb has no
// DataAnnotation validation attributes (XML serialization only). Email format validated only when supplied
// (legacy field optional at create).
public class CreatePortalValidatorTests
{
    private readonly CreatePortalValidator _validator = new();

    // Fully-valid baseline DTO so each test mutates exactly ONE field. All other CreatePortalDto
    // properties remain at their defaults — none are exercised by CreatePortalValidator.
    private static CreatePortalDto ValidDto() => new()
    {
        PortalName = "My Portal",
        Email = "admin@example.com"
    };

    [Fact]
    public void Valid_Dto_Passes()
    {
        var result = _validator.TestValidate(ValidDto());
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void PortalName_Missing_Fails(string? portalName)
    {
        var model = ValidDto();
        model.PortalName = portalName;
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.PortalName);
    }

    [Fact]
    public void PortalName_Provided_NoError()
    {
        var model = ValidDto();
        model.PortalName = "Contoso";
        var result = _validator.TestValidate(model);
        result.ShouldNotHaveValidationErrorFor(x => x.PortalName);
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("plaintext.com")]
    public void Email_InvalidFormat_Fails(string email)
    {
        // Both values contain no '@', so FluentValidation 11.x EmailAddress() (AspNetCoreCompatible mode)
        // reliably fails them. Do NOT use "a@b"-style values — those would pass.
        var model = ValidDto();
        model.Email = email;
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    // MIGRATION: legacy Email optional at create — the .When(!IsNullOrEmpty) guard skips the format rule.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Email_NullOrEmpty_NoError(string? email)
    {
        var model = ValidDto();
        model.Email = email;
        var result = _validator.TestValidate(model);
        result.ShouldNotHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Email_Valid_NoError()
    {
        var model = ValidDto();
        model.Email = "webmaster@contoso.com";
        var result = _validator.TestValidate(model);
        result.ShouldNotHaveValidationErrorFor(x => x.Email);
    }
}
