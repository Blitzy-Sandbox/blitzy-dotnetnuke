using AutoMapper;
using DnnMigration.Domain.Entities;
using DnnMigration.Application.DTOs.Role;

namespace DnnMigration.Application.Mapping;

/// <summary>
/// AutoMapper profile mapping the <see cref="Role"/> entity to/from its DTOs.
/// MIGRATION: replaces legacy CBO reflection hydration with explicit, compile-checked maps.
/// </summary>
public sealed class RoleProfile : Profile
{
    public RoleProfile()
    {
        // Read projection: Role -> RoleDto. Convention by-name. MIGRATION: the NULL-able role fields are now
        // nullable on BOTH sides (RoleGroupID/TrialPeriod/BillingPeriod int?; ServiceFee/TrialFee float?), so
        // they map directly with NO narrowing and NO null -> 0 coercion -- a DB null surfaces as JSON null,
        // preserving the legacy Null.NullInteger "no role group"/"unset fee" semantics. No ignores required.
        CreateMap<Role, RoleDto>();

        // Inbound create: CreateRoleDto -> Role.
        // MIGRATION: RoleID is database-generated (identity); never assigned from input.
        CreateMap<CreateRoleDto, Role>()
            .ForMember(d => d.RoleID, o => o.Ignore());

        // Inbound update: UpdateRoleDto -> Role. All entity members are covered by name. MIGRATION: the
        // nullable role fields map int?/float? -> int?/float? directly (the former lossy int? -> int
        // conversion that silently turned a null role group into 0 is eliminated now that Role.RoleGroupID
        // and the fee/period members are nullable). No ignores required.
        CreateMap<UpdateRoleDto, Role>();
    }
}
