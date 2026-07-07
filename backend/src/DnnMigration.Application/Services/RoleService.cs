using AutoMapper;
using DnnMigration.Application.DTOs;
using DnnMigration.Application.Interfaces;
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
// (AddUserRole/AutoAssignUsers) are intentionally NOT exposed here — they are out of scope for the
// role CRUD surface and this service is injected only with IRoleRepository + IMapper.
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

    // MIGRATION: RoleController.GetRole(RoleID, PortalID) [RoleController.vb L163] = provider.GetRole(...).
    // Preserved: fetch by id; a missing role maps to a null projection (the API layer turns this into 404).
    /// <inheritdoc />
    public async Task<RoleDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var role = await _roleRepository.GetByIdAsync(id, cancellationToken);
        return role is null ? null : _mapper.Map<RoleDto>(role);
    }

    // MIGRATION: RoleController.AddRole(objRoleInfo) [RoleController.vb L100] = provider.CreateRole then,
    // on success, AutoAssignUsers(objRoleInfo). AutoAssignUsers (which looped the portal's users and called
    // AddUserRole for each) is DROPPED — user-role assignment is out of scope for the role CRUD surface and
    // this service is injected only with IRoleRepository + IMapper (no IUserRepository). Only the role record
    // is persisted here; the created role is projected back to a DTO.
    /// <inheritdoc />
    public async Task<RoleDto> CreateAsync(CreateRoleDto dto, CancellationToken cancellationToken = default)
    {
        var role = _mapper.Map<Role>(dto);
        var created = await _roleRepository.AddAsync(role, cancellationToken);
        return _mapper.Map<RoleDto>(created);
    }

    // MIGRATION: RoleController.UpdateRole(objRoleInfo) [RoleController.vb L254] = provider.UpdateRole then
    // AutoAssignUsers(objRoleInfo). The field update is preserved via an in-place map of the request DTO onto
    // the tracked entity; AutoAssignUsers is DROPPED for the same reason documented on CreateAsync. A missing
    // role maps to a null projection (the API layer turns this into 404).
    /// <inheritdoc />
    public async Task<RoleDto?> UpdateAsync(int id, UpdateRoleDto dto, CancellationToken cancellationToken = default)
    {
        var role = await _roleRepository.GetByIdAsync(id, cancellationToken);
        if (role is null)
        {
            return null;
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
