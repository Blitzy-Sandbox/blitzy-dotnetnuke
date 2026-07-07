using DnnMigration.Application.DTOs;
using DnnMigration.Application.Validators;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace DnnMigration.UnitTests;

/// <summary>
/// Unit tests for <see cref="CreateModuleDtoValidator"/> (FluentValidation) covering the
/// <c>POST /api/modules</c> request contract (<see cref="CreateModuleDto"/>).
/// </summary>
/// <remarks>
/// MIGRATION: These tests pin the validator to EXACTLY its three migrated, field-level rules
/// and nothing more:
/// <list type="bullet">
///   <item><description><c>ModuleTitle</c> — <c>NotEmpty()</c> (legacy modulesettings.ascx <c>txtTitle</c> was required).</description></item>
///   <item><description><c>ModuleDefID</c> — <c>GreaterThan(0)</c> (must reference a valid module definition).</description></item>
///   <item><description><c>CacheTime</c> — <c>GreaterThanOrEqualTo(0)</c> (cache seconds cannot be negative).</description></item>
/// </list>
/// The Phase 4 "negative-space" tests deliberately assert the ABSENCE of any other rule
/// (no rule on the optional container fields, and no cross-field <c>StartDate</c>/<c>EndDate</c>
/// rule). This guards the "no extra rules" contract so a downstream author cannot silently add
/// unintended validation without a failing test. All assertions use
/// <see cref="FluentValidation.TestHelper"/> against a real validator instance (no mocks needed —
/// the validator has no collaborators).
/// </remarks>
public class ModuleValidatorTests
{
    /// <summary>
    /// System under test. A fresh validator is created per test (xUnit instantiates the test
    /// class once per test method), so there is no shared mutable state across tests.
    /// </summary>
    private readonly CreateModuleDtoValidator _sut = new();

    /// <summary>
    /// Builds a fully valid <see cref="CreateModuleDto"/> that satisfies all three rules:
    /// a non-empty <see cref="CreateModuleDto.ModuleTitle"/>, a positive
    /// <see cref="CreateModuleDto.ModuleDefID"/> (1), and a non-negative
    /// <see cref="CreateModuleDto.CacheTime"/> (0). Individual tests mutate a single field via a
    /// record <c>with</c> expression so every case is isolated to the rule under test.
    /// </summary>
    private static CreateModuleDto ValidCreate() => new()
    {
        PortalID = 0,
        TabID = 1,
        ModuleDefID = 1,
        ModuleTitle = "Announcements",
        PaneName = "ContentPane",
        ModuleOrder = 1,
        CacheTime = 0,
        Visibility = 0
    };

    // -------------------------------------------------------------------------------------------
    // Phase 2 — a fully valid model passes with zero validation errors.
    // -------------------------------------------------------------------------------------------

    [Fact]
    public void Validate_WithValidModel_HasNoValidationErrors()
    {
        var result = _sut.TestValidate(ValidCreate());

        result.ShouldNotHaveAnyValidationErrors();
    }

    // -------------------------------------------------------------------------------------------
    // Phase 3 — ModuleTitle: NotEmpty().
    // FluentValidation's NotEmpty() treats null, "" and whitespace-only strings as empty.
    // -------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithEmptyOrWhitespaceModuleTitle_HasValidationErrorForModuleTitle(string moduleTitle)
    {
        var dto = ValidCreate() with { ModuleTitle = moduleTitle };

        var result = _sut.TestValidate(dto);

        result.ShouldHaveValidationErrorFor(x => x.ModuleTitle);
    }

    [Fact]
    public void Validate_WithNonEmptyModuleTitle_HasNoValidationErrorForModuleTitle()
    {
        var result = _sut.TestValidate(ValidCreate());

        result.ShouldNotHaveValidationErrorFor(x => x.ModuleTitle);
    }

