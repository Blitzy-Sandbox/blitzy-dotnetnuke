using AutoMapper;
using DnnMigration.Domain.Entities;
using DnnMigration.Application.DTOs.User;

namespace DnnMigration.Application.Mapping;

/// <summary>
/// AutoMapper profile mapping the <see cref="User"/> entity to/from its DTOs.
/// MIGRATION: replaces legacy CBO reflection hydration with explicit, compile-checked maps.
/// </summary>
public sealed class UserProfile : Profile
{
    public UserProfile()
    {
        // Read projection: User -> UserDto.
        // MIGRATION: UserDto.FullName is a read-only computed member ($"{FirstName} {LastName}", per UserInfo.vb
        // L375-386); AutoMapper auto-skips it (no setter) and it self-computes from the mapped FirstName/LastName.
        // MIGRATION: Roles (string[]?) maps by name but is hydrated separately by the service from the UserRole join
        // (legacy RoleController.GetRolesByUser, UserInfo.vb L261-274); it may be null until resolved.
        // Password/PasswordQuestion/PasswordAnswer are intentionally ABSENT from UserDto (security).
        CreateMap<User, UserDto>();

        // Inbound create: CreateUserDto -> User.
        // MIGRATION/SECURITY: Password is DELIBERATELY ignored. The plaintext CreateUserDto.Password MUST be hashed
        // by the service via IPasswordHasher (BCrypt.Net-Next) before persistence — never copied raw into the entity.
        // MIGRATION: Roles is assigned via the UserRole join by the service, not mapped from the DTO.
        CreateMap<CreateUserDto, User>()
            .ForMember(d => d.UserID, o => o.Ignore())
            .ForMember(d => d.AffiliateID, o => o.Ignore())
            .ForMember(d => d.Roles, o => o.Ignore())
            .ForMember(d => d.CreatedDate, o => o.Ignore())
            .ForMember(d => d.IsOnLine, o => o.Ignore())
            .ForMember(d => d.LastActivityDate, o => o.Ignore())
            .ForMember(d => d.LastLockoutDate, o => o.Ignore())
            .ForMember(d => d.LastLoginDate, o => o.Ignore())
            .ForMember(d => d.LastPasswordChangeDate, o => o.Ignore())
            .ForMember(d => d.LockedOut, o => o.Ignore())
            .ForMember(d => d.Password, o => o.Ignore())
            .ForMember(d => d.PasswordAnswer, o => o.Ignore())
            .ForMember(d => d.PasswordQuestion, o => o.Ignore())
            .ForMember(d => d.UpdatePassword, o => o.Ignore());

        // Inbound update: UpdateUserDto -> User (carries UserID).
        // MIGRATION: Username is immutable on update (UserInfo.vb L301 IsReadOnly(True)); PortalID is immutable;
        // password fields are managed via a dedicated change-password flow, not this map.
        CreateMap<UpdateUserDto, User>()
            .ForMember(d => d.PortalID, o => o.Ignore())
            .ForMember(d => d.AffiliateID, o => o.Ignore())
            .ForMember(d => d.Username, o => o.Ignore())
            .ForMember(d => d.Roles, o => o.Ignore())
            .ForMember(d => d.CreatedDate, o => o.Ignore())
            .ForMember(d => d.IsOnLine, o => o.Ignore())
            .ForMember(d => d.LastActivityDate, o => o.Ignore())
            .ForMember(d => d.LastLockoutDate, o => o.Ignore())
            .ForMember(d => d.LastLoginDate, o => o.Ignore())
            .ForMember(d => d.LastPasswordChangeDate, o => o.Ignore())
            .ForMember(d => d.LockedOut, o => o.Ignore())
            .ForMember(d => d.Password, o => o.Ignore())
            .ForMember(d => d.PasswordAnswer, o => o.Ignore())
            .ForMember(d => d.PasswordQuestion, o => o.Ignore())
            .ForMember(d => d.UpdatePassword, o => o.Ignore());
    }
}
