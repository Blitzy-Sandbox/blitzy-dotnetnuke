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
        // Convention by-name maps every member. Nullable physical FK/tab-id columns are int? on BOTH the
        // entity and PortalDto, so they map directly with no widening and no null -> 0 coercion. The entity's
        // ProcessorPassword secret has NO destination on PortalDto (deliberately omitted), so it is simply not
        // projected into read responses. No ignores required.
        CreateMap<Portal, PortalDto>();

        // Inbound create: CreatePortalDto -> Portal.
        // MIGRATION: PortalID is database-generated (identity); Users/Pages are derived metrics
        // (legacy lazy-loaded via UserController.GetUserCountByPortal / page count, PortalInfo.vb L308-322)
        // and are never assigned from inbound input.
        // MIGRATION (INTEGRITY): GUID is server-managed (physical column default newid()) and is excluded from
        // the create DTO; it is explicitly ignored here so it is never set from client input and AutoMapper
        // configuration validation passes (no unmapped destination member).
        CreateMap<CreatePortalDto, Portal>()
            .ForMember(d => d.PortalID, o => o.Ignore())
            .ForMember(d => d.GUID, o => o.Ignore())
            .ForMember(d => d.Users, o => o.Ignore())
            .ForMember(d => d.Pages, o => o.Ignore());

        // Inbound update: UpdatePortalDto -> Portal (carries PortalID; still ignores derived metrics and the
        // server-managed GUID so an existing portal's identity GUID is never overwritten by client input).
        CreateMap<UpdatePortalDto, Portal>()
            .ForMember(d => d.GUID, o => o.Ignore())
            .ForMember(d => d.Users, o => o.Ignore())
            .ForMember(d => d.Pages, o => o.Ignore());
    }
}
