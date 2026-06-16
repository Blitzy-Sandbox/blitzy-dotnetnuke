using AutoMapper;
using DnnMigration.Domain.Entities;
using DnnMigration.Application.DTOs.Role;

namespace DnnMigration.Application.Mapping;

/// <summary>
/// AutoMapper profile mapping the <see cref="UserRole"/> JOIN entity to/from its assignment DTOs.
/// MIGRATION: gives the Role service contract a round-trip surface for the per-assignment
/// membership metadata (EffectiveDate/ExpiryDate/IsTrialUsed/Subscribed) that legacy
/// UserRoleInfo.vb carried, replacing CBO reflection hydration with explicit, compile-checked maps.
/// </summary>
public sealed class UserRoleProfile : Profile
{
    public UserRoleProfile()
    {
        // Read projection: UserRole -> UserRoleAssignmentDto. Convention by-name. The User/Role
        // navigations on the entity are source-only and intentionally not projected onto the DTO.
        CreateMap<UserRole, UserRoleAssignmentDto>();

        // Inbound assignment: AssignUserRoleDto -> UserRole.
        // MIGRATION: UserRoleID is database-generated (identity) and the User/Role navigations are
        // resolved by the repository via the UserID/RoleID foreign keys, never from the command DTO;
        // both are ignored so the mapper configuration validates with no unmapped destination members.
        CreateMap<AssignUserRoleDto, UserRole>()
            .ForMember(d => d.UserRoleID, o => o.Ignore())
            .ForMember(d => d.User, o => o.Ignore())
            .ForMember(d => d.Role, o => o.Ignore());
    }
}
