using AutoMapper;
using DnnMigration.Domain.Entities;
using DnnMigration.Application.DTOs.Tab;

namespace DnnMigration.Application.Mapping;

/// <summary>
/// AutoMapper profile mapping the <see cref="Tab"/> entity to/from its DTOs.
/// MIGRATION: replaces legacy CBO reflection hydration with explicit, compile-checked maps.
/// </summary>
public sealed class TabProfile : Profile
{
    public TabProfile()
    {
        // Read projection: Tab -> TabDto.
        // MIGRATION: TabDto.TabType is populated from the entity's read-only computed TabType
        // (TabInfo.vb L406-408: GetURLType(_Url)); AutoMapper reads its getter. RefreshInterval int -> int? widens.
        CreateMap<Tab, TabDto>();

        // Inbound create: CreateTabDto -> Tab.
        // MIGRATION: IsDeleted is soft-delete state owned by the service/repository — never set from inbound DTO.
        // Level/HasChildren/TabPath are server-computed hierarchy metadata; DisableLink/PageHeadText/
        // AuthorizedRoles/AdministratorRoles are not part of the create contract; TabPermissions resolved separately.
        // TabType is read-only (no setter) and is auto-skipped — do NOT add a ForMember for it.
        CreateMap<CreateTabDto, Tab>()
            .ForMember(d => d.TabID, o => o.Ignore())
            .ForMember(d => d.Level, o => o.Ignore())
            .ForMember(d => d.DisableLink, o => o.Ignore())
            .ForMember(d => d.IsDeleted, o => o.Ignore())
            .ForMember(d => d.TabPath, o => o.Ignore())
            .ForMember(d => d.HasChildren, o => o.Ignore())
            .ForMember(d => d.PageHeadText, o => o.Ignore())
            .ForMember(d => d.AuthorizedRoles, o => o.Ignore())
            .ForMember(d => d.AdministratorRoles, o => o.Ignore())
            .ForMember(d => d.TabPermissions, o => o.Ignore());

        // Inbound update: UpdateTabDto -> Tab (carries TabID; same ignores minus TabID).
        CreateMap<UpdateTabDto, Tab>()
            .ForMember(d => d.Level, o => o.Ignore())
            .ForMember(d => d.DisableLink, o => o.Ignore())
            .ForMember(d => d.IsDeleted, o => o.Ignore())
            .ForMember(d => d.TabPath, o => o.Ignore())
            .ForMember(d => d.HasChildren, o => o.Ignore())
            .ForMember(d => d.PageHeadText, o => o.Ignore())
            .ForMember(d => d.AuthorizedRoles, o => o.Ignore())
            .ForMember(d => d.AdministratorRoles, o => o.Ignore())
            .ForMember(d => d.TabPermissions, o => o.Ignore());
    }
}
