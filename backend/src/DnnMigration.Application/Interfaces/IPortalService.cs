using DnnMigration.Application.Common;
using DnnMigration.Application.DTOs.Portal;

namespace DnnMigration.Application.Interfaces;

/// <summary>
/// Application service contract for the Portal aggregate. MIGRATION: ported from the public
/// business surface of PortalController.vb, re-expressed as async DTO-based operations that
/// parallel IPortalRepository. Implemented by Application/Services/PortalService.cs.
/// </summary>
public interface IPortalService
{
    Task<PortalDto?> GetByIdAsync(int portalId, CancellationToken cancellationToken = default);

    Task<IEnumerable<PortalDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<PagedResult<PortalDto>> GetByNameAsync(string nameToMatch, int pageIndex, int pageSize, CancellationToken cancellationToken = default);

    Task<PortalDto?> GetByAliasAsync(string httpAlias, CancellationToken cancellationToken = default);

    Task<PortalDto> CreateAsync(CreatePortalDto request, CancellationToken cancellationToken = default);

    Task<PortalDto> UpdateAsync(UpdatePortalDto request, CancellationToken cancellationToken = default);

    Task DeleteAsync(int portalId, CancellationToken cancellationToken = default);
}
