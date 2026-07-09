using FluentValidation;
using DnnMigration.Application.DTOs;

namespace DnnMigration.Application.Validators;

/// <summary>
/// FluentValidation validator for <see cref="CreateTabDto"/>, the request payload
/// for <c>POST /api/tabs</c> (creating a tab / page within a portal's page hierarchy).
/// </summary>
/// <remarks>
/// MIGRATION QA finding D: before this validator existed, <c>POST /api/tabs</c> with a
/// blank <c>tabName</c> was accepted and returned <c>201 Created</c> instead of the
/// expected <c>400 Bad Request</c>, because the Validators folder shipped only the
/// Module / Portal / User validators. This validator restores the field-level validation
/// of the legacy page-management screen (<c>Website/admin/Tabs/managetabs.ascx</c>), whose
/// RequiredFieldValidator made the tab name mandatory, so the modern API preserves the
/// original UI's functional parity for page creation.
///
/// The class is declared <c>public sealed</c> (not <c>internal</c>) so it is discoverable
/// by the API host's FluentValidation assembly scan
/// (<c>AddValidatorsFromAssembly(typeof(MappingProfile).Assembly)</c> in <c>Program.cs</c>)
/// and is automatically registered in the dependency-injection container; combined with
/// <c>AddFluentValidationAutoValidation()</c> and the controller's <c>[ApiController]</c>
/// attribute, an invalid payload is short-circuited into an RFC 7807 ProblemDetails
/// <c>400</c> before the controller action or <c>ITabService</c> runs. It carries no data
/// access and no business logic — validation only — and never emits the
/// <c>{ data, meta }</c> response envelope.
/// </remarks>
public sealed class CreateTabDtoValidator : AbstractValidator<CreateTabDto>
{
    /// <summary>
    /// Configures the validation rules for <see cref="CreateTabDto"/>.
    /// Only fields that exist on the create contract are validated.
    /// </summary>
    public CreateTabDtoValidator()
    {
        // MIGRATION: managetabs.ascx RequiredFieldValidator on the page-name input — a tab
        // must have a non-empty name before it can be created.
        RuleFor(x => x.TabName).NotEmpty();

        // MIGRATION: PortalID scopes the tab to a portal. DNN portal identifiers are
        // zero-based (the first portal is PortalID 0), so a non-negative identifier is required.
        RuleFor(x => x.PortalID).GreaterThanOrEqualTo(0);
    }
}

/// <summary>
/// FluentValidation validator for <see cref="UpdateTabDto"/>, the request payload for
/// <c>PUT /api/tabs/{id}</c> (editing an existing tab's settings).
/// </summary>
/// <remarks>
/// MIGRATION QA finding D: mirrors the same mandatory tab-name rule enforced by the legacy
/// page-management screen (<c>Website/admin/Tabs/managetabs.ascx</c>), restricted to the
/// fields the update contract exposes. <see cref="UpdateTabDto"/> deliberately omits the
/// owning <c>PortalID</c> (the portal that owns a tab is immutable and is not re-assigned
/// through an update), so no <c>PortalID</c> rule is emitted here.
///
/// Like its create-side counterpart it performs validation only (no data access, no business
/// orchestration) and is declared <c>public sealed</c> so FluentValidation's assembly scan
/// (<c>AddValidatorsFromAssembly</c>) can discover and register it in the DI container.
/// </remarks>
public sealed class UpdateTabDtoValidator : AbstractValidator<UpdateTabDto>
{
    /// <summary>
    /// Configures the validation rules for <see cref="UpdateTabDto"/>.
    /// Only fields that exist on the update contract are validated.
    /// </summary>
    public UpdateTabDtoValidator()
    {
        // MIGRATION: managetabs.ascx RequiredFieldValidator on the page-name input — the tab
        // name remains mandatory when editing an existing tab.
        RuleFor(x => x.TabName).NotEmpty();
    }
}
