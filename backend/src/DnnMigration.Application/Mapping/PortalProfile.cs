using AutoMapper;
using DnnMigration.Domain.Entities;
using DnnMigration.Application.DTOs.Portal;

namespace DnnMigration.Application.Mapping;

/// <summary>
/// AutoMapper profile mapping the <see cref="Portal"/> entity to/from its DTOs.
/// MIGRATION: replaces legacy CBO.FillObject reflection hydration (Library/Components/Shared/CBO.vb)
/// with explicit, compile-checked maps. Registered by the Api composition root via AddAutoMapper.
/// </summary>
public sealed class PortalProfile : Profile
{
    public PortalProfile()
    {
        // Read projection: Portal -> PortalDto.
        // Convention by-name maps every member; AutoMapper widens int->int? (Users/Pages)
        // and DateTime->DateTime? (ExpiryDate) implicitly. No ignores required.
        CreateMap<Portal, PortalDto>();

        // Inbound create: CreatePortalDto -> Portal.
        // MIGRATION: PortalID is database-generated (identity); Users/Pages are derived metrics
        // (legacy lazy-loaded via UserController.GetUserCountByPortal / page count, PortalInfo.vb L308-322)
        // and are never assigned from inbound input.
        CreateMap<CreatePortalDto, Portal>()
            .ForMember(d => d.PortalID, o => o.Ignore())
            .ForMember(d => d.Users, o => o.Ignore())
            .ForMember(d => d.Pages, o => o.Ignore());

        // Inbound update: UpdatePortalDto -> Portal (carries PortalID; still ignores derived metrics).
        CreateMap<UpdatePortalDto, Portal>()
            .ForMember(d => d.Users, o => o.Ignore())
            .ForMember(d => d.Pages, o => o.Ignore());
    }
}
