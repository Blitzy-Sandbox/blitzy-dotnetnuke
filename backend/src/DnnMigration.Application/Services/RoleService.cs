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
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="RoleService"/> class.
    /// </summary>
    /// <param name="roleRepository">Role persistence gateway (portal-scoped reads/writes).</param>
    /// <param name="userRepository">User persistence gateway; required by AutoAssignUsers on create.</param>
    /// <param name="unitOfWork">Unit-of-work boundary that commits pending changes.</param>
    /// <param name="mapper">AutoMapper instance projecting entities to/from DTOs.</param>
    // MIGRATION: Replaces the legacy `Private Shared provider As RoleProvider = RoleProvider.Instance()`
    // reflection singleton (RoleController.vb L53) with built-in Microsoft DI (AAP §0.3.3).
    public RoleService(
        IRoleRepository roleRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        IMapper mapper)
    {
        _roleRepository = roleRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    /// <inheritdoc />
    // MIGRATION: RoleController.GetPortalRoles (RoleController.vb L146) returned ALL portal roles
    // (provider.GetRoles(PortalId), cache-aside) as an ArrayList; paging is applied in-memory here to
    // satisfy the PagedResult contract. PageIndex is ZERO-BASED (matches the legacy DNN paging
    // convention preserved on PagedResult). GetRolesByGroup / GetRoles() (all-portals) / RoleGroup CRUD
    // are NOT on IRoleService and are deferred (see the deferral block at the end of this class).
    public async Task<Result<PagedResult<RoleResponse>>> GetByPortalAsync(
        int portalId,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var roles = (await _roleRepository.GetByPortalIdAsync(portalId)).ToList();
        var total = roles.Count;

        var pageItems = roles
            .Skip(pageIndex * pageSize)
            .Take(pageSize)
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
    // MIGRATION: RoleController.GetRole(RoleID, PortalID) (RoleController.vb L163) returned the RoleInfo
    // or Nothing. `Is Nothing` -> `is null`; a missing role becomes an expected business failure
    // (Result.Failure) rather than a null return, so the Api can map it to 404.
    public async Task<Result<RoleResponse>> GetByIdAsync(
        int roleId,
        CancellationToken cancellationToken = default)
    {
        var role = await _roleRepository.GetByIdAsync(roleId);
        if (role is null)
        {
            return Result<RoleResponse>.Failure($"Role {roleId} was not found.");
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

        // MIGRATION: RoleController.AddRole billing/trial frequency defaulted to "N" (none) when
        // unspecified, signalling no recurring billing. ServiceFee/TrialFee/BillingPeriod/TrialPeriod are
        // left exactly as provided (faithful legacy coherence). `&`/Is Nothing checks -> IsNullOrEmpty.
        if (string.IsNullOrEmpty(role.BillingFrequency))
        {
            role.BillingFrequency = "N";
        }

        if (string.IsNullOrEmpty(role.TrialFrequency))
        {
            role.TrialFrequency = "N";
        }

        await _roleRepository.AddAsync(role);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // MIGRATION: RoleController.AutoAssignUsers (RoleController.vb L68) — when AutoAssignment is true,
        // For Each portal user: AddUserRole(PortalID, UserID, RoleID, Null.NullDate, Null.NullDate).
        // Implemented here via the User.UserRoles navigation because IRoleRepository exposes no user-role
        // write method and the Role entity has no UserRoles navigation; EF persists the generated join
        // rows on SaveChanges. Null.NullDate -> null effective/expiry dates (preserved). The legacy code
        // swallowed "user already belongs to role" duplicate errors inside the loop; with a freshly created
        // role no such duplicates can exist, so the empty-catch is intentionally not reproduced.
        if (role.AutoAssignment)
        {
            var portalUsers = await _userRepository.GetByPortalIdAsync(request.PortalId);
            foreach (var user in portalUsers)
            {
                user.UserRoles.Add(new UserRole
                {
                    UserId = user.UserId,
                    RoleId = role.RoleId,
                    EffectiveDate = null,
                    ExpiryDate = null
                });
                await _userRepository.UpdateAsync(user);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Result<RoleResponse>.Success(_mapper.Map<RoleResponse>(role));
    }

    /// <inheritdoc />
    // MIGRATION: RoleController.UpdateRole(objRoleInfo) (RoleController.vb L254) persisted the role
    // (provider.UpdateRole). A missing role becomes an expected business failure (Result.Failure).
    public async Task<Result<RoleResponse>> UpdateAsync(
        int roleId,
        UpdateRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        var role = await _roleRepository.GetByIdAsync(roleId);
        if (role is null)
        {
            return Result<RoleResponse>.Failure($"Role {roleId} was not found.");
        }

        // MIGRATION: project the editable fields onto the tracked entity. The mapper Ignores RoleId and
        // PortalId (keys/tenant scope are immutable on update — see RoleProfile.cs).
        _mapper.Map(request, role);

        // MIGRATION: route id is authoritative (PUT /api/roles/{id}); reassert it after mapping so the
        // persisted identity always matches the route, never the payload.
        role.RoleId = roleId;

        await _roleRepository.UpdateAsync(role);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

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
        int roleId,
        CancellationToken cancellationToken = default)
    {
        var role = await _roleRepository.GetByIdAsync(roleId);
        if (role is null)
        {
            return Result.Failure($"Role {roleId} was not found.");
        }

        await _roleRepository.DeleteAsync(roleId);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    /// <inheritdoc />
    // MIGRATION: READ-ONLY projection of RoleController.GetUserRoles(PortalId, UserId)
    // (RoleController.vb L392, provider.GetUserRoles(..., includePrivate:=True)). Backed by
    // IRoleRepository.GetUserRolesAsync(int userId) and flattened to UserRoleDto by RoleProfile
    // (RoleName from the Role navigation; Username/DisplayName from the User navigation — all null-guarded).
    public async Task<Result<IEnumerable<UserRoleDto>>> GetUserRolesAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        var userRoles = await _roleRepository.GetUserRolesAsync(userId);
        var dtos = userRoles
            .Select(ur => _mapper.Map<UserRoleDto>(ur))
            .ToList();

        return Result<IEnumerable<UserRoleDto>>.Success(dtos);
    }

    // -------------------------------------------------------------------------------------------------
    // MIGRATION (DEFERRED — documented for traceability, AAP §0.7.2; intentionally NOT implemented).
    // The following RoleController.vb rules are NOT migrated in this phase because they are not on the
    // IRoleService contract and have no repository support (IRoleRepository exposes only the read
    // GetUserRolesAsync — there is no user-role write method, and no AssignUserRoleRequest DTO exists).
    // Recorded here so the behavior is captured even though the code is absent.
    //
    //  • UpdateRole re-assignment-on-update (RoleController.vb L254-L257): legacy UpdateRole called
    //    AutoAssignUsers AFTER provider.UpdateRole, so toggling AutoAssignment on during an edit would
    //    enroll all existing portal users. This re-assignment-on-update nuance is DEFERRED; UpdateAsync
    //    above only persists field changes. (AutoAssignUsers itself IS preserved on create.)
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
