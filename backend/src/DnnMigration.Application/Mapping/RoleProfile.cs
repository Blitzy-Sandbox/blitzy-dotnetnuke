using AutoMapper;
using DnnMigration.Application.DTOs.Role;          // RoleResponse, CreateRoleRequest, UpdateRoleRequest, UserRoleDto
using RoleEntity = DnnMigration.Domain.Entities.Role;
using UserRoleEntity = DnnMigration.Domain.Entities.UserRole;

namespace DnnMigration.Application.Mapping;

// MIGRATION: AutoMapper profile for the Role domain. Replaces the legacy direct XML/serialization of
// DotNetNuke.Security.Roles.RoleInfo (Library/Components/Security/Roles/RoleInfo.vb, decorated with
// <XmlRoot("role")>/<XmlElement>) and DotNetNuke.Entities.Users.UserRoleInfo
// (Library/Components/Users/UserRoleInfo.vb); there was no single legacy mapping class. Centralizing
// object-to-object mapping here lets the Api/services project the Domain entity to DTOs and never return
// raw entities (AAP §0.7.7).
//
// Namespace-collision note: the DTO namespace leaf (DnnMigration.Application.DTOs.Role) collides with the
// entity type name DnnMigration.Domain.Entities.Role, so the entities are imported under the aliases
// "RoleEntity" and "UserRoleEntity". The bare type names "Role"/"UserRole" are never referenced in this
// file (avoids CS0118 'is a namespace but is used like a type' / ambiguity). The Domain.Entities namespace
// is deliberately NOT imported.
//
// This type contains MAPPING CONFIGURATION ONLY — no business logic, validation, data access, or service
// calls (AAP §0.7.1/§0.7.3). The host registers it via AddAutoMapper(typeof(RoleProfile).Assembly).
public class RoleProfile : Profile
{
    public RoleProfile()
    {
        // MIGRATION: Role (Library/Components/Security/Roles/RoleInfo.vb) -> RoleResponse. Faithful 1:1 by name
        // across all 15 members (RoleId, PortalId, RoleGroupId, RoleName, Description, ServiceFee,
        // BillingFrequency, TrialPeriod, TrialFrequency, BillingPeriod, TrialFee, IsPublic, AutoAssignment,
        // RSVPCode, IconFile). The verbatim legacy name "RSVPCode" is preserved on both sides. No .ForMember
        // is required — every RoleResponse member matches a Role entity member by name.
        CreateMap<RoleEntity, RoleResponse>();

        // MIGRATION: UserRole join -> UserRoleDto. UserRoleId, UserId, RoleId, EffectiveDate, ExpiryDate,
        // IsTrialUsed, Subscribed map 1:1 by name. RoleName/Username/DisplayName are flattened from the
        // Role/User navigation properties: the legacy UserRoleInfo "Inherits RoleInfo" and carried
        // denormalized FullName/Email, both of which were dropped from the clean UserRole join entity. They
        // are replaced by Username + DisplayName from the User navigation and RoleName from the Role
        // navigation. The explicit "!= null" guards (rather than the null-conditional ?. operator) are used
        // because AutoMapper compiles these selectors into expression trees, and they also keep the C#
        // nullable flow analysis happy under --warnaserror. UserRoleDto's RoleName/Username/DisplayName are
        // string?, so the conditional null results are valid. All 10 UserRoleDto members are covered.
        CreateMap<UserRoleEntity, UserRoleDto>()
            .ForMember(d => d.RoleName, o => o.MapFrom(src => src.Role != null ? src.Role.RoleName : null))
            .ForMember(d => d.Username, o => o.MapFrom(src => src.User != null ? src.User.Username : null))
            .ForMember(d => d.DisplayName, o => o.MapFrom(src => src.User != null ? src.User.DisplayName : null));

        // MIGRATION: CreateRoleRequest -> Role. Maps all 14 client-supplied creation fields; RoleId is the
        // database-generated identity and is therefore Ignored (never set from the request). The Role entity
        // has no navigation collections, so RoleId is the only destination member absent from the request.
        CreateMap<CreateRoleRequest, RoleEntity>()
            .ForMember(d => d.RoleId, o => o.Ignore());

        // MIGRATION: UpdateRoleRequest -> Role. Maps the 13 editable fields; RoleId is route-bound (PUT
        // /api/roles/{id}) and PortalId is the immutable multi-tenant scope (AAP §0.7.1) — neither is part of
        // the update payload, so both are Ignored. These are the only two destination members absent from the
        // request (the Role entity has no navigation collections).
        CreateMap<UpdateRoleRequest, RoleEntity>()
            .ForMember(d => d.RoleId, o => o.Ignore())
            .ForMember(d => d.PortalId, o => o.Ignore());
    }
}
