using DnnMigration.Domain.Entities;
using DnnMigration.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace DnnMigration.IntegrationTests;

/// <summary>
/// Regression tests that lock the SCHEMA-FIDELITY remodel (review findings #1 and #2) at the EF Core
/// MODEL level: the migrated <see cref="DnnDbContext"/> must map the migrated entities onto the EXISTING
/// DotNetNuke / aspnet_* tables verbatim and must NOT invent columns.
/// </summary>
// MIGRATION: Net-new. No automated gate runs against a real SQL Server (Gates 1-5 use EF Core InMemory,
// Gates 6-7 need Docker), so schema fidelity is verified here as MODEL correctness. The model is built
// with the RELATIONAL SQL Server provider purely so that the relational metadata (table names, column
// names, keys) is populated - accessing DbContext.Model builds the model in memory and opens NO database
// connection, so no SQL Server instance is required. The SQL Server provider assembly is available
// transitively through the DnnMigration.Api -> DnnMigration.Infrastructure project reference; this test
// project adds no direct package reference to it.
public class SchemaFidelityModelTests
{
    // A connection string is required to construct SqlServer options but is never used: no command is
    // ever executed, only the in-memory model graph is inspected. FindEntityType is null-forgiven because
    // the entity is guaranteed to be in the model (a failing assertion below would surface a real defect);
    // this keeps the Gate 1 warnings-as-errors (CS8602) build clean.
    private static DnnDbContext RelationalModelContext() =>
        new(new DbContextOptionsBuilder<DnnDbContext>()
            .UseSqlServer("Server=(local);Database=Dnn;Trusted_Connection=True;TrustServerCertificate=True")
            .Options);

    [Fact]
    public void User_maps_to_Users_and_does_not_invent_PortalID_or_own_Membership()
    {
        using var ctx = RelationalModelContext();
        var et = ctx.Model.FindEntityType(typeof(User))!;
        et.GetTableName().Should().Be("Users");

        // finding #1: there is NO [Users].[PortalID] column - PortalID is a transient carrier hydrated
        // from the UserPortals junction by the repository, so it must not be a mapped scalar property.
        et.FindProperty(nameof(User.PortalID)).Should()
            .BeNull("PortalID is projected from the UserPortals junction, not a [Users] column");

        // finding #1: Membership/Profile are standalone entities, never owned by the int-keyed User.
        et.FindNavigation(nameof(User.Membership)).Should().BeNull();
        et.FindNavigation(nameof(User.Profile)).Should().BeNull();
        et.FindProperty(nameof(User.Membership)).Should().BeNull();
        et.FindProperty(nameof(User.Profile)).Should().BeNull();

        // real scalar columns remain mapped.
        et.FindProperty(nameof(User.Username)).Should().NotBeNull();
        et.FindProperty(nameof(User.Email)).Should().NotBeNull();
        et.FindProperty(nameof(User.DisplayName)).Should().NotBeNull();
    }

    [Fact]
    public void UserMembership_is_standalone_aspnet_Membership_keyed_by_guid_UserId_with_required_columns()
    {
        using var ctx = RelationalModelContext();
        var et = ctx.Model.FindEntityType(typeof(UserMembership))!;
        et.GetTableName().Should().Be("aspnet_Membership");

        var pk = et.FindPrimaryKey()!;
        pk.Properties.Should().ContainSingle();
        var keyProp = pk.Properties[0];
        keyProp.Name.Should().Be(nameof(UserMembership.MembershipUserId));
        keyProp.ClrType.Should().Be(typeof(System.Guid));

        var store = StoreObjectIdentifier.Table("aspnet_Membership", et.GetSchema());
        keyProp.GetColumnName(store).Should().Be("UserId", "the physical aspnet_Membership PK column is [UserId]");

        // finding #1: the required NOT NULL columns the previous int-owned mapping omitted are present.
        et.FindProperty(nameof(UserMembership.ApplicationId)).Should().NotBeNull();
        et.FindProperty(nameof(UserMembership.PasswordFormat)).Should().NotBeNull();
        et.FindProperty(nameof(UserMembership.PasswordSalt)).Should().NotBeNull();
        et.FindProperty(nameof(UserMembership.FailedPasswordAttemptCount)).Should().NotBeNull();
    }

    [Fact]
    public void UserPortal_maps_to_UserPortals_with_composite_key()
    {
        using var ctx = RelationalModelContext();
        var et = ctx.Model.FindEntityType(typeof(UserPortal))!;
        et.GetTableName().Should().Be("UserPortals");

        var pk = et.FindPrimaryKey()!;
        pk.Properties.Select(p => p.Name).Should()
            .BeEquivalentTo(new[] { nameof(UserPortal.UserId), nameof(UserPortal.PortalId) });
    }

    [Fact]
    public void Modules_maps_only_real_columns_and_not_invented_placement_or_control_columns()
    {
        using var ctx = RelationalModelContext();
        var et = ctx.Model.FindEntityType(typeof(Module))!;
        et.GetTableName().Should().Be("Modules");

        // real [Modules] columns remain mapped.
        et.FindProperty(nameof(Module.ModuleTitle)).Should().NotBeNull();
        et.FindProperty(nameof(Module.ModuleDefID)).Should().NotBeNull();
        et.FindProperty(nameof(Module.PortalID)).Should().NotBeNull();

        // finding #2: placement columns physically live on [TabModules]; control columns on
        // [ModuleControls]. They must NOT be mapped onto [Modules].
        et.FindProperty("TabID").Should().BeNull();
        et.FindProperty("TabModuleID").Should().BeNull();
        et.FindProperty("PaneName").Should().BeNull();
        et.FindProperty("ModuleOrder").Should().BeNull();
        et.FindProperty("ControlType").Should().BeNull();
    }

    [Fact]
    public void TabModule_maps_placement_columns_to_TabModules_with_surrogate_key()
    {
        using var ctx = RelationalModelContext();
        var et = ctx.Model.FindEntityType(typeof(TabModule))!;
        et.GetTableName().Should().Be("TabModules");

        // finding #2: placement columns live here.
        et.FindProperty(nameof(TabModule.PaneName)).Should().NotBeNull();
        et.FindProperty(nameof(TabModule.ModuleOrder)).Should().NotBeNull();
        et.FindProperty(nameof(TabModule.ModuleID)).Should().NotBeNull();
        et.FindProperty(nameof(TabModule.TabID)).Should().NotBeNull();

        var pk = et.FindPrimaryKey()!;
        pk.Properties.Should().ContainSingle().Which.Name.Should().Be(nameof(TabModule.TabModuleID));
    }
}
