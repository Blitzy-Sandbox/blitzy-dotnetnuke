using DnnMigration.Application.DTOs.Portal;
using DnnMigration.Application.Validators;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace DnnMigration.UnitTests.Validators;

// MIGRATION: Parity tests for CreatePortalValidator. Asserts the exact rules and verbatim
// error messages migrated from legacy DNN portal-creation validation
// (Website/admin/Portal/Signup.ascx.vb, SiteSettings.ascx.vb) and PortalInfo.vb field
// constraints. Tests encode behavioral parity ONLY - they introduce no new rules.
public sealed class CreatePortalValidatorTests
{
    private readonly CreatePortalValidator _validator = new();

    private static CreatePortalRequest Valid() => new()
    {
        PortalName = "Contoso Portal",
        Email = "admin@contoso.com",
        Description = "A valid portal description",
        KeyWords = "cms, portal, dnn",
        HomeDirectory = "Portals/0"
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

    // ---- PortalName: NotEmpty (custom message) + MaximumLength(128) ----

    [Fact]
    public void PortalName_empty_fails_with_required_message()
    {
        var dto = Valid();
        dto.PortalName = "";
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.PortalName)
            .WithErrorMessage("Portal Name Is Required.");
    }

    // MIGRATION: EXACT legacy parity (CP1 review CreatePortalValidator #1). The ASP.NET
    // RequiredFieldValidator does not trim and fails only when the value equals its empty
    // InitialValue, so a whitespace-only PortalName ("   ") PASSED in the legacy Signup form.
    // The validator must NOT tighten this (AAP §0.7.2), so whitespace must NOT raise a required error.
    [Fact]
    public void PortalName_whitespace_passes_matching_RequiredFieldValidator()
    {
        var dto = Valid();
        dto.PortalName = "   ";
        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveValidationErrorFor(x => x.PortalName);
    }