    // -------------------------------------------------------------------------------------------
    // Phase 3 — ModuleDefID: GreaterThan(0). Zero and negatives fail; positives pass.
    // -------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-5)]
    public void Validate_WithNonPositiveModuleDefID_HasValidationErrorForModuleDefID(int moduleDefId)
    {
        var dto = ValidCreate() with { ModuleDefID = moduleDefId };

        var result = _sut.TestValidate(dto);

        result.ShouldHaveValidationErrorFor(x => x.ModuleDefID);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(999)]
    public void Validate_WithPositiveModuleDefID_HasNoValidationErrorForModuleDefID(int moduleDefId)
    {
        var dto = ValidCreate() with { ModuleDefID = moduleDefId };

        var result = _sut.TestValidate(dto);

        result.ShouldNotHaveValidationErrorFor(x => x.ModuleDefID);
    }

    // -------------------------------------------------------------------------------------------
    // Phase 3 — CacheTime: GreaterThanOrEqualTo(0). Negatives fail; zero and positives pass.
    // -------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(-1)]
    [InlineData(-60)]
    public void Validate_WithNegativeCacheTime_HasValidationErrorForCacheTime(int cacheTime)
    {
        var dto = ValidCreate() with { CacheTime = cacheTime };

        var result = _sut.TestValidate(dto);

        result.ShouldHaveValidationErrorFor(x => x.CacheTime);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(60)]
    [InlineData(3600)]
    public void Validate_WithNonNegativeCacheTime_HasNoValidationErrorForCacheTime(int cacheTime)
    {
        var dto = ValidCreate() with { CacheTime = cacheTime };

        var result = _sut.TestValidate(dto);

        result.ShouldNotHaveValidationErrorFor(x => x.CacheTime);
    }

    // -------------------------------------------------------------------------------------------
    // Phase 4 — negative-space: the optional container fields carry NO rules, so a model that is
    // otherwise valid must still pass when they are null.
    // -------------------------------------------------------------------------------------------

    [Fact]
    public void Validate_WithNullOptionalContainerFields_HasNoValidationErrors()
    {
        var dto = ValidCreate() with
        {
            Alignment = null,
            Color = null,
            Border = null,
            IconFile = null,
            Header = null,
            Footer = null,
            ContainerSrc = null
        };

        var result = _sut.TestValidate(dto);

        result.ShouldNotHaveAnyValidationErrors();
    }

    // -------------------------------------------------------------------------------------------
    // Phase 4 — negative-space: there is NO cross-field date rule. A StartDate later than the
    // EndDate must NOT produce a validation error (the legacy validators were DataTypeCheck only,
    // satisfied by DateTime? typing on the DTO).
    // -------------------------------------------------------------------------------------------

    [Fact]
    public void Validate_WithStartDateAfterEndDate_HasNoValidationErrors()
    {
        var dto = ValidCreate() with
        {
            StartDate = new DateTime(2025, 1, 2),
            EndDate = new DateTime(2025, 1, 1)
        };

        var result = _sut.TestValidate(dto);

        result.ShouldNotHaveAnyValidationErrors();
    }

    // -------------------------------------------------------------------------------------------
    // Phase 4 — negative-space lock: when all three rule-bearing fields are invalid at once, the
    // validator must surface errors for EXACTLY those three properties and no others. This is the
    // strongest guard against a downstream author adding an unintended rule.
    // -------------------------------------------------------------------------------------------

    [Fact]
    public void Validate_WithAllRuleFieldsInvalid_ProducesErrorsForExactlyThoseThreeFields()
    {
        var dto = ValidCreate() with
        {
            ModuleTitle = "",
            ModuleDefID = 0,
            CacheTime = -1
        };

        var result = _sut.TestValidate(dto);

        result.ShouldHaveValidationErrorFor(x => x.ModuleTitle);
        result.ShouldHaveValidationErrorFor(x => x.ModuleDefID);
        result.ShouldHaveValidationErrorFor(x => x.CacheTime);

        result.Errors
            .Select(failure => failure.PropertyName)
            .Distinct()
            .Should()
            .BeEquivalentTo(new[]
            {
                nameof(CreateModuleDto.ModuleTitle),
                nameof(CreateModuleDto.ModuleDefID),
                nameof(CreateModuleDto.CacheTime)
            });
    }
}
