using DnnMigration.Application.DTOs;
using DnnMigration.Application.Validators;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace DnnMigration.UnitTests;

/// <summary>
/// Unit tests for <see cref="LoginRequestDtoValidator"/> and
/// <see cref="RefreshRequestDtoValidator"/>, the FluentValidation validators that
/// guard <c>POST /api/auth/login</c> and <c>POST /api/auth/refresh</c>.
/// </summary>
/// <remarks>
/// MIGRATION: these tests pin the parity of the migrated auth-flow validation to
/// the legacy DotNetNuke sign-in behaviour, where <c>RequiredFieldValidator</c>s on
/// the login control ensured an empty user-name or password never reached the
/// membership provider (<c>Library/Components/Security/PortalSecurity.vb</c>). They
/// also lock in the two deliberate design decisions for this checkpoint: login does
/// NOT apply password-complexity rules (an existing credential must still be able to
/// authenticate), and the optional <c>PortalId</c> is only constrained when present.
///
/// Assertions use <c>FluentValidation.TestHelper</c> with <c>FluentAssertions</c>
/// for the result-level checks.
/// </remarks>
public class AuthValidatorTests
{
    // -------------------------------------------------------------------------
    // LoginRequestDtoValidator
    // -------------------------------------------------------------------------

    private readonly LoginRequestDtoValidator _loginSut = new();

    /// <summary>A fully valid login baseline that individual tests mutate via <c>with</c>.</summary>
    private static LoginRequestDto ValidLogin() => new()
    {
        Username = "jdoe",
        Password = "Passw0rd",
        PortalId = null,
    };

    [Fact]
    public void Login_valid_model_has_no_validation_errors()
    {
        var result = _loginSut.TestValidate(ValidLogin());

        result.ShouldNotHaveAnyValidationErrors();
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Login_username_empty_is_error()
    {
        var dto = ValidLogin() with { Username = string.Empty };

        _loginSut.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.Username);
    }

    [Fact]
    public void Login_password_empty_is_error()
    {
        var dto = ValidLogin() with { Password = string.Empty };

        _loginSut.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Theory]
    [InlineData(256, false)] // exactly the MaximumLength(256) cap -> allowed
    [InlineData(257, true)]  // one over the cap -> rejected
    public void Login_username_length_boundary_is_enforced(int length, bool expectError)
    {
        var dto = ValidLogin() with { Username = new string('a', length) };

        var result = _loginSut.TestValidate(dto);

        if (expectError)
        {
            result.ShouldHaveValidationErrorFor(x => x.Username);
        }
        else
        {
            result.ShouldNotHaveValidationErrorFor(x => x.Username);
        }
    }

    [Theory]
    [InlineData(256, false)] // exactly the MaximumLength(256) cap -> allowed
    [InlineData(257, true)]  // one over the cap -> rejected
    public void Login_password_length_boundary_is_enforced(int length, bool expectError)
    {
        var dto = ValidLogin() with { Password = new string('a', length) };

        var result = _loginSut.TestValidate(dto);

        if (expectError)
        {
            result.ShouldHaveValidationErrorFor(x => x.Password);
        }
        else
        {
            result.ShouldNotHaveValidationErrorFor(x => x.Password);
        }
    }

    [Fact]
    public void Login_short_password_is_allowed()
    {
        // Parity lock: login must NOT apply the MinimumLength(7) creation policy, so a
        // 3-character password is a valid login payload (verification happens later).
        var dto = ValidLogin() with { Password = "abc" };

        _loginSut.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.Password);
    }

    [Theory]
    [InlineData(0)]   // smallest legal portal id (IDENTITY(0,1))
    [InlineData(7)]   // an arbitrary positive portal id
    public void Login_portalId_non_negative_is_allowed(int portalId)
    {
        var dto = ValidLogin() with { PortalId = portalId };

        _loginSut.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.PortalId);
    }

    [Fact]
    public void Login_portalId_null_has_no_error()
    {
        var dto = ValidLogin() with { PortalId = null };

        _loginSut.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.PortalId);
    }

    [Fact]
    public void Login_portalId_negative_is_error()
    {
        var dto = ValidLogin() with { PortalId = -1 };

        _loginSut.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.PortalId);
    }

    // -------------------------------------------------------------------------
    // RefreshRequestDtoValidator
    // -------------------------------------------------------------------------

    private readonly RefreshRequestDtoValidator _refreshSut = new();

    [Fact]
    public void Refresh_valid_token_has_no_validation_errors()
    {
        var dto = new RefreshRequestDto { RefreshToken = "a-valid-opaque-refresh-token" };

        var result = _refreshSut.TestValidate(dto);

        result.ShouldNotHaveAnyValidationErrors();
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Refresh_empty_token_is_error()
    {
        var dto = new RefreshRequestDto { RefreshToken = string.Empty };

        _refreshSut.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.RefreshToken);
    }

    [Theory]
    [InlineData(4096, false)] // exactly the MaximumLength(4096) cap -> allowed
    [InlineData(4097, true)]  // one over the cap -> rejected
    public void Refresh_token_length_boundary_is_enforced(int length, bool expectError)
    {
        var dto = new RefreshRequestDto { RefreshToken = new string('a', length) };

        var result = _refreshSut.TestValidate(dto);

        if (expectError)
        {
            result.ShouldHaveValidationErrorFor(x => x.RefreshToken);
        }
        else
        {
            result.ShouldNotHaveValidationErrorFor(x => x.RefreshToken);
        }
    }
}
