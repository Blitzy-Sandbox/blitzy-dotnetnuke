using DnnMigration.Application.DTOs;

namespace DnnMigration.Application.Interfaces;

/// <summary>
/// Application service contract for security-role management, exposing the CRUD surface
/// consumed by the <c>/api/roles</c> endpoints.
/// </summary>
// MIGRATION: replaces the business surface of the legacy DotNetNuke RoleController.vb
// (GetRoles/GetPortalRoles/GetRole/AddRole/UpdateRole/DeleteRole). DTO-only, async.
public interface IRoleService
{
    /// <summary>Returns all roles.</summary>
    Task<IEnumerable<RoleDto>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns all roles belonging to the specified portal.</summary>
    // MIGRATION: legacy RoleController.GetPortalRoles(PortalId).
    Task<IEnumerable<RoleDto>> GetByPortalAsync(int portalId, CancellationToken cancellationToken = default);

    /// <summary>Returns the role with the given id, or <c>null</c> if not found.</summary>
    Task<RoleDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Creates a new role and returns the created projection.</summary>
    Task<RoleDto> CreateAsync(CreateRoleDto dto, CancellationToken cancellationToken = default);

    /// <summary>Updates the role with the given id, or returns <c>null</c> if it does not exist.</summary>
    Task<RoleDto?> UpdateAsync(int id, UpdateRoleDto dto, CancellationToken cancellationToken = default);

    /// <summary>Deletes the role with the given id; returns <c>true</c> if deleted, <c>false</c> if not found.</summary>
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
}
