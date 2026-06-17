using AutoMapper;
using FluentValidation;
using DnnMigration.Application.Common;
using DnnMigration.Application.DTOs.Portal;
using DnnMigration.Application.Interfaces;
using DnnMigration.Domain.Entities;
using DnnMigration.Domain.Interfaces;

namespace DnnMigration.Application.Services;

/// <summary>
/// Application service for the Portal aggregate.
/// MIGRATION: ports the business surface of <c>Library/Components/Portal/PortalController.vb</c> (1632 lines)
/// into async, DTO-based operations. Data access is delegated to <see cref="IPortalRepository"/> (an EF Core
/// implementation in the Infrastructure layer), replacing the legacy controller's co-mingled ADO.NET /
/// <c>SqlDataProvider</c> / <c>CBO</c> reflection-hydration calls. Reads rely on EF Core entity materialization;
/// writes flow through the repository. Entity &lt;-&gt; DTO transformation is delegated to AutoMapper, and
/// create/update payloads are validated with FluentValidation before any persistence work occurs.
/// </summary>
public class PortalService : IPortalService
{
    private readonly IPortalRepository _portalRepository;
    private readonly IMapper _mapper;
    private readonly IValidator<CreatePortalDto> _createValidator;
    private readonly IValidator<UpdatePortalDto> _updateValidator;

