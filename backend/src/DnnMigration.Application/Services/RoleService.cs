using AutoMapper;
using FluentValidation;
using DnnMigration.Application.DTOs.Role;
using DnnMigration.Application.DTOs.User;
using DnnMigration.Application.Interfaces;
using DnnMigration.Domain.Entities;
using DnnMigration.Domain.Interfaces;

namespace DnnMigration.Application.Services;

/// <summary>
/// Application service for the Role aggregate, including user&#8596;role membership. MIGRATION: ports the
/// business surface of the legacy <c>RoleController.vb</c> (DotNetNuke 4.9.0.85,
/// <c>DotNetNuke.Security.Roles</c>) into async, DTO-based, repository-backed operations following the
/// uniform service skeleton (validate &#8594; check rules &#8594; load &#8594; mutate/project via AutoMapper
/// &#8594; save &#8594; return DTO).
/// </summary>
/// <remarks>
/// <para>
/// Behavioral equivalence with the legacy controller is mandatory: business logic is ported verbatim and is
/// NOT "improved" or simplified. Pre-existing legacy quirks are reproduced and documented rather than
/// silently fixed; every deviation, omission, or ported bug is annotated with a <c>// MIGRATION:</c> comment
/// and recorded in the root <c>MIGRATION_NOTES.md</c>.
/// </para>
/// <para>
/// The legacy reflection-based hydration (<c>CBO</c>) and <c>RoleProvider</c> data calls are replaced
/// wholesale by EF Core entity materialization behind <see cref="IRoleRepository"/>. The Role aggregate is
/// HARD-deleted (with a transactional <see cref="UserRole"/> cascade performed in the repository), matching
/// the AAP delete strategy for roles. The single most behavior-critical port is the user-role expiry
/// schedule in <see cref="AddUserRoleAsync"/>, which is reproduced step-for-step from the legacy
/// <c>UpdateUserRole</c>.
/// </para>
/// </remarks>
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
    /// <param name="roleRepository">Persistence gateway for the <see cref="Role"/> aggregate and user-role membership.</param>
    /// <param name="userRepository">
    /// User lookup, used solely by <see cref="AutoAssignUsersAsync"/> to enumerate the portal's users when a
    /// role has auto-assignment enabled.
    /// </param>
    /// <param name="portalRepository">
    /// Portal lookup, used solely by the <c>CanRemoveUserFromRole</c> guard in <see cref="RemoveUserRoleAsync"/>.
    /// </param>
    /// <param name="mapper">AutoMapper instance providing the Role entity &#8596; DTO projections.</param>
    /// <param name="createValidator">FluentValidation rules for <see cref="CreateRoleDto"/>.</param>
    /// <param name="updateValidator">FluentValidation rules for <see cref="UpdateRoleDto"/>.</param>
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
    /// Auto-assigns every user in the role's portal to the role.
    /// </summary>
    /// <remarks>
    /// MIGRATION: ports <c>RoleController.AutoAssignUsers</c> [L68-83] verbatim. The legacy method looped ALL
    /// portal users (<c>UserController.GetUsers(PortalID, False)</c>) and swallowed any exception raised when a
    /// user already belonged to the role. The per-user add deliberately carries NO effective/expiry dates — it
    /// is a DIRECT add that BYPASSES the <see cref="AddUserRoleAsync"/> expiry algorithm, faithful to the
    /// legacy code which called the simple <c>AddUserRole(..., Null.NullDate, Null.NullDate)</c> overload
    /// rather than <c>UpdateUserRole</c>. The legacy guarded the loop with <c>If objRoleInfo.AutoAssignment</c>;
    /// that guard is hoisted to the callers (<see cref="CreateAsync"/>/<see cref="UpdateAsync"/>), so this
    /// helper is only invoked when auto-assignment is already enabled.
    /// </remarks>
    /// <param name="role">The role to auto-assign the portal's users to.</param>
    /// <param name="cancellationToken">Token used to observe cancellation requests.</param>
    private async Task AutoAssignUsersAsync(Role role, CancellationToken cancellationToken)
    {
        // MIGRATION: legacy AutoAssignUsers [L68-83] looped ALL portal users (UserController.GetUsers(PortalID, False)).
        // The paged repository surface is queried with one max-size page to enumerate all users.
        var (users, _) = await _userRepository.GetByPortalAsync(role.PortalID, 0, int.MaxValue, cancellationToken);
        foreach (var user in users)
        {
            try
            {
                // MIGRATION: legacy AddUserRole(PortalID, UserID, RoleID, Null.NullDate, Null.NullDate) — auto-assign carries NO expiry; this is a DIRECT add and deliberately BYPASSES the AddUserRoleAsync expiry algorithm (faithful to legacy, which called the simple AddUserRole, not UpdateUserRole).
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

    /// <summary>
    /// Gets a single role by its identifier, or <c>null</c> when no role exists.
    /// </summary>
    /// <param name="roleId">The unique identifier of the role.</param>
    /// <param name="cancellationToken">Token used to observe cancellation requests.</param>
    /// <returns>The mapped <see cref="RoleDto"/>, or <c>null</c> if the role was not found.</returns>
    public async Task<RoleDto?> GetByIdAsync(int roleId, CancellationToken cancellationToken = default)
    {
        // MIGRATION: legacy RoleController.GetRole [L163-165] delegated to provider.GetRole (reflection hydration); replaced by EF Core materialization behind IRoleRepository, then projected to a read DTO.
        var role = await _roleRepository.GetByIdAsync(roleId, cancellationToken);
        return role is null ? null : _mapper.Map<RoleDto>(role);
    }

    /// <summary>
    /// Gets all roles for a portal, ordered by role name (case-insensitive).
    /// </summary>
    /// <param name="portalId">The portal whose roles are listed.</param>
    /// <param name="cancellationToken">Token used to observe cancellation requests.</param>
    /// <returns>The portal's roles projected to <see cref="RoleDto"/> objects, name-ordered.</returns>
    public async Task<IEnumerable<RoleDto>> GetByPortalAsync(int portalId, CancellationToken cancellationToken = default)
    {
        var roles = await _roleRepository.GetByPortalAsync(portalId, cancellationToken);
        // MIGRATION: legacy RoleComparer [RoleComparer.vb:L55-57] ordered by RoleName case-insensitively (CurrentCulture) — preserved.
        var ordered = roles.OrderBy(r => r.RoleName ?? string.Empty, StringComparer.CurrentCultureIgnoreCase);
        return _mapper.Map<IEnumerable<RoleDto>>(ordered);
    }

    /// <summary>
    /// Creates a new role. Validates the request, persists the entity, auto-assigns the portal's users when
    /// the role enables auto-assignment, and returns the created role projected to a <see cref="RoleDto"/>.
    /// </summary>
    /// <param name="request">The role creation request.</param>
    /// <param name="cancellationToken">Token used to observe cancellation requests.</param>
    /// <returns>The created role as a <see cref="RoleDto"/>.</returns>
    /// <exception cref="ValidationException">Thrown when <paramref name="request"/> fails validation.</exception>
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

    /// <summary>
    /// Updates an existing role. Validates the request, loads the tracked entity, applies the mapped fields,
    /// persists, re-runs auto-assignment when enabled, and returns the refreshed <see cref="RoleDto"/>.
    /// </summary>
    /// <param name="request">The role update request.</param>
    /// <param name="cancellationToken">Token used to observe cancellation requests.</param>
    /// <returns>The updated role as a <see cref="RoleDto"/>.</returns>
    /// <exception cref="ValidationException">Thrown when <paramref name="request"/> fails validation.</exception>
    /// <exception cref="KeyNotFoundException">Thrown when the target role does not exist.</exception>
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

    /// <summary>
    /// Hard-deletes a role by identifier. No-ops when the role does not exist.
    /// </summary>
    /// <param name="roleId">The unique identifier of the role to delete.</param>
    /// <param name="cancellationToken">Token used to observe cancellation requests.</param>
    public async Task DeleteAsync(int roleId, CancellationToken cancellationToken = default)
    {
        var role = await _roleRepository.GetByIdAsync(roleId, cancellationToken);
        if (role is null)
        {
            return; // MIGRATION: legacy DeleteRole no-ops when the role is not found.
        }

        // MIGRATION: HARD-delete with transactional UserRole cascade handled by the repository.
        await _roleRepository.DeleteAsync(roleId, cancellationToken);
    }

    /// <summary>
    /// Adds (or refreshes) a user's membership of a role, computing the assignment's expiry date from the
    /// role's trial or billing schedule.
    /// </summary>
    /// <remarks>
    /// <para>
    /// MIGRATION: ports the non-<c>Cancel</c> branch of <c>RoleController.UpdateUserRole</c> [L489-557]
    /// verbatim. The order of the <c>&lt; now</c> resets, the <c>period == -1</c> (<c>Null.NullInteger</c>)
    /// short-circuit, and the no-default <c>switch</c> are all behaviorally significant and are deliberately
    /// NOT reordered or "cleaned up". Legacy <c>DateTime.Now</c> (server-local) is replaced with
    /// <c>DateTime.UtcNow</c> for container/timezone consistency.
    /// </para>
    /// <para>
    /// MIGRATION: <see cref="Role.TrialPeriod"/>/<see cref="Role.BillingPeriod"/> are nullable (<c>int?</c>) in
    /// the rewritten entity, where <c>null</c> carries the legacy <c>Null.NullInteger</c> ("unset") meaning;
    /// each is coalesced to the <c>nullInteger</c> (<c>-1</c>) sentinel so the <c>period == nullInteger</c>
    /// short-circuit below behaves exactly as the legacy <c>Integer</c> did (an unset period yields no expiry).
    /// </para>
    /// <para>
    /// MIGRATION (DEV-069): when <paramref name="requestedEffectiveDate"/> or <paramref name="requestedExpiryDate"/>
    /// is supplied (the legacy <c>SecurityRoles.ascx.vb</c> ADMIN assignment workflow, which called
    /// <c>RoleController.AddUserRole(PortalId, UserId, RoleId, EffectiveDate, ExpiryDate)</c>), the dates are used
    /// DIRECTLY and the trial/billing expiry schedule below is intentionally NOT consulted. When BOTH are null
    /// (the subscription path), the verbatim <c>UpdateUserRole</c> algorithm runs unchanged.
    /// <paramref name="notify"/> reproduces the legacy "notify user" checkbox and is a DOCUMENTED NO-OP — the
    /// bulk-email subsystem is out of scope (AAP 0.2.2), so the flag is accepted but no mail is sent.
    /// </para>
    /// </remarks>
    /// <param name="userId">The user to assign.</param>
    /// <param name="roleId">The role to assign the user to.</param>
    /// <param name="requestedEffectiveDate">Operator-supplied effective date (admin workflow); null = compute/none.</param>
    /// <param name="requestedExpiryDate">Operator-supplied expiry date (admin workflow); null = compute/none.</param>
    /// <param name="notify">Legacy notify-user flag; accepted but a documented NO-OP (no mail subsystem in scope).</param>
    /// <param name="cancellationToken">Token used to observe cancellation requests.</param>
    public async Task AddUserRoleAsync(
        int userId,
        int roleId,
        DateTime? requestedEffectiveDate = null,
        DateTime? requestedExpiryDate = null,
        bool notify = false,
        CancellationToken cancellationToken = default)
    {
        // MIGRATION (DEV-069): `notify` reproduces the legacy SecurityRoles "Notify user" checkbox. The
        // newsletter/bulk-email subsystem is explicitly OUT OF SCOPE (AAP 0.2.2), so there is no mail transport
        // to invoke; the flag is accepted and recorded as a DOCUMENTED NO-OP rather than silently dropped from
        // the contract. See MIGRATION_NOTES.md DEV-069.
        _ = notify;

        // Load any pre-existing assignment ONCE; reused by both the explicit-date (admin) path and the
        // trial/billing auto-compute path below. (Hoisted up from its original position inside the
        // auto-compute block so the admin path can reuse it without a second query.)
        var existing = (await _roleRepository.GetUserRolesAsync(userId, cancellationToken))
            .FirstOrDefault(ur => ur.RoleID == roleId);

        // MIGRATION (DEV-069): an explicit Effective/Expiry date marks the legacy SecurityRoles ADMIN
        // assignment workflow — RoleController.AddUserRole(PortalId, UserId, RoleId, EffectiveDate, ExpiryDate)
        // — a DIRECT add/update with the operator-supplied dates that does NOT consult the trial/billing expiry
        // schedule. The subscription path (no dates supplied) preserves the legacy UpdateUserRole expiry
        // algorithm verbatim in the block below this short-circuit. The add-vs-update split mirrors the legacy
        // GetUserRole/UserRoleId check (update an existing membership in place; insert a new one).
        if (requestedEffectiveDate.HasValue || requestedExpiryDate.HasValue)
        {
            if (existing is not null)
            {
                existing.EffectiveDate = requestedEffectiveDate;
                existing.ExpiryDate = requestedExpiryDate;
                await _roleRepository.UpdateUserRoleAsync(existing, cancellationToken);
            }
            else
            {
                await _roleRepository.AddUserRoleAsync(
                    new UserRole { UserID = userId, RoleID = roleId, EffectiveDate = requestedEffectiveDate, ExpiryDate = requestedExpiryDate },
                    cancellationToken);
            }
            return;
        }

        // MIGRATION: ports RoleController.UpdateUserRole non-Cancel branch [L489-557] verbatim; legacy DateTime.Now (server-local) → UtcNow for container/timezone consistency.
        var now = DateTime.UtcNow;
        const int nullInteger = -1;            // MIGRATION: Null.NullInteger
        DateTime? effectiveDate = null;        // MIGRATION: Null.NullDate (DateTime.MinValue) → null
        DateTime? expiryDate = now;
        bool isTrialUsed = false;
        int period = 0;
        string frequency = string.Empty;

        if (existing is not null)
        {
            effectiveDate = existing.EffectiveDate;
            expiryDate = existing.ExpiryDate;
            isTrialUsed = existing.IsTrialUsed;
        }

        var role = await _roleRepository.GetByIdAsync(roleId, cancellationToken);
        if (role is not null)
        {
            // MIGRATION: legacy `role.TrialFrequency.ToString() <> "N"` NREs when TrialFrequency is null; guard null/empty and treat as billing.
            if (!isTrialUsed && !string.IsNullOrEmpty(role.TrialFrequency) && role.TrialFrequency != "N")
            {
                // MIGRATION: Role.TrialPeriod is nullable (int?); a null = legacy Null.NullInteger ("unset"), so coalesce to nullInteger (-1) to keep the `period == nullInteger` short-circuit faithful. Coalescing to 0 would WRONGLY fall through to the frequency switch and compute a real expiry.
                period = role.TrialPeriod ?? nullInteger;
                frequency = role.TrialFrequency!;
            }
            else
            {
                // MIGRATION: Role.BillingPeriod is nullable (int?); a null = legacy Null.NullInteger ("unset"), so coalesce to nullInteger (-1), preserving the `period == nullInteger` short-circuit.
                period = role.BillingPeriod ?? nullInteger;
                frequency = role.BillingFrequency ?? string.Empty;
            }
        }

        if (effectiveDate.HasValue && effectiveDate.Value < now)
        {
            effectiveDate = null;              // MIGRATION: Null.NullDate
        }
        if (expiryDate.HasValue && expiryDate.Value < now)
        {
            expiryDate = now;
        }

        if (period == nullInteger)
        {
            expiryDate = null;                 // MIGRATION: Null.NullDate
        }
        else
        {
            var baseDate = expiryDate ?? now;
            switch (frequency)
            {
                case "N":
                    expiryDate = null;
                    break;
                case "O":
                    expiryDate = new DateTime(9999, 12, 31);
                    break;
                case "D":
                    expiryDate = baseDate.AddDays(period);
                    break;
                case "W":
                    expiryDate = baseDate.AddDays(period * 7);
                    break;
                case "M":
                    expiryDate = baseDate.AddMonths(period);
                    break;
                case "Y":
                    expiryDate = baseDate.AddYears(period);
                    break;
                    // MIGRATION (PORTED BUG BUG-002): legacy had no Case Else — an unmatched frequency leaves ExpiryDate unchanged (a stale/now expiry rather than no-expiry). Reproduced verbatim per the Minimal Change Clause.
            }
        }

        // MIGRATION (M2/DEV-034): preserve the legacy add-vs-update split from UpdateUserRole
        // (UserRoleId <> -1 ? provider.UpdateUserRole(userId, roleId, ExpiryDate) : provider.AddUserRole(...)).
        // An EXISTING assignment is UPDATED in place (its loaded EffectiveDate/IsTrialUsed are retained, only
        // ExpiryDate is recomputed) via UpdateUserRoleAsync — NOT routed through the insert-only AddUserRoleAsync,
        // which previously risked a duplicate join row / relied on undocumented upsert behavior. A NEW assignment
        // is INSERTED via AddUserRoleAsync.
        if (existing is not null)
        {
            existing.ExpiryDate = expiryDate;
            await _roleRepository.UpdateUserRoleAsync(existing, cancellationToken);
        }
        else
        {
            await _roleRepository.AddUserRoleAsync(
                new UserRole { UserID = userId, RoleID = roleId, EffectiveDate = effectiveDate, ExpiryDate = expiryDate },
                cancellationToken);
        }
    }

    /// <summary>
    /// Gets all roles a user is a member of.
    /// </summary>
    /// <param name="userId">The user whose role memberships are listed.</param>
    /// <param name="cancellationToken">Token used to observe cancellation requests.</param>
    /// <returns>The roles the user belongs to, projected to <see cref="RoleDto"/> objects.</returns>
    public async Task<IEnumerable<RoleDto>> GetUserRolesAsync(int userId, CancellationToken cancellationToken = default)
    {
        var userRoles = await _roleRepository.GetUserRolesAsync(userId, cancellationToken);
        // MIGRATION: project each join row's Role navigation; skip rows whose Role nav is null.
        var roles = userRoles.Where(ur => ur.Role is not null).Select(ur => ur.Role!);
        return _mapper.Map<IEnumerable<RoleDto>>(roles);
    }

    /// <summary>
    /// Gets all users assigned to a role.
    /// </summary>
    /// <param name="roleId">The role whose member users are listed.</param>
    /// <param name="cancellationToken">Token used to observe cancellation requests.</param>
    /// <returns>The users assigned to the role, projected to <see cref="UserDto"/> objects.</returns>
    public async Task<IEnumerable<UserDto>> GetUsersInRoleAsync(int roleId, CancellationToken cancellationToken = default)
    {
        var users = await _roleRepository.GetUsersInRoleAsync(roleId, cancellationToken);
        return _mapper.Map<IEnumerable<UserDto>>(users);
    }

    /// <summary>
    /// Removes a user from a role, refusing to remove protected administrator/registered-role assignments.
    /// </summary>
    /// <remarks>
    /// MIGRATION: reproduces the legacy <c>CanRemoveUserFromRole</c> guard [L741-746/L764-769] exactly. The
    /// legacy <c>DeleteUserRole</c> returned <c>False</c> silently when removal was disallowed [L330-347]; here
    /// the disallowed case THROWS so the API surfaces an RFC 7807 response, consistent with the other guard
    /// modernizations. The legacy optional <c>SendNotification</c> email [L577-610] is OMITTED
    /// (Mail/Localization/Profile are out of scope for Phase 1).
    /// </remarks>
    /// <param name="userId">The user to remove.</param>
    /// <param name="roleId">The role to remove the user from.</param>
    /// <param name="cancellationToken">Token used to observe cancellation requests.</param>
    /// <exception cref="InvalidOperationException">Thrown when the assignment is protected and cannot be removed.</exception>
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
            // MIGRATION: CanRemoveUserFromRole = Not ((AdministratorId = UserId And AdministratorRoleId = RoleId) Or RegisteredRoleId = RoleId).
            // MIGRATION: Portal.AdministratorId/AdministratorRoleId/RegisteredRoleId are nullable (int?); a null field yields false in the lifted == comparison, so an unset administrator/registered role does not protect the assignment (it remains removable) — a safe superset of the legacy non-null semantics.
            var cannotRemove =
                (portal.AdministratorId == userId && portal.AdministratorRoleId == roleId)
                || portal.RegisteredRoleId == roleId;
            if (cannotRemove)
            {
                // MIGRATION: legacy DeleteUserRole returned False silently [L330-347]; we throw so the API surfaces RFC 7807 (consistent with the other guard modernizations).
                throw new InvalidOperationException("Cannot remove this user from the role.");
            }
        }

        await _roleRepository.RemoveUserRoleAsync(userId, roleId, cancellationToken);
        // MIGRATION: legacy optional SendNotification email [L577-610] OMITTED (Mail/Localization/Profile out of scope; no email port; interface carries no notify flag).
    }
}
