using AutoMapper;
using DnnMigration.Application.DTOs.Module;           // ModuleResponse, CreateModuleRequest, UpdateModuleRequest
using ModuleEntity = DnnMigration.Domain.Entities.Module;

namespace DnnMigration.Application.Mapping;

// MIGRATION: AutoMapper profile for the Module domain. Replaces the legacy direct serialization of
// DotNetNuke.Entities.Modules.ModuleInfo (Library/Components/Modules/ModuleInfo.vb, 936 lines); there was
// no single legacy mapping class — the Web Forms tier serialized ModuleInfo objects directly. Centralizing
// object-to-object mapping here lets the Api/services project the Domain entity to DTOs and never return
// raw entities (AAP §0.7.7).
//
// Namespace-collision note: the DTO namespace leaf (DnnMigration.Application.DTOs.Module) collides with the
// entity type name DnnMigration.Domain.Entities.Module, so the entity is imported under the alias
// "ModuleEntity". The bare type name "Module" is never referenced in this file (avoids CS0118/ambiguity).
//
// This type contains MAPPING CONFIGURATION ONLY — no business logic, validation, data access, or service
// calls (AAP §0.7.1/§0.7.3). The host registers it via AddAutoMapper(typeof(ModuleProfile).Assembly).
public class ModuleProfile : Profile
{
    public ModuleProfile()
    {
        // MIGRATION: Module (Library/Components/Modules/ModuleInfo.vb) -> ModuleResponse. Faithful 1:1 by name
        // across all 48 scalar members. Two legacy-shape notes:
        //   - Visibility: legacy ModuleInfo._Visibility was the VisibilityState enum (Maximized=0/Minimized=1/None=2);
        //     the Domain entity and ModuleResponse both model it as int, so this is a plain int->int copy (no enum cast).
        //   - ControlType: maps SecurityAccessLevel (Domain.Enums) -> SecurityAccessLevel directly (enum->enum).
        // The entity's ModulePermissions navigation collection is intentionally NOT projected (ModuleResponse omits it).
        CreateMap<ModuleEntity, ModuleResponse>();

        // MIGRATION: CreateModuleRequest -> Module. Maps the 23 creation fields; identity/registration/definition
        // metadata, permissions and audit-ish members are server/DB-managed and Ignored (26 members).
        CreateMap<CreateModuleRequest, ModuleEntity>()
            .ForMember(d => d.TabModuleId, o => o.Ignore())
            .ForMember(d => d.ModuleId, o => o.Ignore())
            .ForMember(d => d.IsDeleted, o => o.Ignore())
            .ForMember(d => d.FriendlyName, o => o.Ignore())
            .ForMember(d => d.FolderName, o => o.Ignore())
            .ForMember(d => d.Description, o => o.Ignore())
            .ForMember(d => d.Version, o => o.Ignore())
            .ForMember(d => d.IsPremium, o => o.Ignore())
            .ForMember(d => d.IsAdmin, o => o.Ignore())
            .ForMember(d => d.BusinessControllerClass, o => o.Ignore())
            .ForMember(d => d.ModuleName, o => o.Ignore())
            .ForMember(d => d.SupportedFeatures, o => o.Ignore())
            .ForMember(d => d.CompatibleVersions, o => o.Ignore())
            .ForMember(d => d.Dependencies, o => o.Ignore())
            .ForMember(d => d.Permissions, o => o.Ignore())
            .ForMember(d => d.DefaultCacheTime, o => o.Ignore())
            .ForMember(d => d.ModuleControlId, o => o.Ignore())
            .ForMember(d => d.ControlSrc, o => o.Ignore())
            .ForMember(d => d.ControlType, o => o.Ignore())
            .ForMember(d => d.ControlTitle, o => o.Ignore())
            .ForMember(d => d.HelpUrl, o => o.Ignore())
            .ForMember(d => d.SupportsPartialRendering, o => o.Ignore())
            .ForMember(d => d.AuthorizedEditRoles, o => o.Ignore())
            .ForMember(d => d.AuthorizedViewRoles, o => o.Ignore())
            .ForMember(d => d.AuthorizedRoles, o => o.Ignore())
            .ForMember(d => d.ModulePermissions, o => o.Ignore());

        // MIGRATION: UpdateModuleRequest -> Module. Maps the 20 editable fields. Identity/placement keys
        // (PortalId, ModuleDefId, DesktopModuleId) are immutable on update and Ignored, along with the
        // primary/join keys (TabModuleId, ModuleId) that are never present on the request and the
        // registration/permission members above (29 members total).
        CreateMap<UpdateModuleRequest, ModuleEntity>()
            .ForMember(d => d.PortalId, o => o.Ignore())
            .ForMember(d => d.TabModuleId, o => o.Ignore())
            .ForMember(d => d.ModuleId, o => o.Ignore())
            .ForMember(d => d.ModuleDefId, o => o.Ignore())
            .ForMember(d => d.DesktopModuleId, o => o.Ignore())
            .ForMember(d => d.IsDeleted, o => o.Ignore())
            .ForMember(d => d.FriendlyName, o => o.Ignore())
            .ForMember(d => d.FolderName, o => o.Ignore())
            .ForMember(d => d.Description, o => o.Ignore())
            .ForMember(d => d.Version, o => o.Ignore())
            .ForMember(d => d.IsPremium, o => o.Ignore())
            .ForMember(d => d.IsAdmin, o => o.Ignore())
            .ForMember(d => d.BusinessControllerClass, o => o.Ignore())
            .ForMember(d => d.ModuleName, o => o.Ignore())
            .ForMember(d => d.SupportedFeatures, o => o.Ignore())
            .ForMember(d => d.CompatibleVersions, o => o.Ignore())
            .ForMember(d => d.Dependencies, o => o.Ignore())
            .ForMember(d => d.Permissions, o => o.Ignore())
            .ForMember(d => d.DefaultCacheTime, o => o.Ignore())
            .ForMember(d => d.ModuleControlId, o => o.Ignore())
            .ForMember(d => d.ControlSrc, o => o.Ignore())
            .ForMember(d => d.ControlType, o => o.Ignore())
            .ForMember(d => d.ControlTitle, o => o.Ignore())
            .ForMember(d => d.HelpUrl, o => o.Ignore())
            .ForMember(d => d.SupportsPartialRendering, o => o.Ignore())
            .ForMember(d => d.AuthorizedEditRoles, o => o.Ignore())
            .ForMember(d => d.AuthorizedViewRoles, o => o.Ignore())
            .ForMember(d => d.AuthorizedRoles, o => o.Ignore())
            .ForMember(d => d.ModulePermissions, o => o.Ignore());
    }
}
