using AutoMapper;
using FluentValidation;
using DnnMigration.Application.DTOs.Module;
using DnnMigration.Application.Interfaces;
using DnnMigration.Domain.Entities;
using DnnMigration.Domain.Interfaces;

namespace DnnMigration.Application.Services;

/// <summary>
/// Application service for the Module aggregate.
/// MIGRATION: ports the business surface of <c>Library/Components/Modules/ModuleController.vb</c> (1456 lines)
/// into async, DTO-based operations. Data access is delegated to <see cref="IModuleRepository"/> (an EF Core
/// implementation in the Infrastructure layer), replacing the legacy controller's co-mingled ADO.NET /
/// <c>SqlDataProvider</c> calls and the <c>CBO</c> / <c>FillModuleInfo</c> reflection hydration. Entity
/// &lt;-&gt; DTO transformation is delegated to AutoMapper, and create/update payloads are validated with
/// FluentValidation before any persistence work occurs.
/// MIGRATION: Module uses SOFT-delete via the <c>IsDeleted</c> flag (AAP 0.3.3). All list reads return only
/// non-deleted modules; that <c>IsDeleted == false</c> filtering is performed by the repository
/// (<see cref="IModuleRepository.GetByTabAsync"/> / <see cref="IModuleRepository.GetByPortalAsync"/>), so this
/// service never re-adds deleted rows.
/// </summary>
public class ModuleService : IModuleService
{
    private readonly IModuleRepository _moduleRepository;
    private readonly IMapper _mapper;
    private readonly IValidator<CreateModuleDto> _createValidator;
    private readonly IValidator<UpdateModuleDto> _updateValidator;

