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
}
