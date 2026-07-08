using DnnMigration.Application.DTOs;

namespace DnnMigration.Application.Interfaces;

/// <summary>
/// Application service contract for portal (site) management, exposing the CRUD surface
/// consumed by the <c>/api/portals</c> endpoints.
/// </summary>
// MIGRATION: replaces the business surface of the legacy DotNetNuke PortalController.vb
// (GetPortals/GetPortal/CreatePortal/UpdatePortalInfo/DeletePortalInfo). Business rules move to
// PortalService (Application) and data access to a repository (Infrastructure); this contract is
// DTO-only and async, and static Shared members become DI-registered instance methods.
public interface IPortalService
{
    /// <summary>Returns all portals.</summary>
    Task<IEnumerable<PortalDto>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns the portal with the given id, or <c>null</c> if not found.</summary>
    Task<PortalDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Returns the portal matching the given HTTP alias, or <c>null</c> if none matches.</summary>
    // MIGRATION: mirrors legacy PortalAliasController/GetPortalByAlias lookup.
    Task<PortalDto?> GetByAliasAsync(string alias, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the portals whose name, description, or keywords match the free-text <paramref name="query"/>
    /// (case-insensitive substring). Serves the <c>GET /api/portals?query=...</c> search contract.
    /// </summary>
    // MIGRATION: PortalController.GetPortalsByName — the Portals.ascx.vb grid text/letter search — now
    // performed server-side (AAP §0.7.2) rather than being silently ignored by the list endpoint.
    Task<IEnumerable<PortalDto>> SearchAsync(string query, CancellationToken cancellationToken = default);

    /// <summary>Creates a new portal and returns the created projection.</summary>
    Task<PortalDto> CreateAsync(CreatePortalDto dto, CancellationToken cancellationToken = default);

    /// <summary>Updates the portal with the given id, or returns <c>null</c> if it does not exist.</summary>
    Task<PortalDto?> UpdateAsync(int id, UpdatePortalDto dto, CancellationToken cancellationToken = default);

    /// <summary>Deletes the portal with the given id; returns <c>true</c> if deleted, <c>false</c> if not found.</summary>
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
}
