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
    private readonly IPasswordHasher _passwordHasher;
    private readonly ICredentialStore _credentialStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="PortalService"/> class.
    /// </summary>
    /// <param name="portalRepository">Portal data-access abstraction (replaces DataProvider portal operations).</param>
    /// <param name="userRepository">User data-access abstraction (required to bootstrap the portal administrator and to delete portal users on portal deletion).</param>
    /// <param name="unitOfWork">Transactional persistence boundary (replaces the legacy DataProvider transaction surface).</param>
    /// <param name="mapper">AutoMapper instance configured with the Portal mapping profile.</param>
    /// <param name="passwordHasher">One-way (BCrypt) password hasher used to hash the bootstrapped administrator's initial password (AAP §0.7.6).</param>
    /// <param name="credentialStore">Credential-store port that persists the administrator's hashed password so the account is never credentialless.</param>
    // MIGRATION: Constructor injection ONLY (AAP §0.7.3) — replaces the legacy DataProvider.Instance() reflection
    // singleton lookup and the `New PortalController`/`New UserController` direct instantiations. CP1 review
    // (PortalService #3): IPasswordHasher + ICredentialStore are injected so the portal-administrator bootstrap can
    // hash and persist the admin credential through the SAME ports UserService.CreateAsync uses (never credentialless).
    public PortalService(
        IPortalRepository portalRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        IPasswordHasher passwordHasher,
        ICredentialStore credentialStore)
    {
        ArgumentNullException.ThrowIfNull(portalRepository);
        ArgumentNullException.ThrowIfNull(userRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(mapper);
        ArgumentNullException.ThrowIfNull(passwordHasher);
        ArgumentNullException.ThrowIfNull(credentialStore);

        _portalRepository = portalRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _passwordHasher = passwordHasher;
        _credentialStore = credentialStore;
    }

    /// <inheritdoc />
    // MIGRATION: PortalController.GetPortals() L1263 returned all portals (FillPortalInfoCollection); the legacy host
    // grid paged via GetPortalsByName(..., pageIndex, pageSize, ByRef total) at the DB (PortalController.vb L262).
    public async Task<Result<PagedResult<PortalListItemDto>>> GetAllAsync(
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        // MIGRATION: CP1 review (PortalService #1) — validate paging inputs BEFORE any repository access. A negative
        // page index or a non-positive page size is a controlled validation failure (Api -> 400 ProblemDetails), never
        // a repository call with invalid bounds. Mirrors UserService/RoleService exact-message parity.
        if (pageIndex < 0)
        {
            return Result<PagedResult<PortalListItemDto>>.Failure("Page index must be zero or greater.");
        }

        if (pageSize <= 0)
        {
            return Result<PagedResult<PortalListItemDto>>.Failure("Page size must be greater than zero.");
        }

        // MIGRATION: CP1 review (PortalService #5 / performance #22, AAP 0.7.7) — use the paged repository so ONLY the
        // requested page plus the total count is materialized, replacing the previous fetch-all-then-Skip/Take-in-memory.
        // PageIndex stays ZERO-BASED (see PagedResult) for behavioral parity with the legacy GetPortalsByName paging.
        var (portals, total) = await _portalRepository.GetPagedAsync(pageIndex, pageSize);

        var pageItems = portals
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
    // defaults, then bootstrap the portal Administrator and assign portal.AdministratorId (CP1 review PortalService #3)
    // — is transcribed here; only the skin/file-system/template/alias provisioning remains OUT-OF-SCOPE (AAP §0.6.2),
    // as documented inline below.
    public async Task<Result<PortalDto>> CreateAsync(
        CreatePortalRequest request,
        CancellationToken cancellationToken = default)
    {
        // MIGRATION (CP1 review PortalService #2): fail-fast null guard. A null request is a programming/binding error,
        // not an expected business failure, so it throws before any request field is dereferenced (the Api maps it to a
        // ProblemDetails) — consistent with the constructor null guards.
        ArgumentNullException.ThrowIfNull(request);

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

        // MIGRATION (CP1 review PortalService #3 — admin-user bootstrap): PortalController.CreatePortal L998-L1019
        // created the portal Administrator — a new UserInfo populated with Username/Password/FirstName/LastName/Email,
        // DisplayName = FirstName + " " + LastName (L1004), Membership.Approved=True (L1008) and IsSuperUser=False
        // (L1007), persisted via UserController.CreateUser — and then assigned portal.AdministratorId = the new UserID
        // (L1015-1016). This is now PERFORMED here when the optional Admin* group is supplied (CreatePortalValidator
        // requires the whole group together). The credential is hashed one-way with BCrypt (IPasswordHasher, AAP §0.7.6
        // — replaces the legacy reversible Membership.Password/DES) and persisted through the ICredentialStore port —
        // the SAME ports UserService.CreateAsync uses — so the administrator is never credentialless. A brand-new portal
        // has no existing users (a duplicate username is impossible) and no roles yet (the AutoAssignment-role enrolment
        // would be a no-op), so neither is re-checked here. When the admin group is omitted the portal is created
        // without an administrator and AdministratorId is left unset — the administrator can be provisioned later via the
        // User API. Recorded in MIGRATION_NOTES.md.
        if (!string.IsNullOrEmpty(request.AdminUsername) && !string.IsNullOrEmpty(request.AdminPassword))
        {
            var admin = new User
            {
                PortalId = portal.PortalId,
                Username = request.AdminUsername,
                FirstName = request.AdminFirstName,
                LastName = request.AdminLastName,
                // MIGRATION: PortalController.CreatePortal L1004 — DisplayName = FirstName + " " + LastName (verbatim).
                DisplayName = $"{request.AdminFirstName} {request.AdminLastName}",
                Email = request.AdminEmail,
                IsSuperUser = false,
                IsApproved = true
            };

            await _userRepository.AddAsync(admin);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // MIGRATION: legacy persisted Membership.Password for the admin; the target hashes the initial password
            // one-way with BCrypt and persists it via the credential port AFTER the first SaveChanges so the
            // database-generated UserId is available to key the credential (mirrors UserService.CreateAsync).
            var passwordHash = _passwordHasher.Hash(request.AdminPassword);
            await _credentialStore.SetPasswordAsync(admin.UserId, passwordHash, cancellationToken);

            // MIGRATION: PortalController.CreatePortal L1015-1016 — assign portal.AdministratorId to the new admin's id.
            portal.AdministratorId = admin.UserId;
            await _portalRepository.UpdateAsync(portal);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

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
        // CP1 review (IUserRepository #1): IUserRepository.DeleteAsync is now PORTAL-SCOPED, so each delete passes the
        // owning portalId (every user here was read via GetByPortalIdAsync(portalId), so the scope is consistent).
        foreach (var user in await _userRepository.GetByPortalIdAsync(portalId))
        {
            await _userRepository.DeleteAsync(portalId, user.UserId);
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

        // MIGRATION: CP1 review (PortalService #4 CRITICAL) — the numerator MUST be the real consumed bytes, not a
        // hardcoded 0 (which made the quota permissive for portals that had already consumed storage). Ported from
        // PortalController.GetPortalSpaceUsedBytes(portalId) L1296 (reads the persisted "SpaceUsed" column) via the new
        // IPortalRepository.GetSpaceUsedBytesAsync port. The query is skipped only when there is no portal (a missing or
        // -1/Null.NullInteger portal) — exactly the case where hostSpace=0 short-circuits the check to "available", and
        // where the legacy GetPortalSpaceUsedBytes(-1) returned 0 anyway, so the numeric result is identical.
        long usedBytes = portal is null ? 0 : await _portalRepository.GetSpaceUsedBytesAsync(portalId);

        // MIGRATION: HasSpaceAvailable L1323 — preserved verbatim. Legacy:
        // (((GetPortalSpaceUsedBytes + fileSizeBytes) / 1024 ^ 2) <= hostSpace) Or (hostSpace = 0). VB '^' is power, so
        // 1024^2 = 1048576 (the MB divisor); VB '/' is floating division, reproduced via the 1048576d double divisor;
        // "hostSpace = 0" means unlimited.
        bool available = ((usedBytes + fileSizeBytes) / 1048576d <= hostSpace) || hostSpace == 0;

        return Result<bool>.Success(available);
    }
}
