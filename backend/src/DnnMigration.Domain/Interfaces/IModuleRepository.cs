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
}
