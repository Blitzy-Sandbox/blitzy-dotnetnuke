using AutoMapper;
using DnnMigration.Application.DTOs.Auth;             // CurrentUserDto
using UserEntity = DnnMigration.Domain.Entities.User;

namespace DnnMigration.Application.Mapping;

// MIGRATION: AutoMapper profile for the Auth contract. Provides the single non-trivial entity->DTO projection
// the authentication flow needs: User -> CurrentUserDto, which backs GET /api/auth/me (AAP §0.3.4) and is the
// modern replacement for the legacy UserController.GetCurrentUserInfo() (Library/Components/Users/
// UserController.vb L381) that returned a UserInfo (Library/Components/Users/UserInfo.vb). The remaining Auth
// DTOs (LoginRequest, LoginResponse, RefreshRequest) are NOT entity projections — they are model-bound or
// constructed in AuthService — so they have no map here.
//
// Namespace-collision note: the Auth DTO namespace (DnnMigration.Application.DTOs.Auth) is imported for
// CurrentUserDto, and the Domain entity DnnMigration.Domain.Entities.User is imported under the alias
// "UserEntity" — matching the other profiles — to avoid any bare-"User" ambiguity (CS0118). The bare type
// name "User" is never referenced in this file. UserRole/Role are reached only through the UserRoles
// navigation inside the Roles lambda and are inferred, so they need no using or alias.
//
// This type contains MAPPING CONFIGURATION ONLY — no business logic, validation, data access, or token
// issuance (AAP §0.7.1/§0.7.3/§0.7.6). The host registers it via AddAutoMapper(typeof(AuthProfile).Assembly).
public class AuthProfile : Profile
{
    public AuthProfile()
    {
        // MIGRATION: User -> CurrentUserDto. Projects the authenticated user for GET /api/auth/me, replacing the
        // legacy UserController.GetCurrentUserInfo() (Library/Components/Users/UserController.vb L381) that returned
        // a UserInfo. Nine members map 1:1 by name (UserId, Username, Email, DisplayName, FirstName, LastName,
        // FullName, IsSuperUser, PortalId). Roles is flattened from the UserRoles navigation to role-name strings
        // (same logic as UserResponse.Roles); legacy UserInfo.Roles was a String() hydrated via
        // RoleController.GetRolesByUser. NO credential members are mapped (AAP §0.7.6).
        CreateMap<UserEntity, CurrentUserDto>()
            .ForMember(d => d.Roles,
                o => o.MapFrom(src => src.UserRoles
                    .Where(ur => ur.Role != null)
                    .Select(ur => ur.Role!.RoleName)
                    .ToList()));
    }
}
