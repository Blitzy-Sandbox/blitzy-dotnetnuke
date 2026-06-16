using Xunit;
using FluentValidation.TestHelper;
using DnnMigration.Application.Validators;
using DnnMigration.Application.DTOs.Portal;

namespace DnnMigration.UnitTests.Validators;

// MIGRATION: Verifies UpdatePortalValidator parity (PortalController.vb UpdatePortalInfo). PortalID must be a
// valid existing key (>0); PortalName required (SiteSettings.ascx.vb); Email format checked only when supplied.
public class UpdatePortalValidatorTests
{
    private readonly UpdatePortalValidator _validator = new();

    private static UpdatePortalDto ValidDto() => new()
    {
        PortalID = 1,
        PortalName = "My Portal",
        Email = "admin@example.com"
    };

    [Fact]
    public void Valid_Dto_Passes()
    {
        var result = _validator.TestValidate(ValidDto());
        result.ShouldNotHaveAnyValidationErrors();
    }

    // MIGRATION: update targets an existing portal — key must be valid (> 0).
    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void PortalID_NotPositive_Fails(int portalId)
    {
        var model = ValidDto();
        model.PortalID = portalId;
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.PortalID);
    }

    [Fact]
    public void PortalID_Positive_NoError()
    {
        var model = ValidDto();
        model.PortalID = 42;
        var result = _validator.TestValidate(model);
        result.ShouldNotHaveValidationErrorFor(x => x.PortalID);
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
        var model = ValidDto();
        model.Email = email;
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

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
