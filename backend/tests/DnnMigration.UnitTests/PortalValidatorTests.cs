// -----------------------------------------------------------------------------
//  PortalValidatorTests.cs
//
//  Unit tests for the two Portal FluentValidation validators that live in
//  DnnMigration.Application.Validators.PortalValidator.cs:
//
//      * CreatePortalDtoValidator : AbstractValidator<CreatePortalDto>
//      * UpdatePortalDtoValidator : AbstractValidator<UpdatePortalDto>
//
//  PURPOSE / PARITY
//  ----------------
//  MIGRATION: these validators were migrated from the legacy DotNetNuke 4.x Web
//  Forms admin screens. The create-side rules reproduce the RequiredFieldValidators
//  declared on Website/admin/Portal/signup.ascx (Portal Name / First Name /
//  Last Name / Username / Password / Email) plus the required portal alias on
//  Website/admin/Portal/editportalalias.ascx (txtAlias), and the 128-character
//  cap mirrors that screen's maxlength="128" on txtPortalName. The update-side
//  rules mirror the editable, non-negative quota/fee fields on
//  Website/admin/Portal/sitesettings.ascx.
//
//  Behavioural parity is the contract under test: the user-visible error messages
//  MUST match the legacy text VERBATIM (including capitalisation and the trailing
//  period), so every message assertion below is a literal copy of the message the
//  original Web Forms validator produced for the same input.
//
//  These are pure, fast, isolated unit tests: the validators hold no state,
//  perform no I/O, and require no mocks, so each System-Under-Test (SUT) is simply
//  instantiated directly. Assertions use FluentValidation.TestHelper (TestValidate
//  / ShouldHaveValidationErrorFor / WithErrorMessage / ShouldNotHaveValidationErrorFor
//  / ShouldNotHaveAnyValidationErrors); FluentAssertions provides the complementary
//  overall-validity checks (IsValid).
// -----------------------------------------------------------------------------

using DnnMigration.Application.DTOs;
using DnnMigration.Application.Validators;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace DnnMigration.UnitTests;

/// <summary>
/// xUnit test-suite verifying the field-level rules of
/// <see cref="CreatePortalDtoValidator"/> and <see cref="UpdatePortalDtoValidator"/>.
/// </summary>
/// <remarks>
/// The suite is organised in two regions - one per validator. Each region first
/// proves that a fully populated, valid payload produces zero validation errors,
/// then exercises every individual rule in isolation by starting from a valid
/// baseline (<see cref="ValidCreate"/> / <see cref="ValidUpdate"/>) and mutating
/// exactly one property with a C# <c>with</c> expression, so a failing assertion
/// unambiguously identifies the single rule that regressed.
/// </remarks>
public class PortalValidatorTests
{
    // The validators are stateless and dependency-free, so a single shared
    // instance per SUT is safe to reuse across every test in the class.
    private readonly CreatePortalDtoValidator _createSut = new();
    private readonly UpdatePortalDtoValidator _updateSut = new();

    #region Test data builders

    /// <summary>
    /// Builds a fully valid <see cref="CreatePortalDto"/> that satisfies every rule
    /// of <see cref="CreatePortalDtoValidator"/>. Individual tests derive invalid
    /// variants from this baseline via a <c>with</c> expression so that only the
    /// property under test differs from a known-good payload.
    /// </summary>
    private static CreatePortalDto ValidCreate() => new()
    {
        PortalName = "Contoso Intranet",
        FirstName = "Grace",
        LastName = "Hopper",
        Username = "ghopper",
        Password = "P@ssw0rd!2024",
        Email = "grace.hopper@contoso.com",
        Description = "Primary corporate intranet portal.",
        KeyWords = "intranet, corporate, contoso",
        HomeDirectory = "Portals/0",
        PortalAlias = "intranet.contoso.com"
    };

    /// <summary>
    /// Builds a fully valid <see cref="UpdatePortalDto"/> that satisfies every rule
    /// of <see cref="UpdatePortalDtoValidator"/>: a non-empty
    /// <see cref="UpdatePortalDto.PortalName"/> within 128 characters and
    /// non-negative <see cref="UpdatePortalDto.HostFee"/>,
    /// <see cref="UpdatePortalDto.HostSpace"/>, <see cref="UpdatePortalDto.PageQuota"/>
    /// and <see cref="UpdatePortalDto.UserQuota"/>. The remaining properties carry no
    /// validation rules, so their default values are intentionally left unset.
    /// </summary>
    private static UpdatePortalDto ValidUpdate() => new()
    {
        PortalName = "Contoso Intranet",
        HostFee = 0f,
        HostSpace = 500,
        PageQuota = 250,
        UserQuota = 1000
    };

    #endregion

    #region CreatePortalDtoValidator

    /// <summary>A fully populated create payload must produce no validation errors.</summary>
    [Fact]
    public void CreateValidator_WithFullyValidModel_HasNoValidationErrors()
    {
        var result = _createSut.TestValidate(ValidCreate());

        result.ShouldNotHaveAnyValidationErrors();
        result.IsValid.Should().BeTrue();
    }

