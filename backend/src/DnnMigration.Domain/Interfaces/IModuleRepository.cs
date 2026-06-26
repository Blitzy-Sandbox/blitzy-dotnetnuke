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
    // for multi-tenant isolation (AAP 0.7.1). Retained for internal all-portal reads (the AllModules display-settings
    // propagation in UpdateModule); list endpoints use the paged overload below to avoid over-fetching.
    Task<IEnumerable<Module>> GetByPortalIdAsync(int portalId);

    // MIGRATION: CP1 review (performance #22 — avoid over-fetching, AAP 0.7.7) — paged portal-scoped query returning
    // ONLY the requested page plus the total count, replacing the previous fetch-all-then-page-in-memory in
    // ModuleService.GetByPortalAsync. Excludes soft-deleted modules (legacy GetPortalModules SP: IsDeleted = 0) so the
    // total count and page window match the active listing. Zero-based page index preserved for parity.
    Task<(IEnumerable<Module> Items, int TotalCount)> GetByPortalPagedAsync(int portalId, int pageIndex, int pageSize);

    // MIGRATION: Replaces DataProvider.GetTabModules(TabId) — modules placed on a specific page (tab). PORTAL-SCOPED
    // (CP1 review IModuleRepository #1 / multi-tenant isolation, AAP 0.7.1): carries portalId so a tab's modules are
    // read only within the owning portal and the service can enforce tenant ownership + re-sequence siblings safely.
    Task<IEnumerable<Module>> GetByTabIdAsync(int portalId, int tabId);

    // MIGRATION: Replaces DataProvider.GetModule(ModuleId, TabId). PORTAL-SCOPED (CP1 review IModuleRepository #1):
    // the lookup MUST carry portalId so the service can enforce that a module is only read/mutated within its owning
    // portal (the earlier moduleId-only collapse is rejected by the review — it cannot express the legacy
    // portal-scoped operation). The implementation MUST eager-load the ModulePermissions navigation so the service
    // can run the legacy permission diff (UpdateModule L1099-1118). Returns null when no module with that id exists
    // IN that portal.
    Task<Module?> GetByIdAsync(int portalId, int moduleId);

    // MIGRATION: Replaces DataProvider.GetModuleByDefinition(PortalId, FriendlyName)/
    // ModuleController.GetModuleByDefinition. Portal-scoped lookup by module-definition friendly name.
    Task<Module?> GetByDefinitionAsync(int portalId, string friendlyName);

    // MIGRATION: Replaces DataProvider.AddModule(...)/ModuleController.AddModule(objModule). Returns the persisted
    // Module so the database-generated ModuleId is available.
    Task<Module> AddAsync(Module module);

    // MIGRATION: Replaces DataProvider.UpdateModule(...)/ModuleController.UpdateModule(objModule).
    Task UpdateAsync(Module module);

    // MIGRATION: Replaces DataProvider.DeleteModule(ModuleId)/ModuleController.DeleteModule(ModuleId). PORTAL-SCOPED
    // (CP1 review IModuleRepository #1): carries portalId so a delete is constrained to the owning portal (the
    // moduleId-only collapse is rejected — it cannot enforce the legacy portal-scoped delete).
    Task DeleteAsync(int portalId, int moduleId);
}
