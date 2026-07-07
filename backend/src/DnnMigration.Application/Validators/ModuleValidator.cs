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

        // MIGRATION: FriendlyName/DesktopModuleID are read-only projection fields (ModuleDto)
        // and are not part of CreateModuleDto; no rule applies.
        // MIGRATION: legacy StartDate/EndDate CompareValidators were DataTypeCheck(Date) only,
        // satisfied by DateTime? typing on the DTO, so no cross-field date rule is added.
        // MIGRATION: legacy valBorder (Integer 0-9) is not reproduced because Border is a
        // string? on the DTO; adding numeric parsing here would exceed the DTO contract.
    }
}