    /// <summary>
    /// An empty <see cref="CreatePortalDto.PortalName"/> must fail with the exact
    /// legacy message "Portal Name Is Required." (signup.ascx valPortalName).
    /// </summary>
    [Fact]
    public void CreateValidator_WithEmptyPortalName_ReportsRequiredMessage()
    {
        var dto = ValidCreate() with { PortalName = "" };

        var result = _createSut.TestValidate(dto);

        result.ShouldHaveValidationErrorFor(x => x.PortalName)
              .WithErrorMessage("Portal Name Is Required.");
        result.IsValid.Should().BeFalse();
    }

    /// <summary>
    /// An empty <see cref="CreatePortalDto.FirstName"/> must fail with the exact
    /// legacy message "First Name Is Required." (signup.ascx valFirstName).
    /// </summary>
    [Fact]
    public void CreateValidator_WithEmptyFirstName_ReportsRequiredMessage()
    {
        var dto = ValidCreate() with { FirstName = "" };

        var result = _createSut.TestValidate(dto);

        result.ShouldHaveValidationErrorFor(x => x.FirstName)
              .WithErrorMessage("First Name Is Required.");
        result.IsValid.Should().BeFalse();
    }

    /// <summary>
    /// An empty <see cref="CreatePortalDto.LastName"/> must fail with the exact
    /// legacy message "Last Name Is Required." (signup.ascx valLastName).
    /// </summary>
    [Fact]
    public void CreateValidator_WithEmptyLastName_ReportsRequiredMessage()
    {
        var dto = ValidCreate() with { LastName = "" };

        var result = _createSut.TestValidate(dto);

        result.ShouldHaveValidationErrorFor(x => x.LastName)
              .WithErrorMessage("Last Name Is Required.");
        result.IsValid.Should().BeFalse();
    }

    /// <summary>
    /// An empty <see cref="CreatePortalDto.Username"/> must fail with the exact
    /// legacy message "Username Is Required." (signup.ascx valUsername).
    /// </summary>
    [Fact]
    public void CreateValidator_WithEmptyUsername_ReportsRequiredMessage()
    {
        var dto = ValidCreate() with { Username = "" };

        var result = _createSut.TestValidate(dto);

        result.ShouldHaveValidationErrorFor(x => x.Username)
              .WithErrorMessage("Username Is Required.");
        result.IsValid.Should().BeFalse();
    }

    /// <summary>
    /// An empty <see cref="CreatePortalDto.Password"/> must fail with the exact
    /// legacy message "Password Is Required." (signup.ascx valPassword).
    /// </summary>
    [Fact]
    public void CreateValidator_WithEmptyPassword_ReportsRequiredMessage()
    {
        var dto = ValidCreate() with { Password = "" };

        var result = _createSut.TestValidate(dto);

        result.ShouldHaveValidationErrorFor(x => x.Password)
              .WithErrorMessage("Password Is Required.");
        result.IsValid.Should().BeFalse();
    }

    /// <summary>
    /// An empty <see cref="CreatePortalDto.Email"/> must fail the NotEmpty rule with
    /// the exact legacy message "Email Is Required." (signup.ascx valEmail). The
    /// EmailAddress rule also flags an empty value, but the required-field message
    /// must be present among the reported failures.
    /// </summary>
    [Fact]
    public void CreateValidator_WithEmptyEmail_ReportsRequiredMessage()
    {
        var dto = ValidCreate() with { Email = "" };

        var result = _createSut.TestValidate(dto);

        result.ShouldHaveValidationErrorFor(x => x.Email)
              .WithErrorMessage("Email Is Required.");
        result.IsValid.Should().BeFalse();
    }

    /// <summary>
    /// A syntactically invalid but non-empty <see cref="CreatePortalDto.Email"/> must
    /// still fail the EmailAddress rule. The built-in EmailAddress message text is
    /// framework-owned and therefore not asserted - only that an error is raised for
    /// the Email property.
    /// </summary>
    [Fact]
    public void CreateValidator_WithInvalidEmailFormat_ReportsEmailError()
    {
        var dto = ValidCreate() with { Email = "not-an-email" };

        var result = _createSut.TestValidate(dto);

        result.ShouldHaveValidationErrorFor(x => x.Email);
        result.IsValid.Should().BeFalse();
    }

    /// <summary>
    /// An empty <see cref="CreatePortalDto.PortalAlias"/> must fail the NotEmpty rule
    /// (the initial HTTP alias required to provision the portal;
    /// editportalalias.ascx txtAlias). The rule carries no custom message, so only
    /// the presence of an error is asserted.
    /// </summary>
    [Fact]
    public void CreateValidator_WithEmptyPortalAlias_ReportsError()
    {
        var dto = ValidCreate() with { PortalAlias = "" };

        var result = _createSut.TestValidate(dto);

        result.ShouldHaveValidationErrorFor(x => x.PortalAlias);
        result.IsValid.Should().BeFalse();
    }

