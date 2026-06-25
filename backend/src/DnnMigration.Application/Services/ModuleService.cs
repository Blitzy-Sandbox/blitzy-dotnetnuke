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
// Result/Result<T> (AAP §0.7.7). All entity<->DTO conversion goes through the injected IMapper
// (Mapping/ModuleProfile); there is no hand-mapping here. No DbContext/EF Core/System.Data reference exists
// in this layer (AAP §0.7.3). Modules remain scoped by PortalId (tenant) and TabId (page placement),
// preserving DNN multi-tenant isolation (AAP §0.7.1).
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

    /// <summary>
    /// Initializes a new instance of the <see cref="ModuleService"/> class.
    /// </summary>
    /// <param name="moduleRepository">Module data-access abstraction (replaces the legacy DataProvider module operations).</param>
    /// <param name="unitOfWork">Transactional persistence boundary (replaces the legacy DataProvider transaction surface).</param>
    /// <param name="mapper">AutoMapper instance configured with the Module mapping profile.</param>
    // MIGRATION: Constructor injection ONLY (AAP §0.7.3) — replaces the legacy DataProvider.Instance()
    // reflection singleton lookup and the `New ModulePermissionController`/`New TabController` direct
    // instantiations scattered throughout ModuleController.vb.
    public ModuleService(
        IModuleRepository moduleRepository,
        IUnitOfWork unitOfWork,
        IMapper mapper)
    {
        ArgumentNullException.ThrowIfNull(moduleRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(mapper);

        _moduleRepository = moduleRepository;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    /// <inheritdoc />
    // MIGRATION: ModuleController.GetModules(PortalID) L915 -> FillModuleInfoCollection(DataProvider.GetModules(PortalID)).
    // Portal-scoped for multi-tenant isolation (AAP §0.7.1). The repository returns the portal's modules and paging is
    // applied in-memory here (the contract is paged because a portal can host many modules).
    public async Task<Result<PagedResult<ModuleResponse>>> GetByPortalAsync(
        int portalId,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        // MIGRATION: legacy GetPortalModules SP returns only non-deleted modules (IsDeleted = 0); soft-deleted
        // modules are excluded from active listings.
        var modules = (await _moduleRepository.GetByPortalIdAsync(portalId))
            .Where(m => !m.IsDeleted)
            .ToList();

        var total = modules.Count;

        // MIGRATION: PageIndex is ZERO-BASED (see PagedResult). Skip whole pages, take the page window.
        var pageItems = modules
            .Skip(pageIndex * pageSize)
            .Take(pageSize)
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
        int tabId,
        CancellationToken cancellationToken = default)
    {
        var modules = (await _moduleRepository.GetByTabIdAsync(tabId))
            .Where(m => !m.IsDeleted);

        return Result<IEnumerable<ModuleResponse>>.Success(
            modules.Select(m => _mapper.Map<ModuleResponse>(m)).ToList());
    }

    /// <inheritdoc />
    // MIGRATION: ModuleController.GetModule(ModuleId, TabId, ignoreCache) L885. The legacy member used cache-aside and
    // also took TabId (a module can appear on multiple tabs via TabModule rows); collapsed to a ModuleId-only lookup of
    // the module aggregate. A missing module becomes an expected business failure (Result.Failure), not an exception —
    // the Api maps it to a 404 ProblemDetails. Caching is an Infrastructure concern, omitted here.
    public async Task<Result<ModuleResponse>> GetByIdAsync(
        int moduleId,
        CancellationToken cancellationToken = default)
    {
        var module = await _moduleRepository.GetByIdAsync(moduleId);
        if (module is null)
        {
            return Result<ModuleResponse>.Failure($"Module {moduleId} was not found.");
        }

        return Result<ModuleResponse>.Success(_mapper.Map<ModuleResponse>(module));
    }

    /// <inheritdoc />
    // MIGRATION: ModuleController.AddModule(objModule) L645. The in-scope portion — persist the module row with its
    // editable settings — is transcribed here; ModuleProfile maps the creation fields and Ignores the
    // identity/registration/definition metadata, permissions, and ControlType.
    public async Task<Result<ModuleResponse>> CreateAsync(
        CreateModuleRequest request,
        CancellationToken cancellationToken = default)
    {
        var module = _mapper.Map<Module>(request);

        // MIGRATION: AddModule L648 inserted with the IsDeleted flag explicitly passed through; a newly created module
        // is never soft-deleted, so IsDeleted is defaulted false.
        module.IsDeleted = false;

        // MIGRATION: AddModule L662-676 placed the module on the tab (DataProvider.AddTabModule) inside a try/catch that
        // *ignored* the "module already in the page" error, so re-adding an existing module to a page was tolerated and
        // never hard-failed. Accordingly no duplicate-definition / duplicate-placement rejection is performed here; a
        // GetByDefinitionAsync guard would be unsafe because the definition key is not unambiguous in the create
        // contract, so duplicate-definition rejection is deferred. Recorded in MIGRATION_NOTES.md.

        await _moduleRepository.AddAsync(module);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // MIGRATION (DEFERRED): AddModule also (a) added each ModulePermission via
        // ModulePermissionController.AddModulePermission (L649-659), (b) called DataProvider.AddTabModule to place the
        // module on the tab with order/pane/display settings (L662-665), and (c) computed module order via
        // UpdateModuleOrder/UpdateTabModuleOrder (L667-673). In this simplified entity model, tab placement is persisted
        // through the Module entity (TabId), while module-permission rows and tab-module ordering are NOT part of
        // ModuleResponse/CreateModuleRequest, so granular permission and tab-module-order management is deferred.
        // Recorded in MIGRATION_NOTES.md.
        return Result<ModuleResponse>.Success(_mapper.Map<ModuleResponse>(module));
    }

    /// <inheritdoc />
    // MIGRATION: ModuleController.UpdateModule(objModule) L1095. The in-scope portion — apply the editable module
    // settings onto the tracked entity and persist — is transcribed here; ModuleProfile applies the editable fields and
    // Ignores the immutable identity/placement keys and the registration/permission members.
    public async Task<Result<ModuleResponse>> UpdateAsync(
        int moduleId,
        UpdateModuleRequest request,
        CancellationToken cancellationToken = default)
    {
        var module = await _moduleRepository.GetByIdAsync(moduleId);
        if (module is null)
        {
            return Result<ModuleResponse>.Failure($"Module {moduleId} was not found.");
        }

        // UpdateModuleRequest -> Module map applies the editable fields onto the tracked entity; identity/placement keys
        // (PortalId, ModuleDefId, DesktopModuleId), the primary/join keys (TabModuleId, ModuleId) and the
        // registration/permission members are Ignored by ModuleProfile.
        _mapper.Map(request, module);

        await _moduleRepository.UpdateAsync(module);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // MIGRATION (DEFERRED): UpdateModule L1099-1116 diffed module permissions
        // (ModulePermissionCollection.CompareTo -> delete-all-then-re-add, with the
        // `InheritViewPermissions && PermissionKey = "VIEW"` special case); L1126-1130 applied the IsDefaultModule
        // site-setting; and L1132-1144 propagated settings across all (non-admin) tabs when AllModules was set
        // (UpdateTabModule/UpdateModuleOrder). These permission-diff and cross-tab-propagation rules are deferred
        // because permissions and tab-module ordering are not part of the DTO. Recorded in MIGRATION_NOTES.md.
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
        int moduleId,
        CancellationToken cancellationToken = default)
    {
        var module = await _moduleRepository.GetByIdAsync(moduleId);
        if (module is null)
        {
            return Result.Failure($"Module {moduleId} was not found.");
        }

        // MIGRATION: DeleteTabModule L851-853 — soft-delete: set IsDeleted, clear the tab placement
        // (objModule.TabID = Null.NullInteger -> TabId = null), then persist via UpdateModule.
        module.IsDeleted = true;
        module.TabId = null;

        await _moduleRepository.UpdateAsync(module);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // MIGRATION: the controller maps Result.Success() -> HTTP 204 (DELETE contract, Gate 5).
        return Result.Success();
    }
}
