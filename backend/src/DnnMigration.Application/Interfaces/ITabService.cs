using DnnMigration.Application.DTOs.Tab;
using DnnMigration.Domain.Common;

namespace DnnMigration.Application.Interfaces;

/// <summary>
/// Service-layer contract for tab (page) management. Consumed by TabsController via
/// constructor injection. The implementation orchestrates the tab repository + unit of work
/// and projects Domain entities to DTOs. The per-portal list is the full page tree (unpaged).
/// </summary>
// MIGRATION: Abstracted from the public operations of Library/Components/Tabs/TabController.vb
// (AddTab L326, UpdateTab L780, DeleteTab L446, GetTab L467, GetTabs(PortalId) L516). Business logic moves to
// TabService; data access to ITabRepository. DTO-only contract — no raw Domain entities exposed (AAP 0.7.7).
public interface ITabService
{
    // MIGRATION: Legacy GetTabs(PortalId) (TabController.vb L516) — scoped by portalId (multi-tenant, AAP 0.7.1).
    // Returned UNPAGED (IEnumerable<TabResponse>): tabs form a hierarchical page tree the SPA renders whole
    // (TabResponse carries ParentId/Level/TabOrder/HasChildren/TabPath). Hence no PagedResult here.
    Task<Result<IEnumerable<TabResponse>>> GetByPortalAsync(int portalId, CancellationToken cancellationToken = default);

    // MIGRATION: Legacy GetTab(TabId, PortalId) (TabController.vb L467). CP1 review (ITabService #1 / multi-tenant
    // isolation, AAP 0.7.1) — PORTAL-SCOPED: the contract carries portalId so a tab from another portal can never be
    // read through this tenant. The earlier tabId-only signature is rejected because it cannot enforce tenant scope.
    Task<Result<TabResponse>> GetByIdAsync(int portalId, int tabId, CancellationToken cancellationToken = default);

    // MIGRATION: Legacy AddTab (TabController.vb L326). POST /api/tabs -> 201. PortalId travels on CreateTabRequest.
    Task<Result<TabResponse>> CreateAsync(CreateTabRequest request, CancellationToken cancellationToken = default);

    // MIGRATION: Legacy UpdateTab (TabController.vb L780). tabId route-bound; PUT -> 200. CP1 review (ITabService #1)
    // — PORTAL-SCOPED: portalId enforces tenant ownership before the tab is mutated (multi-tenant, AAP 0.7.1).
    Task<Result<TabResponse>> UpdateAsync(int portalId, int tabId, UpdateTabRequest request, CancellationToken cancellationToken = default);

    // MIGRATION: Legacy DeleteTab(TabId, PortalId) (TabController.vb L446). DELETE -> 204. Non-generic Result.
    // CP1 review (ITabService #1 / TabService #4) — PORTAL-SCOPED: portalId enforces tenant ownership/authorization
    // before the child-page guard and the delete run (multi-tenant, AAP 0.7.1).
    Task<Result> DeleteAsync(int portalId, int tabId, CancellationToken cancellationToken = default);
}
