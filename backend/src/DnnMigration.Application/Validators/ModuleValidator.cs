using FluentValidation;
using DnnMigration.Application.DTOs;

namespace DnnMigration.Application.Validators;

/// <summary>
/// FluentValidation validator for <see cref="CreateModuleDto"/>, the request payload
/// for <c>POST /api/modules</c> (placing a module instance on a tab).
/// </summary>
/// <remarks>
/// MIGRATION: This validator mirrors the field-level validation of the legacy Module
/// Settings screen (<c>Website/admin/Modules/modulesettings.ascx</c>) so the modern API
/// preserves the original UI's functional parity for module placement.
///
/// MIGRATION: The legacy <c>Library/Components/Modules/ModuleDefinitionValidator.vb</c>
/// (<c>Inherits XmlValidatorBase</c>) is an XML/XSD module-definition manifest validator
/// that selects a <c>ModuleDef_Vx.xsd</c> schema and validates a manifest stream. Module
/// definition manifest / XML processing is out of scope for this migration, so that
/// validator's XSD selection and version-detection logic is intentionally NOT ported.
///
/// The class is <c>public</c> (not <c>internal</c>) so it is discoverable by the API
/// host's FluentValidation assembly scan (<c>AddValidatorsFromAssembly</c>) and is
/// automatically registered in the dependency-injection container. It carries no data
/// access and no business logic — validation only — and never emits the
/// <c>{ data, meta }</c> response envelope.
/// </remarks>
public sealed class CreateModuleDtoValidator : AbstractValidator<CreateModuleDto>
{
    /// <summary>
    /// Configures the validation rules for <see cref="CreateModuleDto"/>.
    /// Only fields that exist on the create contract are validated.
    /// </summary>
    public CreateModuleDtoValidator()
    {
        // MIGRATION: modulesettings.ascx txtTitle — a module title is required to place a
        // module instance on a page, so the create payload must supply a non-empty title.
        RuleFor(x => x.ModuleTitle).NotEmpty();

        // MIGRATION: ModuleDefID is the definition identifier a module instance is created
        // from; it must reference a valid (positive) module definition.
        RuleFor(x => x.ModuleDefID).GreaterThan(0);

        // MIGRATION: modulesettings.ascx valCacheTime CompareValidator(Integer); the cache
        // timeout is expressed in seconds and cannot be negative.
        RuleFor(x => x.CacheTime).GreaterThanOrEqualTo(0);

        // MIGRATION: TabID identifies the page the module instance is placed on; the legacy
        // Module Settings screen always operated within a valid (positive) tab context.
        RuleFor(x => x.TabID).GreaterThan(0);

        // MIGRATION: PortalID scopes the module instance to a portal. DNN portal identifiers are
        // zero-based (the first portal is PortalID 0), so a non-negative identifier is required.
        RuleFor(x => x.PortalID).GreaterThanOrEqualTo(0);

        // MIGRATION: modulesettings.ascx cboVisibility is a RadioButtonList whose only values are
        // 0 = Maximized, 1 = Minimized, 2 = None; any other integer is invalid.
        RuleFor(x => x.Visibility).InclusiveBetween(0, 2);

        // MIGRATION: modulesettings.ascx valBorder validated txtBorder (maxlength="1") as a single
        // digit 0-9 with the message "Invalid Border (must be a number between 0 and 9)". Border is
        // modeled as an optional string? on the DTO, so the rule only runs when a value is supplied
        // and the user-visible message is preserved verbatim.
        RuleFor(x => x.Border)
            .Matches("^[0-9]$").WithMessage("Invalid Border (must be a number between 0 and 9)")
            .When(x => !string.IsNullOrEmpty(x.Border));

        // MIGRATION: FriendlyName/DesktopModuleID are read-only projection fields (ModuleDto)
        // and are not part of CreateModuleDto; no rule applies.
        // MIGRATION: legacy StartDate/EndDate CompareValidators were DataTypeCheck(Date) only,
        // satisfied by DateTime? typing on the DTO, so no cross-field date rule is added.
    }
}

/// <summary>
/// FluentValidation validator for <see cref="UpdateModuleDto"/>, the request payload for
/// <c>PUT /api/modules/{id}</c> (editing an existing module instance's settings).
/// </summary>
/// <remarks>
/// MIGRATION: mirrors the same field-level validation of the legacy Module Settings screen
/// (<c>Website/admin/Modules/modulesettings.ascx</c>) that the create-side validator reproduces,
/// restricted to the fields the update contract exposes. <see cref="UpdateModuleDto"/> deliberately
/// omits the placement identity (<c>PortalID</c>/<c>TabID</c>/<c>ModuleDefID</c>) — those are never
/// re-assigned through an update — so no identifier rules are emitted here.
///
/// Like its create-side counterpart it performs validation only (no data access, no business
/// orchestration) and is declared <c>public sealed</c> so FluentValidation's assembly scan
/// (<c>AddValidatorsFromAssembly</c>) can discover and register it in the DI container.
/// </remarks>
public sealed class UpdateModuleDtoValidator : AbstractValidator<UpdateModuleDto>
{
    /// <summary>
    /// Configures the validation rules for <see cref="UpdateModuleDto"/>.
    /// Only fields that exist on the update contract are validated.
    /// </summary>
    public UpdateModuleDtoValidator()
    {
        // MIGRATION: modulesettings.ascx txtTitle — a module title is required.
        RuleFor(x => x.ModuleTitle).NotEmpty();

        // MIGRATION: modulesettings.ascx valCacheTime CompareValidator(Integer); the cache
        // timeout is expressed in seconds and cannot be negative.
        RuleFor(x => x.CacheTime).GreaterThanOrEqualTo(0);

        // MIGRATION: modulesettings.ascx cboVisibility RadioButtonList — only 0 = Maximized,
        // 1 = Minimized, 2 = None are valid.
        RuleFor(x => x.Visibility).InclusiveBetween(0, 2);

        // MIGRATION: modulesettings.ascx valBorder — single digit 0-9 on txtBorder (maxlength="1").
        // Border is an optional string? so the rule only runs when a value is supplied; message
        // preserved verbatim.
        RuleFor(x => x.Border)
            .Matches("^[0-9]$").WithMessage("Invalid Border (must be a number between 0 and 9)")
            .When(x => !string.IsNullOrEmpty(x.Border));
    }
}
