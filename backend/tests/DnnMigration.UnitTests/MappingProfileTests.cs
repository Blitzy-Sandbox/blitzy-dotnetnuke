// -----------------------------------------------------------------------------
//  MappingProfileTests.cs
//
//  Unit tests for the central AutoMapper profile
//  DnnMigration.Application.Mapping.MappingProfile.
//
//  PURPOSE / PARITY
//  ----------------
//  MIGRATION: The DTO boundary replaces the legacy DotNetNuke 4.x practice of
//  returning the *Info entities (PortalInfo/ModuleInfo/UserInfo/RoleInfo/TabInfo,
//  e.g. Library/Components/Portal/PortalInfo.vb, Library/Components/Modules/ModuleInfo.vb,
//  Library/Components/Users/UserInfo.vb) straight out of the Web Forms code-behind.
//  MappingProfile is the single place that projects the migrated Domain entities to
//  the outbound Application DTOs, so these tests lock the READ (entity -> DTO)
//  projections that the API contract depends on.
//
//  CRITICAL GUARDRAIL
//  ------------------
//  The profile also declares WRITE maps (Create*/Update* DTO -> entity) that are
//  INTENTIONALLY PARTIAL: the owning services (PortalService/UserService/...) fill the
//  members the mapper cannot (administrator credentials, initial alias, BCrypt password
//  hash, approval state, ...). Consequently a blanket
//  MapperConfiguration.AssertConfigurationIsValid() over the whole profile can report
//  unmapped destination members on the entity side and throw. This test file therefore
//  DELIBERATELY DOES NOT call AssertConfigurationIsValid(). Instead each READ map is
//  validated explicitly by projecting a populated entity and asserting field equality;
//  a missing or broken READ map would surface as an AutoMapperMappingException at
//  Map<T>() time, failing the corresponding test.
//
//  The eight READ maps under test (all declared in MappingProfile):
//      Portal          -> PortalDto
//      Module          -> ModuleDto
//      Role            -> RoleDto
//      Tab             -> TabDto
//      UserMembership  -> MembershipDto
//      UserProfile     -> ProfileDto
//      User            -> UserDto
//      User            -> CurrentUserDto   (whole user wrapped under .User)
//
//  Members intentionally EXCLUDED from the read DTOs are never asserted here because
//  they do not exist on the destination type (and would not compile):
//      PortalDto     omits PaymentProcessor / ProcessorUserId / ProcessorPassword
//      ModuleDto     omits ControlType (SecurityAccessLevel)
//      MembershipDto omits Password / PasswordAnswer / PasswordQuestion
//
//  These are pure, fast, isolated unit tests: the SUT holds no state and performs no
//  I/O, so the mapper is built once in the constructor. Assertions use FluentAssertions.
// -----------------------------------------------------------------------------

using AutoMapper;
using DnnMigration.Application.DTOs;
using DnnMigration.Application.Mapping;
using DnnMigration.Domain.Entities;
using DnnMigration.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace DnnMigration.UnitTests;

/// <summary>
/// Verifies that the AutoMapper <see cref="MappingProfile"/> produces correct
/// entity-to-DTO (READ) projections and that every READ map is internally consistent.
/// </summary>
/// <remarks>
/// The mapper is constructed once from <see cref="MappingProfile"/> via a
/// <see cref="MapperConfiguration"/>. Per the guardrail documented at the top of this
/// file, no blanket <c>AssertConfigurationIsValid()</c> is invoked; instead the READ
/// maps are exercised directly with populated source entities and their scalar, enum,
/// and nested members are asserted equal to the source.
/// </remarks>
public class MappingProfileTests
{
    private readonly IMapper _mapper;