    /// <summary>
    /// A <see cref="CreatePortalDto.PortalName"/> longer than 128 characters must fail
    /// the MaximumLength(128) rule (signup.ascx txtPortalName maxlength="128"). A
    /// 129-character value passes NotEmpty but breaches the length cap.
    /// </summary>
    [Fact]
    public void CreateValidator_WithPortalNameExceeding128Chars_ReportsError()
    {
        var dto = ValidCreate() with { PortalName = new string('a', 129) };

        var result = _createSut.TestValidate(dto);

        result.ShouldHaveValidationErrorFor(x => x.PortalName);
        result.IsValid.Should().BeFalse();
    }

    #endregion

    #region UpdatePortalDtoValidator

    /// <summary>A fully populated update payload must produce no validation errors.</summary>
    [Fact]
    public void UpdateValidator_WithFullyValidModel_HasNoValidationErrors()
    {
        var result = _updateSut.TestValidate(ValidUpdate());

        result.ShouldNotHaveAnyValidationErrors();
        result.IsValid.Should().BeTrue();
    }

    /// <summary>
    /// An empty <see cref="UpdatePortalDto.PortalName"/> must fail the NotEmpty rule
    /// (sitesettings.ascx txtPortalName; PortalName is a required portal attribute).
    /// </summary>
    [Fact]
    public void UpdateValidator_WithEmptyPortalName_ReportsError()
    {
        var dto = ValidUpdate() with { PortalName = "" };

        var result = _updateSut.TestValidate(dto);

        result.ShouldHaveValidationErrorFor(x => x.PortalName);
        result.IsValid.Should().BeFalse();
    }

    /// <summary>
    /// A <see cref="UpdatePortalDto.PortalName"/> longer than 128 characters must fail
    /// the MaximumLength(128) rule (sitesettings.ascx txtPortalName maxlength="128").
    /// </summary>
    [Fact]
    public void UpdateValidator_WithPortalNameExceeding128Chars_ReportsError()
    {
        var dto = ValidUpdate() with { PortalName = new string('a', 129) };

        var result = _updateSut.TestValidate(dto);

        result.ShouldHaveValidationErrorFor(x => x.PortalName);
        result.IsValid.Should().BeFalse();
    }

    /// <summary>
    /// A negative <see cref="UpdatePortalDto.HostFee"/> must fail the
    /// GreaterThanOrEqualTo(0) rule (bound to the <c>float</c> overload).
    /// </summary>
    [Fact]
    public void UpdateValidator_WithNegativeHostFee_ReportsError()
    {
        var dto = ValidUpdate() with { HostFee = -1f };

        var result = _updateSut.TestValidate(dto);

        result.ShouldHaveValidationErrorFor(x => x.HostFee);
        result.IsValid.Should().BeFalse();
    }

    /// <summary>
    /// A negative <see cref="UpdatePortalDto.HostSpace"/> must fail the
    /// GreaterThanOrEqualTo(0) rule.
    /// </summary>
    [Fact]
    public void UpdateValidator_WithNegativeHostSpace_ReportsError()
    {
        var dto = ValidUpdate() with { HostSpace = -1 };

        var result = _updateSut.TestValidate(dto);

        result.ShouldHaveValidationErrorFor(x => x.HostSpace);
        result.IsValid.Should().BeFalse();
    }

    /// <summary>
    /// A negative <see cref="UpdatePortalDto.PageQuota"/> must fail the
    /// GreaterThanOrEqualTo(0) rule.
    /// </summary>
    [Fact]
    public void UpdateValidator_WithNegativePageQuota_ReportsError()
    {
        var dto = ValidUpdate() with { PageQuota = -1 };

        var result = _updateSut.TestValidate(dto);

        result.ShouldHaveValidationErrorFor(x => x.PageQuota);
        result.IsValid.Should().BeFalse();
    }

    /// <summary>
    /// A negative <see cref="UpdatePortalDto.UserQuota"/> must fail the
    /// GreaterThanOrEqualTo(0) rule.
    /// </summary>
    [Fact]
    public void UpdateValidator_WithNegativeUserQuota_ReportsError()
    {
        var dto = ValidUpdate() with { UserQuota = -1 };

        var result = _updateSut.TestValidate(dto);

        result.ShouldHaveValidationErrorFor(x => x.UserQuota);
        result.IsValid.Should().BeFalse();
    }

    /// <summary>
    /// Zero is the boundary value accepted by every GreaterThanOrEqualTo(0)
    /// quota/fee rule, so a payload whose numeric fields are all zero must raise no
    /// error for any of those members (and the payload as a whole stays valid).
    /// </summary>
    [Fact]
    public void UpdateValidator_WithZeroNumericValues_HasNoNumericErrors()
    {
        var dto = ValidUpdate() with
        {
            HostFee = 0f,
            HostSpace = 0,
            PageQuota = 0,
            UserQuota = 0
        };

        var result = _updateSut.TestValidate(dto);

        result.ShouldNotHaveValidationErrorFor(x => x.HostFee);
        result.ShouldNotHaveValidationErrorFor(x => x.HostSpace);
        result.ShouldNotHaveValidationErrorFor(x => x.PageQuota);
        result.ShouldNotHaveValidationErrorFor(x => x.UserQuota);
        result.IsValid.Should().BeTrue();
    }

    #endregion
}
