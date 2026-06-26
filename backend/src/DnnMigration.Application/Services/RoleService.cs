using AutoMapper;
using DnnMigration.Application.DTOs.Role;
using DnnMigration.Application.DTOs.Common;
using DnnMigration.Application.Interfaces;
using DnnMigration.Domain.Entities;
using DnnMigration.Domain.Interfaces;
using DnnMigration.Domain.Common;

namespace DnnMigration.Application.Services;

/// <summary>
/// Application-layer service that holds the Role/Security business rules. Consumed by the
/// Api <c>RolesController</c> via constructor injection; it orchestrates the role repository,
/// the user repository (for auto-assignment), and the unit of work, and projects Domain
/// entities to DTOs through AutoMapper. Raw <see cref="Role"/>/<see cref="UserRole"/> entities
/// are never returned (DTO-only contract, AAP §0.7.7).
/// </summary>
// MIGRATION: Converted from VB.NET DotNetNuke.Security.Roles.RoleController
// (Library/Components/Security/Roles/RoleController.vb, 892 lines). Business rules are extracted
// verbatim (AAP §0.7.2): they are NOT optimized, refactored, or "improved" during conversion.
// Data access moves to IRoleRepository/IUserRepository; transaction boundaries move to IUnitOfWork.
// The legacy reflection-instantiated RoleProvider singleton (provider.* calls) is replaced by the
// constructor-injected repositories below (Provider rule, AAP §0.1.3).
//
// MIGRATION (multi-tenant isolation, AAP §0.7.1): every operation that spans more than one entity
// (the duplicate-name guard and AutoAssignUsers) is scoped by request.PortalId, preserving the DNN
// PortalId tenant discriminator so portals remain independent sites.
public sealed class RoleService : IRoleService
{
    private readonly IRoleRepository _roleRepository;
    private readonly IUserRepository _userRepository;
    private readonly IPortalRepository _portalRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="RoleService"/> class.
    /// </summary>
    /// <param name="roleRepository">Role persistence gateway (portal-scoped reads/writes).</param>
    /// <param name="userRepository">User persistence gateway; required by AutoAssignUsers on create/update.</param>
    /// <param name="portalRepository">Portal persistence gateway; supplies the Administrator/Registered system-role ids.</param>
    /// <param name="unitOfWork">Unit-of-work boundary that commits pending changes.</param>
    /// <param name="mapper">AutoMapper instance projecting entities to/from DTOs.</param>
    // MIGRATION: Replaces the legacy `Private Shared provider As RoleProvider = RoleProvider.Instance()`
    // reflection singleton (RoleController.vb L53) with built-in Microsoft DI (AAP §0.3.3).
    // MIGRATION: CP1 review (RoleService #1): fail-fast null guards on every dependency so DI misconfiguration surfaces
    // at construction rather than later as a NullReferenceException — consistent with the other Application services.
    public RoleService(
        IRoleRepository roleRepository,
        IUserRepository userRepository,
        IPortalRepository portalRepository,
        IUnitOfWork unitOfWork,
        IMapper mapper)
    {
        ArgumentNullException.ThrowIfNull(roleRepository);
        ArgumentNullException.ThrowIfNull(userRepository);
        ArgumentNullException.ThrowIfNull(portalRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(mapper);

        _roleRepository = roleRepository;
        _userRepository = userRepository;
        _portalRepository = portalRepository;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    /// <inheritdoc />
    // MIGRATION: RoleController.GetPortalRoles (RoleController.vb L146) returned ALL portal roles
    // (provider.GetRoles(PortalId), cache-aside) as an ArrayList. PageIndex is ZERO-BASED (matches the legacy DNN
    // paging convention preserved on PagedResult). GetRolesByGroup / GetRoles() (all-portals) / RoleGroup CRUD are NOT
    // on IRoleService and are deferred (see the deferral block at the end of this class).
    public async Task<Result<PagedResult<RoleResponse>>> GetByPortalAsync(
        int portalId,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        // MIGRATION: CP1 review (RoleService #2) — validate paging inputs BEFORE any repository access; a negative page
        // index or non-positive page size is a controlled validation failure (Api -> 400), never an invalid repo call.
        if (pageIndex < 0)
        {
            return Result<PagedResult<RoleResponse>>.Failure("Page index must be zero or greater.");
        }

        if (pageSize <= 0)
        {
            return Result<PagedResult<RoleResponse>>.Failure("Page size must be greater than zero.");
        }

        // MIGRATION: CP1 review (performance #22) — use the paged repository so ONLY the requested page plus the total
        // count is materialized (no fetch-all-then-page-in-memory). PageIndex stays zero-based for behavioral parity.
        var (roles, total) = await _roleRepository.GetByPortalPagedAsync(portalId, pageIndex, pageSize);

        var pageItems = roles
            .Select(r => _mapper.Map<RoleResponse>(r))
            .ToList();

        return Result<PagedResult<RoleResponse>>.Success(new PagedResult<RoleResponse>
        {
            Items = pageItems,
            TotalCount = total,
            PageIndex = pageIndex,
            PageSize = pageSize
        });
    }

    /// <inheritdoc />
    // MIGRATION: RoleController.GetRole(RoleID, PortalID) (RoleController.vb L163) returned the RoleInfo or Nothing.
    // CP1 review (IRoleService #1 / IRoleRepository #1) — PORTAL-SCOPED: the lookup is constrained to portalId so a
    // role from another portal is never returned (multi-tenant isolation, AAP §0.7.1). `Is Nothing` -> `is null`; a
    // missing/unowned role is an expected failure (Api -> 404). CP1 review #7 — the message is OPAQUE (no raw id).
    public async Task<Result<RoleResponse>> GetByIdAsync(
        int portalId,
        int roleId,
        CancellationToken cancellationToken = default)
    {
        var role = await _roleRepository.GetByIdAsync(portalId, roleId);
        if (role is null)
        {
            return Result<RoleResponse>.Failure("The requested role was not found.");
        }

        return Result<RoleResponse>.Success(_mapper.Map<RoleResponse>(role));
    }

    /// <inheritdoc />
    // MIGRATION: RoleController.AddRole(objRoleInfo) (RoleController.vb L100) created the role
    // (provider.CreateRole) and, on success, ran AutoAssignUsers (L106). Both behaviors are preserved
    // below. Unexpected failures bubble to the Api ExceptionHandlingMiddleware; expected business
    // failures (duplicate name) return Result.Failure.
    public async Task<Result<RoleResponse>> CreateAsync(
        CreateRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        // MIGRATION: RoleController.AddRole rejected a duplicate role name with the message
        // "A role with the same name already exists. The role was not added." (preserved verbatim,
        // AAP §0.7.1). The guard is portal-scoped (multi-tenant isolation) and case-insensitive, matching
        // the legacy VB string comparison semantics (StringComparison.OrdinalIgnoreCase).
        var existing = await _roleRepository.GetByPortalIdAsync(request.PortalId);
        if (existing.Any(r => string.Equals(r.RoleName, request.RoleName, StringComparison.OrdinalIgnoreCase)))
        {
            return Result<RoleResponse>.Failure("A role with the same name already exists. The role was not added.");
        }

        var role = _mapper.Map<Role>(request);

        // MIGRATION: EditRoles.ascx.vb cmdUpdate_Click (L212-L230) — billing/trial field GROUPING + defaults, ported
        // VERBATIM (CP1 review RoleService #3 / AAP §0.7.2). Replaces the previous partial frequency-only defaulting.
        // Applied on create AND update (legacy ran it on every cmdUpdate before AddRole/UpdateRole).
        ApplyBillingTrialDefaults(role);

        await _roleRepository.AddAsync(role);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // MIGRATION: RoleController.AddRole ran AutoAssignUsers (RoleController.vb L106) after creating the role.
        // Ported via the shared AutoAssignUsersAsync helper (legacy AutoAssignUsers, RoleController.vb L68): when
        // AutoAssignment is true, the new role is assigned to every portal user. On create no user can already hold the
        // freshly created role, so the helper's duplicate-skip is a no-op here (legacy swallowed the equivalent
        // duplicate error inside its loop).
        await AutoAssignUsersAsync(role, request.PortalId, cancellationToken);

        return Result<RoleResponse>.Success(_mapper.Map<RoleResponse>(role));
    }

    /// <inheritdoc />
    // MIGRATION: RoleController.UpdateRole(objRoleInfo) (RoleController.vb L254-L257) persisted the role
    // (provider.UpdateRole) and THEN ran AutoAssignUsers. CP1 review (IRoleService #1 / RoleService #3, #4, #5) —
    // PORTAL-SCOPED, with the system-role guard, billing/trial grouping, and AutoAssignUsers-on-update all restored.
    public async Task<Result<RoleResponse>> UpdateAsync(
        int portalId,
        int roleId,
        UpdateRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        // MIGRATION (CP1 review RoleService — guard clauses): fail-fast null guard before any request field is read.
        ArgumentNullException.ThrowIfNull(request);

        // MIGRATION: CP1 review (IRoleService #1) — PORTAL-SCOPED lookup so a role from another portal can never be
        // updated through this tenant (multi-tenant isolation, AAP §0.7.1). Missing/unowned -> opaque failure (#7).
        var role = await _roleRepository.GetByIdAsync(portalId, roleId);
        if (role is null)
        {
            return Result<RoleResponse>.Failure("The requested role was not found.");
        }

        // MIGRATION: CP1 review (RoleService #5, CRITICAL) — SYSTEM-ROLE guard. Legacy EditRoles.ascx.vb L174-176 hid
        // BOTH cmdUpdate and cmdDelete when RoleID = PortalSettings.AdministratorRoleId Or RoleID =
        // PortalSettings.RegisteredRoleId, so the Administrators and Registered Users roles could never be edited or
        // deleted. Enforced at the service boundary (the legacy UI hide carried no runtime string, so a clear
        // equivalent message is surfaced).
        var systemRoleGuard = await GuardSystemRoleAsync(portalId, roleId, "modified");
        if (systemRoleGuard is not null)
        {
            return Result<RoleResponse>.Failure(systemRoleGuard);
        }

        // MIGRATION: project the editable fields onto the tracked entity. The mapper Ignores RoleId and
        // PortalId (keys/tenant scope are immutable on update — see RoleProfile.cs).
        _mapper.Map(request, role);

        // MIGRATION: route id is authoritative (PUT /api/roles/{id}); reassert it after mapping so the
        // persisted identity always matches the route, never the payload.
        role.RoleId = roleId;

        // MIGRATION: EditRoles.ascx.vb cmdUpdate_Click (L212-L230) ran the billing/trial GROUPING + defaults on every
        // update before UpdateRole (CP1 review RoleService #3) — applied here for exact create/update parity.
        ApplyBillingTrialDefaults(role);

        await _roleRepository.UpdateAsync(role);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // MIGRATION: CP1 review (RoleService #4) — RoleController.UpdateRole (L256) called AutoAssignUsers AFTER
        // provider.UpdateRole, so toggling AutoAssignment ON during an edit enrolls all existing portal users. Restored
        // via the shared helper; the duplicate-skip preserves the legacy "user already belongs to role" swallow so
        // users that already hold the role are not duplicated.
        await AutoAssignUsersAsync(role, portalId, cancellationToken);

        return Result<RoleResponse>.Success(_mapper.Map<RoleResponse>(role));
    }

    /// <inheritdoc />
    // MIGRATION: RoleController.DeleteRole(RoleId, PortalId) (RoleController.vb L125) fetched the role and,
    // if it existed (`Not objRole Is Nothing`), called provider.DeleteRole. A missing role becomes an
    // expected business failure (Result.Failure).
    //
    // MIGRATION: the legacy delete path also cleared role-related security caches and removed the
    // associated UserRole rows (DataProvider.DeleteUserRoles cascade). Here UserRole row cleanup relies on
    // the EF Core cascade configured in Infrastructure; cache clearing is OUT OF SCOPE (the DNN caching
    // subsystem is excluded by AAP §0.6.2).
    public async Task<Result> DeleteAsync(
        int portalId,
        int roleId,
        CancellationToken cancellationToken = default)
    {
        // MIGRATION: CP1 review (IRoleService #1 / RoleService #6) — PORTAL-SCOPED lookup so a role from another portal
        // can never be deleted through this tenant (multi-tenant isolation, AAP §0.7.1). Missing/unowned -> opaque (#7).
        var role = await _roleRepository.GetByIdAsync(portalId, roleId);
        if (role is null)
        {
            return Result.Failure("The requested role was not found.");
        }

        // MIGRATION: CP1 review (RoleService #5, CRITICAL) — SYSTEM-ROLE guard. EditRoles.ascx.vb L174-176 hid cmdDelete
        // for RoleID = AdministratorRoleId Or RegisteredRoleId, so the two system roles can never be deleted.
        var systemRoleGuard = await GuardSystemRoleAsync(portalId, roleId, "deleted");
        if (systemRoleGuard is not null)
        {
            return Result.Failure(systemRoleGuard);
        }

        await _roleRepository.DeleteAsync(portalId, roleId);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    /// <inheritdoc />
    // MIGRATION: READ-ONLY projection of RoleController.GetUserRoles(PortalId, UserId)
    // (RoleController.vb L392, provider.GetUserRoles(..., includePrivate:=True)). CP1 review (IRoleService #1) —
    // PORTAL-SCOPED: the legacy query carried PortalId, so the lookup is constrained to portalId. Backed by
    // IRoleRepository.GetUserRolesAsync(int portalId, int userId) and flattened to UserRoleDto by RoleProfile
    // (RoleName from the Role navigation; Username/DisplayName from the User navigation — all null-guarded).
    public async Task<Result<IEnumerable<UserRoleDto>>> GetUserRolesAsync(
        int portalId,
        int userId,
        CancellationToken cancellationToken = default)
    {
        var userRoles = await _roleRepository.GetUserRolesAsync(portalId, userId);
        var dtos = userRoles
            .Select(ur => _mapper.Map<UserRoleDto>(ur))
            .ToList();

        return Result<IEnumerable<UserRoleDto>>.Success(dtos);
    }

    // =================================================================================================
    // Private helpers — exact ports of legacy Role billing/trial defaulting, auto-assignment, and the
    // system-role guard. Shared by CreateAsync/UpdateAsync/DeleteAsync to guarantee create/update parity.
    // =================================================================================================

    // MIGRATION: EditRoles.ascx.vb cmdUpdate_Click (L212-L230) — billing/trial GROUPING + defaults applied on every
    // create AND update before persistence (CP1 review RoleService #3). The legacy Web Forms code used empty-textbox
    // tests (txtServiceFee.Text <> "" etc.) to decide whether a COMPLETE billing/trial group was supplied; the
    // migrated DTO carries typed `float ServiceFee` / `int BillingPeriod` with no "empty" concept, so the frequency
    // code ("N" = none) is the faithful discriminator for "no group supplied". The reset values mirror the legacy
    // defaults exactly: ServiceFee/TrialFee = 0, BillingPeriod/TrialPeriod = 1, Frequency = "N".
    private static void ApplyBillingTrialDefaults(Role role)
    {
        // MIGRATION: BILLING group (legacy L212-L218). Legacy initialized sglServiceFee=0, intBillingPeriod=1,
        // strBillingFrequency="N" and overwrote them only inside
        //   `If txtServiceFee.Text <> "" And txtBillingPeriod.Text <> "" And cboBillingFrequency.SelectedItem.Value <> "N"`.
        // Absent/"N" frequency => not a complete group => reset to the legacy defaults.
        if (string.IsNullOrEmpty(role.BillingFrequency) || role.BillingFrequency == "N")
        {
            role.ServiceFee = 0f;
            role.BillingPeriod = 1;
            role.BillingFrequency = "N";
        }

        // MIGRATION: TRIAL group (legacy L220-L230) — evaluated AFTER billing because the legacy guard
        //   `If sglServiceFee <> 0 And txtTrialFee.Text <> "" And txtTrialPeriod.Text <> "" And cboTrialFrequency.SelectedItem.Value <> "N"`
        // depends on the FINAL service fee. The `role.ServiceFee == 0f` test faithfully mirrors the legacy
        // `sglServiceFee <> 0` Single comparison (Option Strict On — an exact value test, no tolerance).
        if (role.ServiceFee == 0f || string.IsNullOrEmpty(role.TrialFrequency) || role.TrialFrequency == "N")
        {
            role.TrialFee = 0f;
            role.TrialPeriod = 1;
            role.TrialFrequency = "N";
        }
    }

    // MIGRATION: RoleController.AutoAssignUsers (RoleController.vb L68-L83) — when a role has AutoAssignment = True,
    // every existing portal user is enrolled in the role (CP1 review RoleService #4). Legacy iterated
    // UserController.GetUsers(PortalID, False) and called AddUserRole(...) inside a Try/Catch that SWALLOWED the
    // "user already belongs to role" exception. Here the duplicate is avoided proactively (skip when the user already
    // holds the role), reaching the identical committed end state without relying on an exception swallow.
    private async Task AutoAssignUsersAsync(Role role, int portalId, CancellationToken cancellationToken)
    {
        // MIGRATION: legacy guarded the whole loop with `If objRoleInfo.AutoAssignment Then`. A non-auto-assign role
        // is a no-op (and on CREATE the role is brand-new so the skip below never triggers — nothing to de-dupe).
        if (!role.AutoAssignment)
        {
            return;
        }

        // MIGRATION: legacy fetched the portal user list via UserController.GetUsers(PortalID, False). The
        // portal-scoped read preserves multi-tenant isolation (AAP §0.7.1) — only users of this portal are enrolled.
        var users = await _userRepository.GetByPortalIdAsync(portalId);

        var changed = false;
        foreach (var user in users)
        {
            // MIGRATION: duplicate-swallow parity — legacy AddUserRole threw (and was caught) when the user already
            // held the role. Skipping pre-existing membership reaches the same result without the exception.
            if (user.UserRoles.Any(ur => ur.RoleId == role.RoleId))
            {
                continue;
            }

            // MIGRATION: legacy AddUserRole(PortalID, UserID, RoleID, Null.NullDate, Null.NullDate) — the two
            // Null.NullDate arguments are the effective/expiry dates, mapped to null per the migration's
            // sentinel->nullable convention (Section 0.5.1).
            user.UserRoles.Add(new UserRole
            {
                UserId = user.UserId,
                RoleId = role.RoleId,
                EffectiveDate = null,
                ExpiryDate = null
            });

            await _userRepository.UpdateAsync(user);
            changed = true;
        }

        // MIGRATION: a single SaveChanges commits all enrollments (legacy AddUserRole persisted per call; batching
        // here is an EF Core unit-of-work detail that yields the same committed rows).
        if (changed)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    // MIGRATION: EditRoles.ascx.vb (L174-L176) — when RoleID = PortalSettings.AdministratorRoleId Or
    // RoleID = PortalSettings.RegisteredRoleId the UI HID BOTH cmdUpdate and cmdDelete, so the two system roles could
    // never be modified or deleted (CP1 review RoleService #5, CRITICAL). The legacy UI carried no runtime message (it
    // simply removed the buttons); a clear equivalent message is surfaced here so the API fails closed with an
    // explanation. Returns null when the role is NOT a system role (the operation may proceed). The portal lookup is
    // already portal-scoped (Portal is the tenant root), so this also reinforces tenant ownership (CP1 review #6).
    private async Task<string?> GuardSystemRoleAsync(int portalId, int roleId, string action)
    {
        var portal = await _portalRepository.GetByIdAsync(portalId);
        if (portal is not null && (roleId == portal.AdministratorRoleId || roleId == portal.RegisteredRoleId))
        {
            return $"System roles (Administrators and Registered Users) cannot be {action}.";
        }

        return null;
    }

    // -------------------------------------------------------------------------------------------------
    // MIGRATION (DEFERRED — documented for traceability, AAP §0.7.2; intentionally NOT implemented).
    // The following RoleController.vb rules are NOT migrated in this phase because they are not on the
    // IRoleService contract and have no repository support (IRoleRepository exposes only the read
    // GetUserRolesAsync — there is no user-role write method, and no AssignUserRoleRequest DTO exists).
    // Recorded here so the behavior is captured even though the code is absent.
    //
    // NOTE: UpdateRole re-assignment-on-update (RoleController.vb L254-L257) is NO LONGER deferred — it was
    // restored in CP1 review (RoleService #4). UpdateAsync now calls AutoAssignUsersAsync after persistence,
    // and AutoAssignUsers is preserved on create as well (see AutoAssignUsersAsync below). The remaining
    // bullets are user-role WRITE operations that genuinely have no contract/repository/DTO support yet.
    //
    //  • AddUserRole (RoleController.vb L277 / L295) and DeleteUserRole (RoleController.vb L330):
    //    user-role WRITE operations (assign/remove a single user to/from a role). Deferred — no contract
    //    method, no repository write method, no request DTO.
    //
    //  • CanRemoveUserFromRole (RoleController.vb L764): guard enforcing that the portal Administrator
    //    cannot be removed from the Administrators role, and that NO user can be removed from the
    //    Registered-Users role — i.e.
    //    Return Not ((AdministratorId = UserId And AdministratorRoleId = RoleId) Or RegisteredRoleId = RoleId).
    //    Deferred together with DeleteUserRole (the only caller of this guard).
    //
    //  • UpdateUserRole subscription/expiry frequency codes (RoleController.vb L489-L557): on Cancel,
    //    expire the role (ExpiryDate = yesterday) when ServiceFee > 0 AndAlso IsTrialUsed, else delete it;
    //    otherwise compute ExpiryDate from the trial/billing Period + Frequency code —
    //    N -> Null date; O -> DateTime(9999,12,31); D -> AddDays(Period); W -> AddDays(Period*7);
    //    M -> AddMonths(Period); Y -> AddYears(Period); and Period = Null.NullInteger (-1) -> Null date.
    //    Deferred — depends on the absent user-role write surface.
    //
    //  • RoleGroup CRUD (RoleController.vb L626+: AddRoleGroup / DeleteRoleGroup / GetRoleGroup /
    //    GetRoleGroups, Public Shared) and GetRolesByGroup (L224): role-group management is out of the
    //    Roles CRUD resource surface (AAP §0.3.4) and is deferred.
    //
    //  • SendNotification (RoleController.vb L577): role assignment/unassignment email — depends on the
    //    DNN Mail/Localization subsystems, which are excluded by AAP §0.6.2.
    // -------------------------------------------------------------------------------------------------
}
