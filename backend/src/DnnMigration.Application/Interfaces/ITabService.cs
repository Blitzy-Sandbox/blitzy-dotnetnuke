using DnnMigration.Application.DTOs.Tab;

namespace DnnMigration.Application.Interfaces;

/// <summary>
/// Application service contract for the Tab (page) aggregate. MIGRATION: ported from the public
/// business surface of TabController.vb, re-expressed as async DTO-based operations that parallel
/// ITabRepository. Tab identity is portal-scoped (GetByIdAsync/DeleteAsync take tabId + portalId).
/// Tab is soft-deleted. Implemented by Application/Services/TabService.cs.
/// </summary>
public interface ITabService
{
    Task<TabDto?> GetByIdAsync(int tabId, int portalId, CancellationToken cancellationToken = default);

    Task<IEnumerable<TabDto>> GetByPortalAsync(int portalId, CancellationToken cancellationToken = default);

    Task<IEnumerable<TabDto>> GetByParentAsync(int parentId, int portalId, CancellationToken cancellationToken = default);

    Task<int> GetCountAsync(int portalId, CancellationToken cancellationToken = default);

    Task<TabDto> CreateAsync(CreateTabDto request, CancellationToken cancellationToken = default);

    Task<TabDto> UpdateAsync(UpdateTabDto request, CancellationToken cancellationToken = default);

    Task DeleteAsync(int tabId, int portalId, CancellationToken cancellationToken = default);
}
