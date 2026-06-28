using DnnMigration.Application.DTOs.Role;
using DnnMigration.Application.Validators;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace DnnMigration.UnitTests.Validators;

// MIGRATION: (QA F10 Issue #18, tenant-contract alignment) Parity tests for UpdateUserRoleValidator. The PortalId
// rule was aligned to GreaterThanOrEqualTo(0) (mirroring AssignUserRoleValidator) so the assignment UPDATE/CANCEL
// write accepts portal 0 -- the canonical portal context the rest of the stack uses. These tests lock that in.
public sealed class UpdateUserRoleValidatorTests
{
    private readonly UpdateUserRoleValidator _validator = new();

    private const string InvalidPortalMessage = "A valid Portal must be specified.";
    private const string InvalidUserMessage = "A valid User must be specified.";
    private const string InvalidRoleMessage = "A valid Role must be specified.";

    private static UpdateUserRoleRequest Valid() => new()
    {
        PortalId = 0,
        UserId = 1,
        RoleId = 1,
        Cancel = false
    };

    [Fact]
    public void Valid_request_with_portal_zero_passes_with_no_errors()
    {
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
