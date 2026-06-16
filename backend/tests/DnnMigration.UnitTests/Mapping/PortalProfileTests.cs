using AutoMapper;
using DnnMigration.Application.DTOs.Portal;
using DnnMigration.Application.Mapping;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using PortalEntity = DnnMigration.Domain.Entities.Portal;

namespace DnnMigration.UnitTests.Mapping;

/// <summary>
/// Round-trip tests for <see cref="PortalProfile"/>. Confirms the read projection
/// (Portal -> PortalDto) preserves every field and the write maps
/// (CreatePortalDto/UpdatePortalDto -> Portal) copy supplied fields while ignoring the
/// database-generated identity and the server-derived Users/Pages metrics.
/// </summary>
public class PortalProfileTests
{
    private static IMapper CreateMapper() =>
        new MapperConfiguration(cfg => cfg.AddProfile<PortalProfile>(), NullLoggerFactory.Instance).CreateMapper();

    private static PortalEntity CreateSampleEntity() => new()
    {
        PortalID = 7,
        PortalName = "Primary Portal",
        LogoFile = "logo.png",
        FooterText = "(c) Contoso",
        ExpiryDate = new DateTime(2030, 1, 1),
        UserRegistration = 1,
        BannerAdvertising = 2,
        AdministratorId = 3,
        Currency = "USD",
        HostFee = 9.99f,
        HostSpace = 100,
        PageQuota = 50,
        UserQuota = 25,
        AdministratorRoleId = 4,
        AdministratorRoleName = "Administrators",
        RegisteredRoleId = 5,
        RegisteredRoleName = "Registered Users",
        Description = "A sample portal.",
        KeyWords = "dnn, portal",
        BackgroundFile = "bg.png",
        GUID = Guid.Parse("11111111-1111-1111-1111-111111111111"),
        PaymentProcessor = "PayPal",
        ProcessorPassword = "pp",
        ProcessorUserId = "pu",
        SiteLogHistory = 30,
        Email = "admin@contoso.com",
        AdminTabId = 10,
        SuperTabId = 11,
        Users = 200,
        Pages = 40,
        SplashTabId = 12,
        HomeTabId = 13,
        LoginTabId = 14,
        UserTabId = 15,
        DefaultLanguage = "en-US",
        TimeZoneOffset = -480,
        HomeDirectory = "Portals/0",
        Version = "1.0.0"
    };

    [Fact]
    public void Configuration_IsValid()
    {
        var configuration = new MapperConfiguration(cfg => cfg.AddProfile<PortalProfile>(), NullLoggerFactory.Instance);

        configuration.Invoking(c => c.AssertConfigurationIsValid()).Should().NotThrow();
    }

    [Fact]
    public void Map_Portal_To_PortalDto_PreservesAllProjectedFields()
    {
        var mapper = CreateMapper();
        var entity = CreateSampleEntity();

        var dto = mapper.Map<PortalDto>(entity);

        dto.Should().BeEquivalentTo(entity, options => options.ExcludingMissingMembers());
    }

    [Fact]
    public void Map_CreatePortalDto_To_Portal_CopiesFields_AndIgnoresIdentityAndDerivedMetrics()
    {
        var mapper = CreateMapper();
        var dto = new CreatePortalDto
        {
            PortalName = "New Portal",
            LogoFile = "logo.png",
            FooterText = "footer",
            ExpiryDate = new DateTime(2031, 6, 15),
            UserRegistration = 1,
            BannerAdvertising = 0,
            AdministratorId = 2,
            Currency = "USD",
            HostFee = 12.5f,
            HostSpace = 500,
            PageQuota = 100,
            UserQuota = 100,
            AdministratorRoleId = 3,
            AdministratorRoleName = "Administrators",
            RegisteredRoleId = 4,
            RegisteredRoleName = "Registered Users",
            Description = "desc",
            KeyWords = "kw",
            BackgroundFile = "bg.png",
            GUID = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            PaymentProcessor = "PayPal",
            ProcessorPassword = "pp",
            ProcessorUserId = "pu",
            SiteLogHistory = 60,
            Email = "admin@new.com",
            AdminTabId = 5,
            SuperTabId = 6,
            SplashTabId = 7,
            HomeTabId = 8,
            LoginTabId = 9,
            UserTabId = 10,
            DefaultLanguage = "en-US",
            TimeZoneOffset = 0,
            HomeDirectory = "Portals/1",
            Version = "1.0.0"
        };

        var entity = mapper.Map<PortalEntity>(dto);

        entity.Should().BeEquivalentTo(dto, options => options.ExcludingMissingMembers());
        entity.PortalID.Should().Be(0, "PortalID is database-generated and ignored on create");
        entity.Users.Should().BeNull("Users is a derived metric not supplied on create");
        entity.Pages.Should().BeNull("Pages is a derived metric not supplied on create");
    }

    [Fact]
    public void Map_UpdatePortalDto_To_Portal_CopiesFields_AndIgnoresDerivedMetrics()
    {
        var mapper = CreateMapper();
        var dto = new UpdatePortalDto
        {
            PortalID = 42,
            PortalName = "Updated Portal",
            LogoFile = "logo2.png",
            FooterText = "footer2",
            ExpiryDate = new DateTime(2032, 3, 10),
            UserRegistration = 2,
            BannerAdvertising = 1,
            AdministratorId = 9,
            Currency = "EUR",
            HostFee = 7.25f,
            HostSpace = 250,
            PageQuota = 75,
            UserQuota = 60,
            AdministratorRoleId = 11,
            AdministratorRoleName = "Admins",
            RegisteredRoleId = 12,
            RegisteredRoleName = "Members",
            Description = "updated",
            KeyWords = "k2",
            BackgroundFile = "bg2.png",
            GUID = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            PaymentProcessor = "Stripe",
            ProcessorPassword = "pp2",
            ProcessorUserId = "pu2",
            SiteLogHistory = 90,
            Email = "admin@upd.com",
            AdminTabId = 20,
            SuperTabId = 21,
            SplashTabId = 22,
            HomeTabId = 23,
            LoginTabId = 24,
            UserTabId = 25,
            DefaultLanguage = "fr-FR",
            TimeZoneOffset = 60,
            HomeDirectory = "Portals/42",
            Version = "2.0.0"
        };

        var entity = mapper.Map<PortalEntity>(dto);

        entity.Should().BeEquivalentTo(dto, options => options.ExcludingMissingMembers());
        entity.PortalID.Should().Be(42, "UpdatePortalDto carries the identity and the profile maps it");
        entity.Users.Should().BeNull("Users is a derived metric ignored on update");
        entity.Pages.Should().BeNull("Pages is a derived metric ignored on update");
    }
}
