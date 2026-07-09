using AutoMapper;
using DnnMigration.Application.DTOs;
using DnnMigration.Application.Exceptions;
using DnnMigration.Application.Interfaces;
using DnnMigration.Domain.Common;
using DnnMigration.Domain.Entities;
using DnnMigration.Domain.Interfaces;

namespace DnnMigration.Application.Services;

/// <summary>
/// Application service implementing module management business rules.
/// </summary>
/// <remarks>
/// MIGRATION: business rules extracted from the legacy DotNetNuke ModuleController.vb
/// (Library/Components/Modules/ModuleController.vb). Data access is delegated to
/// <see cref="IModuleRepository"/> (Domain port implemented over EF Core in the
/// Infrastructure layer) and entities are projected to DTOs via AutoMapper
/// (<see cref="IMapper"/>). The legacy controller co-mingled business logic with ADO.NET
/// <c>SqlDataProvider</c> calls.
/// <para>
/// A DNN module is a two-part aggregate: the portal-scoped <c>[Modules]</c> record (content
/// identity) PLUS one or more <c>[TabModules]</c> placement rows that position the module on a
/// tab pane and carry its presentation settings. This service now co-ordinates BOTH parts,
/// restoring the legacy placement side-effects (<c>AddTabModule</c> on create, <c>UpdateTabModule</c>
/// on update, and the <c>ON DELETE CASCADE</c> of a module's TabModules on delete, including the
/// <c>AllTabs</c> "add to every portal tab" fan-out) that the reviewer flagged as dropped. The
/// <see cref="ITabRepository"/> is injected purely to enumerate the portal's tabs for that fan-out.
/// </para>
/// <para>
/// The following legacy side-effects remain intentionally out of scope, each grounded in the request
/// contract and AAP §0.2.2, and documented inline with <c>// MIGRATION:</c> comments rather than
/// silently optimized away:
/// module-permission synchronization (the Create/Update DTOs carry NO ModulePermission payload — only
/// the persisted <c>InheritViewPermissions</c> flag — so there is nothing to sync; the permission
/// provider variant is out of scope per AAP §0.2.2); sibling module-order re-sequencing within a pane
/// (the caller-supplied <c>ModuleOrder</c> VALUE is persisted faithfully, but a cross-row reflow of
/// neighbouring modules is not performed); default/host-module registration (<c>DotNetNuke.Entities.Host</c>
/// / <c>Globals</c> are excluded by AAP §0.2.2 and no DTO field carries it); cache invalidation
/// (<c>DataCache</c>/<c>ClearCache</c> is cross-cutting and off the core migration path); and
/// search-index maintenance (the Search provider is excluded by AAP §0.2.2).
/// </para>
/// </remarks>
public sealed class ModuleService : IModuleService
{
    private readonly IModuleRepository _moduleRepository;
    private readonly ITabRepository _tabRepository;
    private readonly IMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="ModuleService"/> class.
    /// </summary>
    /// <param name="moduleRepository">The module repository used for all module + TabModule data access.</param>
    /// <param name="tabRepository">
    /// The tab repository, used only to enumerate a portal's tabs when a module is created with
    /// <c>AllTabs = true</c> (the legacy "add to all pages" placement fan-out).
    /// </param>
    /// <param name="mapper">The AutoMapper instance used to project entities to DTOs.</param>
    public ModuleService(IModuleRepository moduleRepository, ITabRepository tabRepository, IMapper mapper)
    {
        _moduleRepository = moduleRepository;
        _tabRepository = tabRepository;
        _mapper = mapper;
    }

    // MIGRATION: builds a [TabModules] placement row from the create request's placement fields. The
    // TabID is the target tab (a single tab, or each portal tab in turn for the AllTabs fan-out).
    // PaneName is required and non-null on the DTO; the presentation fields map across one-for-one.
    // MIGRATION (QA finding C): the store-generated ModuleID is NOT set here. Persisting the [Modules]
    // row and its placement rows is now a single atomic unit performed by
    // IModuleRepository.AddWithPlacementsAsync, which stamps each placement's ModuleID after the module
    // insert. Leaving ModuleID unset here keeps the placement decoupled from the (not-yet-assigned) id.
    private static TabModule BuildPlacement(CreateModuleDto dto, int tabId) => new()
    {
        TabID = tabId,
        PaneName = dto.PaneName,
        ModuleOrder = dto.ModuleOrder,
        CacheTime = dto.CacheTime,
        Alignment = dto.Alignment,
        Color = dto.Color,
        Border = dto.Border,
        IconFile = dto.IconFile,
        Visibility = dto.Visibility,
        ContainerSrc = dto.ContainerSrc,
        DisplayTitle = dto.DisplayTitle,
        DisplayPrint = dto.DisplayPrint,
        DisplaySyndicate = dto.DisplaySyndicate,
    };

