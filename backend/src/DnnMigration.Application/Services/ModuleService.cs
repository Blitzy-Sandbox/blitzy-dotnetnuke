using AutoMapper;
using DnnMigration.Application.DTOs.Common;
using DnnMigration.Application.DTOs.Module;
using DnnMigration.Application.Interfaces;
using DnnMigration.Domain.Common;
using DnnMigration.Domain.Entities;
using DnnMigration.Domain.Interfaces;

namespace DnnMigration.Application.Services;

// MIGRATION: Application-layer service holding the Module business rules. Replaces the orchestration and
// data-access portions of the monolithic VB.NET DotNetNuke.Entities.Modules.ModuleController
// (Library/Components/Modules/ModuleController.vb, 1456 lines). The legacy controller mixed business rules
// with data access via the reflection-instantiated DataProvider.Instance() + SqlHelper + stored-procedure +
// IDataReader/FillModuleInfo pipeline; here the business rules live in this service while data access is
// delegated to the injected IModuleRepository and the persistence boundary to IUnitOfWork
// (Clean/Onion, AAP §0.3.3/§0.7.3).
// MIGRATION: This service NEVER returns a raw Domain entity — every public result is a DTO wrapped in
// Result/Result<T> (AAP §0.7.7). The top-level entity<->DTO projection goes through the injected IMapper
// (Mapping/ModuleProfile). The inbound ModulePermission child collection is hand-built from ModulePermissionDto
// (MapModulePermission below) because it is part of the module-permission lifecycle RULE (the legacy add-all /
// diff-on-update behavior), not a top-level projection — keeping it in the service keeps the rule readable and
// avoids a cyclic AutoMapper graph (ModuleResponse intentionally omits permissions). No DbContext/EF Core/
// System.Data reference exists in this layer (AAP §0.7.3). Modules remain scoped by PortalId (tenant) and TabId
// (page placement), preserving DNN multi-tenant isolation (AAP §0.7.1).
/// <summary>
/// Implements <see cref="IModuleService"/>: module CRUD plus portal- and tab-scoped listings, orchestrating
/// the module repository, the unit of work, and the AutoMapper projections. Mirrors the canonical
/// <c>PortalService</c> CRUD shape (Gate 5 validates Module CRUD status codes).
/// </summary>
public sealed class ModuleService : IModuleService
{
    private readonly IModuleRepository _moduleRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly IPortalSettingsService _portalSettingsService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ModuleService"/> class.
    /// </summary>
    /// <param name="moduleRepository">Module data-access abstraction (replaces the legacy DataProvider module operations).</param>
    /// <param name="unitOfWork">Transactional persistence boundary (replaces the legacy DataProvider transaction surface).</param>
    /// <param name="mapper">AutoMapper instance configured with the Module mapping profile.</param>
    /// <param name="portalSettingsService">Per-portal site-settings port; writes the "defaultmoduleid"/"defaulttabid" settings when a module is flagged the portal default (legacy UpdateModule L1126-1130).</param>
    // MIGRATION: Constructor injection ONLY (AAP §0.7.3) — replaces the legacy DataProvider.Instance()
    // reflection singleton lookup and the `New ModulePermissionController`/`New TabController` direct
    // instantiations scattered throughout ModuleController.vb.
    // MIGRATION: CP1 review (RoleService #1 / enterprise guard standards) — fail-fast null guards on every dependency
    // so DI misconfiguration surfaces at construction rather than later as a NullReferenceException.
    public ModuleService(
        IModuleRepository moduleRepository,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        IPortalSettingsService portalSettingsService)
    {
        ArgumentNullException.ThrowIfNull(moduleRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(mapper);
        ArgumentNullException.ThrowIfNull(portalSettingsService);

        _moduleRepository = moduleRepository;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _portalSettingsService = portalSettingsService;
    }

    /// <inheritdoc />
    // MIGRATION: ModuleController.GetModules(PortalID) L915 -> FillModuleInfoCollection(DataProvider.GetModules(PortalID)).
    // Portal-scoped for multi-tenant isolation (AAP §0.7.1). The legacy GetPortalModules SP returns only non-deleted
    // modules (IsDeleted = 0); that filter now lives in the paged repository so the count and page window are correct.
    public async Task<Result<PagedResult<ModuleResponse>>> GetByPortalAsync(
        int portalId,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        // MIGRATION: CP1 review (ModuleService #1) — validate paging inputs BEFORE any repository access; a negative
        // page index or non-positive page size is a controlled validation failure (Api -> 400), never an invalid call.
        if (pageIndex < 0)
        {
            return Result<PagedResult<ModuleResponse>>.Failure("Page index must be zero or greater.");
        }

        if (pageSize <= 0)
        {
            return Result<PagedResult<ModuleResponse>>.Failure("Page size must be greater than zero.");
        }

        // MIGRATION: CP1 review (ModuleService #5 / performance #22) — use the paged repository so ONLY the requested
        // page plus the total count is materialized (no fetch-all-then-page-in-memory). The repository applies the
        // IsDeleted = 0 filter before paging. PageIndex stays zero-based for behavioral parity.
        var (modules, total) = await _moduleRepository.GetByPortalPagedAsync(portalId, pageIndex, pageSize);

        var pageItems = modules
            .Select(m => _mapper.Map<ModuleResponse>(m))
            .ToList();

        return Result<PagedResult<ModuleResponse>>.Success(new PagedResult<ModuleResponse>
        {
            Items = pageItems,
            TotalCount = total,
            PageIndex = pageIndex,
            PageSize = pageSize,
        });
    }

    /// <inheritdoc />
    // MIGRATION: ModuleController.GetTabModules(TabId) L1044 -> FillModuleInfoDictionary(DataProvider.GetTabModules(TabId)).
    // A tab/page hosts only a handful of modules, so the result is an unpaged IEnumerable. Soft-deleted modules are
    // excluded for parity with the active portal listing.
    // MIGRATION: legacy GetModule/GetTabModules used DataCache cache-aside (L885/L1044); caching is an Infrastructure
    // concern, omitted here.
    public async Task<Result<IEnumerable<ModuleResponse>>> GetByTabAsync(
        int portalId,
        int tabId,
        CancellationToken cancellationToken = default)
    {
        // MIGRATION: CP1 review (IModuleService #1 / IModuleRepository #1) — PORTAL-SCOPED so a tab's modules are read
        // only within the owning portal (multi-tenant isolation, AAP §0.7.1).
        var modules = (await _moduleRepository.GetByTabIdAsync(portalId, tabId))
            .Where(m => !m.IsDeleted);

        return Result<IEnumerable<ModuleResponse>>.Success(
            modules.Select(m => _mapper.Map<ModuleResponse>(m)).ToList());
    }

    /// <inheritdoc />
    // MIGRATION: ModuleController.GetModule(ModuleId, TabId, ignoreCache) L885. CP1 review (IModuleService #1 /
    // IModuleRepository #1) — PORTAL-SCOPED: the lookup is constrained to portalId so a module from another portal is
    // never returned (multi-tenant isolation, AAP §0.7.1). A missing/unowned module is an expected business failure
    // (Result.Failure) -> Api 404 ProblemDetails. CP1 review #7 — the message is OPAQUE (no raw id). Caching is an
    // Infrastructure concern, omitted here.
    public async Task<Result<ModuleResponse>> GetByIdAsync(
        int portalId,
        int moduleId,
        CancellationToken cancellationToken = default)
    {
        var module = await _moduleRepository.GetByIdAsync(portalId, moduleId);
        if (module is null)
        {
            return Result<ModuleResponse>.Failure("The requested module was not found.");
        }

        return Result<ModuleResponse>.Success(_mapper.Map<ModuleResponse>(module));
    }

    /// <inheritdoc />
    // MIGRATION: ModuleController.AddModule(objModule) L645-682 — ported in full (CP1 review ModuleService #3, CRITICAL).
    // Legacy sequence: (1) insert the module row (L648); (2) add EVERY supplied ModulePermission with NO AllowAccess
    // filter at add time (ModulePermissionController.AddModulePermission, L649-659); (3) place the module on the tab
    // (DataProvider.AddTabModule, L662-665) inside a try/catch that IGNORED the "module already in the page" error so a
    // duplicate placement never hard-failed; (4) compute module order — when ModuleOrder = -1 place at the BOTTOM of the
    // pane (max existing order + 2, UpdateModuleOrder L1160-1173) then re-sequence the pane (UpdateTabModuleOrder
    // L1197-1209, Counter*2-1). ModuleProfile maps the creation fields and Ignores the identity/registration/definition
    // metadata, permissions, and ControlType.
    public async Task<Result<ModuleResponse>> CreateAsync(
        CreateModuleRequest request,
        CancellationToken cancellationToken = default)
    {
        // MIGRATION: CP1 review (ModuleService #2) — fail-fast null guard before mapping/reading request fields.
        ArgumentNullException.ThrowIfNull(request);

        var module = _mapper.Map<Module>(request);

        // MIGRATION: AddModule L648 inserted with the IsDeleted flag explicitly passed through; a newly created module
        // is never soft-deleted, so IsDeleted is defaulted false.
        module.IsDeleted = false;

        // MIGRATION: AddModule L649-659 — add EVERY supplied permission with NO AllowAccess filter at add time. The
        // legacy add path persisted the full ModulePermissionCollection (the filtering/diff only happened on UPDATE).
        module.ModulePermissions = request.Permissions
            .Select(p => MapModulePermission(p, module.ModuleId))
            .ToList();

        // MIGRATION: AddModule/UpdateModuleOrder L1160-1173 — when ModuleOrder = -1 the module is placed at the BOTTOM of
        // its pane: max existing order in (tab, pane) + 2. (The +2 leaves an even-numbered gap so the subsequent
        // Counter*2-1 re-sequence can interleave deterministically.)
        if (module.ModuleOrder == -1)
        {
            module.ModuleOrder = await GetMaxPaneOrderAsync(request.PortalId, request.TabId, module.PaneName) + 2;
        }

        // MIGRATION: AddModule L662-665 placed the module on the tab inside a try/catch that *ignored* the "module
        // already in the page" error, so re-adding an existing module to a page was tolerated and never hard-failed.
        // Accordingly no duplicate-definition / duplicate-placement rejection is performed here. Recorded in MIGRATION_NOTES.md.
        await _moduleRepository.AddAsync(module);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // MIGRATION: AddModule L667-673 -> UpdateTabModuleOrder (L1197-1209) re-sequenced the tab's placements per pane
        // (Counter*2-1) after the insert so orders stay normalized. Run against the owning portal/tab.
        await ResequenceTabAsync(request.PortalId, request.TabId, cancellationToken);

        // MIGRATION: ModuleResponse intentionally omits the permission collection, so the in-memory module (with its
        // final ModuleOrder) is the faithful projection — no reload needed.
        return Result<ModuleResponse>.Success(_mapper.Map<ModuleResponse>(module));
    }

    /// <inheritdoc />
    // MIGRATION: ModuleController.UpdateModule(objModule) L1095-1148 — ported in full (CP1 review ModuleService #3, CRITICAL).
    // Legacy sequence: (1) UpdateModule row; (2) diff module permissions — ModulePermissionCollection.CompareTo, and when
    // they differ delete all then re-add, SKIPPING any grant where InheritViewPermissions && PermissionKey = "VIEW" (that
    // VIEW grant is inherited from the tab) and persisting only AllowAccess grants (L1099-1118); (3) UpdateTabModule +
    // UpdateModuleOrder re-sequence (L1122-1124); (4) when IsDefaultModule, write the portal "defaultmoduleid"/
    // "defaulttabid" site settings (L1126-1130); (5) when AllModules, propagate THIS module's display settings to every
    // module on every (non-admin) tab in the portal (L1132-1144). CP1 review (IModuleService #1) — PORTAL-SCOPED.
    public async Task<Result<ModuleResponse>> UpdateAsync(
        int portalId,
        int moduleId,
        UpdateModuleRequest request,
        CancellationToken cancellationToken = default)
    {
        // MIGRATION: CP1 review (ModuleService #2) — fail-fast null guard before reading request fields.
        ArgumentNullException.ThrowIfNull(request);

        // MIGRATION: CP1 review (IModuleService #1 / IModuleRepository #1) — PORTAL-SCOPED lookup so a module from another
        // portal can never be updated through this tenant (multi-tenant isolation, AAP §0.7.1). The repository
        // eager-loads ModulePermissions so the diff below operates on the stored set. Missing/unowned -> opaque (#7).
        var module = await _moduleRepository.GetByIdAsync(portalId, moduleId);
        if (module is null)
        {
            return Result<ModuleResponse>.Failure("The requested module was not found.");
        }

        // UpdateModuleRequest -> Module map applies the editable fields onto the tracked entity; identity/placement keys
        // (PortalId, ModuleDefId, DesktopModuleId), the primary/join keys (TabModuleId, ModuleId) and the
        // registration/permission members are Ignored by ModuleProfile. The transient AllModules/IsDefaultModule flags
        // and the Permissions list are source-only (no matching entity member) and are read explicitly below.
        _mapper.Map(request, module);

        // MIGRATION: route id is authoritative (PUT /api/modules/{id}); reassert it after mapping.
        module.ModuleId = moduleId;

        // MIGRATION: UpdateModule L1099-1118 — permission diff (delete-all-then-re-add) with the VIEW-inherit skip and
        // the AllowAccess filter. Applied onto the eager-loaded ModulePermissions navigation.
        ApplyModulePermissionDiff(module, request.Permissions);

        await _moduleRepository.UpdateAsync(module);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // MIGRATION: UpdateModule L1122-1124 -> UpdateTabModuleOrder (L1197-1209) re-sequenced the tab's placements per
        // pane (Counter*2-1) after the update. Only when the module is placed on a tab.
        if (module.TabId.HasValue)
        {
            await ResequenceTabAsync(portalId, module.TabId.Value, cancellationToken);
        }

        // MIGRATION: UpdateModule L1126-1130 — when IsDefaultModule, persist the portal default-module/default-tab site
        // settings (legacy UpdateSiteSetting "defaultmoduleid"/"defaulttabid") via the IPortalSettingsService port.
        if (request.IsDefaultModule)
        {
            await _portalSettingsService.SetSettingAsync(portalId, "defaultmoduleid", moduleId.ToString(), cancellationToken);
            if (module.TabId.HasValue)
            {
                await _portalSettingsService.SetSettingAsync(portalId, "defaulttabid", module.TabId.Value.ToString(), cancellationToken);
            }
        }

        // MIGRATION: UpdateModule L1132-1144 — when AllModules, propagate THIS module's display settings across the
        // portal's modules.
        if (request.AllModules)
        {
            await PropagateDisplaySettingsAsync(portalId, module, cancellationToken);
        }

        return Result<ModuleResponse>.Success(_mapper.Map<ModuleResponse>(module));
    }

    /// <inheritdoc />
    // MIGRATION: ModuleController.DeleteTabModule(TabId, ModuleId) L837 — the SOFT-DELETE behavior is PRESERVED EXACTLY
    // (AAP §0.7.1, the critical preserved rule). The legacy method first removed the tab-module placement
    // (DataProvider.DeleteTabModule, L843) and re-ordered the remaining placements (UpdateTabModuleOrder, L846); then,
    // IF no module instances remained — `GetModule(ModuleId, Null.NullInteger, True).TabID = Null.NullInteger` (L849) —
    // it set objModule.IsDeleted = True and cleared objModule.TabID (= Null.NullInteger, L851-852), persisted via
    // UpdateModule (L853), and finally called DataProvider.DeleteSearchItems (L856).
    // MIGRATION: In this simplified single-instance entity model a Module maps to a single placement, so deletion IS the
    // soft-delete: IsDeleted = true and TabId = null (Null.NullInteger -> null for the nullable key), persisted via the
    // repository + unit of work. The "remove a single placement but keep the module when other instances remain"
    // multi-instance nuance, and the DeleteSearchItems call (the search subsystem is OUT OF SCOPE, AAP §0.6.2), are
    // documented in MIGRATION_NOTES.md. A missing module is an expected business failure (Result.Failure).
    public async Task<Result> DeleteAsync(
        int portalId,
        int moduleId,
        CancellationToken cancellationToken = default)
    {
        // MIGRATION: CP1 review (IModuleService #1 / IModuleRepository #1) — PORTAL-SCOPED lookup so a module from another
        // portal can never be deleted through this tenant (multi-tenant isolation, AAP §0.7.1). Missing/unowned -> opaque (#7).
        var module = await _moduleRepository.GetByIdAsync(portalId, moduleId);
        if (module is null)
        {
            return Result.Failure("The requested module was not found.");
        }

        // MIGRATION: DeleteTabModule L849-853 — capture the tab the module is being removed from so the remaining
        // placements on that tab can be re-sequenced afterwards (UpdateTabModuleOrder L846).
        var formerTabId = module.TabId;

        // MIGRATION: DeleteTabModule L851-853 — soft-delete: set IsDeleted, clear the tab placement
        // (objModule.TabID = Null.NullInteger -> TabId = null), then persist via UpdateModule.
        // MIGRATION: the legacy method removed a single TAB-MODULE placement and only flipped IsDeleted when NO module
        // instances remained on any tab (`GetModule(ModuleId, Null.NullInteger).TabID = Null.NullInteger`, L849). In this
        // simplified single-instance entity model a Module maps to a single placement, so deletion IS the soft-delete.
        // The DeleteSearchItems call (L856) is OUT OF SCOPE (the search subsystem is excluded, AAP §0.6.2). Both the
        // multi-instance nuance and the search-item cleanup are documented in MIGRATION_NOTES.md.
        module.IsDeleted = true;
        module.TabId = null;

        await _moduleRepository.UpdateAsync(module);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // MIGRATION: DeleteTabModule L846 -> UpdateTabModuleOrder — re-sequence the placements that remain on the former
        // tab so their order stays normalized (Counter*2-1).
        if (formerTabId.HasValue)
        {
            await ResequenceTabAsync(portalId, formerTabId.Value, cancellationToken);
        }

        // MIGRATION: the controller maps Result.Success() -> HTTP 204 (DELETE contract, Gate 5).
        return Result.Success();
    }

    // =================================================================================================
    // Private helpers — exact ports of the legacy module-permission lifecycle, tab-module ordering, and the
    // AllModules display-settings propagation. Shared by CreateAsync/UpdateAsync/DeleteAsync.
    // =================================================================================================

    // MIGRATION: Builds a Domain ModulePermission from the inbound ModulePermissionDto. Mirrors the legacy
    // ModulePermissionInfo population in ModuleController.AddModule (L649-659) / UpdateModule (L1106-1118). The
    // permission is scoped to the owning module (ModuleId). Hand-mapped (not via AutoMapper) because the permission
    // collection is a child of the module-lifecycle rule, not a top-level entity<->DTO projection.
    private static ModulePermission MapModulePermission(ModulePermissionDto dto, int? moduleId) => new()
    {
        ModuleId = moduleId,
        RoleId = dto.RoleId,
        RoleName = dto.RoleName,
        AllowAccess = dto.AllowAccess,
        UserId = dto.UserId,
        DisplayName = dto.DisplayName,
        PermissionId = dto.PermissionId,
        PermissionKey = dto.PermissionKey,
    };

    // MIGRATION: ModuleController.UpdateModule L1099-1118 — module-permission diff. Legacy compared the supplied
    // ModulePermissionCollection against the stored set (CompareTo) and, when they differed, deleted all then re-added.
    // Clearing-and-re-adding always reaches the same persisted end state, so the CompareTo short-circuit (a pure
    // optimization) is collapsed to an unconditional rebuild. Two legacy filters are preserved EXACTLY:
    //   (1) InheritViewPermissions && PermissionKey = "VIEW" -> SKIP (the VIEW grant is inherited from the tab and is
    //       NOT stored on the module — legacy treated this as "skip = delete", L1106-1112);
    //   (2) only AllowAccess grants are persisted (L1106-1118).
    private static void ApplyModulePermissionDiff(Module module, List<ModulePermissionDto> requested)
    {
        module.ModulePermissions.Clear();

        foreach (var dto in requested)
        {
            // MIGRATION: VIEW-inherit skip — when the module inherits its view permission from the tab, a "VIEW" grant
            // is not stored on the module (legacy L1106-1112). Ordinal/case-insensitive match of the legacy key string.
            if (module.InheritViewPermissions
                && string.Equals(dto.PermissionKey, "VIEW", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // MIGRATION: only AllowAccess grants are persisted (legacy L1106-1118 added the permission only inside the
            // `If AllowAccess` branch).
            if (!dto.AllowAccess)
            {
                continue;
            }

            module.ModulePermissions.Add(MapModulePermission(dto, module.ModuleId));
        }
    }

    // MIGRATION: ModuleController.UpdateModuleOrder L1160-1173 (the -1 -> bottom-of-pane case) — returns the maximum
    // ModuleOrder currently used by non-deleted modules in the given (tab, pane) so a new module can be placed at
    // max + 2. Returns 0 when the pane is empty (so the first module lands at order 2, re-sequenced to 1 afterwards).
    private async Task<int> GetMaxPaneOrderAsync(int portalId, int tabId, string? paneName)
    {
        var paneModules = (await _moduleRepository.GetByTabIdAsync(portalId, tabId))
            .Where(m => !m.IsDeleted && string.Equals(m.PaneName, paneName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return paneModules.Count > 0 ? paneModules.Max(m => m.ModuleOrder) : 0;
    }

    // MIGRATION: ModuleController.UpdateTabModuleOrder L1197-1209 — re-sequences a tab's module placements PER PANE so
    // their orders become 1, 3, 5, ... (ModuleCounter * 2 - 1). The odd-only spacing leaves even-numbered gaps so a
    // later insert can be positioned between two modules. Only modules whose order actually changes are persisted.
    private async Task ResequenceTabAsync(int portalId, int tabId, CancellationToken cancellationToken)
    {
        var tabModules = (await _moduleRepository.GetByTabIdAsync(portalId, tabId))
            .Where(m => !m.IsDeleted)
            .ToList();

        var changed = false;

        // MIGRATION: legacy iterated the tab's modules grouped BY PANE; the counter restarts at 1 for each pane.
        foreach (var pane in tabModules.GroupBy(m => m.PaneName, StringComparer.OrdinalIgnoreCase))
        {
            var counter = 1;
            foreach (var m in pane.OrderBy(m => m.ModuleOrder))
            {
                var newOrder = (counter * 2) - 1;
                if (m.ModuleOrder != newOrder)
                {
                    m.ModuleOrder = newOrder;
                    await _moduleRepository.UpdateAsync(m);
                    changed = true;
                }

                counter++;
            }
        }

        if (changed)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    // MIGRATION: ModuleController.UpdateModule L1132-1144 — AllModules propagation. Legacy looped GetTabsByPortal,
    // SKIPPED admin tabs (IsAdminTab), and for each remaining tab looped GetTabModules applying THIS module's display
    // settings (Alignment/Color/Border/IconFile/Visibility/ContainerSrc/DisplayTitle/DisplayPrint/DisplaySyndicate).
    // In this simplified model the portal-scoped module list is the faithful target: portal scoping already excludes
    // other portals and host modules. The Tab entity carries NO admin/super-tab flag (dropped in migration), so the
    // legacy IsAdminTab exclusion is documented (MIGRATION_NOTES.md) rather than expressible here.
    private async Task PropagateDisplaySettingsAsync(int portalId, Module source, CancellationToken cancellationToken)
    {
        var portalModules = (await _moduleRepository.GetByPortalIdAsync(portalId))
            .Where(m => !m.IsDeleted && m.ModuleId != source.ModuleId)
            .ToList();

        var changed = false;
        foreach (var m in portalModules)
        {
            m.Alignment = source.Alignment;
            m.Color = source.Color;
            m.Border = source.Border;
            m.IconFile = source.IconFile;
            m.Visibility = source.Visibility;
            m.ContainerSrc = source.ContainerSrc;
            m.DisplayTitle = source.DisplayTitle;
            m.DisplayPrint = source.DisplayPrint;
            m.DisplaySyndicate = source.DisplaySyndicate;
            await _moduleRepository.UpdateAsync(m);
            changed = true;
        }

        if (changed)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
