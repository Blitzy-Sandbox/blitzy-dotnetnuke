using AutoMapper;
using DnnMigration.Application.DTOs;
using DnnMigration.Application.Interfaces;
using DnnMigration.Domain.Entities;
using DnnMigration.Domain.Interfaces;

namespace DnnMigration.Application.Services;

/// <summary>
/// Application service implementing portal (site) management business rules and exposing the
/// CRUD surface consumed by the <c>/api/portals</c> endpoints via <see cref="IPortalService"/>.
/// </summary>
/// <remarks>
/// The service is stateless and holds no application state: data access is delegated to the
/// injected <see cref="IPortalRepository"/> and entity&lt;-&gt;DTO projection to the injected
/// AutoMapper <see cref="IMapper"/>. The Domain <c>Portal</c> entity never crosses this public
/// surface; only DTOs (and <see cref="bool"/> for delete) are accepted and returned.
/// </remarks>
// MIGRATION: business rules extracted from the legacy DotNetNuke PortalController.vb
// (Library/Components/Portal/PortalController.vb), which co-mingled business logic with ADO.NET /
// SqlDataProvider data access. Here the data access is delegated to IPortalRepository and entities
// are projected to DTOs via AutoMapper. The legacy `Public [Shared]` controller members become
// dependency-injected instance methods, and every operation is async (no synchronous blocking).
public sealed class PortalService : IPortalService
{
    private readonly IPortalRepository _portalRepository;
    private readonly IMapper _mapper;

    /// <summary>
    /// Initializes a new <see cref="PortalService"/> with its injected collaborators.
    /// </summary>
    /// <param name="portalRepository">Repository providing persistence for portal aggregates.</param>
    /// <param name="mapper">AutoMapper instance projecting entities to/from DTOs.</param>
    public PortalService(IPortalRepository portalRepository, IMapper mapper)
    {
        _portalRepository = portalRepository;
        _mapper = mapper;
    }

    // MIGRATION: PortalController.GetPortals() [L1263] = FillPortalInfoCollection(DataProvider.GetPortals()).
    // The stored-proc reader + ArrayList hydration becomes an async repository fetch mapped to a DTO sequence.
    /// <inheritdoc />
    public async Task<IEnumerable<PortalDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var portals = await _portalRepository.GetAllAsync(cancellationToken);
        return _mapper.Map<IEnumerable<PortalDto>>(portals);
    }

    // MIGRATION: PortalController.GetPortal(PortalId) [L1224] performed a DataCache lookup, then
    // DataProvider.GetPortal -> FillPortalInfo, then re-cached the result. The DataCache caching layer is
    // dropped here (this service is stateless); a missing portal (null) is projected to null.
    /// <inheritdoc />
    public async Task<PortalDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var portal = await _portalRepository.GetByIdAsync(id, cancellationToken);
        return portal is null ? null : _mapper.Map<PortalDto>(portal);
    }

    // MIGRATION: mirrors the legacy PortalAliasController/GetPortalByAlias lookup (an HTTP-alias -> PortalInfo
    // resolution). Delegated to IPortalRepository.GetByAliasAsync; a missing match (null) is projected to null.
    /// <inheritdoc />
    public async Task<PortalDto?> GetByAliasAsync(string alias, CancellationToken cancellationToken = default)
    {
        var portal = await _portalRepository.GetByAliasAsync(alias, cancellationToken);
        return portal is null ? null : _mapper.Map<PortalDto>(portal);
    }

    // MIGRATION: PortalController.CreatePortal(...) [L980]. Beyond inserting the portal row, the legacy method
    // also (a) created an OPTIONAL initial admin user via UserController.CreateUser [L1013] and set
    // portal.AdministratorId, (b) added an initial PortalAlias, and (c) performed template + file-system
    // provisioning (CreateProfileDefinitions/ParseTemplate/ProcessResourceFile/directory creation, L996-L1075).
    // Those behaviors are DROPPED from this service because: it is injected ONLY with IPortalRepository + IMapper
    // (admin-user creation, requiring IUserRepository/IPasswordHasher, is UserService's concern); there is no
    // portal-alias repository port; and template/file-system provisioning is explicitly out of scope (AAP
    // section 0.2.2). CreateAsync therefore persists ONLY the portal record (PortalName/Description/KeyWords/
    // HomeDirectory copied by AutoMapper); CreatePortalDto.FirstName/LastName/Username/Password/PortalAlias are
    // accepted for API parity but not persisted here.
    /// <inheritdoc />
    public async Task<PortalDto> CreateAsync(CreatePortalDto dto, CancellationToken cancellationToken = default)
    {
        var portal = _mapper.Map<Portal>(dto);
        var created = await _portalRepository.AddAsync(portal, cancellationToken);
        return _mapper.Map<PortalDto>(created);
    }

    // MIGRATION: PortalController.UpdatePortalInfo(PortalInfo) [L1524] delegated to the full-field overload
    // [L1568], which was a pure 27-field copy into DataProvider.UpdatePortalInfo [L1570] followed by
    // DataCache.ClearPortalCache [L1573]. The field copy is preserved EXACTLY via AutoMapper's in-place map onto
    // the fetched entity; the cache clear is dropped (stateless service). A missing portal (null) yields null.
    // Note: the legacy overload widened HostFee/HostSpace to Double and typed UserRegistration/BannerAdvertising
    // as Integer; the entity/DTO use float and the UserRegistrationType/BannerType enums, and AutoMapper performs
    // those conversions with no manual casts.
    /// <inheritdoc />
    public async Task<PortalDto?> UpdateAsync(int id, UpdatePortalDto dto, CancellationToken cancellationToken = default)
    {
        var portal = await _portalRepository.GetByIdAsync(id, cancellationToken);
        if (portal is null)
        {
            return null;
        }

        _mapper.Map(dto, portal);
        await _portalRepository.UpdateAsync(portal, cancellationToken);
        return _mapper.Map<PortalDto>(portal);
    }

    // MIGRATION: PortalController.DeletePortalInfo(PortalId) [L1191] removed skin assignments, deleted the
    // portal's users (UserController.DeleteUsers [L1199]), deleted the portal (DataProvider.DeletePortalInfo
    // [L1202]), then cleared the host cache (DataCache.ClearHostCache [L1205]). The cascade user-deletion, skin
    // cleanup, and cache clearing are dropped here (handled by DB cascade / outside this service's injected
    // scope). A missing portal (null) yields false; otherwise the portal is deleted and true is returned.
    /// <inheritdoc />
    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var portal = await _portalRepository.GetByIdAsync(id, cancellationToken);
        if (portal is null)
        {
            return false;
        }

        await _portalRepository.DeleteAsync(id, cancellationToken);
        return true;
    }
}