    [Fact]
    public void PortalName_exceeding_128_fails()
    {
        var dto = Valid();
        dto.PortalName = new string('a', 129);
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.PortalName);
    }

    [Fact]
    public void PortalName_at_128_passes()
    {
        var dto = Valid();
        dto.PortalName = new string('a', 128);
        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveValidationErrorFor(x => x.PortalName);
    }

    // ---- Email: NotEmpty (custom message) + MaximumLength(100), NO format regex ----

    [Fact]
    public void Email_empty_fails_with_required_message()
    {
        var dto = Valid();
        dto.Email = "";
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.Email)
            .WithErrorMessage("Email Is Required.");
    }

    [Fact]
    public void Email_null_fails_with_required_message()
    {
        var dto = Valid();
        dto.Email = null;
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.Email)
            .WithErrorMessage("Email Is Required.");
    }

    // MIGRATION: EXACT legacy parity (CP1 review CreatePortalValidator #1). RequiredFieldValidator did not
    // trim, so a whitespace-only Email ("  ") PASSED in the legacy Signup form. Whitespace must NOT raise a
    // required error (no .NotEmpty() tightening — AAP §0.7.2). Empty/null still fail (asserted above).
    [Fact]
    public void Email_whitespace_passes_matching_RequiredFieldValidator()
    {
        var dto = Valid();
        dto.Email = "  ";
        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Email_exceeding_100_fails()
    {
        var dto = Valid();
        dto.Email = new string('a', 101);
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Email_at_100_passes()
    {
        var dto = Valid();
        dto.Email = new string('a', 100);
        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveValidationErrorFor(x => x.Email);
    }

    // MIGRATION: CreatePortal Email has NO format regex in legacy parity - a malformed
    // address must NOT raise a validation error (only NotEmpty + MaximumLength(100) apply).
    [Fact]
    public void Email_with_invalid_format_passes_no_regex()
    {
        var dto = Valid();
        dto.Email = "not-an-email";
        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveValidationErrorFor(x => x.Email);
    }

    // ---- Description: MaximumLength(500), passes on null ----

    [Fact]
    public void Description_null_passes()
    {
        var dto = Valid();
        dto.Description = null;
        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void Description_exceeding_500_fails()
    {
        var dto = Valid();
        dto.Description = new string('a', 501);
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void Description_at_500_passes()
    {
        var dto = Valid();
        dto.Description = new string('a', 500);
        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveValidationErrorFor(x => x.Description);
    }

    // ---- KeyWords: MaximumLength(500), passes on null ----

    [Fact]
    public void KeyWords_null_passes()
    {
        var dto = Valid();
        dto.KeyWords = null;
        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveValidationErrorFor(x => x.KeyWords);
    }

    [Fact]
    public void KeyWords_exceeding_500_fails()
    {
        var dto = Valid();
        dto.KeyWords = new string('a', 501);
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.KeyWords);
    }

    [Fact]
    public void KeyWords_at_500_passes()
    {
        var dto = Valid();
        dto.KeyWords = new string('a', 500);
        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveValidationErrorFor(x => x.KeyWords);
    }

    // ---- HomeDirectory: MaximumLength(100), passes on null ----

    [Fact]
    public void HomeDirectory_null_passes()
    {
        var dto = Valid();
        dto.HomeDirectory = null;
        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveValidationErrorFor(x => x.HomeDirectory);
    }

    [Fact]
    public void HomeDirectory_exceeding_100_fails()
    {
        var dto = Valid();
        dto.HomeDirectory = new string('a', 101);
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.HomeDirectory);
    }

    [Fact]
    public void HomeDirectory_at_100_passes()
    {
        var dto = Valid();
        dto.HomeDirectory = new string('a', 100);
        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveValidationErrorFor(x => x.HomeDirectory);
    }

    // ---- Optional administrator group (CP1 review PortalService #3) ----
    // Required together ONLY when admin provisioning is requested (any Admin* field present).

    // MIGRATION: when NO admin field is supplied the group is skipped entirely, so the base request stays valid
    // (this is also why the existing Valid()-based tests above remain green).
    [Fact]
    public void Admin_group_omitted_passes_with_no_admin_errors()
    {
        var result = _validator.TestValidate(Valid());
        result.ShouldNotHaveValidationErrorFor(x => x.AdminUsername);
        result.ShouldNotHaveValidationErrorFor(x => x.AdminPassword);
        result.ShouldNotHaveValidationErrorFor(x => x.AdminFirstName);
        result.ShouldNotHaveValidationErrorFor(x => x.AdminLastName);
        result.ShouldNotHaveValidationErrorFor(x => x.AdminEmail);
    }

    // MIGRATION: a complete admin group (all five fields) is accepted — mirrors the legacy Signup admin section.
    [Fact]
    public void Admin_group_complete_passes()
    {
        var dto = Valid();
        dto.AdminUsername = "admin";
        dto.AdminPassword = "P@ssw0rd!";
        dto.AdminFirstName = "Ada";
        dto.AdminLastName = "Lovelace";
        dto.AdminEmail = "ada@contoso.com";
        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveAnyValidationErrors();
    }

    // MIGRATION: supplying AdminUsername (the group trigger) without AdminPassword fails the password rule with the
    // exact required message.
    [Fact]
    public void Admin_username_without_password_fails_with_required_message()
    {
        var dto = Valid();
        dto.AdminUsername = "admin";
        // AdminPassword left null
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.AdminPassword)
            .WithErrorMessage("Administrator Password Is Required.");
    }

    // MIGRATION: supplying ANY single admin field (here AdminFirstName) triggers the whole group — the remaining
    // required fields (Username/Password/LastName/Email) must all raise required errors.
    [Fact]
    public void Admin_partial_group_requires_remaining_fields()
    {
        var dto = Valid();
        dto.AdminFirstName = "Ada";
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.AdminUsername)
            .WithErrorMessage("Administrator User Name Is Required.");
        result.ShouldHaveValidationErrorFor(x => x.AdminPassword)
            .WithErrorMessage("Administrator Password Is Required.");
        result.ShouldHaveValidationErrorFor(x => x.AdminLastName)
            .WithErrorMessage("Administrator Last Name Is Required.");
        result.ShouldHaveValidationErrorFor(x => x.AdminEmail)
            .WithErrorMessage("Administrator Email Is Required.");
        // The field that triggered the group is itself supplied, so it must NOT raise a required error.
        result.ShouldNotHaveValidationErrorFor(x => x.AdminFirstName);
    }

    // MIGRATION: parity with PortalName/Email — the admin group uses non-trimming IsNullOrEmpty semantics, so a
    // whitespace-only value counts as "supplied" (no .NotEmpty() tightening, AAP §0.7.2).
    [Fact]
    public void Admin_whitespace_values_are_treated_as_supplied()
    {
        var dto = Valid();
        dto.AdminUsername = "   ";
        dto.AdminPassword = "   ";
        dto.AdminFirstName = "   ";
        dto.AdminLastName = "   ";
        dto.AdminEmail = "   ";
        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveValidationErrorFor(x => x.AdminUsername);
        result.ShouldNotHaveValidationErrorFor(x => x.AdminPassword);
        result.ShouldNotHaveValidationErrorFor(x => x.AdminFirstName);
        result.ShouldNotHaveValidationErrorFor(x => x.AdminLastName);
        result.ShouldNotHaveValidationErrorFor(x => x.AdminEmail);
    }
}
