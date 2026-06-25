using DnnMigration.Application.DTOs.Common;
using DnnMigration.Application.DTOs.Portal;
using DnnMigration.Domain.Common;

namespace DnnMigration.Application.Interfaces;

/// <summary>
/// Service-layer contract for portal (tenant) management. Consumed by PortalsController
/// via constructor injection (Dependency Inversion). The implementation orchestrates the
/// portal repository + unit of work and projects Domain entities to DTOs.
/// </summary>
// MIGRATION: Abstracted from the public operations of Library/Components/Portal/PortalController.vb
// (CreatePortal L980, GetPortal L1224, GetPortals L1263, DeletePortalInfo L1191, UpdatePortalInfo L1568,
// HasSpaceAvailable L1323). Business logic moves to PortalService (Application/Services); data access moves
// to IPortalRepository (Infrastructure). DTO-only contract — no raw Domain entities exposed (AAP 0.7.7).
public interface IPortalService
{
    // MIGRATION: Portal is the host-level tenant aggregate, so the list is NOT scoped by a single portalId
    // (mirrors IPortalRepository.GetAllAsync). Paging mirrors the legacy GetPortalsByName(..., pageIndex,
    // pageSize, ByRef total) at PortalController.vb L262, projected to the lightweight PortalListItemDto.
    Task<Result<PagedResult<PortalListItemDto>>> GetAllAsync(int pageIndex, int pageSize, CancellationToken cancellationToken = default);

    // MIGRATION: Legacy GetPortal(PortalId) (PortalController.vb L1224). Returns the full PortalDto read model.
    Task<Result<PortalDto>> GetByIdAsync(int portalId, CancellationToken cancellationToken = default);

    // MIGRATION: Legacy CreatePortal (PortalController.vb L980). POST /api/portals -> 201 (Gate 5).
    Task<Result<PortalDto>> CreateAsync(CreatePortalRequest request, CancellationToken cancellationToken = default);

    // MIGRATION: Legacy UpdatePortalInfo (PortalController.vb L1568). portalId is route-bound; PUT -> 200 (Gate 5).
    Task<Result<PortalDto>> UpdateAsync(int portalId, UpdatePortalRequest request, CancellationToken cancellationToken = default);

    // MIGRATION: Legacy DeletePortalInfo (PortalController.vb L1191). DELETE -> 204 (Gate 5). Non-generic Result (no payload).
    Task<Result> DeleteAsync(int portalId, CancellationToken cancellationToken = default);

    // MIGRATION: Legacy HasSpaceAvailable(portalId As Integer, fileSizeBytes As Long) As Boolean
    // (PortalController.vb L1323) — preserved as a TWO-parameter quota check for behavioral parity (AAP 0.7.1).
    // fileSizeBytes is a primitive (long) matching the legacy signature, not a DTO.
    Task<Result<bool>> HasSpaceAvailableAsync(int portalId, long fileSizeBytes, CancellationToken cancellationToken = default);
}
