using DnnMigration.Application.DTOs;
using DnnMigration.Domain.Common;

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

    /// <summary>
    /// Returns a single bounded page of roles together with the total role count, for server-side
    /// pagination of <c>GET /api/roles</c>.
    /// </summary>
    // MIGRATION (QA finding — R6 Issue 1): the BOUNDED counterpart of <see cref="GetAllAsync"/>.
    Task<PagedResult<RoleDto>> GetPagedAsync(int skip, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a single bounded page of roles belonging to the specified portal together with the total
    /// count for that portal.
    /// </summary>
    // MIGRATION (QA finding — R6 Issue 1): the BOUNDED counterpart of <see cref="GetByPortalAsync"/>.
    Task<PagedResult<RoleDto>> GetByPortalPagedAsync(int portalId, int skip, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a single bounded page of roles whose name or description contains <paramref name="query"/>
    /// (case-insensitive), for server-side search of <c>GET /api/roles?query=...</c>.
    /// </summary>
    // MIGRATION (QA finding - R10 Issue 13): the roles endpoint previously had no server-side search, so the
    // SPA could only filter the first bounded page client-side and a newly created role beyond it was
    // undiscoverable. This adds the searchable counterpart of GetPagedAsync (parity with Portal/Module/User).
    Task<PagedResult<RoleDto>> SearchPagedAsync(string query, int skip, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a single bounded page of roles belonging to the specified portal whose name or description
    /// contains <paramref name="query"/> (case-insensitive).
    /// </summary>
    // MIGRATION (QA finding - R10 Issue 13): portal-scoped counterpart of <see cref="SearchPagedAsync"/>.
    Task<PagedResult<RoleDto>> SearchByPortalPagedAsync(int portalId, string query, int skip, int take, CancellationToken cancellationToken = default);

    /// <summary>Returns the role with the given id, or <c>null</c> if not found.</summary>
    Task<RoleDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Creates a new role and returns the created projection.</summary>
    Task<RoleDto> CreateAsync(CreateRoleDto dto, CancellationToken cancellationToken = default);

    /// <summary>Updates the role with the given id, or returns <c>null</c> if it does not exist.</summary>
    Task<RoleDto?> UpdateAsync(int id, UpdateRoleDto dto, CancellationToken cancellationToken = default);

    /// <summary>Deletes the role with the given id; returns <c>true</c> if deleted, <c>false</c> if not found.</summary>
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
}
