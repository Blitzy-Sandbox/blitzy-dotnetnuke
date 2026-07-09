using DnnMigration.Domain.Common;
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

    /// <summary>
    /// Retrieves a single bounded page of modules (ordered by <c>ModuleID</c>), fully hydrated with their
    /// placement + definition lookup carriers, together with the total module count, for server-side
    /// pagination of <c>GET /api/modules</c>.
    /// </summary>
    // MIGRATION (QA finding — R6 Issue 1): the BOUNDED counterpart of <see cref="IRepository{T}.GetAllAsync"/>.
    // GetAllAsync (left untouched for internal callers/tests) materialized the whole [Modules] table; this
    // fetches only the Skip/Take window (ordered by the ModuleID primary key) plus a COUNT and runs the same
    // batched HydrateManyAsync so a paged row round-trips the full denormalized field set (QA finding I).
    Task<PagedResult<Module>> GetPagedAsync(int skip, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a single bounded page of modules belonging to the specified portal (ordered by
    /// <c>ModuleID</c>), fully hydrated, together with the total count for that portal.
    /// </summary>
    // MIGRATION (QA finding — R6 Issue 1): the BOUNDED counterpart of <see cref="GetByPortalAsync"/>.
    Task<PagedResult<Module>> GetByPortalPagedAsync(int portalId, int skip, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a single bounded page of modules whose title matches the free-text
    /// <paramref name="query"/> (case-insensitive substring), optionally scoped to a portal, ordered by
    /// <c>ModuleID</c> and fully hydrated, together with the total match count.
    /// </summary>
    // MIGRATION (QA finding — R6 Issue 1): the BOUNDED counterpart of <see cref="SearchAsync"/>.
    Task<PagedResult<Module>> SearchPagedAsync(int? portalId, string query, int skip, int take, CancellationToken cancellationToken = default);

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

    /// <summary>
    /// Atomically persists a new module and its placement rows: inserts the <see cref="Module"/> record
    /// (assigning its store-generated id), stamps that id onto each supplied <see cref="TabModule"/>
    /// placement, and inserts the placements - all as a single transactional unit on relational providers.
    /// Returns the persisted module.
    /// </summary>
    // MIGRATION (QA finding C): the legacy ModuleController.AddModule [ModuleController.vb L645] wrote the
    // [Modules] row and its [TabModules] placement row(s) together. Splitting that into a module insert
    // followed by independent placement inserts (each in its own SaveChanges) meant a placement failure -
    // e.g. a bad TabID violating FK_TabModules_Tabs - left an ORPHANED [Modules] row behind and surfaced a
    // raw HTTP 500. This method restores the all-or-nothing semantics: on a relational provider the module
    // insert and the placement inserts run inside one execution-strategy-wrapped transaction, so any
    // failure rolls the whole unit back. (The service also pre-validates the TabID, so the happy path never
    // relies on the FK to reject a bad tab.) The EF Core InMemory provider has no transaction support, so
    // the saves run without an explicit transaction there.
    Task<Module> AddWithPlacementsAsync(
        Module module,
        IReadOnlyList<TabModule> placements,
        CancellationToken cancellationToken = default);

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
