using DnnMigration.Application.DTOs.Common;
using DnnMigration.Application.DTOs.Module;
using DnnMigration.Domain.Common;

namespace DnnMigration.Application.Interfaces;

/// <summary>
/// Service-layer contract for module management. Consumed by ModulesController via
/// constructor injection. The implementation orchestrates the module repository + unit of
/// work and projects Domain entities to DTOs.
/// </summary>
// MIGRATION: Abstracted from the public operations of Library/Components/Modules/ModuleController.vb
// (AddModule L645, UpdateModule L1095, DeleteModule L819, GetModule L1418, GetModules(PortalID) L915,
// GetTabModules(TabId) L1044). Business logic moves to ModuleService; data access to IModuleRepository.
// DTO-only contract — no raw Domain entities exposed (AAP 0.7.7).
public interface IModuleService
{
    // MIGRATION: Legacy GetModules(PortalID) (ModuleController.vb L915) — scoped by portalId (multi-tenant, AAP 0.7.1).
    // Paged (PagedResult<ModuleResponse>) because a portal can host many modules.
    Task<Result<PagedResult<ModuleResponse>>> GetByPortalAsync(int portalId, int pageIndex, int pageSize, CancellationToken cancellationToken = default);

    // MIGRATION: Legacy GetTabModules(TabId) (ModuleController.vb L1044) — scoped by tabId. Unpaged IEnumerable
    // (a tab/page hosts only a handful of modules).
    Task<Result<IEnumerable<ModuleResponse>>> GetByTabAsync(int tabId, CancellationToken cancellationToken = default);

    // MIGRATION: Legacy GetModule(ModuleId, TabId) (ModuleController.vb L1418). Single key (moduleId) in the contract.
    Task<Result<ModuleResponse>> GetByIdAsync(int moduleId, CancellationToken cancellationToken = default);

    // MIGRATION: Legacy AddModule (ModuleController.vb L645). POST /api/modules -> 201.
    Task<Result<ModuleResponse>> CreateAsync(CreateModuleRequest request, CancellationToken cancellationToken = default);

    // MIGRATION: Legacy UpdateModule (ModuleController.vb L1095). moduleId route-bound; PUT -> 200.
    Task<Result<ModuleResponse>> UpdateAsync(int moduleId, UpdateModuleRequest request, CancellationToken cancellationToken = default);

    // MIGRATION: Legacy DeleteModule (ModuleController.vb L819). DELETE -> 204. Non-generic Result (no payload).
    Task<Result> DeleteAsync(int moduleId, CancellationToken cancellationToken = default);
}
