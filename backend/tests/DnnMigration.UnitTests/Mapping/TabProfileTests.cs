using AutoMapper;
using DnnMigration.Application.DTOs.Tab;
using DnnMigration.Application.Mapping;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using TabEntity = DnnMigration.Domain.Entities.Tab;
using TabType = DnnMigration.Domain.Entities.TabType;

namespace DnnMigration.UnitTests.Mapping;

/// <summary>
/// Round-trip tests for <see cref="TabProfile"/>. Confirms Tab -> TabDto projects the
/// computed read-only TabType, and the write maps (CreateTabDto/UpdateTabDto -> Tab)
/// copy supplied fields while ignoring server-managed members (TabID, Level, IsDeleted,
/// HasChildren, TabPath, permissions, etc.).
/// </summary>
public class TabProfileTests
{
    private static IMapper CreateMapper() =>
        new MapperConfiguration(cfg => cfg.AddProfile<TabProfile>(), NullLoggerFactory.Instance).CreateMapper();

    [Fact]
    public void Configuration_IsValid()
    {
        var configuration = new MapperConfiguration(cfg => cfg.AddProfile<TabProfile>(), NullLoggerFactory.Instance);

        configuration.Invoking(c => c.AssertConfigurationIsValid()).Should().NotThrow();
    }

    [Fact]
    public void Map_Tab_To_TabDto_PreservesProjectedFields_IncludingComputedTabType()
    {
        var mapper = CreateMapper();
        var entity = new TabEntity
        {
            TabID = 100,
            TabOrder = 1,
            PortalID = 0,
            TabName = "Home",
            IsVisible = true,
            ParentId = 5,
            Level = 0,
            IconFile = "home.png",
            Title = "Home Page",
            Description = "Landing page",
            KeyWords = "home, landing",
            Url = "https://example.com/page",
            SkinSrc = "skin.ascx",
            ContainerSrc = "container.ascx",
            TabPath = "//Home",
            StartDate = new DateTime(2024, 1, 1),
            EndDate = new DateTime(2025, 1, 1),
            HasChildren = true,
            RefreshInterval = 60,
            IsSecure = true
        };

        var dto = mapper.Map<TabDto>(entity);

        dto.Should().BeEquivalentTo(entity, options => options.ExcludingMissingMembers());
        dto.TabType.Should().Be(TabType.Url, "a fully-qualified Url maps the computed TabType to Url");
    }

    [Fact]
    public void Map_CreateTabDto_To_Tab_CopiesFields_AndIgnoresServerManagedMembers()
    {
        var mapper = CreateMapper();
        var dto = new CreateTabDto
        {
            PortalID = 0,
            TabName = "About",
            ParentId = 1,
            Title = "About Us",
            Description = "About page",
            KeyWords = "about",
            IsVisible = true,
            IconFile = "about.png",
            Url = "https://example.com/about",
            SkinSrc = "skin.ascx",
            ContainerSrc = "container.ascx",
            StartDate = new DateTime(2024, 2, 1),
            EndDate = new DateTime(2025, 2, 1),
            RefreshInterval = 120,
            IsSecure = false,
            TabOrder = 2
        };

        var entity = mapper.Map<TabEntity>(dto);

        entity.Should().BeEquivalentTo(dto, options => options.ExcludingMissingMembers());
        entity.TabID.Should().Be(0, "TabID is database-generated and ignored on create");
        entity.Level.Should().Be(0, "Level is server-computed from hierarchy and ignored on create");
        entity.IsDeleted.Should().BeFalse("IsDeleted is a soft-delete flag not supplied on create");
        entity.HasChildren.Should().BeFalse("HasChildren is server-computed and ignored on create");
    }

    [Fact]
    public void Map_UpdateTabDto_To_Tab_CopiesFields_AndIgnoresServerManagedMembers()
    {
        var mapper = CreateMapper();
        var dto = new UpdateTabDto
        {
            TabID = 200,
            PortalID = 0,
            TabName = "Contact",
            ParentId = 2,
            Title = "Contact Us",
            Description = "Contact page",
            KeyWords = "contact",
            IsVisible = false,
            IconFile = "contact.png",
            Url = "https://example.com/contact",
            SkinSrc = "skin2.ascx",
            ContainerSrc = "container2.ascx",
            StartDate = new DateTime(2024, 3, 1),
            EndDate = new DateTime(2025, 3, 1),
            RefreshInterval = 30,
            IsSecure = true,
            TabOrder = 3
        };

        var entity = mapper.Map<TabEntity>(dto);

        entity.Should().BeEquivalentTo(dto, options => options.ExcludingMissingMembers());
        entity.TabID.Should().Be(200, "UpdateTabDto carries the identity and the profile maps it");
        entity.Level.Should().Be(0, "Level is server-computed and ignored on update");
        entity.IsDeleted.Should().BeFalse("IsDeleted is preserved server-side and ignored on update");
        entity.HasChildren.Should().BeFalse("HasChildren is server-computed and ignored on update");
    }
}
