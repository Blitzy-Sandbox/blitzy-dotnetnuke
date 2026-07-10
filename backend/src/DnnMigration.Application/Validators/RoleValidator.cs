using FluentValidation;
using DnnMigration.Application.DTOs;

namespace DnnMigration.Application.Validators;

/// <summary>
/// FluentValidation validator for <see cref="CreateRoleDto"/>, the request payload
/// for <c>POST /api/roles</c> (creating a security role within a portal).
/// </summary>
/// <remarks>
/// MIGRATION QA finding D: before this validator existed, <c>POST /api/roles</c> with a
/// blank <c>roleName</c> was accepted and returned <c>201 Created</c> instead of the
/// expected <c>400 Bad Request</c>, because the Validators folder shipped only the
/// Module / Portal / User validators. This validator restores the field-level validation
/// of the legacy role-edit screen (<c>Website/admin/Security/editroles.ascx</c>), whose
/// <c>valName</c> RequiredFieldValidator made the role name mandatory, so the modern API
/// preserves the original UI's functional parity for role creation.
///
/// The class is declared <c>public sealed</c> (not <c>internal</c>) so it is discoverable
/// by the API host's FluentValidation assembly scan
/// (<c>AddValidatorsFromAssembly(typeof(MappingProfile).Assembly)</c> in <c>Program.cs</c>)
/// and is automatically registered in the dependency-injection container; combined with
/// <c>AddFluentValidationAutoValidation()</c> and the controller's <c>[ApiController]</c>
/// attribute, an invalid payload is short-circuited into an RFC 7807 ProblemDetails
/// <c>400</c> before the controller action or <c>IRoleService</c> runs. It carries no data
/// access and no business logic — validation only — and never emits the
/// <c>{ data, meta }</c> response envelope.
/// </remarks>
public sealed class CreateRoleDtoValidator : AbstractValidator<CreateRoleDto>
{
    /// <summary>
    /// Configures the validation rules for <see cref="CreateRoleDto"/>.
    /// Only fields that exist on the create contract are validated.
    /// </summary>
    public CreateRoleDtoValidator()
    {
        // MIGRATION: editroles.ascx valName RequiredFieldValidator on txtRoleName — a role
        // must have a non-empty name before it can be created.
        // MIGRATION (QA finding - R10 Issue 14): bounded to the legacy schema column width. The DNN Roles
        // table declares [RoleName] nvarchar(50) NOT NULL (01.00.00.SqlDataProvider L117), so an over-length
        // name is rejected with 400 Bad Request (RFC 7807 ProblemDetails) before it can reach persistence.
        RuleFor(x => x.RoleName).NotEmpty().MaximumLength(50);

        // MIGRATION: PortalID scopes the role to a portal. DNN portal identifiers are
        // zero-based (the first portal is PortalID 0), so a non-negative identifier is required.
        RuleFor(x => x.PortalID).GreaterThanOrEqualTo(0);
    }
}

/// <summary>
/// FluentValidation validator for <see cref="UpdateRoleDto"/>, the request payload for
/// <c>PUT /api/roles/{id}</c> (editing an existing role's attributes).
/// </summary>
/// <remarks>
/// MIGRATION QA finding D: mirrors the same mandatory role-name rule enforced by the legacy
/// role-edit screen (<c>Website/admin/Security/editroles.ascx</c>), restricted to the fields
/// the update contract exposes. <see cref="UpdateRoleDto"/> deliberately omits the owning
/// <c>PortalID</c> (the portal that owns a role is immutable and is not re-assigned through an
/// update), so no <c>PortalID</c> rule is emitted here.
///
/// Like its create-side counterpart it performs validation only (no data access, no business
/// orchestration) and is declared <c>public sealed</c> so FluentValidation's assembly scan
/// (<c>AddValidatorsFromAssembly</c>) can discover and register it in the DI container.
/// </remarks>
public sealed class UpdateRoleDtoValidator : AbstractValidator<UpdateRoleDto>
{
    /// <summary>
    /// Configures the validation rules for <see cref="UpdateRoleDto"/>.
    /// Only fields that exist on the update contract are validated.
    /// </summary>
    public UpdateRoleDtoValidator()
    {
        // MIGRATION: editroles.ascx valName RequiredFieldValidator on txtRoleName — the role
        // name remains mandatory when editing an existing role.
        // MIGRATION (QA finding - R10 Issue 14): the same nvarchar(50) schema bound applies on edit
        // (01.00.00.SqlDataProvider L117), so an over-length rename is rejected with 400 Bad Request.
        RuleFor(x => x.RoleName).NotEmpty().MaximumLength(50);
    }
}
