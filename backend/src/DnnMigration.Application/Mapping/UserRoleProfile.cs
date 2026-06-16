using AutoMapper;
using DnnMigration.Domain.Entities;
using DnnMigration.Application.DTOs.Role;

namespace DnnMigration.Application.Mapping;

/// <summary>
/// AutoMapper profile mapping the <see cref="UserRole"/> JOIN entity to/from its assignment DTOs.
/// MIGRATION: gives the Role service contract its assignment surface. The READ projection
/// (UserRole -> UserRoleAssignmentDto) surfaces the full per-assignment membership metadata
/// (EffectiveDate/ExpiryDate/IsTrialUsed/Subscribed) that legacy UserRoleInfo.vb carried. The
/// WRITE map (AssignUserRoleDto -> UserRole) carries ONLY the admin-supplied effective/expiry
/// window (the admin AddUserRole path never sets IsTrialUsed/Subscribed; see MIGRATION_NOTES.md
/// §6.2). This replaces CBO reflection hydration with explicit, compile-checked maps.
/// </summary>
public sealed class UserRoleProfile : Profile
{
    public UserRoleProfile()
    {
        // Read projection: UserRole -> UserRoleAssignmentDto. Convention by-name. The User/Role
        // navigations on the entity are source-only and intentionally not projected onto the DTO.
        CreateMap<UserRole, UserRoleAssignmentDto>();

        // Inbound assignment: AssignUserRoleDto -> UserRole (admin path: UserID/RoleID + effective/expiry window).
        // MIGRATION: UserRoleID is database-generated (identity) and the User/Role navigations are resolved by
        // the repository via the UserID/RoleID foreign keys, never from the command DTO. IsTrialUsed/Subscribed
        // are NOT supplied by the admin assignment command — they belong to the out-of-scope self-service
        // UpdateUserRole(Cancel) computed-window path (see MIGRATION_NOTES.md §6.2) — so they are ignored on the
        // destination (left at the entity default for a new row, untouched by an upsert). All four are ignored so
        // the mapper configuration validates with no unmapped destination members.
        CreateMap<AssignUserRoleDto, UserRole>()
            .ForMember(d => d.UserRoleID, o => o.Ignore())
            .ForMember(d => d.IsTrialUsed, o => o.Ignore())
            .ForMember(d => d.Subscribed, o => o.Ignore())
            .ForMember(d => d.User, o => o.Ignore())
            .ForMember(d => d.Role, o => o.Ignore());
    }
}
