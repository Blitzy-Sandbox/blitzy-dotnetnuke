using AutoMapper;
using DnnMigration.Application.DTOs.Portal;          // PortalDto, PortalListItemDto, CreatePortalRequest, UpdatePortalRequest
using PortalEntity = DnnMigration.Domain.Entities.Portal;

namespace DnnMigration.Application.Mapping;

// MIGRATION: AutoMapper profile for the Portal domain. Replaces the legacy direct XML/serialization of
// DotNetNuke.Entities.Portals.PortalInfo (Library/Components/Portal/PortalInfo.vb, decorated with
// <XmlRoot>/<XmlElement>); there was no single legacy mapping class. Centralizing object-to-object mapping
// here lets the Api/services project the Domain entity to DTOs and never return raw entities (AAP §0.7.7).
//
// Namespace-collision note: the DTO namespace leaf (DnnMigration.Application.DTOs.Portal) collides with the
// entity type name DnnMigration.Domain.Entities.Portal, so the entity is imported under the alias
// "PortalEntity". The bare type name "Portal" is never referenced in this file (avoids CS0118/ambiguity).
//
// This type contains MAPPING CONFIGURATION ONLY — no business logic, validation, data access, or service
// calls (AAP §0.7.1/§0.7.3). The host registers it via AddAutoMapper(typeof(PortalProfile).Assembly).
public class PortalProfile : Profile
{
    public PortalProfile()
    {
        // MIGRATION: Portal (Library/Components/Portal/PortalInfo.vb) -> PortalDto. Faithful 1:1 by name.
        // PortalDto deliberately OMITS ProcessorPassword (sensitive payment-processor credential, AAP §0.7.6);
        // the entity's ProcessorPassword is simply an unused source member here. All 37 PortalDto members
        // match Portal entity members by name (ID-suffix already normalized to PortalId, etc.).
        CreateMap<PortalEntity, PortalDto>();

        // MIGRATION: Portal -> PortalListItemDto (lightweight grid row). 8 members
        // (PortalId, PortalName, Description, Currency, ExpiryDate, Users, Pages, HostFee) all match by name.
        // Users/Pages were lazy getters in PortalInfo.vb (computed via UserController/TabController when < 0);
        // the entity now holds plain int? values and the mapper copies them as-is (lazy hydration dropped).
        CreateMap<PortalEntity, PortalListItemDto>();

        // MIGRATION: CreatePortalRequest -> Portal. Maps the 17 client-supplied creation fields;
        // server-assigned / role / tab / counter / audit members are Ignored (set by the service/DB, not the client).
        CreateMap<CreatePortalRequest, PortalEntity>()
            .ForMember(d => d.PortalId, o => o.Ignore())
            .ForMember(d => d.AdministratorId, o => o.Ignore())
            .ForMember(d => d.AdministratorRoleId, o => o.Ignore())
            .ForMember(d => d.AdministratorRoleName, o => o.Ignore())
            .ForMember(d => d.RegisteredRoleId, o => o.Ignore())
            .ForMember(d => d.RegisteredRoleName, o => o.Ignore())
            .ForMember(d => d.BackgroundFile, o => o.Ignore())
            .ForMember(d => d.Guid, o => o.Ignore())
            .ForMember(d => d.PaymentProcessor, o => o.Ignore())
            .ForMember(d => d.ProcessorPassword, o => o.Ignore())
            .ForMember(d => d.ProcessorUserId, o => o.Ignore())
            .ForMember(d => d.SiteLogHistory, o => o.Ignore())
            .ForMember(d => d.AdminTabId, o => o.Ignore())
            .ForMember(d => d.SuperTabId, o => o.Ignore())
            .ForMember(d => d.Users, o => o.Ignore())
            .ForMember(d => d.Pages, o => o.Ignore())
            .ForMember(d => d.SplashTabId, o => o.Ignore())
            .ForMember(d => d.HomeTabId, o => o.Ignore())
            .ForMember(d => d.LoginTabId, o => o.Ignore())
            .ForMember(d => d.UserTabId, o => o.Ignore())
            .ForMember(d => d.Version, o => o.Ignore());

        // MIGRATION: UpdatePortalRequest -> Portal. Maps the 27 editable fields (incl. ProcessorPassword inbound,
        // write-only — never exposed on a response). Members absent from the update contract are Ignored.
        CreateMap<UpdatePortalRequest, PortalEntity>()
            .ForMember(d => d.AdministratorRoleId, o => o.Ignore())
            .ForMember(d => d.AdministratorRoleName, o => o.Ignore())
            .ForMember(d => d.RegisteredRoleId, o => o.Ignore())
            .ForMember(d => d.RegisteredRoleName, o => o.Ignore())
            .ForMember(d => d.Guid, o => o.Ignore())
            .ForMember(d => d.Email, o => o.Ignore())
            .ForMember(d => d.AdminTabId, o => o.Ignore())
            .ForMember(d => d.SuperTabId, o => o.Ignore())
            .ForMember(d => d.Users, o => o.Ignore())
            .ForMember(d => d.Pages, o => o.Ignore())
            .ForMember(d => d.Version, o => o.Ignore());
    }
}
