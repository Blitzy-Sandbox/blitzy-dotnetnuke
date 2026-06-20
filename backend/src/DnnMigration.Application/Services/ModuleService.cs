using AutoMapper;
using FluentValidation;
using DnnMigration.Application.DTOs.Module;
using DnnMigration.Application.Interfaces;
using DnnMigration.Domain.Entities;
using DnnMigration.Domain.Interfaces;

namespace DnnMigration.Application.Services;

/// <summary>
/// Application service for the Module aggregate.
/// MIGRATION: ports the business logic of the legacy <c>Library/Components/Modules/ModuleController.vb</c>
/// (DotNetNuke 4.9.0.85, 1456 lines), decoupled from data access. The legacy controller co-mingled
/// ADO.NET / <c>SqlDataProvider</c> calls, <c>CBO</c>/<c>FillModuleInfo</c> reflection hydration, the
/// <c>DataCache</c> tab-module dictionary, and cache/search side effects directly inside its methods.
/// Here data access is delegated to <see cref="IModuleRepository"/> (EF Core in the Infrastructure
/// layer), entity&#8596;DTO translation to AutoMapper, and inbound input validation to FluentValidation.
/// </summary>
/// <remarks>
/// <para>
/// Module is a <b>soft-delete</b> aggregate (AAP §0.3.3): <see cref="DeleteAsync"/> flips the
/// <c>IsDeleted</c> flag through the repository rather than physically removing the row, and every list
/// read (<see cref="GetByTabAsync"/>, <see cref="GetByPortalAsync"/>) returns only non-deleted modules.
/// In the legacy stack this filtering happened inside the stored procedures; here the repository owns
/// the <c>IsDeleted == false</c> predicate, so this service never re-introduces deleted rows.
/// </para>
/// <para>
/// The service is async-first and stateless; each deviation from the legacy behavior is annotated with a
/// <c>// MIGRATION:</c> comment and recorded in the root <c>MIGRATION_NOTES.md</c>. Per the Minimal
/// Change Clause the ported domain logic is preserved as-is and is not "improved".
/// </para>
/// </remarks>
public class ModuleService : IModuleService
{
    private readonly IModuleRepository _moduleRepository;
    private readonly IMapper _mapper;
    private readonly IValidator<CreateModuleDto> _createValidator;
    private readonly IValidator<UpdateModuleDto> _updateValidator;

