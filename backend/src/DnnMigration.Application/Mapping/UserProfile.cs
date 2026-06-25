using AutoMapper;
using DnnMigration.Application.DTOs.User;             // UserResponse, CreateUserRequest, UpdateUserRequest
using UserEntity = DnnMigration.Domain.Entities.User;

namespace DnnMigration.Application.Mapping;

// MIGRATION: AutoMapper profile for the User domain. Replaces the legacy direct serialization of
// DotNetNuke.Entities.Users.UserInfo (Library/Components/Users/UserInfo.vb) together with the
// UserRoleInfo join (Library/Components/Users/UserRoleInfo.vb). Centralizing object-to-object mapping
// here lets the Api/services project the Domain entity to DTOs and never return raw entities — and,
// critically, never surface credential material (AAP §0.7.6/§0.7.7).
//
// Namespace-collision note: the DTO namespace leaf (DnnMigration.Application.DTOs.User) collides with the
// entity type name DnnMigration.Domain.Entities.User, so the entity is imported under the alias
// "UserEntity". The bare type name "User" is never referenced in this file (avoids CS0118/ambiguity), and
// "using DnnMigration.Domain.Entities;" is deliberately NOT added. The UserRole/Role types appear only
// inside the Roles projection lambda (reached via the UserRoles navigation) and are inferred there.
//
// This type contains MAPPING CONFIGURATION ONLY — no business logic, validation, data access, or service
// calls (AAP §0.7.1/§0.7.3). The host registers it via AddAutoMapper(typeof(UserProfile).Assembly).
public class UserProfile : Profile
{
    public UserProfile()
    {
        // MIGRATION: User (Library/Components/Users/UserInfo.vb) -> UserResponse. 14 members map 1:1 by name.
        // Roles: legacy UserInfo.Roles was a String() of role names hydrated via RoleController.GetRolesByUser;
        // the entity replaces it with the UserRoles navigation, so flatten to role-name strings here.
        // NO credential members are present on either side (AAP §0.7.6). The entity's LastActivityDate /
        // LastLockoutDate are unused source members (UserResponse omits them) and need no configuration.
        CreateMap<UserEntity, UserResponse>()
            .ForMember(d => d.Roles,
                o => o.MapFrom(src => src.UserRoles
                    .Where(ur => ur.Role != null)
                    .Select(ur => ur.Role!.RoleName)
                    .ToList()));

        // MIGRATION: CreateUserRequest -> User. Maps PortalId, Username, Email, DisplayName, FirstName, LastName.
        // The request's Password/Confirm are inbound credential inputs that DO NOT exist on the entity (handled by
        // the Identity layer / PasswordHasher, AAP §0.7.6) — they are simply unused source members, never mapped here.
        // All entity members not supplied by the create contract are Ignored (UserId is DB-generated; FullName is
        // derived; IsSuperUser/AffiliateId/IsApproved/audit dates/LockedOut and the UserRoles nav are server-managed).
        CreateMap<CreateUserRequest, UserEntity>()
            .ForMember(d => d.UserId, o => o.Ignore())
            .ForMember(d => d.FullName, o => o.Ignore())
            .ForMember(d => d.IsSuperUser, o => o.Ignore())
            .ForMember(d => d.AffiliateId, o => o.Ignore())
            .ForMember(d => d.IsApproved, o => o.Ignore())
            .ForMember(d => d.CreatedDate, o => o.Ignore())
            .ForMember(d => d.LastLoginDate, o => o.Ignore())
            .ForMember(d => d.LastActivityDate, o => o.Ignore())
            .ForMember(d => d.LastLockoutDate, o => o.Ignore())
            .ForMember(d => d.LockedOut, o => o.Ignore())
            .ForMember(d => d.UserRoles, o => o.Ignore());

        // MIGRATION: UpdateUserRequest -> User. Maps Email, DisplayName, FirstName, LastName, IsApproved, LockedOut.
        // Identity (UserId, PortalId tenant scope, Username) is immutable on update and Ignored, along with derived/
        // server-managed members.
        CreateMap<UpdateUserRequest, UserEntity>()
            .ForMember(d => d.UserId, o => o.Ignore())
            .ForMember(d => d.PortalId, o => o.Ignore())
            .ForMember(d => d.Username, o => o.Ignore())
            .ForMember(d => d.FullName, o => o.Ignore())
            .ForMember(d => d.IsSuperUser, o => o.Ignore())
            .ForMember(d => d.AffiliateId, o => o.Ignore())
            .ForMember(d => d.CreatedDate, o => o.Ignore())
            .ForMember(d => d.LastLoginDate, o => o.Ignore())
            .ForMember(d => d.LastActivityDate, o => o.Ignore())
            .ForMember(d => d.LastLockoutDate, o => o.Ignore())
            .ForMember(d => d.UserRoles, o => o.Ignore());
    }
}
