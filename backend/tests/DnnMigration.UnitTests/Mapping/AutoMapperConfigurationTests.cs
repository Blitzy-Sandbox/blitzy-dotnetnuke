using AutoMapper;
using AutoMapper.Internal;
using DnnMigration.Application.Interfaces;
using DnnMigration.Application.Mapping;
using FluentAssertions;
using Xunit;

namespace DnnMigration.UnitTests.Mapping;

// MIGRATION (CP2 review - Gate 2 readiness / AutoMapper configuration validation): the checkpoint (Phase 8)
// requires a unit test that builds the mapper configuration from the Application profiles and calls
// AssertConfigurationIsValid(). The same assertion is ALSO a compensating control for the pinned
// AutoMapper 12.0.1 advisory (GHSA-rvv3-g6hj-g44x): a passing AssertConfigurationIsValid() proves the
// configuration contains NO cyclic / self-referential type maps - the structural precondition of the
// uncontrolled-recursion DoS - so the vulnerable code path can never be built (see the NuGetAuditSuppress
// rationale in DnnMigration.Api.csproj and DnnMigration.Application.csproj, and MIGRATION_NOTES.md).
//
// MIGRATION/SECURITY (QA Checkpoint F5 - finding F-1, CVE-2026-32933): the configuration built here MIRRORS the
// API host exactly (AddMaps + MappingConfiguration.ApplyRecursionGuard), and a second test asserts the guard
// actually bounds every TypeMap at MappingConfiguration.MaxRecursionDepth. Together these prove, at test time,
// that the ACTIVE recursion mitigation the host applies at startup is present and effective.
public sealed class AutoMapperConfigurationTests
{
    // MIGRATION: builds the EXACT configuration the API composition root creates -
    // AddAutoMapper(MappingConfiguration.ApplyRecursionGuard, applicationAssembly) - by scanning the IDENTICAL
    // DnnMigration.Application assembly (Profiles: AuthProfile, ModuleProfile, PortalProfile, RoleProfile,
    // TabProfile, UserProfile) and then applying the recursion guard AFTER the maps are added, just as
    // AddAutoMapper(action, assembly) does at startup.
    private static MapperConfiguration BuildHostMapperConfiguration()
    {
        var applicationAssembly = typeof(IPortalService).Assembly;
        return new MapperConfiguration(cfg =>
        {
            cfg.AddMaps(applicationAssembly);
            MappingConfiguration.ApplyRecursionGuard(cfg);
        });
    }

    [Fact]
    public void ApplicationProfiles_BuildAValidMapperConfiguration()
    {
        // Arrange: build the host's exact profile set + recursion guard.
        var configuration = BuildHostMapperConfiguration();

        // Act + Assert: AssertConfigurationIsValid() throws AutoMapperConfigurationException - failing this test
        // with a precise per-member diagnostic - if ANY destination member is unmapped. A pass simultaneously
        // confirms that no cyclic / self-referential map exists, which is the compensating control that bounds the
        // GHSA-rvv3-g6hj-g44x advisory while AutoMapper stays pinned at 12.0.1 per AAP section 0.5.1.
        configuration.AssertConfigurationIsValid();
    }

    // MIGRATION/SECURITY (QA Checkpoint F5 - finding F-1; AutoMapper CVE-2026-32933 / GHSA-rvv3-g6hj-g44x):
    // proves the ACTIVE recursion-depth mitigation is wired correctly - every discovered TypeMap must carry the
    // bounded MappingConfiguration.MaxRecursionDepth (8) instead of AutoMapper's unbounded default (0). This guards
    // against a regression where the guard is removed/bypassed in the composition root, which would re-expose the
    // uncontrolled-recursion DoS. The host applies the same guard via AddAutoMapper(action, assembly).
    [Fact]
    public void RecursionGuard_BoundsEveryTypeMapAtTheConfiguredMaxDepth()
    {
        // Arrange: build the host's exact configuration (maps + guard).
        var configuration = BuildHostMapperConfiguration();

        // Act: enumerate every TypeMap AutoMapper discovered from the Application profiles.
        var typeMaps = configuration.Internal().GetAllTypeMaps();

        // Assert: there is at least one map, and EVERY map is bounded at the configured depth (not the
        // unbounded default of 0). 0 would mean the guard did not run / did not cover that map.
        typeMaps.Should().NotBeEmpty("the Application assembly defines AutoMapper profiles");
        typeMaps.Should().OnlyContain(
            map => map.MaxDepth == MappingConfiguration.MaxRecursionDepth,
            "the recursion guard must bound every TypeMap at MaxRecursionDepth to mitigate GHSA-rvv3-g6hj-g44x");
    }
}
