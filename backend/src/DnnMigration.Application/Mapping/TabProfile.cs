using AutoMapper;
using DnnMigration.Application.DTOs.Tab;              // TabResponse, CreateTabRequest, UpdateTabRequest
using TabEntity = DnnMigration.Domain.Entities.Tab;

namespace DnnMigration.Application.Mapping;

// MIGRATION: AutoMapper profile for the Tab (DNN content page) domain. Replaces the legacy direct
// XML/serialization of DotNetNuke.Entities.Tabs.TabInfo (Library/Components/Tabs/TabInfo.vb, decorated with
// <XmlRoot("tab")>/<XmlElement>); there was no single legacy mapping class. Centralizing object-to-object
// mapping here lets the Api/services project the Domain entity to DTOs and never return raw entities
// (AAP §0.7.7: DTO projection avoids over-fetching; raw EF entities are never returned).
//
// Namespace-collision note: the DTO namespace leaf (DnnMigration.Application.DTOs.Tab) collides with the
// entity type name DnnMigration.Domain.Entities.Tab, so the entity is imported under the alias "TabEntity".
// The bare type name "Tab" is never referenced in this file, and "using DnnMigration.Domain.Entities;" is
// intentionally NOT added (avoids CS0118 / ambiguous-reference between the namespace and the type).
//
// This type contains MAPPING CONFIGURATION ONLY — no business logic, validation, data access, or service
// calls (AAP §0.7.1/§0.7.3). The host registers it via AddAutoMapper(typeof(TabProfile).Assembly).
public class TabProfile : Profile
{
    public TabProfile()
    {
        // MIGRATION: Tab (Library/Components/Tabs/TabInfo.vb) -> TabResponse. Faithful 1:1 by name across all 25
        // scalar members (TabId, TabOrder, PortalId, TabName, IsVisible, ParentId, Level, IconFile, DisableLink,
        // Title, Description, KeyWords, IsDeleted, Url, SkinSrc, ContainerSrc, TabPath, StartDate, EndDate,
        // HasChildren, RefreshInterval, PageHeadText, IsSecure, AuthorizedRoles, AdministratorRoles).
        // The entity's TabPermissions navigation collection is intentionally NOT projected (TabResponse omits it):
        // tab-permission management is out of /api/tabs CRUD scope and the collection would over-fetch/leak
        // persistence types. As an unmapped *source* member it requires no ForMember (AutoMapper validates only
        // unmapped *destination* members), so this read map needs no explicit member configuration.
        CreateMap<TabEntity, TabResponse>();

        // MIGRATION: CreateTabRequest -> Tab. Maps the 18 client-supplied creation fields (PortalId, TabName,
        // ParentId, Title, Description, KeyWords, IsVisible, DisableLink, Url, IconFile, SkinSrc, ContainerSrc,
        // IsSecure, RefreshInterval, PageHeadText, StartDate, EndDate, TabOrder). The remaining 8 entity members
        // are server/DB-managed and Ignored: TabId (DB-generated key), Level/TabPath/HasChildren (server-computed
        // hierarchy derived from ParentId + the page tree), IsDeleted (soft-delete flag), AuthorizedRoles/
        // AdministratorRoles (legacy permission strings set by the service, not the client), and the
        // TabPermissions navigation collection (managed outside this CRUD map).
        CreateMap<CreateTabRequest, TabEntity>()
            .ForMember(d => d.TabId, o => o.Ignore())
            .ForMember(d => d.Level, o => o.Ignore())
            .ForMember(d => d.IsDeleted, o => o.Ignore())
            .ForMember(d => d.TabPath, o => o.Ignore())
            .ForMember(d => d.HasChildren, o => o.Ignore())
            .ForMember(d => d.AuthorizedRoles, o => o.Ignore())
            .ForMember(d => d.AdministratorRoles, o => o.Ignore())
            .ForMember(d => d.TabPermissions, o => o.Ignore());

        // MIGRATION: UpdateTabRequest -> Tab. Maps the 17 editable fields (the creation set minus PortalId).
        // TabId (route-bound, taken from /api/tabs/{id}) and PortalId (multi-tenant scope, immutable on update —
        // the service preserves it from the existing entity) are additionally Ignored alongside the same
        // server-managed members as the create map: Level/TabPath/HasChildren (server-computed hierarchy),
        // IsDeleted (soft-delete flag), AuthorizedRoles/AdministratorRoles (permission strings), and the
        // TabPermissions navigation collection.
        CreateMap<UpdateTabRequest, TabEntity>()
            .ForMember(d => d.TabId, o => o.Ignore())
            .ForMember(d => d.PortalId, o => o.Ignore())
            .ForMember(d => d.Level, o => o.Ignore())
            .ForMember(d => d.IsDeleted, o => o.Ignore())
            .ForMember(d => d.TabPath, o => o.Ignore())
            .ForMember(d => d.HasChildren, o => o.Ignore())
            .ForMember(d => d.AuthorizedRoles, o => o.Ignore())
            .ForMember(d => d.AdministratorRoles, o => o.Ignore())
            .ForMember(d => d.TabPermissions, o => o.Ignore());
    }
}