    /// <summary>
    /// Initializes a new <see cref="ModuleService"/> with its injected collaborators.
    /// </summary>
    /// <param name="moduleRepository">Repository providing data access for the Module aggregate.</param>
    /// <param name="mapper">AutoMapper instance used for entity &lt;-&gt; DTO projection.</param>
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
        // MIGRATION: legacy GetModule [ModuleController.vb:L885-905] first consulted the DataCache
        // (the GetTabModules dictionary) and otherwise hydrated the row with FillModuleInfo reflection over an
        // IDataReader. Both are replaced here by EF Core entity materialization behind IModuleRepository.
        var module = await _moduleRepository.GetByIdAsync(moduleId, cancellationToken);
        return module is null ? null : _mapper.Map<ModuleDto>(module);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<ModuleDto>> GetByTabAsync(int tabId, CancellationToken cancellationToken = default)
    {
        // MIGRATION: the repository excludes soft-deleted modules (IsDeleted == false). In legacy DNN this
        // filtering happened at the stored-procedure level (GetTabModules); here IModuleRepository.GetByTabAsync
        // owns it, so this service must not re-add deleted rows.
        var modules = await _moduleRepository.GetByTabAsync(tabId, cancellationToken);
        return _mapper.Map<IEnumerable<ModuleDto>>(modules);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<ModuleDto>> GetByPortalAsync(int portalId, CancellationToken cancellationToken = default)
    {
        // MIGRATION: legacy GetModules(PortalID) [ModuleController.vb:L915-930] called FillModuleInfoCollection
        // over DataProvider.GetModules. The repository excludes soft-deleted modules (IsDeleted == false) -
        // legacy filtered at the stored-procedure level - so this service must not re-add deleted rows.
        var modules = await _moduleRepository.GetByPortalAsync(portalId, cancellationToken);
        return _mapper.Map<IEnumerable<ModuleDto>>(modules);
    }

    /// <inheritdoc />
    public async Task<ModuleDto> CreateAsync(CreateModuleDto request, CancellationToken cancellationToken = default)
    {
        await _createValidator.ValidateAndThrowAsync(request, cancellationToken);

        // The CreateModuleDto -> Module map ignores ModuleID, TabModuleID, IsDeleted, the DesktopModule /
        // ModuleControl descriptor fields, and the ModulePermissions navigation (see ModuleProfile).
        var module = _mapper.Map<Module>(request);

        var created = await _moduleRepository.AddAsync(module, cancellationToken);

        // MIGRATION: legacy AddModule [ModuleController.vb:L645-682] inserted the physical Modules row, the
        // denormalized per-tab placement row (AddTabModule), looped objModule.ModulePermissions to seed
        // ModulePermission rows, positioned the module at the bottom of its pane when ModuleOrder = -1
        // (UpdateModuleOrder), and cleared the cache (ClearCache). IModuleRepository.AddAsync DOES persist the
        // TabModules placement (pane/order/cache/visibility/container/display flags) for a placed module
        // (TabID > 0), reproducing AddTabModule against the schema-faithful TabModule entity. The remaining
        // legacy steps are OMITTED here: the ModuleOrder bottom-of-pane auto-positioning (UpdateModuleOrder)
        // and - OUT OF SCOPE per AAP 0.2.2 - ModulePermission seeding and the Cache Provider. Recorded in
        // MIGRATION_NOTES.md §4.2 / D-030.
        return _mapper.Map<ModuleDto>(created);
    }

    /// <inheritdoc />
    public async Task<ModuleDto> UpdateAsync(UpdateModuleDto request, CancellationToken cancellationToken = default)
    {
        await _updateValidator.ValidateAndThrowAsync(request, cancellationToken);

        // MIGRATION: legacy UpdateModule [ModuleController.vb:L1095-1148] issued a direct
        // DataProvider.UpdateModule call without first loading the row. The EF Core repository pattern requires
        // a tracked entity, so the existing module is loaded first and the request is then mapped onto it.
        var existing = await _moduleRepository.GetByIdAsync(request.ModuleID, cancellationToken);
        if (existing is null)
        {
            throw new KeyNotFoundException($"Module {request.ModuleID} not found.");
        }

        // The UpdateModuleDto -> Module map ignores the immutable relationship keys (PortalID, TabID,
        // ModuleDefID, DesktopModuleID, TabModuleID), IsDeleted, and the descriptor / control / permission
        // members (see ModuleProfile), so only the editable module-instance settings are applied.
        _mapper.Map(request, existing);

        await _moduleRepository.UpdateAsync(existing, cancellationToken);

        // MIGRATION: legacy UpdateModule synced the per-tab placement (UpdateTabModule + UpdateModuleOrder),
        // performed a ModulePermission diff/replace, persisted IsDefaultModule into the portal site-settings,
        // propagated settings to every tab when AllModules was set, then cleared the cache.
        // IModuleRepository.UpdateAsync DOES sync the TabModules placement-settings columns
        // (pane/order/cache/visibility/container/display flags) onto this module's placement row(s),
        // reproducing UpdateTabModule. The remaining legacy steps are OMITTED here: the ModuleOrder
        // bottom-of-pane auto-positioning (UpdateModuleOrder) and - OUT OF SCOPE per AAP 0.2.2 -
        // ModulePermission management, site-settings, AllModules cross-tab propagation, and the Cache Provider.
        // Recorded in MIGRATION_NOTES.md §4.2 / D-031.
        return _mapper.Map<ModuleDto>(existing);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(int moduleId, CancellationToken cancellationToken = default)
    {
        // MIGRATION: legacy DeleteModule [ModuleController.vb:L819-826] called DataProvider.DeleteModule (which
        // performed the SOFT-delete at the stored-procedure level) followed by DeleteSearchItems. Here
        // IModuleRepository.DeleteAsync performs the soft-delete (sets IsDeleted = true); the Search Provider
        // (DeleteSearchItems), tab-module reordering, and the Cache Provider (ClearCache) are OMITTED (OUT OF
        // SCOPE per AAP 0.2.2). Legacy tolerated a non-existent module (the stored proc was a no-op); calling the
        // repository directly preserves that tolerance.
        await _moduleRepository.DeleteAsync(moduleId, cancellationToken);
    }
}