    /// <inheritdoc />
    public async Task<IEnumerable<ModuleDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var modules = await _moduleRepository.GetAllAsync(cancellationToken);
        return _mapper.Map<IEnumerable<ModuleDto>>(modules);
    }

    /// <inheritdoc />
    // MIGRATION: ModuleController.GetModules(PortalID) [ModuleController.vb L915] =
    // FillModuleInfoCollection(DataProvider.GetModules(PortalID)); the ADO.NET reader hydration is
    // delegated to IModuleRepository.GetByPortalAsync and the result is projected to DTOs.
    public async Task<IEnumerable<ModuleDto>> GetByPortalAsync(int portalId, CancellationToken cancellationToken = default)
    {
        var modules = await _moduleRepository.GetByPortalAsync(portalId, cancellationToken);
        return _mapper.Map<IEnumerable<ModuleDto>>(modules);
    }

    /// <inheritdoc />
    // MIGRATION: ModuleController.GetModule(ModuleId, TabId, ignoreCache) [ModuleController.vb L885].
    // The legacy DataCache lookup (GetTabModules dictionary + TryGetValue) is DROPPED; reads go
    // straight to the repository. A missing module maps to null (legacy returned Nothing).
    public async Task<ModuleDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var module = await _moduleRepository.GetByIdAsync(id, cancellationToken);
        return module is null ? null : _mapper.Map<ModuleDto>(module);
    }

    /// <inheritdoc />
    // MIGRATION: ModuleController.GetModuleByDefinition(PortalId, FriendlyName) [ModuleController.vb L955].
    // The legacy DataCache.GetPersistentCacheItem dictionary cache is DROPPED; the by-definition
    // lookup is delegated to IModuleRepository.GetByDefinitionAsync. A missing module maps to null.
    public async Task<ModuleDto?> GetByDefinitionAsync(int portalId, string friendlyName, CancellationToken cancellationToken = default)
    {
        var module = await _moduleRepository.GetByDefinitionAsync(portalId, friendlyName, cancellationToken);
        return module is null ? null : _mapper.Map<ModuleDto>(module);
    }

    /// <inheritdoc />
    // MIGRATION: the legacy Website/admin/Modules/** inventory grid search. Delegated to
    // IModuleRepository.SearchAsync (case-insensitive substring over ModuleTitle - the only free-text
    // field physically on the Modules table; the denormalized FriendlyName/ModuleName live on related
    // tables and are unmapped - optionally portal-scoped) and projected to DTOs. Serves the AAP §0.7.2
    // GET /api/modules?query=...
    // contract server-side.
    public async Task<IEnumerable<ModuleDto>> SearchAsync(int? portalId, string query, CancellationToken cancellationToken = default)
    {
        var modules = await _moduleRepository.SearchAsync(portalId, query, cancellationToken);
        return _mapper.Map<IEnumerable<ModuleDto>>(modules);
    }

    /// <inheritdoc />
    // MIGRATION (QA finding — R6 Issue 1): bounded page of GetAllAsync. The repository fetches only the
    // Skip/Take window (fully hydrated) plus a COUNT; the page is projected to DTOs and the total count is
    // carried for the controller's pagination meta.
    public async Task<PagedResult<ModuleDto>> GetPagedAsync(int skip, int take, CancellationToken cancellationToken = default)
    {
        var page = await _moduleRepository.GetPagedAsync(skip, take, cancellationToken);
        var dtos = _mapper.Map<List<ModuleDto>>(page.Items);
        return new PagedResult<ModuleDto>(dtos, page.TotalCount);
    }

    /// <inheritdoc />
    // MIGRATION (QA finding — R6 Issue 1): bounded page of GetByPortalAsync.
    public async Task<PagedResult<ModuleDto>> GetByPortalPagedAsync(int portalId, int skip, int take, CancellationToken cancellationToken = default)
    {
        var page = await _moduleRepository.GetByPortalPagedAsync(portalId, skip, take, cancellationToken);
        var dtos = _mapper.Map<List<ModuleDto>>(page.Items);
        return new PagedResult<ModuleDto>(dtos, page.TotalCount);
    }

    /// <inheritdoc />
    // MIGRATION (QA finding — R6 Issue 1): bounded page of SearchAsync, serving the AAP §0.7.2
    // GET /api/modules?query=... search contract with server-side pagination.
    public async Task<PagedResult<ModuleDto>> SearchPagedAsync(int? portalId, string query, int skip, int take, CancellationToken cancellationToken = default)
    {
        var page = await _moduleRepository.SearchPagedAsync(portalId, query, skip, take, cancellationToken);
        var dtos = _mapper.Map<List<ModuleDto>>(page.Items);
        return new PagedResult<ModuleDto>(dtos, page.TotalCount);
    }

    /// <inheritdoc />
    // MIGRATION: ModuleController.AddModule(objModule) [ModuleController.vb L645] persisted the [Modules]
    // record (DataProvider.AddModule, which assigned the new id) AND placed the module on a tab pane by
    // writing a [TabModules] row (DataProvider.AddTabModule), fanning that placement out to every portal
    // tab when AllTabs was set. BOTH the module record and its TabModule placement(s) are now restored:
    //   (a) persist the [Modules] row first so the store-generated ModuleID is available for the FK; then
    //   (b) write the placement row(s) — one per portal tab when dto.AllTabs is true (the legacy "add to
    //       all pages" fan-out via ITabRepository.GetByPortalAsync), otherwise a single row for dto.TabID.
    // A non-positive TabID with AllTabs=false places nothing (guard against orphaned rows; the legacy add
    // path always targeted a real tab).
    //
    // OUT OF SCOPE (see class remarks): the legacy AddModule also synced ModulePermissions
    // (ModulePermissionController.AddModulePermission) — the CreateModuleDto carries NO permission payload,
    // only InheritViewPermissions, so there is nothing to sync (contract-grounded) and the permission
    // provider variant is excluded by AAP §0.2.2; re-sequenced sibling ModuleOrder within the pane
    // (UpdateModuleOrder/UpdateTabModuleOrder) — the supplied order VALUE is persisted, sibling reflow is
    // not; and cleared the tab cache (ClearCache/DataCache) — cross-cutting caching off the core path.
    public async Task<ModuleDto> CreateAsync(CreateModuleDto dto, CancellationToken cancellationToken = default)
    {
        // Map the [Modules] record from the request. The store-generated ModuleID is assigned when the
        // row is persisted below (atomically, together with its placement rows).
        var module = _mapper.Map<Module>(dto);

        // Compute the module's placement rows (business rule). AllTabs fans the placement out to every
        // tab in the portal (those tabs exist by construction); otherwise the module is placed on the
        // single target tab; a non-positive TabID (with AllTabs=false) places nothing.
        var placements = new List<TabModule>();
        if (dto.AllTabs)
        {
            var tabs = await _tabRepository.GetByPortalAsync(dto.PortalID, cancellationToken);
            foreach (var tab in tabs)
            {
                placements.Add(BuildPlacement(dto, tab.TabID));
            }
        }
        else if (dto.TabID > 0)
        {
            // MIGRATION (QA finding C): PRE-VALIDATE the target tab exists BEFORE any write. The legacy
            // add path always targeted a real tab; a request naming a non-existent tab (e.g. TabID
            // 999999) previously reached the [TabModules] insert and violated the
            // FK_TabModules_Tabs foreign key, surfacing as a raw HTTP 500 AND leaving an orphaned
            // [Modules] row behind (the module had already been inserted in its own SaveChanges). By
            // checking the tab up front and throwing a ConflictException, the caller receives a clean
            // HTTP 409 (RFC 7807 Problem Details) and NO module row is created. This runs on both the
            // SQL Server host and the EF Core InMemory provider (a plain read + guard, no FK reliance).
            var targetTab = await _tabRepository.GetByIdAsync(dto.TabID, cancellationToken);
            if (targetTab is null)
            {
                throw new ConflictException(
                    $"Cannot create the module: the target tab (TabID {dto.TabID}) does not exist.");
            }

            placements.Add(BuildPlacement(dto, dto.TabID));
        }

        // MIGRATION (QA finding C): persist the [Modules] row and its [TabModules] placement row(s) as a
        // SINGLE ATOMIC UNIT. On a relational provider the repository wraps both saves in a transaction,
        // so if any placement write fails the module insert is rolled back too - never an orphaned module
        // row. The repository stamps each placement's ModuleID from the store-generated module id.
        var created = await _moduleRepository.AddWithPlacementsAsync(module, placements, cancellationToken);

        return _mapper.Map<ModuleDto>(created);
    }

    /// <inheritdoc />
    // MIGRATION: ModuleController.UpdateModule(objModule) [ModuleController.vb L1095] performed a field
    // copy of the [Modules] record (DataProvider.UpdateModule) AND a tab-module update
    // (DataProvider.UpdateTabModule) that persisted edits to the module's pane placement / presentation
    // settings. BOTH are now restored:
    //   (a) in-place field copy onto the [Modules] record (AutoMapper Map(dto, module)); then
    //   (b) propagate the placement/presentation field edits onto EVERY [TabModules] row the module owns.
    // A missing module maps to null (legacy update targeted an existing record) and performs no writes.
    //
    // BOUNDARY: toggling AllTabs on UPDATE does NOT retroactively add or remove placement rows — the
    // legacy "add to all tabs" fan-out ran only on the ADD path; here update mutates the fields of the
    // module's EXISTING placement rows. UpdateModuleDto.PaneName is nullable, so a null PaneName preserves
    // the row's current pane rather than clearing it.
    //
    // OUT OF SCOPE (see class remarks): permission synchronization (DeleteModulePermissionsByModuleID /
    // AddModulePermission) — the UpdateModuleDto carries no permission payload (contract-grounded) and the
    // permission provider variant is excluded by AAP §0.2.2; sibling module-order repositioning
    // (UpdateModuleOrder) — the supplied order VALUE is persisted, sibling reflow is not; default-module
    // registration (IsDefaultModule -> host/portal setting) — Host/Globals are excluded by AAP §0.2.2 and
    // no DTO field carries it; and the cache clear (ClearCache/DataCache) — cross-cutting, off the core path.
    public async Task<ModuleDto?> UpdateAsync(int id, UpdateModuleDto dto, CancellationToken cancellationToken = default)
    {
        var module = await _moduleRepository.GetByIdAsync(id, cancellationToken);
        if (module is null)
        {
            return null;
        }

        // (a) Field copy onto the [Modules] record.
        _mapper.Map(dto, module);
        await _moduleRepository.UpdateAsync(module, cancellationToken);

        // (b) Propagate the placement/presentation edits onto the module's existing [TabModules] rows.
        var placements = await _moduleRepository.GetTabModulesByModuleAsync(id, cancellationToken);
        foreach (var placement in placements)
        {
            // PaneName is nullable on the update DTO; a null value preserves the row's current pane.
            placement.PaneName = dto.PaneName ?? placement.PaneName;
            placement.ModuleOrder = dto.ModuleOrder;
            placement.CacheTime = dto.CacheTime;
            placement.Alignment = dto.Alignment;
            placement.Color = dto.Color;
            placement.Border = dto.Border;
            placement.IconFile = dto.IconFile;
            placement.Visibility = dto.Visibility;
            placement.ContainerSrc = dto.ContainerSrc;
            placement.DisplayTitle = dto.DisplayTitle;
            placement.DisplayPrint = dto.DisplayPrint;
            placement.DisplaySyndicate = dto.DisplaySyndicate;
            await _moduleRepository.UpdateTabModuleAsync(placement, cancellationToken);
        }

        return _mapper.Map<ModuleDto>(module);
    }

    /// <inheritdoc />
    // MIGRATION: ModuleController.DeleteModule(ModuleId) [ModuleController.vb L819] removed the [Modules]
    // record; the legacy ON DELETE CASCADE FK_{objectQualifier}TabModules_{objectQualifier}Modules removed
    // the module's [TabModules] placement rows automatically. That placement cascade is now restored
    // explicitly (the EF Core InMemory provider does not enforce referential cascade, so the placement
    // rows are removed first for provider-agnostic parity), then the module record is deleted. Returns
    // false when the module does not exist so the API layer can surface a 404 (legacy performed an
    // unconditional delete).
    //
    // OUT OF SCOPE (see class remarks): removal of the module's search-index entries
    // (DataProvider.DeleteSearchItems) — the Search provider is excluded by AAP §0.2.2.
    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var module = await _moduleRepository.GetByIdAsync(id, cancellationToken);
        if (module is null)
        {
            return false;
        }

        // Cascade the module's TabModule placement rows first, then remove the module record.
        await _moduleRepository.DeleteTabModulesByModuleAsync(id, cancellationToken);
        await _moduleRepository.DeleteAsync(id, cancellationToken);
        return true;
    }
}
