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
}
