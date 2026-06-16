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
        // Read projection: Role -> RoleDto. Convention by-name. Entity and DTO now share
        // identical nullability for RoleGroupID (int?), ServiceFee/TrialFee (float?) and
        // TrialPeriod/BillingPeriod (int?), so these map DIRECTLY null->null with no
        // coercion. A null/unassigned role group is preserved as null (never forced to 0).
        CreateMap<Role, RoleDto>();

        // Inbound create: CreateRoleDto -> Role.
        // MIGRATION: RoleID is database-generated (identity); never assigned from input.
        CreateMap<CreateRoleDto, Role>()
            .ForMember(d => d.RoleID, o => o.Ignore());

        // Inbound update: UpdateRoleDto -> Role. All entity members are covered by name.
        // RoleGroupID maps int? -> int? directly (the entity is nullable), so an unassigned
        // role group round-trips as null rather than being coerced to 0; no ignores required.
        CreateMap<UpdateRoleDto, Role>();
    }
}
