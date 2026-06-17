using AutoMapper;
using FluentValidation;
using DnnMigration.Application.Common;
using DnnMigration.Application.DTOs.Role;
using DnnMigration.Application.DTOs.User;
using DnnMigration.Application.Interfaces;
using DnnMigration.Domain.Entities;
using DnnMigration.Domain.Interfaces;

namespace DnnMigration.Application.Services;

/// <summary>
/// Application service for the Role aggregate, including user-role membership.
/// MIGRATION: ports the business surface of <c>Library/Components/Security/Roles/RoleController.vb</c>
/// (+ <c>RoleComparer.vb</c>) into async, DTO-based operations. Data access is delegated to
/// <see cref="IRoleRepository"/> (an EF Core implementation in the Infrastructure layer), replacing the
/// legacy controller's co-mingled RoleProvider / ADO.NET / <c>CBO</c> reflection-hydration calls. Entity
/// &lt;-&gt; DTO transformation is delegated to AutoMapper, and create/update payloads are validated with
/// FluentValidation before any persistence work occurs.
///
/// Behavioral parity (AAP §0.7.1) is mandatory: several legacy algorithms — the auto-assign loop, the
/// user-role expiry computation, and the "can remove user from role" guard — are reproduced verbatim,
/// including pre-existing quirks. Every deviation, omission, or ported bug is annotated with a
/// <c>// MIGRATION:</c> comment and recorded in the root <c>MIGRATION_NOTES.md</c>. Role is HARD-deleted
/// with a transactional user-role cascade performed by the repository.
/// </summary>
public class RoleService : IRoleService
{
    private readonly IRoleRepository _roleRepository;
    private readonly IUserRepository _userRepository;
    private readonly IPortalRepository _portalRepository;
    private readonly IMapper _mapper;
    private readonly IValidator<CreateRoleDto> _createValidator;
    private readonly IValidator<UpdateRoleDto> _updateValidator;

