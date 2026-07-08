using AutoMapper;
using DnnMigration.Application.DTOs;
using DnnMigration.Application.Interfaces;
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
/// <c>SqlDataProvider</c> calls, static caching, permission synchronization, tab-module
/// placement and cache invalidation; per the Minimal Change Clause only the persisted
/// module record semantics are preserved here. Removed side-effects are documented inline
/// with <c>// MIGRATION:</c> comments and never silently optimized away.
/// </remarks>
public sealed class ModuleService : IModuleService
{
    private readonly IModuleRepository _moduleRepository;
    private readonly IMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="ModuleService"/> class.
    /// </summary>
    /// <param name="moduleRepository">The module repository used for all data access.</param>
    /// <param name="mapper">The AutoMapper instance used to project entities to DTOs.</param>
    public ModuleService(IModuleRepository moduleRepository, IMapper mapper)
    {
        _moduleRepository = moduleRepository;
        _mapper = mapper;
    }

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
    // MIGRATION: ModuleController.AddModule(objModule) [ModuleController.vb L645] also synced
    // ModulePermissions (ModulePermissionController.AddModulePermission), placed a TabModule
    // (DataProvider.AddTabModule), updated the module order in the pane (UpdateModuleOrder /
    // UpdateTabModuleOrder) and cleared the tab cache (ClearCache). Those side-effects are DROPPED
    // here because this service is injected only with IModuleRepository + IMapper; only the module
    // record is persisted. The persisted-id assignment the legacy did via DataProvider.AddModule is
    // handled by the repository's insert, which returns the stored entity.
    public async Task<ModuleDto> CreateAsync(CreateModuleDto dto, CancellationToken cancellationToken = default)
    {
        var module = _mapper.Map<Module>(dto);
        var created = await _moduleRepository.AddAsync(module, cancellationToken);
        return _mapper.Map<ModuleDto>(created);
    }

    /// <inheritdoc />
    // MIGRATION: ModuleController.UpdateModule(objModule) [ModuleController.vb L1095] performed a field
    // copy (DataProvider.UpdateModule) PLUS permission synchronization (DeleteModulePermissionsByModuleID
    // / AddModulePermission), a tab-module update (DataProvider.UpdateTabModule), module-order
    // repositioning (UpdateModuleOrder), default-module registration (IsDefaultModule ->
    // PortalSettings.UpdateSiteSetting) and all-modules propagation (AllModules loop over portal tabs),
    // followed by a cache clear (ClearCache). Only the field copy is preserved here (in-place
    // AutoMapper Map(dto, module)); every propagation/permission/cache side-effect is DROPPED. A
    // missing module maps to null (legacy update targeted an existing record).
    public async Task<ModuleDto?> UpdateAsync(int id, UpdateModuleDto dto, CancellationToken cancellationToken = default)
    {
        var module = await _moduleRepository.GetByIdAsync(id, cancellationToken);
        if (module is null)
        {
            return null;
        }

        _mapper.Map(dto, module);
        await _moduleRepository.UpdateAsync(module, cancellationToken);
        return _mapper.Map<ModuleDto>(module);
    }

    /// <inheritdoc />
    // MIGRATION: ModuleController.DeleteModule(ModuleId) [ModuleController.vb L819] also removed the
    // module's search-index entries (DataProvider.DeleteSearchItems). That side-effect is DROPPED here;
    // only the module record is deleted. Returns false when the module does not exist so the API layer
    // can surface a 404 (legacy performed an unconditional delete).
    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var module = await _moduleRepository.GetByIdAsync(id, cancellationToken);
        if (module is null)
        {
            return false;
        }

        await _moduleRepository.DeleteAsync(id, cancellationToken);
        return true;
    }
}
