using DnnMigration.Application.DTOs;

namespace DnnMigration.Application.Interfaces;

/// <summary>
/// Application service contract for tab (portal page) management, exposing the CRUD surface
/// consumed by the <c>/api/tabs</c> endpoints.
/// </summary>
// MIGRATION: replaces the business surface of the legacy DotNetNuke TabController.vb
// (GetTabs/GetTab/GetTabsByParentId/AddTab/UpdateTab/DeleteTab). DTO-only, async.
public interface ITabService
{
    /// <summary>Returns all tabs (pages).</summary>
    Task<IEnumerable<TabDto>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns all tabs belonging to the specified portal.</summary>
    // MIGRATION: legacy TabController.GetTabs(PortalId).
    Task<IEnumerable<TabDto>> GetByPortalAsync(int portalId, CancellationToken cancellationToken = default);

    /// <summary>Returns the tab with the given id, or <c>null</c> if not found.</summary>
    Task<TabDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Returns the immediate child tabs of the specified parent tab.</summary>
    // MIGRATION: legacy TabController.GetTabsByParentId(ParentId).
    Task<IEnumerable<TabDto>> GetByParentAsync(int parentId, CancellationToken cancellationToken = default);

    /// <summary>Creates a new tab and returns the created projection.</summary>
    Task<TabDto> CreateAsync(CreateTabDto dto, CancellationToken cancellationToken = default);

    /// <summary>Updates the tab with the given id, or returns <c>null</c> if it does not exist.</summary>
    Task<TabDto?> UpdateAsync(int id, UpdateTabDto dto, CancellationToken cancellationToken = default);

    /// <summary>Deletes the tab with the given id; returns <c>true</c> if deleted, <c>false</c> if not found.</summary>
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
}
