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
        // Read projection: Role -> RoleDto. Convention by-name; AutoMapper widens
        // RoleGroupID int -> int? implicitly. No ignores required.
        CreateMap<Role, RoleDto>();

        // Inbound create: CreateRoleDto -> Role.
        // MIGRATION: RoleID is database-generated (identity); never assigned from input.
        CreateMap<CreateRoleDto, Role>()
            .ForMember(d => d.RoleID, o => o.Ignore());

        // Inbound update: UpdateRoleDto -> Role. All 15 entity members are covered by name
        // (RoleGroupID int? -> int is a valid AutoMapper conversion); no ignores required.
        CreateMap<UpdateRoleDto, Role>();
    }
}
