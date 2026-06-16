using DnnMigration.Application.DTOs.Module;

namespace DnnMigration.Application.Interfaces;

/// <summary>
/// Application service contract for the Module aggregate. MIGRATION: ported from the public
/// business surface of ModuleController.vb, re-expressed as async DTO-based operations that
/// parallel IModuleRepository. Module is soft-deleted (IsDeleted); list operations exclude
/// deleted rows in the implementation. Implemented by Application/Services/ModuleService.cs.
/// </summary>
public interface IModuleService
{
    Task<ModuleDto?> GetByIdAsync(int moduleId, CancellationToken cancellationToken = default);

    Task<IEnumerable<ModuleDto>> GetByTabAsync(int tabId, CancellationToken cancellationToken = default);

    Task<IEnumerable<ModuleDto>> GetByPortalAsync(int portalId, CancellationToken cancellationToken = default);

    Task<ModuleDto> CreateAsync(CreateModuleDto request, CancellationToken cancellationToken = default);

    Task<ModuleDto> UpdateAsync(UpdateModuleDto request, CancellationToken cancellationToken = default);

    Task DeleteAsync(int moduleId, CancellationToken cancellationToken = default);
}
