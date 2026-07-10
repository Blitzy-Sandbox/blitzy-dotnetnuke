using AutoMapper;
using DnnMigration.Application.DTOs;
using DnnMigration.Application.Exceptions;
using DnnMigration.Application.Interfaces;
using DnnMigration.Domain.Common;
using DnnMigration.Domain.Entities;
using DnnMigration.Domain.Interfaces;

namespace DnnMigration.Application.Services;

/// <summary>
/// Application service implementing security-role management business rules for the
/// <c>/api/roles</c> resource. This is the "use-case" collaborator sitting between the
/// API controllers and the persistence ports: it accepts/returns Data Transfer Objects
/// only, delegates all data access to <see cref="IRoleRepository"/>, and projects between
/// the <see cref="Role"/> domain entity and its DTOs via the injected <see cref="IMapper"/>.
/// The class is <c>sealed</c> and stateless (no mutable/static fields), so a single
/// instance is safe to resolve per request from the built-in DI container.
/// </summary>
/// <remarks>
/// MIGRATION: business rules extracted from the legacy DotNetNuke <c>RoleController</c>
/// (Library/Components/Security/Roles/RoleController.vb). The legacy class exposed static-style
/// business methods backed by a <c>RoleProvider</c> singleton and co-mingled user-role assignment
/// side effects; here the concerns are split — data access is delegated to the injected
/// <see cref="IRoleRepository"/> (EF Core downstream), entities are projected to DTOs via
/// AutoMapper, and the class carries no HTTP/API concerns. Only the six CRUD-style operations
/// declared on <see cref="IRoleService"/> are surfaced.
/// </remarks>
// MIGRATION: legacy RoleController role-group / by-name lookups (GetRoleByName [RoleController.vb L179],
// GetRolesByGroup [L224], GetRoleNames, GetRolesByUser) and the user-role assignment methods
// (AddUserRole / AutoAssignUsers) are intentionally NOT exposed here.
//
// AUTO-ASSIGNMENT EXCLUSION (finding #7 — Minimal Change Clause / behavioral equivalence): the legacy
// AddRole/UpdateRole called AutoAssignUsers, which looped the portal's users and wrote a user-role
// membership row (AddUserRole) for each when the role's AutoAssignment flag was set. That fan-out is
// DROPPED because it is genuinely outside the entire AAP target design: AAP §0.3.1 enumerates NO
// user-role junction entity, NO user-role repository, and NO role-assignment endpoint (the Roles
// resource is pure CRUD); the User.Roles convenience list is an Ignore()'d transient (UserConfiguration).
// There is therefore no persistence port to assign through. This is also consistent with §0.2.2 (DNN
// provider-based membership is out of scope, replaced by JWT) and §0.6.4 (membership collapses to
// JWT/claims). IMPORTANT: the Role.AutoAssignment column itself IS faithfully persisted and round-trips
// through Create/Update/RoleDto (schema fidelity) — only the imperative user fan-out side effect is
// excluded. The exclusion boundary is pinned by RoleServiceTests (create/update persist AutoAssignment
// without any user assignment, which is structurally guaranteed by this service depending only on
// IRoleRepository + IMapper — it has no user port).
public sealed class RoleService : IRoleService
{
    private readonly IRoleRepository _roleRepository;
    private readonly IMapper _mapper;

    /// <summary>
    /// Initializes a new <see cref="RoleService"/> with its injected collaborators.
    /// </summary>
    /// <param name="roleRepository">The role persistence port (data access only).</param>
    /// <param name="mapper">The AutoMapper instance used for entity &lt;-&gt; DTO projection.</param>
    public RoleService(IRoleRepository roleRepository, IMapper mapper)
    {
        _roleRepository = roleRepository;
        _mapper = mapper;
    }

