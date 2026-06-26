using DnnMigration.Application.DTOs.Auth;
using DnnMigration.Application.Validators;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace DnnMigration.UnitTests.Validators;

// MIGRATION: Contract tests for RefreshRequestValidator (CP1 review AuthService #5). The refresh-token concept is NEW
// to the JWT model (DNN had no refresh token, only a persistent Forms-auth cookie), so these assert the required-token
// contract rather than a legacy-verbatim message. Synchronous-only validator — no repository/data access. RefreshRequest
// is a record with an init-only RefreshToken, so each negative case constructs a fresh instance.
public sealed class RefreshRequestValidatorTests
{
    private readonly RefreshRequestValidator _validator = new();

    private const string RefreshTokenRequired = "Refresh token is required.";

    private static RefreshRequest Valid() => new()
    {
        RefreshToken = "a-non-empty-refresh-token"
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

    // ---- RefreshToken: NotEmpty (custom message) ----

    [Fact]
    public void RefreshToken_empty_fails_with_message()
    {
        var dto = new RefreshRequest { RefreshToken = "" };
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.RefreshToken)
            .WithErrorMessage(RefreshTokenRequired);
    }

    [Fact]
    public void RefreshToken_whitespace_fails_with_message()
    {
        var dto = new RefreshRequest { RefreshToken = "   " };
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.RefreshToken)
            .WithErrorMessage(RefreshTokenRequired);
    }

    [Fact]
    public void RefreshToken_default_request_fails()
    {
        // A default-constructed RefreshRequest has RefreshToken == string.Empty, which must be rejected.
        var result = _validator.TestValidate(new RefreshRequest());
        result.ShouldHaveValidationErrorFor(x => x.RefreshToken)
            .WithErrorMessage(RefreshTokenRequired);
    }

    [Fact]
    public void RefreshToken_populated_passes()
    {
        var dto = new RefreshRequest { RefreshToken = "valid-token-123" };
        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveValidationErrorFor(x => x.RefreshToken);
    }
}
