using Xunit;
using FluentValidation.TestHelper;
using DnnMigration.Application.Validators;
using DnnMigration.Application.DTOs.Role;

namespace DnnMigration.UnitTests.Validators;

// MIGRATION: Verifies UpdateRoleValidator parity (RoleController.vb update path + EditRoles.ascx.vb):
//   RoleID must be a valid existing key (>0); RoleName required; billing/trial frequency in N/O/D/W/M/Y (default "N");
//   fees (float) and periods (int) non-negative. RoleInfo.vb has no DataAnnotation validation attributes.
public class UpdateRoleValidatorTests
{
    private readonly UpdateRoleValidator _validator = new();

    private static UpdateRoleDto ValidDto() => new()
    {
        RoleID = 1,
        RoleName = "Editors"
    };

    [Fact]
    public void Valid_Dto_Passes()
    {
        var result = _validator.TestValidate(ValidDto());
        result.ShouldNotHaveAnyValidationErrors();
    }

    // MIGRATION: update targets an existing role — key must be valid (> 0).
    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void RoleID_NotPositive_Fails(int roleId)
    {
        var model = ValidDto();
        model.RoleID = roleId;
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.RoleID);
    }

    [Fact]
    public void RoleID_Positive_NoError()
    {
        var model = ValidDto();
        model.RoleID = 7;
        var result = _validator.TestValidate(model);
        result.ShouldNotHaveValidationErrorFor(x => x.RoleID);
    }

    // MIGRATION: EditRoles.ascx.vb requires a role name.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void RoleName_Missing_Fails(string? roleName)
    {
        var model = ValidDto();
        model.RoleName = roleName;
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.RoleName);
    }

    [Fact]
    public void RoleName_Provided_NoError()
    {
        var model = ValidDto();
        model.RoleName = "Administrators";
        var result = _validator.TestValidate(model);
        result.ShouldNotHaveValidationErrorFor(x => x.RoleName);
    }

    [Fact]
    public void ServiceFee_Negative_Fails()
    {
        var model = ValidDto();
        model.ServiceFee = -1f;
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.ServiceFee);
    }

    [Fact]
    public void ServiceFee_NonNegative_NoError()
    {
        var model = ValidDto();
        model.ServiceFee = 10.5f;
        var result = _validator.TestValidate(model);
        result.ShouldNotHaveValidationErrorFor(x => x.ServiceFee);
    }

    [Fact]
    public void TrialFee_Negative_Fails()
    {
        var model = ValidDto();
        model.TrialFee = -0.01f;
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.TrialFee);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-12)]
    public void BillingPeriod_Negative_Fails(int period)
    {
        var model = ValidDto();
        model.BillingPeriod = period;
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.BillingPeriod);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(12)]
    public void BillingPeriod_NonNegative_NoError(int period)
    {
        var model = ValidDto();
        model.BillingPeriod = period;
        var result = _validator.TestValidate(model);
        result.ShouldNotHaveValidationErrorFor(x => x.BillingPeriod);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-7)]
    public void TrialPeriod_Negative_Fails(int period)
    {
        var model = ValidDto();
        model.TrialPeriod = period;
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.TrialPeriod);
    }

    [Theory]
    [InlineData("X")]
    [InlineData("Q")]
    [InlineData("Z")]
    public void BillingFrequency_Invalid_Fails(string frequency)
    {
        var model = ValidDto();
        model.BillingFrequency = frequency;
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.BillingFrequency);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void BillingFrequency_NullOrEmpty_NoError(string? frequency)
    {
        var model = ValidDto();
        model.BillingFrequency = frequency;
        var result = _validator.TestValidate(model);
        result.ShouldNotHaveValidationErrorFor(x => x.BillingFrequency);
    }

    [Theory]
    [InlineData("N")]
    [InlineData("O")]
    [InlineData("D")]
    [InlineData("W")]
    [InlineData("M")]
    [InlineData("Y")]
    public void BillingFrequency_ValidCode_NoError(string frequency)
    {
        var model = ValidDto();
        model.BillingFrequency = frequency;
        var result = _validator.TestValidate(model);
        result.ShouldNotHaveValidationErrorFor(x => x.BillingFrequency);
    }

    [Theory]
    [InlineData("X")]
    [InlineData("Q")]
    public void TrialFrequency_Invalid_Fails(string frequency)
    {
        var model = ValidDto();
        model.TrialFrequency = frequency;
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.TrialFrequency);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void TrialFrequency_NullOrEmpty_NoError(string? frequency)
    {
        var model = ValidDto();
        model.TrialFrequency = frequency;
        var result = _validator.TestValidate(model);
        result.ShouldNotHaveValidationErrorFor(x => x.TrialFrequency);
    }

    [Theory]
    [InlineData("N")]
    [InlineData("M")]
    [InlineData("Y")]
    public void TrialFrequency_ValidCode_NoError(string frequency)
    {
        var model = ValidDto();
        model.TrialFrequency = frequency;
        var result = _validator.TestValidate(model);
        result.ShouldNotHaveValidationErrorFor(x => x.TrialFrequency);
    }
}
