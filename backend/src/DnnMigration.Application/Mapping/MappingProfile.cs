using AutoMapper;
using DnnMigration.Application.DTOs;
using DnnMigration.Domain.Entities;

namespace DnnMigration.Application.Mapping;

/// <summary>
/// Central AutoMapper profile: the single place where Domain entities are translated to/from
/// Application DTOs. Registered once in the API host via
/// <c>services.AddAutoMapper(typeof(MappingProfile).Assembly)</c>.
/// </summary>
// MIGRATION: The DTO boundary replaces the legacy DNN practice of returning the *Info entities
// (PortalInfo/ModuleInfo/UserInfo/RoleInfo/TabInfo) straight out of the Web Forms code-behind.
// Controllers now return DTOs only; EF/Domain entities never cross the API boundary.
public sealed class MappingProfile : Profile
{
    public MappingProfile()
    {
        // ------------------------------------------------------------------ Portal
        // Read projection. UserRegistration (UserRegistrationType) and BannerAdvertising
        // (BannerType) are the SAME Domain enum types on both entity and DTO, so AutoMapper
        // maps them directly.
        // MIGRATION: no int<->enum converter is required or added — legacy stored these as int
        // columns, but both the entity and the DTO now use the strongly-typed enum.
        // MIGRATION: PortalDto.Aliases (the legacy "Portal Aliases" grid column) has no counterpart on the
        // Portal entity (which carries no PortalAlias navigation collection), so it is explicitly ignored
        // here and populated by PortalService from a dedicated alias lookup.
        CreateMap<Portal, PortalDto>()
            .ForMember(d => d.Aliases, o => o.Ignore());
        // Write maps intentionally cover a SUBSET of Portal members. PortalService owns the parts
        // the mapper cannot: FirstName/LastName/Username/Password (initial administrator account)
        // and PortalAlias (initial alias) are source-only on CreatePortalDto and are handled by the
        // service, not this profile.
        // MIGRATION: CreatePortalDto exposes HomeDirectory (string?), but Portals.HomeDirectory is NOT NULL
        // (DEFAULT '') in the existing schema. Without a guard, convention mapping overwrites the entity's
        // string.Empty default with null when the client omits it, which EF Core rejects on insert (surfaced
        // as HTTP 500). NullSubstitute replaces a null source with string.Empty so the NOT NULL column is
        // never written null. Description/KeyWords are genuinely NULLable columns (now string? on the entity)
        // so they need no substitution; DefaultLanguage is not exposed on CreatePortalDto (the entity's
        // string.Empty default stands).
        CreateMap<CreatePortalDto, Portal>()
            .ForMember(d => d.HomeDirectory, o => o.NullSubstitute(string.Empty));
        // MIGRATION: Portals.DefaultLanguage (NOT NULL DEFAULT 'en-US') and Portals.HomeDirectory
        // (NOT NULL DEFAULT '') are non-nullable in the existing schema, but the corresponding
        // UpdatePortalDto fields are optional (string?). Without a guard, convention mapping overwrites
        // the entity value with null when the client omits either field, which EF Core rejects against the
        // NOT NULL columns on SaveChanges (SQL Server). NullSubstitute replaces a null source with
        // string.Empty so the NOT NULL columns are never written null - preserving the existing schema (no
        // table-structure change) and staying consistent with the genuinely-nullable optional fields, which
        // clear to null on an omitted PUT.
        CreateMap<UpdatePortalDto, Portal>()
            .ForMember(d => d.DefaultLanguage, o => o.NullSubstitute(string.Empty))
            .ForMember(d => d.HomeDirectory, o => o.NullSubstitute(string.Empty));

        // ------------------------------------------------------------------ Module
        // Visibility is int on both entity and DTO (legacy VisibilityState kept as int) -> direct.
        // Module.ControlType (SecurityAccessLevel enum) is not exposed on any Module DTO -> not mapped.
        CreateMap<Module, ModuleDto>();
        // MIGRATION: StartDate/EndDate are DateTime? on the write DTOs but DateTime on the entity;
        // AutoMapper converts a null source to default(DateTime) automatically ("no schedule").
        CreateMap<CreateModuleDto, Module>();
        CreateMap<UpdateModuleDto, Module>();

        // ------------------------------------------------------------------ Role
        CreateMap<Role, RoleDto>();
        CreateMap<CreateRoleDto, Role>();
        CreateMap<UpdateRoleDto, Role>();

        // ------------------------------------------------------------------ Tab
        // Tab.HasChildren is a settable bool present on TabDto -> direct map.
        CreateMap<Tab, TabDto>();
        CreateMap<CreateTabDto, Tab>();
        CreateMap<UpdateTabDto, Tab>();

        // ------------------------------------------------------------------ User (+ nested VOs)
        // MIGRATION: credentials are excluded STRUCTURALLY, not with .ForMember(...Ignore()).
        // The read DTOs (UserDto/MembershipDto/ProfileDto) simply do not declare
        // Password/PasswordAnswer/PasswordQuestion, so there is no destination member to ignore
        // (and an Ignore() call referencing a non-existent member would not compile). These nested
        // maps are consumed automatically by CreateMap<User, UserDto>() for the Membership/Profile
        // members (both are non-null on User via property initializers).
        CreateMap<UserMembership, MembershipDto>();
        CreateMap<UserProfile, ProfileDto>();
        CreateMap<User, UserDto>();

        // Create: Username/FirstName/LastName/DisplayName/Email/PortalID map by convention.
        // MIGRATION: Password/ConfirmPassword/PasswordQuestion/PasswordAnswer/Authorize/Notify/
        // RandomPassword are source-only and intentionally unmapped — UserService hashes the
        // password (BCrypt) into User.Membership and applies the authorize/notify flags. The mapper
        // never touches credentials.
        CreateMap<CreateUserDto, User>();

        // Update: UpdateUserDto exposes the profile fields FLAT, but the User entity nests them
        // under Profile, so map the flat DTO fields onto the nested Profile via ForPath.
        // MIGRATION: AffiliateID is nullable on the DTO; keep the entity's existing value when the
        // client omits it. Approved (a Membership state flag) is deliberately NOT mapped here —
        // approval is a state transition owned by UserService (business logic), not the mapper.
        CreateMap<UpdateUserDto, User>()
            .ForMember(d => d.AffiliateID, o => o.MapFrom((s, d) => s.AffiliateID ?? d.AffiliateID))
            // MIGRATION: UpdateUserDto.DisplayName is optional (string?), but Users.DisplayName is NOT NULL
            // (DEFAULT '') in the existing schema. Convention mapping would overwrite the loaded entity's
            // value with null when the client omits it, which EF Core rejects on update (SQL Server). Mirror
            // the AffiliateID pattern above: keep the existing DisplayName when the client omits it (null),
            // so the NOT NULL column is never written null and a partial update never blanks a set name.
            .ForMember(d => d.DisplayName, o => o.MapFrom((s, d) => s.DisplayName ?? d.DisplayName))
            .ForPath(d => d.Profile.Street, o => o.MapFrom(s => s.Street))
            .ForPath(d => d.Profile.Unit, o => o.MapFrom(s => s.Unit))
            .ForPath(d => d.Profile.City, o => o.MapFrom(s => s.City))
            .ForPath(d => d.Profile.Region, o => o.MapFrom(s => s.Region))
            .ForPath(d => d.Profile.Country, o => o.MapFrom(s => s.Country))
            .ForPath(d => d.Profile.PostalCode, o => o.MapFrom(s => s.PostalCode))
            .ForPath(d => d.Profile.Telephone, o => o.MapFrom(s => s.Telephone))
            .ForPath(d => d.Profile.Cell, o => o.MapFrom(s => s.Cell))
            .ForPath(d => d.Profile.Fax, o => o.MapFrom(s => s.Fax))
            .ForPath(d => d.Profile.Website, o => o.MapFrom(s => s.Website))
            .ForPath(d => d.Profile.IM, o => o.MapFrom(s => s.IM))
            .ForPath(d => d.Profile.PreferredLocale, o => o.MapFrom(s => s.PreferredLocale))
            .ForPath(d => d.Profile.TimeZone, o => o.MapFrom(s => s.TimeZone));

        // ------------------------------------------------------------------ Auth (/api/auth/me)
        // CurrentUserDto wraps a single User member of type UserDto; map the whole source User into
        // it (AutoMapper reuses CreateMap<User, UserDto>() for the nested conversion).
        CreateMap<User, CurrentUserDto>()
            .ForMember(d => d.User, o => o.MapFrom(s => s));
    }
}
