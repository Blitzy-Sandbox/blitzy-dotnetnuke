using System;
using System.Linq;
using System.Threading.Tasks;
using DnnMigration.Domain.Entities;
using DnnMigration.Infrastructure.Data;
using DnnMigration.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DnnMigration.IntegrationTests.Persistence;

// MIGRATION (QA-FINAL Issues #1/#2 [User] and #3/#4 [Module] - CRITICAL, read/write split): the User and Module
// entities are flattened read models mapped to the legacy read VIEWS (vw_Users / vw_Modules) for queries AND to their
// physical base TABLES ([Users] / [Modules]) for writes. Because the physical [Users] table has no PortalId column and
// the physical [Modules] table has none of the per-page placement columns, the repositories must FAN a write out to a
// second physical table: a new user's portal membership is persisted to [UserPortals], and a placed module's placement
// is persisted to [TabModules]. SchemaFidelityTests proves (offline, via GenerateCreateScript against the SqlServer
// provider) that the physical write tables are emitted with EXACTLY the legacy columns; THIS suite proves the
// repositories STAGE the correct child rows so create/update/delete actually persist the full shape.
//
// STORE NOTE: EF Core InMemory ignores the ToView/ToTable store split (it stores by CLR property), so these tests do
// NOT re-assert column fidelity (SchemaFidelityTests owns that). What they DO assert - that AddAsync/UpdateAsync/
// DeleteAsync track the [UserPortals]/[TabModules] child rows with the right values - is provider-agnostic and is the
// exact repository behavior the final review flagged (UserRepository L73-98, ModuleRepository L114-147). The repos are
// STAGE-ONLY; the test invokes SaveChangesAsync on the context to flush (mirroring IUnitOfWork.SaveChangesAsync).
[Trait("Category", "Integration")]
public sealed class WriteModelFanOutTests
{
    private const int PortalA = 7;

    // Each test gets its own uniquely-named InMemory store so it is independent of the assembly no-parallelization setting.
    private static DnnDbContext NewInMemoryContext() =>
        new(new DbContextOptionsBuilder<DnnDbContext>()
            .UseInMemoryDatabase($"fanout-{Guid.NewGuid():N}")
            .Options);

    // ---- USER -> [Users] + [UserPortals] (Issue #1/#2) -----------------------------------------------------

    [Fact]
    public async Task AddAsync_stages_a_UserPortal_membership_row_for_the_new_user()
    {
        using var context = NewInMemoryContext();
        var repository = new UserRepository(context);

        var user = new User
        {
            Username = "jdoe",
            FirstName = "John",
            LastName = "Doe",
            Email = "jdoe@example.com",
            PortalId = PortalA,
            IsApproved = true,
        };

        await repository.AddAsync(user);
        await context.SaveChangesAsync();

        // The user row is persisted and got a store-generated key.
        user.UserId.Should().BeGreaterThan(0, "the [Users] write maps a store-generated key on insert");

        // The portal membership was fanned out to [UserPortals] with the user's portal + authorization, and EF fixed up
        // the FK from the User navigation to the generated UserId within the single SaveChanges boundary.
        var memberships = await context.Set<UserPortal>().ToListAsync();
        memberships.Should().ContainSingle("exactly one [UserPortals] membership row is staged per new user");
        var membership = memberships[0];
        membership.UserId.Should().Be(user.UserId, "the membership FK must resolve to the generated User.UserId");
        membership.PortalId.Should().Be(PortalA, "the membership records the tenant the user belongs to (AAP multi-tenant isolation)");
        membership.Authorised.Should().BeTrue("Authorised mirrors the user's IsApproved at creation time");
    }

    [Fact]
    public async Task DeleteAsync_removes_the_user_and_its_UserPortal_membership_rows()
    {
        using var context = NewInMemoryContext();
        var repository = new UserRepository(context);

        var user = new User { Username = "todelete", FirstName = "To", LastName = "Delete", PortalId = PortalA, IsApproved = true };
        await repository.AddAsync(user);
        await context.SaveChangesAsync();
        context.Set<UserPortal>().Should().NotBeEmpty("precondition: a membership row exists before delete");

        await repository.DeleteAsync(PortalA, user.UserId);
        await context.SaveChangesAsync();

        (await context.Users.AnyAsync(u => u.UserId == user.UserId)).Should().BeFalse("the [Users] row is removed");
        (await context.Set<UserPortal>().AnyAsync(up => up.UserId == user.UserId)).Should()
            .BeFalse("the [UserPortals] membership row(s) must be removed with the user so no orphaned FK remains");
    }

