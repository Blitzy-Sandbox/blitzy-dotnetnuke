using AutoMapper;
using DnnMigration.Application.Mapping;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DnnMigration.UnitTests.Mapping;

/// <summary>
/// Validates that the combined AutoMapper configuration composed of all five feature
/// profiles is internally consistent. <see cref="MapperConfiguration.AssertConfigurationIsValid()"/>
/// throws <c>AutoMapperConfigurationException</c> if any destination member is left
/// unmapped, so a passing run guarantees the DTO anti-corruption boundary (AAP 0.3.3)
/// has no silent field gaps across Portal, Module, User, Role and Tab.
/// </summary>
public class MappingProfilesConfigurationTests
{
    // MIGRATION: AutoMapper was upgraded 12.0.1 -> 15.1.1 (security advisory GHSA-rvv3-g6hj-g44x;
    // see MIGRATION_NOTES.md and DnnMigration.Application.csproj). From v13+ the MapperConfiguration
    // constructor requires an ILoggerFactory, so NullLoggerFactory.Instance is supplied here exactly as
    // the sibling profile tests do. The validation semantics (AssertConfigurationIsValid) are unchanged.
    private static MapperConfiguration BuildConfiguration() =>
        new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<PortalProfile>();
            cfg.AddProfile<ModuleProfile>();
            cfg.AddProfile<UserProfile>();
            cfg.AddProfile<RoleProfile>();
            cfg.AddProfile<TabProfile>();
        }, NullLoggerFactory.Instance);

    [Fact]
    public void AllProfiles_Configuration_IsValid()
    {
        var configuration = BuildConfiguration();

        configuration.Invoking(c => c.AssertConfigurationIsValid())
            .Should().NotThrow(
                "every destination member across all five profiles must be mapped or explicitly ignored");
    }

    [Fact]
    public void AllProfiles_CreateMapper_ReturnsUsableMapper()
    {
        var configuration = BuildConfiguration();

        var mapper = configuration.CreateMapper();

        mapper.Should().NotBeNull();
    }
}