    /// <summary>
    /// Builds the AutoMapper <see cref="IMapper"/> once for the fixture. Registering the
    /// profile and calling <see cref="MapperConfiguration.CreateMapper()"/> does NOT run
    /// configuration validation, so the intentionally-partial WRITE maps do not cause the
    /// mapper to fail to build.
    /// </summary>
    public MappingProfileTests()
    {
        var config = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>());
        _mapper = config.CreateMapper();
    }

    /// <summary>
    /// Sanity check: the mapper was built from <see cref="MappingProfile"/> without
    /// throwing (the fixture constructor would have failed otherwise).
    /// </summary>
    [Fact]
    public void Mapper_Is_Built_From_MappingProfile_Without_Throwing()
    {
        _mapper.Should().NotBeNull();
    }

    // ------------------------------------------------------------------ Portal
    /// <summary>
    /// Portal -> PortalDto: scalar and enum members map by name. UserRegistration
    /// (UserRegistrationType) and BannerAdvertising (BannerType) are the SAME Domain enum
    /// types on both sides, so they map straight through with no int&lt;-&gt;enum converter.
    /// </summary>
    [Fact]
    public void Map_Portal_To_PortalDto_MapsScalarsAndEnumsByName()
    {
        var portal = new Portal
        {
            PortalID = 7,
            PortalName = "Contoso Portal",
            HostFee = 49.95f,
            HostSpace = 2048,
            UserRegistration = UserRegistrationType.PublicRegistration,
            BannerAdvertising = BannerType.Banner,
            GUID = Guid.NewGuid(),
            Email = "admin@contoso.example",
            HomeDirectory = "Portals/7"
        };

        var dto = _mapper.Map<PortalDto>(portal);

        dto.Should().NotBeNull();
        dto.PortalID.Should().Be(portal.PortalID);
        dto.PortalName.Should().Be(portal.PortalName);
        dto.UserRegistration.Should().Be(portal.UserRegistration);
        dto.BannerAdvertising.Should().Be(portal.BannerAdvertising);
        dto.HostFee.Should().Be(portal.HostFee);
        dto.HostSpace.Should().Be(portal.HostSpace);
        dto.GUID.Should().Be(portal.GUID);
        dto.Email.Should().Be(portal.Email);
        dto.HomeDirectory.Should().Be(portal.HomeDirectory);
    }

    // ------------------------------------------------------------------ Module
    /// <summary>
    /// Module -> ModuleDto: representative scalars map by name and Visibility maps int-&gt;int
    /// (the legacy VisibilityState enum is preserved as an int on both entity and DTO).
    /// ControlType (a SecurityAccessLevel enum) is set on the source to prove the mapper
    /// simply ignores it — it is not a member of ModuleDto and is not asserted.
    /// </summary>
    [Fact]
    public void Map_Module_To_ModuleDto_MapsScalarsAndIntVisibility()
    {
        var module = new Module
        {
            ModuleID = 33,
            ModuleTitle = "Announcements",
            Visibility = 1,
            PortalID = 7,
            TabID = 12,
            CacheTime = 3600,
            StartDate = new DateTime(2020, 1, 15, 8, 30, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2020, 12, 31, 23, 59, 0, DateTimeKind.Utc),
            ControlType = SecurityAccessLevel.Edit
        };

        var dto = _mapper.Map<ModuleDto>(module);

        dto.Should().NotBeNull();
        dto.ModuleID.Should().Be(module.ModuleID);
        dto.ModuleTitle.Should().Be(module.ModuleTitle);
        dto.Visibility.Should().Be(module.Visibility);
        dto.PortalID.Should().Be(module.PortalID);
        dto.TabID.Should().Be(module.TabID);
        dto.CacheTime.Should().Be(module.CacheTime);
        dto.StartDate.Should().Be(module.StartDate);
        dto.EndDate.Should().Be(module.EndDate);
    }

    // -------------------------------------------------------------------- Role
    /// <summary>
    /// Role -> RoleDto: scalar members (including the legacy VB Single -&gt; float ServiceFee)
    /// map by name.
    /// </summary>
    [Fact]
    public void Map_Role_To_RoleDto_MapsFieldsByName()
    {
        var role = new Role
        {
            RoleID = 5,
            PortalID = 7,
            RoleName = "Administrators",
            ServiceFee = 12.5f,
            IsPublic = true,
            AutoAssignment = false
        };

        var dto = _mapper.Map<RoleDto>(role);

        dto.Should().NotBeNull();
        dto.RoleID.Should().Be(role.RoleID);
        dto.PortalID.Should().Be(role.PortalID);
        dto.RoleName.Should().Be(role.RoleName);
        dto.ServiceFee.Should().Be(role.ServiceFee);
        dto.IsPublic.Should().Be(role.IsPublic);
        dto.AutoAssignment.Should().Be(role.AutoAssignment);
    }

    // --------------------------------------------------------------------- Tab
    /// <summary>
    /// Tab -> TabDto: scalar members map by name, including the settable HasChildren flag.
    /// </summary>
    [Fact]
    public void Map_Tab_To_TabDto_MapsFieldsByName()
    {
        var tab = new Tab
        {
            TabID = 12,
            TabName = "Home",
            PortalID = 7,
            ParentId = 0,
            IsVisible = true,
            HasChildren = true
        };

        var dto = _mapper.Map<TabDto>(tab);

        dto.Should().NotBeNull();
        dto.TabID.Should().Be(tab.TabID);
        dto.TabName.Should().Be(tab.TabName);
        dto.PortalID.Should().Be(tab.PortalID);
        dto.ParentId.Should().Be(tab.ParentId);
        dto.IsVisible.Should().Be(tab.IsVisible);
        dto.HasChildren.Should().Be(tab.HasChildren);
    }

    // ---------------------------------------------------------- UserMembership
    /// <summary>
    /// UserMembership -> MembershipDto: account-state members map by name. The DTO omits the
    /// credential fields (Password/PasswordAnswer/PasswordQuestion), so they are never
    /// asserted here — they are structurally absent from the destination type.
    /// </summary>
    [Fact]
    public void Map_UserMembership_To_MembershipDto_MapsStateFields()
    {
        var membership = new UserMembership
        {
            Approved = true,
            LockedOut = false,
            IsOnLine = true,
            UpdatePassword = true,
            CreatedDate = new DateTime(2019, 6, 1, 10, 0, 0, DateTimeKind.Utc),
            LastLoginDate = new DateTime(2021, 3, 22, 14, 45, 0, DateTimeKind.Utc)
        };

        var dto = _mapper.Map<MembershipDto>(membership);

        dto.Should().NotBeNull();
        dto.Approved.Should().Be(membership.Approved);
        dto.LockedOut.Should().Be(membership.LockedOut);
        dto.IsOnLine.Should().Be(membership.IsOnLine);
        dto.UpdatePassword.Should().Be(membership.UpdatePassword);
        dto.CreatedDate.Should().Be(membership.CreatedDate);
        dto.LastLoginDate.Should().Be(membership.LastLoginDate);
    }

    // ------------------------------------------------------------- UserProfile
    /// <summary>
    /// UserProfile -> ProfileDto: address/contact/locale members map by name, including the
    /// int TimeZone offset.
    /// </summary>
    [Fact]
    public void Map_UserProfile_To_ProfileDto_MapsAddressAndLocaleFields()
    {
        var profile = new UserProfile
        {
            Street = "1 Microsoft Way",
            City = "Redmond",
            TimeZone = -480,
            PreferredLocale = "en-US"
        };

        var dto = _mapper.Map<ProfileDto>(profile);

        dto.Should().NotBeNull();
        dto.Street.Should().Be(profile.Street);
        dto.City.Should().Be(profile.City);
        dto.TimeZone.Should().Be(profile.TimeZone);
        dto.PreferredLocale.Should().Be(profile.PreferredLocale);
    }

    // -------------------------------------------------------------------- User
    /// <summary>
    /// User -> UserDto: top-level scalars, the Roles array, and the nested Membership/Profile
    /// projections all map. The nested objects are converted by the reused
    /// UserMembership-&gt;MembershipDto and UserProfile-&gt;ProfileDto maps.
    /// </summary>
    [Fact]
    public void Map_User_To_UserDto_MapsScalarsRolesAndNestedObjects()
    {
        var user = BuildPopulatedUser();

        var dto = _mapper.Map<UserDto>(user);

        dto.Should().NotBeNull();
        dto.UserID.Should().Be(user.UserID);
        dto.PortalID.Should().Be(user.PortalID);
        dto.Username.Should().Be(user.Username);
        dto.DisplayName.Should().Be(user.DisplayName);
        dto.FirstName.Should().Be(user.FirstName);
        dto.LastName.Should().Be(user.LastName);
        dto.Email.Should().Be(user.Email);
        dto.IsSuperUser.Should().Be(user.IsSuperUser);
        dto.AffiliateID.Should().Be(user.AffiliateID);
        dto.Roles.Should().BeEquivalentTo(user.Roles);

        dto.Membership.Should().NotBeNull();
        dto.Membership.Approved.Should().Be(user.Membership.Approved);
        dto.Membership.IsOnLine.Should().Be(user.Membership.IsOnLine);
        dto.Membership.LastLoginDate.Should().Be(user.Membership.LastLoginDate);

        dto.Profile.Should().NotBeNull();
        dto.Profile.Street.Should().Be(user.Profile.Street);
        dto.Profile.City.Should().Be(user.Profile.City);
        dto.Profile.PreferredLocale.Should().Be(user.Profile.PreferredLocale);
    }

    // ------------------------------------------------------------------- Auth
    /// <summary>
    /// User -> CurrentUserDto (/api/auth/me): the whole source user is projected into the
    /// single <c>User</c> member via <c>.ForMember(d =&gt; d.User, o =&gt; o.MapFrom(s =&gt; s))</c>,
    /// reusing the User-&gt;UserDto map for the nested conversion.
    /// </summary>
    [Fact]
    public void Map_User_To_CurrentUserDto_WrapsUserUnderUserProperty()
    {
        var user = BuildPopulatedUser();

        var currentUser = _mapper.Map<CurrentUserDto>(user);

        currentUser.Should().NotBeNull();
        currentUser.User.Should().NotBeNull();
        currentUser.User.UserID.Should().Be(user.UserID);
        currentUser.User.Username.Should().Be(user.Username);
        currentUser.User.Email.Should().Be(user.Email);
        currentUser.User.Roles.Should().BeEquivalentTo(user.Roles);
    }

    /// <summary>
    /// Builds a fully populated <see cref="User"/> aggregate (identity + nested Membership and
    /// Profile) shared by the User-&gt;UserDto and User-&gt;CurrentUserDto tests.
    /// </summary>
    private static User BuildPopulatedUser()
    {
        return new User
        {
            UserID = 101,
            PortalID = 7,
            Username = "jdoe",
            DisplayName = "Jane Doe",
            FirstName = "Jane",
            LastName = "Doe",
            Email = "jane.doe@contoso.example",
            IsSuperUser = false,
            AffiliateID = 3,
            Roles = new[] { "Administrators", "Registered Users" },
            Membership = new UserMembership
            {
                Approved = true,
                LockedOut = false,
                IsOnLine = true,
                UpdatePassword = false,
                CreatedDate = new DateTime(2018, 2, 2, 9, 0, 0, DateTimeKind.Utc),
                LastLoginDate = new DateTime(2022, 7, 7, 7, 7, 0, DateTimeKind.Utc)
            },
            Profile = new UserProfile
            {
                Street = "1 Microsoft Way",
                City = "Redmond",
                TimeZone = -480,
                PreferredLocale = "en-US"
            }
        };
    }
}
