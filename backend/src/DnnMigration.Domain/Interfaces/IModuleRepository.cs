using DnnMigration.Domain.Entities;

namespace DnnMigration.Domain.Interfaces;

/// <summary>
/// Repository contract for the <see cref="Module"/> aggregate. Extends the generic
/// <see cref="IRepository{T}"/> CRUD surface with module-specific lookups.
/// </summary>
public interface IModuleRepository : IRepository<Module>
{
    /// <summary>Retrieves all modules belonging to the specified portal.</summary>
    // MIGRATION: legacy DataProvider.GetModules(PortalId) [DataProvider.vb L127] /
    // ModuleController.GetModules(PortalID) [ModuleController.vb L915] returned an ArrayList of
    // ModuleInfo; converted to an async materialized collection (LINQ over DnnDbContext downstream).
    Task<IEnumerable<Module>> GetByPortalAsync(int portalId, CancellationToken cancellationToken = default);

    /// <summary>Retrieves a single module in a portal by its definition friendly name, or <c>null</c>.</summary>
    // MIGRATION: legacy DataProvider.GetModuleByDefinition(PortalId, FriendlyName) [DataProvider.vb L130]
    // / ModuleController.GetModuleByDefinition [ModuleController.vb L955] returned a single ModuleInfo;
    // converted to an async nullable single-entity lookup.
    Task<Module?> GetByDefinitionAsync(int portalId, string friendlyName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the modules whose title, friendly name, or module name contain the supplied free-text
    /// <paramref name="query"/> (case-insensitive substring match), optionally restricted to a single
    /// portal when <paramref name="portalId"/> is supplied.
    /// </summary>
    // MIGRATION: the legacy Website/admin/Modules/** inventory grid filtered the module list in-page.
    // Re-expressed here as an async, materialized substring filter (LINQ .ToLower().Contains downstream)
    // so the AAP §0.7.2 "Search/Filter -> GET /api/modules?query=..." contract is served server-side
    // rather than ignored. The optional portalId preserves the existing per-portal authorization scoping
    // applied by ModulesController. An empty/whitespace query is filtered out by the caller (the service).
    Task<IEnumerable<Module>> SearchAsync(int? portalId, string query, CancellationToken cancellationToken = default);

    // -------------------------------------------------------------------------
    //  TabModule placement surface
    //
    //  MIGRATION: a DNN module is a two-part aggregate — the portal-scoped [Modules] record (content
    //  container identity) PLUS one or more [TabModules] placement rows that position the module on a
    //  tab's pane and carry its presentation settings (PaneName, ModuleOrder, container, visibility,
    //  alignment, ...). The legacy ModuleController.AddModule/UpdateModule/DeleteModule co-ordinated
    //  BOTH parts via DataProvider.AddTabModule / UpdateTabModule and the ON DELETE CASCADE FK. The
    //  Phase-2 schema split moved every placement column onto the TabModule entity (Ignore()d as
    //  transient carriers on Module), so the placement side-effects the reviewer flagged are restored
    //  here as explicit repository writes rather than being dropped.
    // -------------------------------------------------------------------------

    /// <summary>Persists a new <see cref="TabModule"/> placement row (a module instance on a tab pane).</summary>
    // MIGRATION: legacy DataProvider.AddTabModule(objTabModule) invoked by ModuleController.AddModule
    // [ModuleController.vb L645] to place the module on a tab. TabModules.TabModuleID is a surrogate
    // IDENTITY key so the FK columns are NON-identifying and the row is safely written by value.
    Task<TabModule> AddTabModuleAsync(TabModule tabModule, CancellationToken cancellationToken = default);

    /// <summary>Retrieves every <see cref="TabModule"/> placement row owned by the specified module.</summary>
    // MIGRATION: the [TabModules] rows a module owns (WHERE ModuleID = @ModuleID). Used to apply
    // placement-field edits on update and to cascade placement deletes on delete.
    Task<IEnumerable<TabModule>> GetTabModulesByModuleAsync(int moduleId, CancellationToken cancellationToken = default);

    /// <summary>Applies placement-field changes to an existing <see cref="TabModule"/> row.</summary>
    // MIGRATION: legacy DataProvider.UpdateTabModule(objTabModule) invoked by ModuleController.UpdateModule
    // [ModuleController.vb L1095] to persist edits to a module's pane placement / presentation settings.
    Task UpdateTabModuleAsync(TabModule tabModule, CancellationToken cancellationToken = default);

    /// <summary>Removes every <see cref="TabModule"/> placement row owned by a module (placement cascade).</summary>
    // MIGRATION: the legacy FK_{objectQualifier}TabModules_{objectQualifier}Modules was ON DELETE CASCADE,
    // so deleting a [Modules] row removed its [TabModules] rows automatically. The EF Core InMemory
    // provider used by the integration tests does NOT enforce cascade, so the cascade is performed
    // explicitly (provider-agnostic parity) before the Modules row is removed.
    Task DeleteTabModulesByModuleAsync(int moduleId, CancellationToken cancellationToken = default);
}