    /// <summary>
    /// Initializes a new instance of the <see cref="ModuleService"/> class.
    /// </summary>
    /// <param name="moduleRepository">Repository providing data access for the Module aggregate.</param>
    /// <param name="mapper">AutoMapper instance used for entity&#8596;DTO projection.</param>
    /// <param name="createValidator">FluentValidation validator for <see cref="CreateModuleDto"/>.</param>
    /// <param name="updateValidator">FluentValidation validator for <see cref="UpdateModuleDto"/>.</param>
    public ModuleService(
        IModuleRepository moduleRepository,
        IMapper mapper,
        IValidator<CreateModuleDto> createValidator,
        IValidator<UpdateModuleDto> updateValidator)
    {
        _moduleRepository = moduleRepository;
        _mapper = mapper;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    /// <inheritdoc />
    public async Task<ModuleDto?> GetByIdAsync(int moduleId, CancellationToken cancellationToken = default)
    {
        // MIGRATION: legacy ModuleController.GetModule(ModuleId, TabId, ignoreCache) [ModuleController.vb:L885-905]
        // first probed the in-memory GetTabModules dictionary (DataCache) and, on a miss, hydrated an
        // IDataReader through CBO/FillModuleInfo reflection. Both the cache lookup and the reflection
        // mapping are replaced by EF Core entity materialization behind the repository; the legacy
        // TabId/ignoreCache parameters collapse into a single id-based lookup.
        var module = await _moduleRepository.GetByIdAsync(moduleId, cancellationToken);
        return module is null ? null : _mapper.Map<ModuleDto>(module);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<ModuleDto>> GetByTabAsync(int tabId, CancellationToken cancellationToken = default)
    {
        // MIGRATION: legacy ModuleController.GetTabModules hydrated a Dictionary/ArrayList of ModuleInfo via
        // FillModuleInfoCollection reflection; replaced by EF Core materialization + AutoMapper projection.
        // The repository excludes soft-deleted modules (IsDeleted == false) — in legacy this filtering lived
        // in the stored procedure, so this service does NOT re-add deleted rows.
        var modules = await _moduleRepository.GetByTabAsync(tabId, cancellationToken);
        return _mapper.Map<IEnumerable<ModuleDto>>(modules);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<ModuleDto>> GetByPortalAsync(int portalId, CancellationToken cancellationToken = default)
    {
        // MIGRATION: legacy ModuleController.GetModules(PortalID) [ModuleController.vb:L915-917] returned an
        // ArrayList of ModuleInfo hydrated via FillModuleInfoCollection reflection; replaced by EF Core
        // materialization + AutoMapper projection. The repository excludes soft-deleted modules
        // (IsDeleted == false) — legacy filtering lived in the stored procedure, so this service does NOT
        // re-add deleted rows.
        var modules = await _moduleRepository.GetByPortalAsync(portalId, cancellationToken);
        return _mapper.Map<IEnumerable<ModuleDto>>(modules);
    }

    /// <inheritdoc />
    public async Task<ModuleDto> CreateAsync(CreateModuleDto request, CancellationToken cancellationToken = default)
    {
        await _createValidator.ValidateAndThrowAsync(request, cancellationToken);

        // The CreateModuleDto->Module map (ModuleProfile) ignores the database-generated ModuleID and
        // TabModuleID, the service-managed IsDeleted flag, the denormalized desktop-module / module-control
        // descriptor fields, and the ModulePermissions navigation collection.
        var module = _mapper.Map<Module>(request);

        // MIGRATION: legacy ModuleController.AddModule [ModuleController.vb:L645-682] did far more than the
        // single module insert: it looped objModule.ModulePermissions to seed ModulePermission rows, inserted
        // the denormalized TabModule link (AddTabModule), positioned the module within its pane
        // (ModuleOrder = -1 => bottom of pane via UpdateModuleOrder / UpdateTabModuleOrder), and finally
        // called ClearCache(TabID). All of those side-effects are OMITTED here: ModulePermission seeding,
        // tab-module ordering, and the Cache Provider are OUT OF SCOPE (AAP §0.2.2); the bare persistence is
        // delegated to the repository.
        var created = await _moduleRepository.AddAsync(module, cancellationToken);
        return _mapper.Map<ModuleDto>(created);
    }

    /// <inheritdoc />
    public async Task<ModuleDto> UpdateAsync(UpdateModuleDto request, CancellationToken cancellationToken = default)
    {
        await _updateValidator.ValidateAndThrowAsync(request, cancellationToken);

        // MIGRATION: legacy ModuleController.UpdateModule [ModuleController.vb:L1095-1148] passed the supplied
        // values straight to the data provider (a silent no-op when the row was absent). The DTO+repository
        // pattern requires loading the tracked entity first; a missing module is surfaced as
        // KeyNotFoundException (mapped to a 404 RFC 7807 response by the API middleware) instead of silently
        // no-opping.
        var existing = await _moduleRepository.GetByIdAsync(request.ModuleID, cancellationToken);
        if (existing is null)
        {
            throw new KeyNotFoundException($"Module {request.ModuleID} not found.");
        }

        // The UpdateModuleDto->Module map (ModuleProfile) overlays only the mutable module-instance settings,
        // ignoring the immutable relationship keys (PortalID, TabID, ModuleDefID, DesktopModuleID,
        // TabModuleID) and the service-managed IsDeleted flag.
        _mapper.Map(request, existing);
        await _moduleRepository.UpdateAsync(existing, cancellationToken);

        // MIGRATION: the legacy UpdateModule additionally diffed and replaced the ModulePermission collection
        // (ModulePermissionController.CompareTo / Delete / Add), synchronized the denormalized TabModule row
        // (UpdateTabModule + UpdateModuleOrder), persisted the IsDefaultModule site settings
        // (PortalSettings.UpdateSiteSetting "defaultmoduleid" / "defaulttabid"), propagated the
        // container/visibility settings to every non-admin tab when AllModules was set, and called
        // ClearCache(TabID). All of those are OMITTED here: ModulePermission management, tab-module
        // sync/ordering, site-settings, AllModules cross-tab propagation, and the Cache Provider are OUT OF
        // SCOPE (AAP §0.2.2).
        return _mapper.Map<ModuleDto>(existing);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(int moduleId, CancellationToken cancellationToken = default)
    {
        // MIGRATION: legacy ModuleController.DeleteModule [ModuleController.vb:L819-826] called
        // DataProvider.DeleteModule(ModuleId) followed by DataProvider.DeleteSearchItems(ModuleId). Module is
        // a SOFT-delete aggregate (AAP §0.3.3): IModuleRepository.DeleteAsync flips IsDeleted = true rather
        // than physically removing the row. The legacy Search Provider cleanup (DeleteSearchItems), tab-module
        // reordering, and ClearCache are OMITTED (OUT OF SCOPE per AAP §0.2.2). Legacy DeleteModule tolerated a
        // non-existent module id (the stored procedure simply affected no rows); calling the repository
        // directly preserves that no-op tolerance without a pre-load existence check.
        await _moduleRepository.DeleteAsync(moduleId, cancellationToken);
    }
}
