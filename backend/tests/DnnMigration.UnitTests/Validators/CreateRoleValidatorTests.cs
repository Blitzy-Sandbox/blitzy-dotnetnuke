using DnnMigration.Application.DTOs.Role;
using DnnMigration.Application.Validators;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace DnnMigration.UnitTests.Validators;

// MIGRATION: Parity tests for CreateRoleValidator. Asserts rules and verbatim messages migrated
// from legacy DNN role-creation validation (Website/admin/Security/EditRoles.ascx.vb) and
// RoleInfo.vb. PARITY QUIRK: the billing/trial PERIOD rules are guarded by .When(period != 0),
// so a period of 0 PASSES (the >0 rule only applies to a non-zero period). Tests encode parity ONLY.
public sealed class CreateRoleValidatorTests
{
    private readonly CreateRoleValidator _validator = new();

    private const string InvalidRoleName = "You Must Enter a Valid Name";
    private const string ServiceFeeError = "Service Fee Must Be Greater Than or Equal to Zero";
    private const string BillingPeriodError = "Billing Period Must Be Greater Than Zero";
    private const string TrialFeeError = "Trial Fee Must Be Greater Than or Equal to Zero";
    private const string TrialPeriodError = "Trial Period Must Be Greater Than Zero";

    private static CreateRoleRequest Valid() => new()
    {
        PortalId = 0,
        RoleName = "Administrators",
        ServiceFee = 0f,
        BillingPeriod = 0,
        TrialFee = 0f,
        TrialPeriod = 0
    };

    [Fact]
    public void Valid_request_passes_with_no_errors()
    {
        var result = _validator.TestValidate(Valid());
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Valid_request_reports_IsValid_true()
    {
        var result = _validator.Validate(Valid());
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    // ---- RoleName: NotEmpty (custom message) + MaximumLength(50) ----

    [Fact]
    public void RoleName_empty_fails_with_message()
    {
        var dto = Valid();
        dto.RoleName = "";
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.RoleName)
            .WithErrorMessage(InvalidRoleName);
    }

    [Fact]
    public void RoleName_whitespace_fails_with_message()
    {
        var dto = Valid();
        dto.RoleName = "   ";
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.RoleName)
            .WithErrorMessage(InvalidRoleName);
    }

    [Fact]
    public void RoleName_exceeding_50_fails()
    {
        var dto = Valid();
        dto.RoleName = new string('a', 51);
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.RoleName);
    }

    [Fact]
    public void RoleName_at_50_passes()
    {
        var dto = Valid();
        dto.RoleName = new string('a', 50);
        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveValidationErrorFor(x => x.RoleName);
    }

    // ---- ServiceFee: GreaterThanOrEqualTo(0) (custom message) ----

    [Fact]
    public void ServiceFee_negative_fails_with_message()
    {
        var dto = Valid();
        dto.ServiceFee = -1f;
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.ServiceFee)
            .WithErrorMessage(ServiceFeeError);
    }

    [Fact]
    public void ServiceFee_zero_passes()
    {
        var dto = Valid();
        dto.ServiceFee = 0f;
        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveValidationErrorFor(x => x.ServiceFee);
    }

    [Fact]
    public void ServiceFee_positive_passes()
    {
        var dto = Valid();
        dto.ServiceFee = 19.95f;
        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveValidationErrorFor(x => x.ServiceFee);
    }

    // ---- BillingPeriod: GreaterThan(0) (custom) .When(BillingPeriod != 0) ----

    // MIGRATION: BillingPeriod rule is guarded by .When(BillingPeriod != 0) - a value of 0 PASSES.
    [Fact]
    public void BillingPeriod_zero_passes_due_to_when_guard()
    {
        var dto = Valid();
        dto.BillingPeriod = 0;
        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveValidationErrorFor(x => x.BillingPeriod);
    }

    [Fact]
    public void BillingPeriod_negative_fails_with_message()
    {
        var dto = Valid();
        dto.BillingPeriod = -1;
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.BillingPeriod)
            .WithErrorMessage(BillingPeriodError);
    }

    [Fact]
    public void BillingPeriod_positive_passes()
    {
        var dto = Valid();
        dto.BillingPeriod = 1;
        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveValidationErrorFor(x => x.BillingPeriod);
    }

    // ---- TrialFee: GreaterThanOrEqualTo(0) (custom message) ----

    [Fact]
    public void TrialFee_negative_fails_with_message()
    {
        var dto = Valid();
        dto.TrialFee = -1f;
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.TrialFee)
            .WithErrorMessage(TrialFeeError);
    }

    [Fact]
    public void TrialFee_zero_passes()
    {
        var dto = Valid();
        dto.TrialFee = 0f;
        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveValidationErrorFor(x => x.TrialFee);
    }

    // ---- TrialPeriod: GreaterThan(0) (custom) .When(TrialPeriod != 0) ----

    // MIGRATION: TrialPeriod rule is guarded by .When(TrialPeriod != 0) - a value of 0 PASSES.
    [Fact]
    public void TrialPeriod_zero_passes_due_to_when_guard()
    {
        var dto = Valid();
        dto.TrialPeriod = 0;
        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveValidationErrorFor(x => x.TrialPeriod);
    }

    [Fact]
    public void TrialPeriod_negative_fails_with_message()
    {
        var dto = Valid();
        dto.TrialPeriod = -1;
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.TrialPeriod)
            .WithErrorMessage(TrialPeriodError);
    }

    [Fact]
    public void TrialPeriod_positive_passes()
    {
        var dto = Valid();
        dto.TrialPeriod = 1;
        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveValidationErrorFor(x => x.TrialPeriod);
    }
}
