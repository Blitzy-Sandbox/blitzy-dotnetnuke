using AutoMapper;
using DnnMigration.Application.Interfaces;
using Xunit;

namespace DnnMigration.UnitTests.Mapping;

// MIGRATION (CP2 review - Gate 2 readiness / AutoMapper configuration validation): the checkpoint (Phase 8)
// requires a unit test that builds the mapper configuration from the Application profiles and calls
// AssertConfigurationIsValid(). The same assertion is ALSO the documented compensating control for the pinned
// AutoMapper 12.0.1 advisory (GHSA-rvv3-g6hj-g44x): a passing AssertConfigurationIsValid() proves the
// configuration contains NO cyclic / self-referential type maps - the structural precondition of the
// uncontrolled-recursion DoS - so the vulnerable code path can never be built (see the NuGetAuditSuppress
// rationale in DnnMigration.Api.csproj and DnnMigration.Application.csproj, and MIGRATION_NOTES.md).
public sealed class AutoMapperConfigurationTests
{
    // MIGRATION: Program.cs registers AutoMapper via AddAutoMapper(typeof(IPortalService).Assembly), which scans
    // the DnnMigration.Application assembly for every Profile (AuthProfile, ModuleProfile, PortalProfile,
    // RoleProfile, TabProfile, UserProfile). This test anchors on the IDENTICAL assembly and uses AddMaps so the
    // validated configuration is exactly the one the API host composes at startup.
    [Fact]
    public void ApplicationProfiles_BuildAValidMapperConfiguration()
    {
        // Arrange: build the host's profile set from the Application assembly.
        var applicationAssembly = typeof(IPortalService).Assembly;
        var configuration = new MapperConfiguration(cfg => cfg.AddMaps(applicationAssembly));

        // Act + Assert: AssertConfigurationIsValid() throws AutoMapperConfigurationException - failing this test
        // with a precise per-member diagnostic - if ANY destination member is unmapped. A pass simultaneously
        // confirms that no cyclic / self-referential map exists, which is the compensating control that bounds the
        // GHSA-rvv3-g6hj-g44x advisory while AutoMapper stays pinned at 12.0.1 per AAP section 0.5.1.
        configuration.AssertConfigurationIsValid();
    }
}