    // MIGRATION: RoleController.GetRoles() [RoleController.vb L208] = provider.GetRoles(Null.NullInteger),
    // i.e. every role across all portals. Preserved: fetch all roles, project to DTOs.
    /// <inheritdoc />
    public async Task<IEnumerable<RoleDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var roles = await _roleRepository.GetAllAsync(cancellationToken);
        return _mapper.Map<IEnumerable<RoleDto>>(roles);
    }

    // MIGRATION: RoleController.GetPortalRoles(PortalId) [RoleController.vb L146] = provider.GetRoles(PortalId).
    // Preserved: fetch the portal's roles via the repository finder, project to DTOs.
    /// <inheritdoc />
    public async Task<IEnumerable<RoleDto>> GetByPortalAsync(int portalId, CancellationToken cancellationToken = default)
    {
        var roles = await _roleRepository.GetByPortalAsync(portalId, cancellationToken);
        return _mapper.Map<IEnumerable<RoleDto>>(roles);
    }

    /// <inheritdoc />
    // MIGRATION (QA finding — R6 Issue 1): bounded page of GetAllAsync. The repository fetches only the
    // Skip/Take window plus a COUNT; the page is projected to DTOs and the total count is carried for the
    // controller's pagination meta.
    public async Task<PagedResult<RoleDto>> GetPagedAsync(int skip, int take, CancellationToken cancellationToken = default)
    {
        var page = await _roleRepository.GetPagedAsync(skip, take, cancellationToken);
        var dtos = _mapper.Map<List<RoleDto>>(page.Items);
        return new PagedResult<RoleDto>(dtos, page.TotalCount);
    }

    /// <inheritdoc />
    // MIGRATION (QA finding — R6 Issue 1): bounded page of GetByPortalAsync.
    public async Task<PagedResult<RoleDto>> GetByPortalPagedAsync(int portalId, int skip, int take, CancellationToken cancellationToken = default)
    {
        var page = await _roleRepository.GetByPortalPagedAsync(portalId, skip, take, cancellationToken);
        var dtos = _mapper.Map<List<RoleDto>>(page.Items);
        return new PagedResult<RoleDto>(dtos, page.TotalCount);
    }

    /// <inheritdoc />
    // MIGRATION (QA finding - R10 Issue 13): bounded, searchable page of roles across all portals. Delegates
    // to the repository substring search (RoleName/Description) and projects the page to DTOs, carrying the
    // total match count for the controller's pagination meta. Business-logic-free (data access + projection).
    public async Task<PagedResult<RoleDto>> SearchPagedAsync(string query, int skip, int take, CancellationToken cancellationToken = default)
    {
        var page = await _roleRepository.SearchPagedAsync(query, skip, take, cancellationToken);
        var dtos = _mapper.Map<List<RoleDto>>(page.Items);
        return new PagedResult<RoleDto>(dtos, page.TotalCount);
    }

    /// <inheritdoc />
    // MIGRATION (QA finding - R10 Issue 13): portal-scoped counterpart of SearchPagedAsync.
    public async Task<PagedResult<RoleDto>> SearchByPortalPagedAsync(int portalId, string query, int skip, int take, CancellationToken cancellationToken = default)
    {
        var page = await _roleRepository.SearchByPortalPagedAsync(portalId, query, skip, take, cancellationToken);
        var dtos = _mapper.Map<List<RoleDto>>(page.Items);
        return new PagedResult<RoleDto>(dtos, page.TotalCount);
    }

    // MIGRATION: RoleController.GetRole(RoleID, PortalID) [RoleController.vb L163] = provider.GetRole(...).
    // Preserved: fetch by id; a missing role maps to a null projection (the API layer turns this into 404).
    /// <inheritdoc />
    public async Task<RoleDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var role = await _roleRepository.GetByIdAsync(id, cancellationToken);
        return role is null ? null : _mapper.Map<RoleDto>(role);
    }

    // MIGRATION: RoleController.AddRole(objRoleInfo) [RoleController.vb L100] = provider.CreateRole then,
    // on success, AutoAssignUsers(objRoleInfo). The role record — INCLUDING its AutoAssignment flag — is
    // persisted and projected back to a DTO. The AutoAssignUsers user fan-out is DROPPED for the reasons
    // documented in the class-level AUTO-ASSIGNMENT EXCLUSION (no user-role junction/repository/endpoint in
    // the AAP §0.3.1 design; §0.2.2/§0.6.4). This service depends only on IRoleRepository + IMapper (no user
    // port), so no user-role write is structurally possible here.
    /// <inheritdoc />
    public async Task<RoleDto> CreateAsync(CreateRoleDto dto, CancellationToken cancellationToken = default)
    {
        // MIGRATION (QA finding - R10 Issue 12): role names are unique per portal (the legacy schema kept a
        // LoweredRoleName companion under a UNIQUE (ApplicationId, LoweredRoleName) index
        // [InstallRoles.sql L86]). Enforce that invariant BEFORE any row is written: a name already present
        // in the same portal is rejected with a 409 Conflict (ConflictException -> ExceptionHandlingMiddleware)
        // rather than silently persisting a duplicate. This mirrors the per-portal username-uniqueness guard
        // in UserService.CreateAsync.
        var duplicate = await _roleRepository.GetByNameAsync(dto.PortalID, dto.RoleName, cancellationToken);
        if (duplicate is not null)
        {
            throw new ConflictException(
                $"A role with the name '{dto.RoleName}' already exists in portal {dto.PortalID}.");
        }

        var role = _mapper.Map<Role>(dto);
        var created = await _roleRepository.AddAsync(role, cancellationToken);
        return _mapper.Map<RoleDto>(created);
    }

    // MIGRATION: RoleController.UpdateRole(objRoleInfo) [RoleController.vb L254] = provider.UpdateRole then
    // AutoAssignUsers(objRoleInfo). The field update — INCLUDING the AutoAssignment flag — is preserved via an
    // in-place map of the request DTO onto the tracked entity; the AutoAssignUsers user fan-out is DROPPED for
    // the reason documented in the class-level AUTO-ASSIGNMENT EXCLUSION. A missing role maps to a null
    // projection (the API layer turns this into 404).
    /// <inheritdoc />
    public async Task<RoleDto?> UpdateAsync(int id, UpdateRoleDto dto, CancellationToken cancellationToken = default)
    {
        var role = await _roleRepository.GetByIdAsync(id, cancellationToken);
        if (role is null)
        {
            return null;
        }

        // MIGRATION (QA finding - R10 Issue 12): enforce the same per-portal role-name uniqueness invariant on
        // rename. If the new name is already held by a DIFFERENT role in this role's portal, reject with a 409
        // Conflict rather than creating two roles that collide on LoweredRoleName. Renaming a role to its own
        // current name (or a case variant of it) is permitted because the matched role is itself.
        var collision = await _roleRepository.GetByNameAsync(role.PortalID, dto.RoleName, cancellationToken);
        if (collision is not null && collision.RoleID != role.RoleID)
        {
            throw new ConflictException(
                $"A role with the name '{dto.RoleName}' already exists in portal {role.PortalID}.");
        }

        _mapper.Map(dto, role);
        await _roleRepository.UpdateAsync(role, cancellationToken);
        return _mapper.Map<RoleDto>(role);
    }

    // MIGRATION: RoleController.DeleteRole(RoleId, PortalId) [RoleController.vb L125] fetched the role first
    // (GetRole) and only called provider.DeleteRole "If Not objRole Is Nothing" — i.e. a no-op when the role
    // did not exist. Preserved: fetch (missing -> false, no delete attempted), otherwise delete and return true.
    /// <inheritdoc />
    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var role = await _roleRepository.GetByIdAsync(id, cancellationToken);
        if (role is null)
        {
            return false;
        }

        await _roleRepository.DeleteAsync(id, cancellationToken);
        return true;
    }
}
