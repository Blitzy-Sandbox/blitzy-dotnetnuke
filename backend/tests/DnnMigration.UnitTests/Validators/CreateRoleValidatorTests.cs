using Xunit;
using FluentValidation.TestHelper;
using DnnMigration.Application.Validators;
using DnnMigration.Application.DTOs.Role;

namespace DnnMigration.UnitTests.Validators;

// MIGRATION: Verifies CreateRoleValidator parity with Website/admin/Security/EditRoles.ascx.vb:
//   RoleName required; billing/trial frequency restricted to N/O/D/W/M/Y (default "N");
//   fees (ServiceFee/TrialFee, float) and periods (BillingPeriod/TrialPeriod, int) are non-negative.
// RoleInfo.vb (Library/Components/Security/Roles/RoleInfo.vb) carries no DataAnnotation validation
// attributes — the legacy validation lived in the EditRoles.ascx.vb Web Forms control, so these tests
// pin the ported FluentValidation rules to that legacy behavior.
public class CreateRoleValidatorTests
{
    private readonly CreateRoleValidator _validator = new();

    /// <summary>
    /// Fully valid baseline. Only RoleName must be set: all fees/periods default to 0 (which satisfy
    /// the >= 0 rules) and both frequency strings stay null (the When-guard skips the frequency rules).
    /// </summary>
    private static CreateRoleDto ValidDto() => new()
    {
        RoleName = "Editors"
    };

    [Fact]
    public void Valid_Dto_Passes()
    {
        var result = _validator.TestValidate(ValidDto());
        result.ShouldNotHaveAnyValidationErrors();
    }

    // MIGRATION: EditRoles.ascx.vb requires a role name (RequiredFieldValidator on the RoleName field).
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

    // MIGRATION: RoleInfo.ServiceFee is a non-negative monetary amount (VB Single -> float).
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

    // MIGRATION: RoleInfo.TrialFee is a non-negative monetary amount (VB Single -> float).
    [Fact]
    public void TrialFee_Negative_Fails()
    {
        var model = ValidDto();
        model.TrialFee = -0.01f;
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.TrialFee);
    }

    [Fact]
    public void TrialFee_NonNegative_NoError()
    {
        var model = ValidDto();
        model.TrialFee = 0f;
        var result = _validator.TestValidate(model);
        result.ShouldNotHaveValidationErrorFor(x => x.TrialFee);
    }

    // MIGRATION: RoleInfo.BillingPeriod is a non-negative count (Integer -> int).
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

    // MIGRATION: RoleInfo.TrialPeriod is a non-negative count (Integer -> int).
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
    [InlineData(0)]
    [InlineData(7)]
    public void TrialPeriod_NonNegative_NoError(int period)
    {
        var model = ValidDto();
        model.TrialPeriod = period;
        var result = _validator.TestValidate(model);
        result.ShouldNotHaveValidationErrorFor(x => x.TrialPeriod);
    }

    // MIGRATION: EditRoles.ascx.vb billing frequency restricted to N/O/D/W/M/Y; any other non-empty code is invalid.
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

    // MIGRATION: null/empty frequency is allowed (the When-guard skips the Must rule when the value is empty).
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

    // MIGRATION: trial frequency restricted to N/O/D/W/M/Y; any other non-empty code is invalid.
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

    // MIGRATION: null/empty trial frequency is allowed (the When-guard skips the Must rule when empty).
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
