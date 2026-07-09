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
    private readonly IUserService _userService;
    private readonly IMapper _mapper;

    /// <summary>
    /// Initializes a new <see cref="PortalService"/> with its injected collaborators.
    /// </summary>
    /// <param name="portalRepository">Repository providing persistence for portal aggregates (and portal aliases).</param>
    /// <param name="userService">
    /// Application service used to provision a new portal's initial administrator user. Admin creation is
    /// delegated here rather than duplicated so a SINGLE authoritative user-creation path runs:
    /// <see cref="IUserService.CreateAsync"/> hashes the password (BCrypt via the injected password hasher),
    /// derives the display name, and writes the Users + aspnet_Membership + UserPortals rows.
    /// </param>
    /// <param name="mapper">AutoMapper instance projecting entities to/from DTOs.</param>
    public PortalService(IPortalRepository portalRepository, IUserService userService, IMapper mapper)
    {
        _portalRepository = portalRepository;
        _userService = userService;
        _mapper = mapper;
    }

    // MIGRATION: PortalController.GetPortals() [L1263] = FillPortalInfoCollection(DataProvider.GetPortals()).
    // The stored-proc reader + ArrayList hydration becomes an async repository fetch mapped to a DTO sequence.
    // MIGRATION: the legacy grid additionally rendered a "Portal Aliases" column (FormatPortalAliases); the
    // read model's Aliases are populated here from a single grouped alias lookup (no N+1) since the Portal
    // entity intentionally carries no PortalAlias navigation collection.
    /// <inheritdoc />
    public async Task<IEnumerable<PortalDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var portals = await _portalRepository.GetAllAsync(cancellationToken);
        var dtos = _mapper.Map<List<PortalDto>>(portals);
        await PopulateAliasesAsync(dtos, cancellationToken);
        return dtos;
    }

    // MIGRATION: PortalController.GetPortalsByName(nameToMatch, ...) — the Portals.ascx.vb grid text/letter
    // search. Delegated to IPortalRepository.SearchAsync (case-insensitive substring over name/description/
    // keywords) and projected to DTOs, with aliases populated exactly as GetAllAsync does. Serves the
    // AAP §0.7.2 GET /api/portals?query=... contract server-side.
    /// <inheritdoc />
    public async Task<IEnumerable<PortalDto>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        var portals = await _portalRepository.SearchAsync(query, cancellationToken);
        var dtos = _mapper.Map<List<PortalDto>>(portals);
        await PopulateAliasesAsync(dtos, cancellationToken);
        return dtos;
    }

    // Fills each projected portal's Aliases from a single grouped repository lookup. A portal with no
    // aliases keeps its default (empty) Aliases. PortalDto is an immutable record, so each element is
    // replaced via a non-destructive `with` expression.
    private async Task PopulateAliasesAsync(List<PortalDto> dtos, CancellationToken cancellationToken)
    {
        if (dtos.Count == 0)
        {
            return;
        }

        var aliasesByPortal = await _portalRepository.GetAliasesAsync(cancellationToken);
        for (var i = 0; i < dtos.Count; i++)
        {
            if (aliasesByPortal.TryGetValue(dtos[i].PortalID, out var aliases))
            {
                dtos[i] = dtos[i] with { Aliases = aliases };
            }
        }
    }

    // MIGRATION: PortalController.GetPortal(PortalId) [L1224] performed a DataCache lookup, then
    // DataProvider.GetPortal -> FillPortalInfo, then re-cached the result. The DataCache caching layer is
    // dropped here (this service is stateless); a missing portal (null) is projected to null.
    /// <inheritdoc />
    public async Task<PortalDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var portal = await _portalRepository.GetByIdAsync(id, cancellationToken);
        if (portal is null)
        {
            return null;
        }

        // MIGRATION: populate the read model's Portal Aliases (legacy FormatPortalAliases) from the
        // single-portal alias lookup; PortalDto is an immutable record, so use a non-destructive `with`.
        var dto = _mapper.Map<PortalDto>(portal);
        var aliases = await _portalRepository.GetAliasesForPortalAsync(id, cancellationToken);
        return dto with { Aliases = aliases };
    }

    // MIGRATION: mirrors the legacy PortalAliasController/GetPortalByAlias lookup (an HTTP-alias -> PortalInfo
    // resolution). Delegated to IPortalRepository.GetByAliasAsync; a missing match (null) is projected to null.
    /// <inheritdoc />
    public async Task<PortalDto?> GetByAliasAsync(string alias, CancellationToken cancellationToken = default)
    {
        var portal = await _portalRepository.GetByAliasAsync(alias, cancellationToken);
        return portal is null ? null : _mapper.Map<PortalDto>(portal);
    }

    // MIGRATION: PortalController.CreatePortal(...) [Library/Components/Portal/PortalController.vb L980-L1075].
    // The legacy method was an ORCHESTRATION, not a single INSERT: it (a) added the portal row (AddPortalInfo,
    // yielding the new PortalId), (b) created the initial administrator user via UserController.CreateUser
    // [L1013], (c) set PortalInfo.AdministratorId to that user and persisted the back-reference
    // (UpdatePortalInfo), and (d) registered the initial PortalAlias (PortalAliasController.AddPortalAlias).
    // Steps (a)-(d) are ALL IN SCOPE (core Portal/User parity) and are reproduced below as an application use
    // case coordinating the portal repository, the user service (which owns credential hashing), and the alias
    // repository port. Consuming FirstName/LastName/Username/Password/Email/PortalAlias here closes the parity
    // gap the review flagged, where a created portal had no administrator (AdministratorId defaulted) and could
    // not be resolved by host alias.
    //
    // OUT OF SCOPE (AAP section 0.2.2) and therefore intentionally NOT performed here: the legacy template
    // deserialization + parsing (ParseTemplate), profile-definition seeding (CreateProfileDefinitions),
    // resource-file processing (ProcessResourceFile), and Home-directory / physical file-system creation
    // [L996-L1075]; DataCache priming; e-mail notification; and event-log writes. Those are presentation /
    // host / cross-cutting concerns excluded from the core migration set - not portal-record behavior. There is
    // no ambient transaction here (parity: the legacy sequence was likewise non-transactional; the EF Core
    // InMemory provider used by the integration tests does not support transactions); a partial failure is
    // surfaced to the caller via the thrown exception and the RFC 7807 error middleware.
    /// <inheritdoc />
    public async Task<PortalDto> CreateAsync(CreatePortalDto dto, CancellationToken cancellationToken = default)
    {
        // (a) Persist the portal row FIRST so the store-generated PortalID is available to associate the
        //     administrator (through the UserPortals junction), wire the AdministratorId back-reference, and
        //     register the initial alias.
        var portal = _mapper.Map<Portal>(dto);
        var created = await _portalRepository.AddAsync(portal, cancellationToken);

        // (b) Provision the initial administrator. Delegated to IUserService.CreateAsync so the single
        //     authoritative user-creation path runs: it hashes the password (BCrypt), derives the display
        //     name, and writes the Users + aspnet_Membership + UserPortals rows. Authorize = true so the
        //     administrator is immediately approved (legacy administrators were created pre-approved), and
        //     PortalID = the new portal so the junction associates the admin with THIS portal.
        var adminRequest = new CreateUserDto
        {
            Username = dto.Username,
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            DisplayName = $"{dto.FirstName} {dto.LastName}".Trim(),
            Email = dto.Email,
            Password = dto.Password,
            ConfirmPassword = dto.ConfirmPassword,
            PortalID = created.PortalID,
            Authorize = true,
        };
        var administrator = await _userService.CreateAsync(adminRequest, cancellationToken);

        // (c) Wire the portal's AdministratorId to the newly-created admin and persist the back-reference
        //     (legacy set PortalInfo.AdministratorId then called UpdatePortalInfo).
        created.AdministratorId = administrator.UserID;
        await _portalRepository.UpdateAsync(created, cancellationToken);

        // (d) Register the initial HTTP alias so the portal is resolvable by host alias (legacy
        //     PortalAliasController.AddPortalAlias). Guarded so a blank alias is simply not written.
        if (!string.IsNullOrWhiteSpace(dto.PortalAlias))
        {
            await _portalRepository.AddAliasAsync(
                new PortalAlias { PortalID = created.PortalID, HTTPAlias = dto.PortalAlias },
                cancellationToken);
        }

        // Project the freshly-persisted portal (now carrying AdministratorId) to a DTO, enriched with the
        // alias just registered so the create response is consistent with a subsequent GET (resolvable by
        // alias). PortalDto is an immutable record, so aliases are applied via a non-destructive `with`.
        var resultDto = _mapper.Map<PortalDto>(created);
        var aliases = await _portalRepository.GetAliasesForPortalAsync(created.PortalID, cancellationToken);
        return resultDto with { Aliases = aliases };
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
