using DnnMigration.Application.DTOs.Role;
using DnnMigration.Application.Validators;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace DnnMigration.UnitTests.Validators;

// MIGRATION: (QA F10 Issue #18, tenant-contract alignment) Parity tests for AssignUserRoleValidator.
// The validator's PortalId rule was tightened to GreaterThanOrEqualTo(0) so PortalId 0 (the valid first/default
// DNN portal, and the JWT "portalId" claim / Angular admin default) is ACCEPTED -- matching every other
// validator (CreateModule/CreateUser/ForgotPassword/LoginRequest). These tests lock that contract in: portal 0
// is valid, a negative portal is rejected with the verbatim message, and UserId/RoleId still require a positive
// (1-based) selection.
public sealed class AssignUserRoleValidatorTests
{
    private readonly AssignUserRoleValidator _validator = new();

    private const string InvalidPortalMessage = "A valid Portal must be specified.";
    private const string InvalidUserMessage = "A valid User must be specified.";
    private const string InvalidRoleMessage = "A valid Role must be specified.";

    private static AssignUserRoleRequest Valid() => new()
    {
        PortalId = 0,
        UserId = 1,
        RoleId = 1
    };

    [Fact]
    public void Valid_request_with_portal_zero_passes_with_no_errors()
    {
        // QA F10 Issue #18 reproduction: { portalId: 0, userId: 1, roleId: 1 } must NOT be rejected.
        var result = _validator.TestValidate(Valid());
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void PortalId_zero_is_valid()
    {
        var dto = Valid();
        dto.PortalId = 0;
        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveValidationErrorFor(x => x.PortalId);
    }

    [Fact]
    public void PortalId_positive_is_valid()
    {
        var dto = Valid();
        dto.PortalId = 7;
        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveValidationErrorFor(x => x.PortalId);
    }

    [Fact]
    public void PortalId_negative_fails_with_message()
    {
        var dto = Valid();
        dto.PortalId = -1;
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.PortalId)
            .WithErrorMessage(InvalidPortalMessage);
    }

    [Fact]
    public void UserId_zero_fails_with_message()
    {
        var dto = Valid();
        dto.UserId = 0;
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.UserId)
            .WithErrorMessage(InvalidUserMessage);
    }

    [Fact]
    public void RoleId_zero_fails_with_message()
    {
        var dto = Valid();
        dto.RoleId = 0;
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.RoleId)
            .WithErrorMessage(InvalidRoleMessage);
    }
}