    /// <summary>
    /// Initializes a new <see cref="PortalService"/> with its injected collaborators.
    /// </summary>
    /// <param name="portalRepository">Repository providing data access for the Portal aggregate.</param>
    /// <param name="mapper">AutoMapper instance used for entity &lt;-&gt; DTO projection.</param>
    /// <param name="createValidator">FluentValidation validator for <see cref="CreatePortalDto"/>.</param>
    /// <param name="updateValidator">FluentValidation validator for <see cref="UpdatePortalDto"/>.</param>
    public PortalService(
        IPortalRepository portalRepository,
        IMapper mapper,
        IValidator<CreatePortalDto> createValidator,
        IValidator<UpdatePortalDto> updateValidator)
    {
        _portalRepository = portalRepository;
        _mapper = mapper;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    /// <inheritdoc />
    public async Task<PortalDto?> GetByIdAsync(int portalId, CancellationToken cancellationToken = default)
    {
        // MIGRATION: legacy GetPortal used DataCache + CBO.FillObject reflection hydration
        // [PortalController.vb] — replaced here by EF Core entity materialization behind IPortalRepository.
        var portal = await _portalRepository.GetByIdAsync(portalId, cancellationToken);
        return portal is null ? null : _mapper.Map<PortalDto>(portal);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<PortalDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        // MIGRATION: legacy GetPortals used CBO.FillCollection reflection hydration
        // [PortalController.vb] — replaced here by EF Core entity materialization behind IPortalRepository.
        var portals = await _portalRepository.GetAllAsync(cancellationToken);
        return _mapper.Map<IEnumerable<PortalDto>>(portals);
    }

    /// <inheritdoc />
    public async Task<PagedResult<PortalDto>> GetByNameAsync(
        string nameToMatch,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        // MIGRATION: preserve legacy GetPortalsByName -1 sentinel (return all records on one page)
        // [PortalController.vb:L264-267]: "If pageIndex = -1 Then pageIndex = 0 : pageSize = Integer.MaxValue".
        if (pageIndex == -1)
        {
            pageIndex = 0;
            pageSize = int.MaxValue;
        }

        // MIGRATION: legacy FillPortalInfoCollection returned an ArrayList plus a ByRef totalRecords; the
        // repository returns the equivalent (Items, TotalCount) tuple, projected into a PagedResult envelope.
        var (items, totalCount) = await _portalRepository.GetByNameAsync(nameToMatch, pageIndex, pageSize, cancellationToken);
        var mapped = _mapper.Map<List<PortalDto>>(items);
        return new PagedResult<PortalDto>(mapped, totalCount, pageIndex, pageSize);
    }

    /// <inheritdoc />
    public async Task<PortalDto?> GetByAliasAsync(string httpAlias, CancellationToken cancellationToken = default)
    {
        // MIGRATION: legacy PortalAliasController alias resolution used DataCache + CBO hydration —
        // replaced here by EF Core entity materialization behind IPortalRepository.
        var portal = await _portalRepository.GetByAliasAsync(httpAlias, cancellationToken);
        return portal is null ? null : _mapper.Map<PortalDto>(portal);
    }

    /// <inheritdoc />
    public async Task<PortalDto> CreateAsync(CreatePortalDto request, CancellationToken cancellationToken = default)
    {
        await _createValidator.ValidateAndThrowAsync(request, cancellationToken);

        // MIGRATION: legacy CreatePortal [PortalController.vb:L326-377] seeded ExpiryDate / HostFee / HostSpace /
        // PageQuota / UserQuota / SiteLogHistory / Currency defaults from Common.Globals.HostSettings. The Host
        // namespace is OUT OF SCOPE (AAP §0.2.2), so those host-default lookups are omitted — the client supplies
        // these values on CreatePortalDto. The CreatePortalProfile/PortalProfile mapping ignores the server-assigned
        // PortalID and the server-derived Users/Pages metrics.
        var portal = _mapper.Map<Portal>(request);

        // MIGRATION: legacy CreatePortal performed NO duplicate-name or home-directory-collision check;
        // none is added here so behavior remains equivalent (adding a guard would be a behavioral divergence).
        var created = await _portalRepository.AddAsync(portal, cancellationToken);
        return _mapper.Map<PortalDto>(created);
    }

    /// <inheritdoc />
    public async Task<PortalDto> UpdateAsync(UpdatePortalDto request, CancellationToken cancellationToken = default)
    {
        await _updateValidator.ValidateAndThrowAsync(request, cancellationToken);

        // MIGRATION: legacy UpdatePortalInfo [PortalController.vb:L1524-1575] issued a single
        // DataProvider.UpdatePortalInfo call without first loading the row. The EF Core repository pattern
        // requires a tracked entity, so the existing portal is loaded first and the request is then mapped onto it.
        var existing = await _portalRepository.GetByIdAsync(request.PortalID, cancellationToken);
        if (existing is null)
        {
            throw new KeyNotFoundException($"Portal {request.PortalID} not found.");
        }

        // The UpdatePortalDto -> Portal map ignores the server-derived Users/Pages metrics.
        _mapper.Map(request, existing);

        await _portalRepository.UpdateAsync(existing, cancellationToken);

        // MIGRATION: legacy DataCache.ClearPortalCache(PortalId, True) is omitted (Cache Provider OUT OF SCOPE per AAP §0.2.2).
        return _mapper.Map<PortalDto>(existing);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(int portalId, CancellationToken cancellationToken = default)
    {
        // MIGRATION: legacy DeletePortal [PortalController.vb:L162-204] compared DataProvider.GetPortalCount() > 1;
        // here we count via GetAllAsync().
        var portals = await _portalRepository.GetAllAsync(cancellationToken);
        if (portals.Count() <= 1)
        {
            // MIGRATION: legacy set strMessage="LastPortal" and silently skipped deletion; we surface an explicit
            // error (rendered as RFC 7807 Problem Details by the API exception-handling middleware).
            // MIGRATION (F5-01 hardening): throw the dedicated BusinessRuleConflictException (a subtype of
            // InvalidOperationException) so the middleware returns this client-safe message as a 409 detail, while a
            // RAW InvalidOperationException (e.g. an EF Core transient failure) no longer leaks internals via 409.
            throw new BusinessRuleConflictException("Cannot delete the last remaining portal.");
        }

        // MIGRATION: legacy filesystem cleanup (custom .resx files, child portal folder, upload directory,
        // HomeDirectoryMapPath) is OMITTED (FileSystem OUT OF SCOPE per AAP §0.2.2). The transactional DB
        // cascade lives in IPortalRepository.DeleteAsync — Portal is HARD-delete (AAP §0.3.3).
        await _portalRepository.DeleteAsync(portalId, cancellationToken);
    }
}
