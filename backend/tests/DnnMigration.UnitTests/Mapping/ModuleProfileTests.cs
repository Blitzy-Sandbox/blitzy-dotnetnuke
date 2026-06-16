using AutoMapper;
using DnnMigration.Application.DTOs.Module;
using DnnMigration.Application.Mapping;
using DnnMigration.Domain.Enums;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using ModuleEntity = DnnMigration.Domain.Entities.Module;

namespace DnnMigration.UnitTests.Mapping;

/// <summary>
/// Round-trip tests for <see cref="ModuleProfile"/>. Confirms Module -> ModuleDto surfaces
/// the VisibilityState enum and the IsDeleted soft-delete flag, while the write maps
/// (CreateModuleDto/UpdateModuleDto -> Module) copy supplied fields and ignore generated
/// identity (ModuleID, TabModuleID), the soft-delete flag and immutable foreign keys.
/// </summary>
public class ModuleProfileTests
{
    private static IMapper CreateMapper() =>
        new MapperConfiguration(cfg => cfg.AddProfile<ModuleProfile>(), NullLoggerFactory.Instance).CreateMapper();

    [Fact]
    public void Configuration_IsValid()
    {
        var configuration = new MapperConfiguration(cfg => cfg.AddProfile<ModuleProfile>(), NullLoggerFactory.Instance);

        configuration.Invoking(c => c.AssertConfigurationIsValid()).Should().NotThrow();
    }

    [Fact]
    public void Map_Module_To_ModuleDto_PreservesAllProjectedFields()
    {
        var mapper = CreateMapper();
        var entity = new ModuleEntity
        {
            ModuleID = 50,
            PortalID = 0,
            TabID = 12,
            TabModuleID = 7,
            ModuleDefID = 3,
            ModuleOrder = 1,
            PaneName = "ContentPane",
            ModuleTitle = "Announcements",
            CacheTime = 0,
            Alignment = "left",
            Color = "#fff",
            Border = "0",
            IconFile = "ann.png",
            AllTabs = false,
            Visibility = VisibilityState.Minimized,
            DisplayTitle = true,
            DisplayPrint = true,
            DisplaySyndicate = false,
            Header = "hdr",
            Footer = "ftr",
            StartDate = new DateTime(2024, 1, 1),
            EndDate = new DateTime(2025, 1, 1),
            ContainerSrc = "container.ascx",
            InheritViewPermissions = true,
            DesktopModuleID = 9,
            FriendlyName = "Announcements",
            Description = "Announcement module",
            Version = "1.0.0",
            IsDeleted = false
        };

        var dto = mapper.Map<ModuleDto>(entity);

        dto.Should().BeEquivalentTo(entity, options => options.ExcludingMissingMembers());
        dto.Visibility.Should().Be(VisibilityState.Minimized);
        dto.IsDeleted.Should().BeFalse("the read DTO surfaces the soft-delete flag");
    }

    [Fact]
    public void Map_CreateModuleDto_To_Module_CopiesFields_AndIgnoresServerManagedMembers()
    {
        var mapper = CreateMapper();
        var dto = new CreateModuleDto
        {
            PortalID = 0,
            TabID = 12,
            ModuleDefID = 3,
            DesktopModuleID = 9,
            ModuleOrder = 2,
            PaneName = "ContentPane",
            ModuleTitle = "New Module",
            CacheTime = 0,
            Alignment = "right",
            Color = "#000",
            Border = "1",
            IconFile = "mod.png",
            AllTabs = true,
            Visibility = VisibilityState.None,
            DisplayTitle = false,
            DisplayPrint = false,
            DisplaySyndicate = true,
            Header = "h",
            Footer = "f",
            StartDate = new DateTime(2024, 4, 1),
            EndDate = new DateTime(2025, 4, 1),
            ContainerSrc = "container.ascx",
            InheritViewPermissions = false
        };

        var entity = mapper.Map<ModuleEntity>(dto);

        entity.Should().BeEquivalentTo(dto, options => options.ExcludingMissingMembers());
        entity.ModuleID.Should().Be(0, "ModuleID is database-generated and ignored on create");
        entity.TabModuleID.Should().Be(0, "TabModuleID is database-generated and ignored on create");
        entity.IsDeleted.Should().BeFalse("IsDeleted is a soft-delete flag not supplied on create");
    }

    [Fact]
    public void Map_UpdateModuleDto_To_Module_CopiesFields_AndIgnoresImmutableAndServerManagedMembers()
    {
        var mapper = CreateMapper();
        var dto = new UpdateModuleDto
        {
            ModuleID = 50,
            ModuleOrder = 3,
            PaneName = "RightPane",
            ModuleTitle = "Updated Module",
            CacheTime = 60,
            Alignment = "center",
            Color = "#abc",
            Border = "2",
            IconFile = "upd.png",
            AllTabs = false,
            Visibility = VisibilityState.Minimized,
            DisplayTitle = true,
            DisplayPrint = false,
            DisplaySyndicate = true,
            Header = "h2",
            Footer = "f2",
            StartDate = new DateTime(2024, 5, 1),
            EndDate = new DateTime(2025, 5, 1),
            ContainerSrc = "container2.ascx",
            InheritViewPermissions = true
        };

        var entity = mapper.Map<ModuleEntity>(dto);

        entity.Should().BeEquivalentTo(dto, options => options.ExcludingMissingMembers());
        entity.ModuleID.Should().Be(50, "UpdateModuleDto carries the identity and the profile maps it");
        entity.PortalID.Should().Be(0, "PortalID is an immutable foreign key ignored on update");
        entity.TabID.Should().Be(0, "TabID is an immutable foreign key ignored on update");
        entity.IsDeleted.Should().BeFalse("IsDeleted is preserved server-side and ignored on update");
    }
}