    // ---- MODULE -> [Modules] + [TabModules] (Issue #3/#4) ---------------------------------------------------

    [Fact]
    public async Task AddAsync_stages_a_TabModule_placement_for_a_placed_module()
    {
        using var context = NewInMemoryContext();
        var repository = new ModuleRepository(context);

        var module = new Module
        {
            PortalId = PortalA,
            ModuleDefId = 1,
            ModuleTitle = "Announcements",
            TabId = 42,                 // placed on a page (TabId >= 0)
            PaneName = "ContentPane",
            ModuleOrder = 3,
            CacheTime = 120,
            IsDeleted = false,
        };

        await repository.AddAsync(module);
        await context.SaveChangesAsync();

        module.ModuleId.Should().BeGreaterThan(0, "the [Modules] write maps a store-generated key on insert");

        var placements = await context.Set<TabModule>().ToListAsync();
        placements.Should().ContainSingle("a placed module fans out to exactly one [TabModules] row");
        var placement = placements[0];
        placement.ModuleId.Should().Be(module.ModuleId, "the placement FK must resolve to the generated Module.ModuleId");
        placement.TabId.Should().Be(42);
        placement.PaneName.Should().Be("ContentPane");
        placement.ModuleOrder.Should().Be(3, "ModuleOrder must persist so ModuleService re-sequencing is durable");
        placement.CacheTime.Should().Be(120);
    }

    [Fact]
    public async Task AddAsync_does_not_stage_a_placement_for_an_unplaced_module()
    {
        using var context = NewInMemoryContext();
        var repository = new ModuleRepository(context);

        // No TabId => not placed on any page (e.g. an AllTabs/portal-level module); the vw_Modules LEFT JOIN yields no
        // TabModules row, so none is staged.
        var module = new Module { PortalId = PortalA, ModuleDefId = 1, ModuleTitle = "Unplaced", TabId = null, IsDeleted = false };

        await repository.AddAsync(module);
        await context.SaveChangesAsync();

        (await context.Set<TabModule>().AnyAsync()).Should().BeFalse("an unplaced module must NOT create a [TabModules] row");
    }

    [Fact]
    public async Task UpdateAsync_updates_an_existing_placement_then_removes_it_on_soft_delete()
    {
        using var context = NewInMemoryContext();
        var repository = new ModuleRepository(context);

        var module = new Module
        {
            PortalId = PortalA, ModuleDefId = 1, ModuleTitle = "Reorder Me",
            TabId = 42, PaneName = "ContentPane", ModuleOrder = 1, IsDeleted = false,
        };
        await repository.AddAsync(module);
        await context.SaveChangesAsync();

        // Re-sequence (ModuleService.UpdateTabModuleOrder sets ModuleOrder then calls UpdateAsync) -> placement updated.
        module.ModuleOrder = 9;
        await repository.UpdateAsync(module);
        await context.SaveChangesAsync();

        var afterReorder = await context.Set<TabModule>().SingleAsync(tm => tm.ModuleId == module.ModuleId);
        afterReorder.ModuleOrder.Should().Be(9, "UpdateAsync must sync the re-sequenced ModuleOrder onto the [TabModules] row");

        // Soft delete (ModuleService sets IsDeleted = true and clears TabId, then calls UpdateAsync) -> placement removed.
        module.IsDeleted = true;
        module.TabId = null;
        await repository.UpdateAsync(module);
        await context.SaveChangesAsync();

        (await context.Set<TabModule>().AnyAsync(tm => tm.ModuleId == module.ModuleId)).Should()
            .BeFalse("a soft-deleted/unplaced module must have its [TabModules] placement removed");
    }
}