    /// <summary>
    /// Initializes a new <see cref="RoleService"/> with its injected collaborators.
    /// </summary>
    /// <param name="roleRepository">Repository providing data access for the Role aggregate and user-role membership.</param>
    /// <param name="userRepository">Repository used to enumerate portal users for auto-assignment.</param>
    /// <param name="portalRepository">Repository used by the "can remove user from role" guard.</param>
    /// <param name="mapper">AutoMapper instance used for entity &lt;-&gt; DTO projection.</param>
    /// <param name="createValidator">FluentValidation validator for <see cref="CreateRoleDto"/>.</param>
    /// <param name="updateValidator">FluentValidation validator for <see cref="UpdateRoleDto"/>.</param>
    public RoleService(
        IRoleRepository roleRepository,
        IUserRepository userRepository,
        IPortalRepository portalRepository,
        IMapper mapper,
        IValidator<CreateRoleDto> createValidator,
        IValidator<UpdateRoleDto> updateValidator)
    {
        _roleRepository = roleRepository;
        _userRepository = userRepository;
        _portalRepository = portalRepository;
        _mapper = mapper;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    /// <summary>
    /// Auto-assigns every portal user to the supplied role.
    /// MIGRATION: ports <c>RoleController.AutoAssignUsers</c> [RoleController.vb:L68-83] verbatim, including
    /// the deliberate swallow-all-exceptions loop body that the legacy used to tolerate users who already
    /// belonged to the role.
    /// </summary>
    /// <param name="role">The role to auto-assign existing portal users to.</param>
    /// <param name="cancellationToken">A token to observe while awaiting the asynchronous operations.</param>
    private async Task AutoAssignUsersAsync(Role role, CancellationToken cancellationToken)
    {
        // MIGRATION: legacy AutoAssignUsers [L68-83] looped ALL portal users (UserController.GetUsers(PortalID, False)).
        // The paged repository surface is queried with one max-size page to enumerate all users.
        var (users, _) = await _userRepository.GetByPortalAsync(role.PortalID, 0, int.MaxValue, cancellationToken);
        foreach (var user in users)
        {
            try
            {
                // MIGRATION: legacy AddUserRole(PortalID, UserID, RoleID, Null.NullDate, Null.NullDate) — auto-assign carries NO expiry window. Reproduced as a DIRECT repository INSERT with null dates; this is the simple legacy AddUserRole path, distinct from the admin upsert service method (AddUserRoleAsync), and never touches the self-service UpdateUserRole window logic.
                await _roleRepository.AddUserRoleAsync(
                    new UserRole { UserID = user.UserID, RoleID = role.RoleID, EffectiveDate = null, ExpiryDate = null },
                    cancellationToken);
            }
            catch (Exception)
            {
                // MIGRATION: legacy swallowed exceptions here ("user already belongs to role"). Preserved verbatim — do NOT rethrow. (No exception variable → avoids CS0168.)
            }
        }
    }

    /// <inheritdoc />
    public async Task<RoleDto?> GetByIdAsync(int roleId, CancellationToken cancellationToken = default)
    {
        // MIGRATION: legacy GetRole used the RoleProvider + CBO reflection hydration [RoleController.vb:L163-165] —
        // replaced here by EF Core entity materialization behind IRoleRepository.
        var role = await _roleRepository.GetByIdAsync(roleId, cancellationToken);
        return role is null ? null : _mapper.Map<RoleDto>(role);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<RoleDto>> GetByPortalAsync(int portalId, CancellationToken cancellationToken = default)
    {
        var roles = await _roleRepository.GetByPortalAsync(portalId, cancellationToken);
        // MIGRATION: legacy RoleComparer [RoleComparer.vb:L55-57] ordered by RoleName case-insensitively (CurrentCulture) — preserved.
        var ordered = roles.OrderBy(r => r.RoleName ?? string.Empty, StringComparer.CurrentCultureIgnoreCase);
        return _mapper.Map<IEnumerable<RoleDto>>(ordered);
    }

    /// <inheritdoc />
    public async Task<RoleDto> CreateAsync(CreateRoleDto request, CancellationToken cancellationToken = default)
    {
        await _createValidator.ValidateAndThrowAsync(request, cancellationToken);

        // MIGRATION: legacy AddRole performed NO duplicate-name check before insert — no guard added (behavioral parity).
        var role = _mapper.Map<Role>(request);
        var created = await _roleRepository.AddAsync(role, cancellationToken);

        // MIGRATION: AddRole called AutoAssignUsers [L106] when AutoAssignment is enabled.
        if (created.AutoAssignment)
        {
            await AutoAssignUsersAsync(created, cancellationToken);
        }

        return _mapper.Map<RoleDto>(created);
    }

    /// <inheritdoc />
    public async Task<RoleDto> UpdateAsync(UpdateRoleDto request, CancellationToken cancellationToken = default)
    {
        await _updateValidator.ValidateAndThrowAsync(request, cancellationToken);

        var existing = await _roleRepository.GetByIdAsync(request.RoleID, cancellationToken);
        if (existing is null)
        {
            throw new KeyNotFoundException($"Role {request.RoleID} was not found.");
        }

        _mapper.Map(request, existing);
        await _roleRepository.UpdateAsync(existing, cancellationToken);

        // MIGRATION: UpdateRole called AutoAssignUsers [L256] when AutoAssignment is enabled.
        if (existing.AutoAssignment)
        {
            await AutoAssignUsersAsync(existing, cancellationToken);
        }

        return _mapper.Map<RoleDto>(existing);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(int roleId, CancellationToken cancellationToken = default)
    {
        var role = await _roleRepository.GetByIdAsync(roleId, cancellationToken);
        if (role is null)
        {
            return; // MIGRATION: legacy DeleteRole no-ops when the role is not found [RoleController.vb:L125-133].
        }

        // MIGRATION: HARD-delete with transactional UserRole cascade handled by the repository.
        await _roleRepository.DeleteAsync(roleId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<UserRoleAssignmentDto> AddUserRoleAsync(AssignUserRoleDto request, CancellationToken cancellationToken = default)
    {
        // MIGRATION: ports the ADMIN assignment path RoleController.AddUserRole(PortalID, UserId, RoleId,
        // EffectiveDate, ExpiryDate) [RoleController.vb:L295-317] — the path the admin SecurityRoles.ascx.vb
        // "Add Role To User" screen invokes (a blank date textbox becomes Null.NullDate -> null
        // [SecurityRoles.ascx.vb:L528-542]). It is a plain UPSERT of the admin-supplied effective/expiry
        // window: load the existing assignment; if absent create it, otherwise update its dates. It
        // deliberately does NOT compute a trial/billing window and never sets IsTrialUsed/Subscribed — that
        // is the separate self-service RoleController.UpdateUserRole(Cancel) path [L489-557], which is OUT OF
        // SCOPE for this admin API. An earlier CP2 revision wrongly ported the self-service computed-window
        // algorithm here; corrected per the CP2 role-assignment-contract finding (see MIGRATION_NOTES.md §6.2).
        // The optional notify-user email [legacy shared overload L647] is omitted (Mail/Localization out of
        // scope; consistent with RemoveUserRoleAsync).
        var userId = request.UserID;
        var roleId = request.RoleID;

        // MIGRATION: legacy `objUserRole = GetUserRole(PortalID, UserId, RoleId)` [RoleController.vb:L299].
        var existing = (await _roleRepository.GetUserRolesAsync(userId, cancellationToken))
            .FirstOrDefault(ur => ur.RoleID == roleId);

        UserRole persisted;
        if (existing is not null)
        {
            // MIGRATION: legacy "Else" branch [L308-313] — update the existing assignment's effective/expiry
            // window (provider.UpdateUserRole) via the explicit UpdateUserRoleAsync repository operation (NOT Add).
            existing.EffectiveDate = request.EffectiveDate;
            existing.ExpiryDate = request.ExpiryDate;
            persisted = await _roleRepository.UpdateUserRoleAsync(existing, cancellationToken);
        }
        else
        {
            // MIGRATION: legacy "If objUserRole Is Nothing" branch [L300-307] — create a new assignment
            // carrying the admin-supplied dates (provider.AddUserToRole).
            persisted = await _roleRepository.AddUserRoleAsync(
                new UserRole
                {
                    UserID = userId,
                    RoleID = roleId,
                    EffectiveDate = request.EffectiveDate,
                    ExpiryDate = request.ExpiryDate,
                },
                cancellationToken);
        }

        // MIGRATION: return the persisted assignment (with server-assigned UserRoleID) projected to UserRoleAssignmentDto.
        return _mapper.Map<UserRoleAssignmentDto>(persisted);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<RoleDto>> GetUserRolesAsync(int userId, CancellationToken cancellationToken = default)
    {
        var userRoles = await _roleRepository.GetUserRolesAsync(userId, cancellationToken);
        // MIGRATION: project each join row's Role navigation; skip rows whose Role nav is null.
        var roles = userRoles.Where(ur => ur.Role is not null).Select(ur => ur.Role!);
        return _mapper.Map<IEnumerable<RoleDto>>(roles);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<UserRoleAssignmentDto>> GetUserRoleAssignmentsAsync(int userId, CancellationToken cancellationToken = default)
    {
        // MIGRATION: read side of the round-trip — project each user-role join row's membership metadata
        // (effective/expiry window, trial usage, subscription) to UserRoleAssignmentDto. Mirrors the legacy
        // RoleController.GetUserRoles(PortalId, UserId) [RoleController.vb:L392-394], minus the portal scoping
        // (the repository already keys the join by user).
        var userRoles = await _roleRepository.GetUserRolesAsync(userId, cancellationToken);
        return _mapper.Map<IEnumerable<UserRoleAssignmentDto>>(userRoles);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<UserDto>> GetUsersInRoleAsync(int roleId, CancellationToken cancellationToken = default)
    {
        var users = await _roleRepository.GetUsersInRoleAsync(roleId, cancellationToken);
        return _mapper.Map<IEnumerable<UserDto>>(users);
    }

    /// <inheritdoc />
    public async Task RemoveUserRoleAsync(int userId, int roleId, CancellationToken cancellationToken = default)
    {
        var role = await _roleRepository.GetByIdAsync(roleId, cancellationToken);
        if (role is null)
        {
            return; // MIGRATION: nothing to remove if the role does not exist (idempotent).
        }

        var portal = await _portalRepository.GetByIdAsync(role.PortalID, cancellationToken);
        if (portal is not null)
        {
            // MIGRATION: CanRemoveUserFromRole [RoleController.vb:L764-769] = Not ((AdministratorId = UserId And AdministratorRoleId = RoleId) Or RegisteredRoleId = RoleId).
            // Portal guard fields are int? (schema NULL → Null.NullInteger): a null field can never equal a concrete id, matching the legacy sentinel semantics.
            var cannotRemove =
                (portal.AdministratorId == userId && portal.AdministratorRoleId == roleId)
                || portal.RegisteredRoleId == roleId;
            if (cannotRemove)
            {
                // MIGRATION: legacy DeleteUserRole returned False silently [RoleController.vb:L330-347]; we throw so the API surfaces RFC 7807 (consistent with the other guard modernizations).
                // MIGRATION (F5-01 hardening): BusinessRuleConflictException (subtype of InvalidOperationException) so the safe message maps to a 409 detail while raw IOE no longer leaks internals.
                throw new BusinessRuleConflictException("Cannot remove this user from the role.");
            }
        }

        await _roleRepository.RemoveUserRoleAsync(userId, roleId, cancellationToken);
        // MIGRATION: legacy optional SendNotification email [RoleController.vb:L577-610] OMITTED (Mail/Localization/Profile out of scope; no email port; interface carries no notify flag).
    }
}
