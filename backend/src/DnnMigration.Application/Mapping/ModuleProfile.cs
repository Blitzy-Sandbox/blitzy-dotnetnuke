using AutoMapper;
using DnnMigration.Domain.Entities;
using DnnMigration.Application.DTOs.Module;

namespace DnnMigration.Application.Mapping;

/// <summary>
/// AutoMapper profile mapping the <see cref="Module"/> entity to/from its DTOs.
/// MIGRATION: replaces legacy CBO reflection hydration with explicit, compile-checked maps.
/// </summary>
public sealed class ModuleProfile : Profile
{
    public ModuleProfile()
    {
        // Read projection: Module -> ModuleDto.
        // Module carries flattened DesktopModule/ModuleDefinition + module-control fields; ModuleDto exposes a
        // 29-member subset (all present on Module by name). Visibility (VisibilityState {Maximized=0,Minimized=1,
        // None=2}) maps by value verbatim — no numeric remapping. No ignores required.
        CreateMap<Module, ModuleDto>();

        // Inbound create: CreateModuleDto -> Module.
        // MIGRATION: ModuleID/TabModuleID are database-generated; IsDeleted is soft-delete state owned by the
        // service/repository (ModuleInfo.vb L61). DesktopModule-derived descriptor fields (FriendlyName/FolderName/
        // Description/Version/IsPremium/IsAdmin/BusinessControllerClass/ModuleName/SupportedFeatures) and
        // module-control fields (ModuleControlId/ControlSrc/ControlType/ControlTitle/HelpUrl/SupportsPartialRendering)
        // are not part of the create contract; ModulePermissions is resolved separately.
        CreateMap<CreateModuleDto, Module>()
            .ForMember(d => d.ModuleID, o => o.Ignore())
            .ForMember(d => d.TabModuleID, o => o.Ignore())
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
            .ForMember(d => d.ModuleControlId, o => o.Ignore())
            .ForMember(d => d.ControlSrc, o => o.Ignore())
            .ForMember(d => d.ControlType, o => o.Ignore())
            .ForMember(d => d.ControlTitle, o => o.Ignore())
            .ForMember(d => d.HelpUrl, o => o.Ignore())
            .ForMember(d => d.SupportsPartialRendering, o => o.Ignore())
            .ForMember(d => d.ModulePermissions, o => o.Ignore());

        // Inbound update: UpdateModuleDto -> Module (carries ModuleID).
        // MIGRATION: structural FKs (PortalID/TabID/ModuleDefID/DesktopModuleID) are immutable on update,
        // plus the same descriptor/control/soft-delete/nav ignores as create.
        CreateMap<UpdateModuleDto, Module>()
            .ForMember(d => d.PortalID, o => o.Ignore())
            .ForMember(d => d.TabID, o => o.Ignore())
            .ForMember(d => d.TabModuleID, o => o.Ignore())
            .ForMember(d => d.ModuleDefID, o => o.Ignore())
            .ForMember(d => d.IsDeleted, o => o.Ignore())
            .ForMember(d => d.DesktopModuleID, o => o.Ignore())
            .ForMember(d => d.FriendlyName, o => o.Ignore())
            .ForMember(d => d.FolderName, o => o.Ignore())
            .ForMember(d => d.Description, o => o.Ignore())
            .ForMember(d => d.Version, o => o.Ignore())
            .ForMember(d => d.IsPremium, o => o.Ignore())
            .ForMember(d => d.IsAdmin, o => o.Ignore())
            .ForMember(d => d.BusinessControllerClass, o => o.Ignore())
            .ForMember(d => d.ModuleName, o => o.Ignore())
            .ForMember(d => d.SupportedFeatures, o => o.Ignore())
            .ForMember(d => d.ModuleControlId, o => o.Ignore())
            .ForMember(d => d.ControlSrc, o => o.Ignore())
            .ForMember(d => d.ControlType, o => o.Ignore())
            .ForMember(d => d.ControlTitle, o => o.Ignore())
            .ForMember(d => d.HelpUrl, o => o.Ignore())
            .ForMember(d => d.SupportsPartialRendering, o => o.Ignore())
            .ForMember(d => d.ModulePermissions, o => o.Ignore());
    }
}
