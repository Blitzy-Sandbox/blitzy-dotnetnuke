using AutoMapper;
using DnnMigration.Application.DTOs.Common;
using DnnMigration.Application.DTOs.Portal;
using DnnMigration.Application.Interfaces;
using DnnMigration.Domain.Common;
using DnnMigration.Domain.Entities;
using DnnMigration.Domain.Interfaces;

namespace DnnMigration.Application.Services;

// MIGRATION: Application-layer service holding the Portal (tenant) business rules. Replaces the
// monolithic VB.NET DotNetNuke.Entities.Portals.PortalController (Library/Components/Portal/PortalController.vb,
// 1632 lines) + PortalSettings.vb. The legacy controller mixed business rules with data access via the
// reflection-instantiated DataProvider.Instance() + SqlHelper + stored-procedure + IDataReader/FillInfo pipeline;
// here the business rules live in this service while data access is delegated to the injected repositories
// (IPortalRepository/IUserRepository) and persistence boundaries to IUnitOfWork (Clean/Onion, AAP §0.3.3/§0.7.3).
// MIGRATION: This service NEVER returns a raw Domain entity — every public result is a DTO wrapped in
// Result/Result<T> (AAP §0.7.7). All entity<->DTO conversion goes through the injected IMapper (Mapping/PortalProfile);
// there is no hand-mapping here. No DbContext/EF Core/System.Data reference exists in this layer (AAP §0.7.3).
/// <summary>
/// Implements <see cref="IPortalService"/>: portal CRUD plus the host-level storage-quota check, orchestrating
/// the portal repository, the user repository, the unit of work, and the AutoMapper projections. This is the
/// canonical CRUD service that the other domain services mirror (Gate 5 validates Portal CRUD status codes).
/// </summary>
public sealed class PortalService : IPortalService
{
    private readonly IPortalRepository _portalRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="PortalService"/> class.
    /// </summary>
    /// <param name="portalRepository">Portal data-access abstraction (replaces DataProvider portal operations).</param>
    /// <param name="userRepository">User data-access abstraction (required to delete portal users on portal deletion).</param>
    /// <param name="unitOfWork">Transactional persistence boundary (replaces the legacy DataProvider transaction surface).</param>
    /// <param name="mapper">AutoMapper instance configured with the Portal mapping profile.</param>
    // MIGRATION: Constructor injection ONLY (AAP §0.7.3) — replaces the legacy DataProvider.Instance() reflection
    // singleton lookup and the `New PortalController`/`New UserController` direct instantiations.
    public PortalService(
        IPortalRepository portalRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        IMapper mapper)
    {
        ArgumentNullException.ThrowIfNull(portalRepository);
        ArgumentNullException.ThrowIfNull(userRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(mapper);

        _portalRepository = portalRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    /// <inheritdoc />
    // MIGRATION: PortalController.GetPortals() L1263 returned all portals (FillPortalInfoCollection); legacy host grid
    // paged via GetPortalsByName at the DB. Repo exposes only GetAllAsync(), so paging is applied in-memory here.
    public async Task<Result<PagedResult<PortalListItemDto>>> GetAllAsync(
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var portals = (await _portalRepository.GetAllAsync()).ToList();
        var total = portals.Count;

        // MIGRATION: PageIndex is ZERO-BASED (see PagedResult). Skip whole pages, take the page window.
        var pageItems = portals
            .Skip(pageIndex * pageSize)
            .Take(pageSize)
            .Select(p => _mapper.Map<PortalListItemDto>(p))
            .ToList();

        return Result<PagedResult<PortalListItemDto>>.Success(new PagedResult<PortalListItemDto>
        {
            Items = pageItems,
            TotalCount = total,
            PageIndex = pageIndex,
            PageSize = pageSize,
        });
    }

    /// <inheritdoc />
    // MIGRATION: Legacy GetPortal(PortalId) used cache-aside (PortalController L1224); caching is an Infrastructure
    // concern and is omitted here. A missing portal becomes an expected business failure (Result.Failure), not an
    // exception — the Api maps it to a 404 ProblemDetails.
    public async Task<Result<PortalDto>> GetByIdAsync(int portalId, CancellationToken cancellationToken = default)
    {
        var portal = await _portalRepository.GetByIdAsync(portalId);
        if (portal is null)
        {
            return Result<PortalDto>.Failure($"Portal {portalId} was not found.");
        }

        return Result<PortalDto>.Success(_mapper.Map<PortalDto>(portal));
    }

    /// <inheritdoc />
    // MIGRATION: Legacy PortalController.CreatePortal L980 (public 15-param overload) -> private CreatePortal L326.
    // The in-scope portion — persist the portal row with its configuration and the faithful "USD"/HomeDirectory
    // defaults — is transcribed here; the admin-user bootstrap and the skin/file-system/template/alias provisioning
    // are DEFERRED/OUT-OF-SCOPE as documented inline below.
    public async Task<Result<PortalDto>> CreateAsync(
        CreatePortalRequest request,
        CancellationToken cancellationToken = default)
    {
        // PortalProfile maps the client-supplied configuration fields (incl. Email/Description/KeyWords) and
        // Ignores server-managed identifiers/Guid/role/tab/counter members.
        var portal = _mapper.Map<Portal>(request);

        // MIGRATION: PortalController private CreatePortal L326 defaulted Currency to "USD" (host "HostCurrency"
        // setting when present, else the literal "USD"). Host settings are out of scope, so the "USD" literal default
        // is preserved here.
        if (string.IsNullOrEmpty(portal.Currency))
        {
            portal.Currency = "USD";
        }

        await _portalRepository.AddAsync(portal);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // MIGRATION: PortalController.CreatePortal L990-L993 defaulted HomeDirectory = "Portals/" & PortalId once the
        // database-generated PortalId was known. Reproduced here as a second persistence pass after the id is populated.
        if (string.IsNullOrEmpty(portal.HomeDirectory))
        {
            portal.HomeDirectory = $"Portals/{portal.PortalId}";
            await _portalRepository.UpdateAsync(portal);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // MIGRATION (DEFERRED — admin-user bootstrap): PortalController.CreatePortal L998-L1025 also created the portal
        // Administrator — a new UserInfo populated with Username/Password/FirstName/LastName/Email from the create
        // parameters, with Membership.Approved=True and IsSuperUser=False, persisted via UserController.CreateUser — and
        // then assigned portal.AdministratorId = the new UserID. This is DEFERRED in this phase because
        // CreatePortalRequest intentionally carries NO admin credentials (admin-user creation belongs to the User/Auth
        // flows) and credential persistence lives in Infrastructure/Identity (BCrypt), which is not yet realized.
        // Recorded in MIGRATION_NOTES.md.

        // MIGRATION (OUT OF SCOPE): PortalController.CreatePortal L1027-L1102 performed home-directory creation,
        // child-portal subhost copy, zip ProcessResourceFile, ParseTemplate (portal + admin templates), Default page
        // template copy, skin SynchronizeFolder, and AddPortalAlias — all skin/file-system/template/alias provisioning
        // excluded per AAP §0.6.2.

        // MIGRATION: Behavioral note — legacy on failure called DeletePortalInfo to roll back and re-threw. Here an
        // infrastructure exception from SaveChangesAsync bubbles to the Api ExceptionHandlingMiddleware (RFC 7807);
        // Result.Failure is reserved for validated business failures, so no try/catch is introduced.
        return Result<PortalDto>.Success(_mapper.Map<PortalDto>(portal));
    }

    /// <inheritdoc />
    // MIGRATION: UpdatePortalInfo delegated to the 28-param DataProvider overload (PortalController.vb L1524 -> L1568);
    // here the editable-settings DTO is mapped onto the tracked entity and persisted via EF + IUnitOfWork.
    public async Task<Result<PortalDto>> UpdateAsync(
        int portalId,
        UpdatePortalRequest request,
        CancellationToken cancellationToken = default)
    {
        var portal = await _portalRepository.GetByIdAsync(portalId);
        if (portal is null)
        {
            return Result<PortalDto>.Failure($"Portal {portalId} was not found.");
        }

        // UpdatePortalRequest -> Portal map applies the editable fields onto the tracked entity; server-managed fields
        // (roles, tabs, counters, Guid, Email, Version) are Ignored by PortalProfile.
        _mapper.Map(request, portal);

        // MIGRATION: route /api/portals/{id} is the canonical id (UpdatePortalRequest.PortalId is ignored), AAP §0.7.1.
        portal.PortalId = portalId;

        await _portalRepository.UpdateAsync(portal);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<PortalDto>.Success(_mapper.Map<PortalDto>(portal));
    }

    /// <inheritdoc />
    // MIGRATION: Legacy DeletePortalInfo(PortalId) (PortalController.vb L1191). The portal users are deleted FIRST
    // (legacy UserController.DeleteUsers(PortalId, False, True) — deleteAdmin=True), then the portal itself, inside a
    // single unit-of-work boundary.
    public async Task<Result> DeleteAsync(int portalId, CancellationToken cancellationToken = default)
    {
        var portal = await _portalRepository.GetByIdAsync(portalId);
        if (portal is null)
        {
            return Result.Failure($"Portal {portalId} was not found.");
        }

        // MIGRATION: DeletePortalInfo L1199 deleted all portal users (deleteAdmin=True) before deleting the portal.
        foreach (var user in await _userRepository.GetByPortalIdAsync(portalId))
        {
            await _userRepository.DeleteAsync(user.UserId);
        }

        await _portalRepository.DeleteAsync(portalId);

        // MIGRATION: single transaction boundary — the user deletions and the portal deletion are persisted together.
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // MIGRATION (OUT OF SCOPE): DeletePortalInfo L1192-L1196/L1205 also removed skin assignments and cleared the
        // host cache — skin/cache provisioning is excluded per AAP §0.6.2.
        return Result.Success();
    }

    /// <inheritdoc />
    // MIGRATION: Legacy HasSpaceAvailable(portalId, fileSizeBytes) (PortalController.vb L1323) — quota formula preserved
    // verbatim for behavioral parity (AAP §0.7.1).
    public async Task<Result<bool>> HasSpaceAvailableAsync(
        int portalId,
        long fileSizeBytes,
        CancellationToken cancellationToken = default)
    {
        var portal = await _portalRepository.GetByIdAsync(portalId);

        // MIGRATION: legacy used hostSpace=0 when PortalId = Null.NullInteger (-1); a not-found portal is treated the
        // same way (hostSpace=0) so the "unlimited" branch below applies.
        int hostSpace = (portalId < 0 || portal is null) ? 0 : portal.HostSpace;

        // MIGRATION: PortalController.GetPortalSpaceUsedBytes L1296 read the "SpaceUsed" column populated by file-system
        // provisioning, which is OUT OF SCOPE (AAP §0.6.2); usedBytes is treated as 0 here. Recorded in MIGRATION_NOTES.md.
        long usedBytes = 0;

        // MIGRATION: HasSpaceAvailable L1323 — preserved verbatim. Legacy:
        // (((GetPortalSpaceUsedBytes + fileSizeBytes) / 1024 ^ 2) <= hostSpace) Or (hostSpace = 0). VB '^' is power, so
        // 1024^2 = 1048576 (the MB divisor); VB '/' is floating division, reproduced via the 1048576d double divisor;
        // "hostSpace = 0" means unlimited.
        bool available = ((usedBytes + fileSizeBytes) / 1048576d <= hostSpace) || hostSpace == 0;

        return Result<bool>.Success(available);
    }
}
