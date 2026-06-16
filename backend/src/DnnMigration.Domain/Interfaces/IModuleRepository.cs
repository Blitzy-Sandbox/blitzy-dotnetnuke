using DnnMigration.Domain.Entities;

namespace DnnMigration.Domain.Interfaces;

/// <summary>
/// Repository abstraction for the <see cref="Module"/> aggregate.
/// MIGRATION: extracted from the data-access methods of ModuleController.vb. Implemented by the
/// Infrastructure layer with EF Core; consumed by the Application ModuleService.
/// </summary>
public interface IModuleRepository
{
    /// <summary>Gets a module by its identifier, or <c>null</c> if not found. (legacy ModuleController.GetModule)</summary>
    Task<Module?> GetByIdAsync(int moduleId, CancellationToken cancellationToken = default);

    /// <summary>Gets all modules placed on the given tab/page. (legacy ModuleController.GetTabModules)</summary>
    Task<IEnumerable<Module>> GetByTabAsync(int tabId, CancellationToken cancellationToken = default);

    /// <summary>Gets all modules belonging to the given portal. (legacy ModuleController.GetModules)</summary>
    Task<IEnumerable<Module>> GetByPortalAsync(int portalId, CancellationToken cancellationToken = default);

    /// <summary>Adds a new module and returns the persisted entity (with its generated id populated). (legacy ModuleController.AddModule)</summary>
    Task<Module> AddAsync(Module module, CancellationToken cancellationToken = default);

    /// <summary>Updates an existing module. (legacy ModuleController.UpdateModule)</summary>
    Task UpdateAsync(Module module, CancellationToken cancellationToken = default);

    // MIGRATION: Module uses SOFT-delete via the IsDeleted flag (AAP §0.3.3). The Infrastructure
    // implementation sets IsDeleted = true rather than removing the row, and all list reads
    // (GetByTabAsync / GetByPortalAsync) filter IsDeleted == false. This is an implementation concern;
    // the contract only declares the operation.
    /// <summary>Soft-deletes a module by id. (legacy ModuleController.DeleteModule)</summary>
    Task DeleteAsync(int moduleId, CancellationToken cancellationToken = default);
}
