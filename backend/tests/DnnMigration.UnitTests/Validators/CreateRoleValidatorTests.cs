using Xunit;
using FluentValidation.TestHelper;
using DnnMigration.Application.Validators;
using DnnMigration.Application.DTOs.Role;

namespace DnnMigration.UnitTests.Validators;

// MIGRATION: Verifies CreateRoleValidator parity with Website/admin/Security/EditRoles.ascx.vb:
//   RoleName required; billing/trial frequency restricted to N/O/D/W/M/Y (default "N");
//   fees (ServiceFee/TrialFee, float) and periods (BillingPeriod/TrialPeriod, int) are non-negative.
// RoleInfo.vb (DotNetNuke.Security.Roles) carries no DataAnnotation validation attributes, so all
// validation is asserted against the FluentValidation rules ported into CreateRoleValidator.
public class CreateRoleValidatorTests
{
    private readonly CreateRoleValidator _validator = new();

    // MIGRATION: A fully valid baseline only needs RoleName. Fees/periods default to 0 (pass >= 0) and
    // BillingFrequency/TrialFrequency default to null (the When-guard skips the frequency rule).
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

    // MIGRATION: RoleInfo.ServiceFee (VB Single -> float) is a non-negative monetary amount.
    // Fee tests use [Fact] with explicit f-suffixed literals to avoid float-in-[InlineData] ambiguity.
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

    // MIGRATION: RoleInfo.TrialFee (VB Single -> float) is a non-negative monetary amount.
    [Fact]
    public void TrialFee_Negative_Fails()
    {
        var model = ValidDto();
        model.TrialFee = -0.01f;
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.TrialFee);
    }

    // MIGRATION: RoleInfo.BillingPeriod (VB Integer -> int) is a non-negative count.
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

    // MIGRATION: EditRoles.ascx valBillingPeriod2 — Operator="GreaterThan" ValueToCompare="0" (> 0),
    // so a positive billing period is valid; 0 and negatives are rejected.
    [Theory]
    [InlineData(1)]
    [InlineData(12)]
    public void BillingPeriod_Positive_NoError(int period)
    {
        var model = ValidDto();
        model.BillingPeriod = period;
        var result = _validator.TestValidate(model);
        result.ShouldNotHaveValidationErrorFor(x => x.BillingPeriod);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void BillingPeriod_NonPositive_Fails(int period)
    {
        var model = ValidDto();
        model.BillingPeriod = period;
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.BillingPeriod);
    }

    // MIGRATION: RoleInfo.TrialPeriod (VB Integer -> int) is a non-negative count.
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

    // MIGRATION: EditRoles.ascx.vb billing frequency is restricted to N/O/D/W/M/Y; any other
    // non-empty code is invalid.
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

    // MIGRATION: an empty billing frequency is allowed (the When-guard skips the rule when null/empty).
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

    // MIGRATION: EditRoles.ascx.vb "Frequency" list codes (N=None, O=one-time, D=day, W=week, M=month, Y=year).
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

    // MIGRATION: trial frequency is restricted to the same N/O/D/W/M/Y list as billing frequency.
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

    // MIGRATION: an empty trial frequency is allowed (the When-guard skips the rule when null/empty).
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
