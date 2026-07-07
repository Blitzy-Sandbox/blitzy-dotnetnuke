using DnnMigration.Application.DTOs;

namespace DnnMigration.Application.Interfaces;

/// <summary>
/// Application service contract for module management, exposing the CRUD surface
/// consumed by the <c>/api/modules</c> endpoints.
/// </summary>
// MIGRATION: replaces the business surface of the legacy DotNetNuke ModuleController.vb
// (GetModules/GetModule/AddModule/UpdateModule/DeleteModule). DTO-only, async;
// business rules move to ModuleService and data access to a repository.
public interface IModuleService
{
    /// <summary>Returns all modules.</summary>
    Task<IEnumerable<ModuleDto>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns all modules belonging to the specified portal.</summary>
    // MIGRATION: legacy ModuleController.GetModules(PortalID).
    Task<IEnumerable<ModuleDto>> GetByPortalAsync(int portalId, CancellationToken cancellationToken = default);

    /// <summary>Returns the first module in a portal whose definition has the given friendly name, or <c>null</c> if none matches.</summary>
    // MIGRATION: legacy ModuleController.GetModuleByDefinition(PortalId, FriendlyName) [ModuleController.vb L955];
    // surfaces the by-definition lookup exposed by GET /api/modules/by-definition and backed by
    // IModuleRepository.GetByDefinitionAsync.
    Task<ModuleDto?> GetByDefinitionAsync(int portalId, string friendlyName, CancellationToken cancellationToken = default);

    /// <summary>Returns the module with the given id, or <c>null</c> if not found.</summary>
    Task<ModuleDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Creates/places a new module and returns the created projection.</summary>
    Task<ModuleDto> CreateAsync(CreateModuleDto dto, CancellationToken cancellationToken = default);

    /// <summary>Updates the module with the given id, or returns <c>null</c> if it does not exist.</summary>
    Task<ModuleDto?> UpdateAsync(int id, UpdateModuleDto dto, CancellationToken cancellationToken = default);

    /// <summary>Deletes the module with the given id; returns <c>true</c> if deleted, <c>false</c> if not found.</summary>
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
}
