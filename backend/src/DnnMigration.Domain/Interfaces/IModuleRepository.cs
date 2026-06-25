using DnnMigration.Domain.Entities;

namespace DnnMigration.Domain.Interfaces;

// MIGRATION: Abstracts the module data-access surface of the legacy DataProvider
// (Library/Components/Providers/Data/DataProvider.vb) and ModuleController.vb. IDataReader/FillInfo + stored
// procedures replaced by async LINQ returning Module entities. Concrete EF Core implementation lives in
// DnnMigration.Infrastructure/Repositories/ModuleRepository.cs. Modules are scoped by PortalId (tenant) and
// TabId (page placement) per AAP 0.7.1.
public interface IModuleRepository
{
    // MIGRATION: Replaces DataProvider.GetModules(PortalId)/ModuleController.GetModules(PortalID). Portal-scoped
    // for multi-tenant isolation (AAP 0.7.1).
    Task<IEnumerable<Module>> GetByPortalIdAsync(int portalId);

    // MIGRATION: Replaces DataProvider.GetTabModules(TabId) — modules placed on a specific page (tab).
    Task<IEnumerable<Module>> GetByTabIdAsync(int tabId);

    // MIGRATION: Replaces DataProvider.GetModule(ModuleId, TabId). The legacy member also took TabId (a module can
    // appear on multiple tabs via TabModule rows); collapsed to a ModuleId-only lookup of the module aggregate.
    // Returns null when not found.
    Task<Module?> GetByIdAsync(int moduleId);

    // MIGRATION: Replaces DataProvider.GetModuleByDefinition(PortalId, FriendlyName)/
    // ModuleController.GetModuleByDefinition. Portal-scoped lookup by module-definition friendly name.
    Task<Module?> GetByDefinitionAsync(int portalId, string friendlyName);

    // MIGRATION: Replaces DataProvider.AddModule(...)/ModuleController.AddModule(objModule). Returns the persisted
    // Module so the database-generated ModuleId is available.
    Task<Module> AddAsync(Module module);

    // MIGRATION: Replaces DataProvider.UpdateModule(...)/ModuleController.UpdateModule(objModule).
    Task UpdateAsync(Module module);

    // MIGRATION: Replaces DataProvider.DeleteModule(ModuleId)/ModuleController.DeleteModule(ModuleId).
    Task DeleteAsync(int moduleId);
}
