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
/// MIGRATION: ports the business logic of the legacy <c>Library/Components/Portal/PortalController.vb</c>
/// (DotNetNuke 4.9.0.85, 1632 lines), decoupled from data access. The legacy controller co-mingled
/// ADO.NET / <c>SqlDataProvider</c> calls, <c>CBO</c> reflection hydration, and filesystem/cache side
/// effects directly inside its methods. Here data access is delegated to <see cref="IPortalRepository"/>
/// (EF Core in the Infrastructure layer), entity&#8596;DTO translation to AutoMapper, and inbound input
/// validation to FluentValidation. The service is async-first and stateless; each deviation from the
/// legacy behavior is annotated with a <c>// MIGRATION:</c> comment and recorded in the root
/// <c>MIGRATION_NOTES.md</c>.
/// </summary>
public class PortalService : IPortalService
{
    private readonly IPortalRepository _portalRepository;
    private readonly IMapper _mapper;
    private readonly IValidator<CreatePortalDto> _createValidator;
    private readonly IValidator<UpdatePortalDto> _updateValidator;

    /// <summary>
    /// Initializes a new instance of the <see cref="PortalService"/> class.
    /// </summary>
    /// <param name="portalRepository">Repository providing data access for the Portal aggregate.</param>
    /// <param name="mapper">AutoMapper instance used for entity&#8596;DTO projection.</param>
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
        // MIGRATION: legacy PortalController.GetPortal(portalId) read through DataCache and hydrated the
        // result with CBO.FillObject reflection (Library/Components/Shared/CBO.vb). Both the cache lookup
        // and the reflection mapping are replaced by EF Core entity materialization behind the repository.
        var portal = await _portalRepository.GetByIdAsync(portalId, cancellationToken);
        return portal is null ? null : _mapper.Map<PortalDto>(portal);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<PortalDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        // MIGRATION: legacy PortalController.GetPortals() returned an ArrayList of PortalInfo hydrated via
        // CBO.FillCollection reflection; replaced by EF Core materialization + AutoMapper projection.
        var portals = await _portalRepository.GetAllAsync(cancellationToken);
        return _mapper.Map<IEnumerable<PortalDto>>(portals);
    }

    /// <inheritdoc />
    public async Task<PagedResult<PortalDto>> GetByNameAsync(string nameToMatch, int pageIndex, int pageSize, CancellationToken cancellationToken = default)
    {
        // MIGRATION: preserve legacy GetPortalsByName -1 sentinel (return all records on one page)
        // [PortalController.vb:L264-267: If pageIndex = -1 Then pageIndex = 0 : pageSize = Integer.MaxValue].
        if (pageIndex == -1)
        {
            pageIndex = 0;
            pageSize = int.MaxValue;
        }

        // MIGRATION: legacy GetPortalsByName returned an ArrayList plus a ByRef totalRecords out-parameter;
        // the repository now returns a (Items, TotalCount) tuple which is projected into the PagedResult envelope.
        var (items, totalCount) = await _portalRepository.GetByNameAsync(nameToMatch, pageIndex, pageSize, cancellationToken);
        var mapped = _mapper.Map<List<PortalDto>>(items);
        return new PagedResult<PortalDto>(mapped, totalCount, pageIndex, pageSize);
    }

    /// <inheritdoc />
    public async Task<PortalDto?> GetByAliasAsync(string httpAlias, CancellationToken cancellationToken = default)
    {
        // MIGRATION: legacy alias resolution (PortalAliasController.GetPortalAlias + CBO hydration, fronted
        // by DataCache) is replaced by EF Core entity materialization behind the repository.
        var portal = await _portalRepository.GetByAliasAsync(httpAlias, cancellationToken);
        return portal is null ? null : _mapper.Map<PortalDto>(portal);
    }

    /// <inheritdoc />
    public async Task<PortalDto> CreateAsync(CreatePortalDto request, CancellationToken cancellationToken = default)
    {
        await _createValidator.ValidateAndThrowAsync(request, cancellationToken);

        // MIGRATION: legacy private CreatePortal [PortalController.vb:L326-377] seeded defaults (ExpiryDate,
        // HostFee, HostSpace, PageQuota, UserQuota, SiteLogHistory, Currency) from Common.Globals.HostSettings.
        // The Host namespace / Globals are OUT OF SCOPE (AAP §0.2.2), so those host-derived defaults are
        // omitted; the inbound DTO supplies these values instead. The CreatePortalDto->Portal map ignores the
        // database-generated PortalID and the derived Users/Pages metrics (PortalProfile).
        var portal = _mapper.Map<Portal>(request);

        // MIGRATION: legacy CreatePortal performed NO duplicate-name or home-directory-collision check, so
        // none is added here (adding one would diverge from the ported behavior).
        var created = await _portalRepository.AddAsync(portal, cancellationToken);
        return _mapper.Map<PortalDto>(created);
    }

    /// <inheritdoc />
    public async Task<PortalDto> UpdateAsync(UpdatePortalDto request, CancellationToken cancellationToken = default)
    {
        await _updateValidator.ValidateAndThrowAsync(request, cancellationToken);

        // MIGRATION: the legacy UpdatePortalInfo [PortalController.vb:L1524-1575] passed the supplied values
        // straight to the data provider (a silent no-op when the row was absent). The DTO+repository pattern
        // requires loading the tracked entity first; a missing portal is surfaced as KeyNotFoundException
        // (mapped to a 404 RFC 7807 response by the API middleware) instead of silently no-opping.
        var existing = await _portalRepository.GetByIdAsync(request.PortalID, cancellationToken);
        if (existing is null)
        {
            throw new KeyNotFoundException($"Portal {request.PortalID} not found.");
        }

        // The UpdatePortalDto->Portal map (PortalProfile) ignores the derived Users/Pages metrics.
        _mapper.Map(request, existing);
        await _portalRepository.UpdateAsync(existing, cancellationToken);

        // MIGRATION: legacy DataCache.ClearPortalCache(PortalId, True) is omitted (the Cache Provider is OUT
        // OF SCOPE per AAP §0.2.2; the stateless API holds no portal cache to invalidate).
        return _mapper.Map<PortalDto>(existing);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(int portalId, CancellationToken cancellationToken = default)
    {
        // MIGRATION: legacy DeletePortal compared DataProvider.GetPortalCount() > 1; here we count via GetAllAsync().
        var portals = await _portalRepository.GetAllAsync(cancellationToken);
        if (portals.Count() <= 1)
        {
            // MIGRATION: legacy set strMessage="LastPortal" and silently skipped deletion; we surface an explicit error (RFC 7807 via middleware).
            throw new InvalidOperationException("Cannot delete the last remaining portal.");
        }

        // MIGRATION: legacy filesystem cleanup (resx files, child folders, upload dir, HomeDirectoryMapPath)
        // is OMITTED (FileSystem is OUT OF SCOPE per AAP §0.2.2); the transactional DB cascade lives in
        // IPortalRepository.DeleteAsync (Portal is HARD-delete per AAP §0.3.3).
        await _portalRepository.DeleteAsync(portalId, cancellationToken);
    }
}
