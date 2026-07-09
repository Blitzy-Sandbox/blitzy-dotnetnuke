using DnnMigration.Domain.Entities;
using DnnMigration.Infrastructure.Data;
using DnnMigration.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DnnMigration.IntegrationTests;

/// <summary>
/// Behavioural regression tests for the SCHEMA-FIDELITY repository bridge (review findings #1 and #2).
/// These exercise <see cref="UserRepository"/> and <see cref="ModuleRepository"/> directly against an
/// EF Core InMemory store to lock the runtime contracts that no HTTP-level integration test covers:
/// the user credential/portal round-trip across the three physical tables ([Users], [aspnet_Membership],
/// [UserPortals]) and the corrected GetModuleByDefinition join through [DesktopModules].
/// </summary>
// MIGRATION: Net-new. The end-to-end UserApiTests deliberately never assert Membership/Profile, and the
// AuthService/UserService unit tests mock IUserRepository, so the credential persistence path added by
// the schema remodel would otherwise be un-exercised. These tests fill that gap without inventing any
// column: they assert only behaviour that the migrated repository guarantees. Nullable results from the
// repository are null-forgiven at assignment (then guarded by a NotBeNull() assertion that fails clearly
// if the row is genuinely absent) to keep the Gate 1 warnings-as-errors (CS8602/CS8604) build clean.
public class RepositoryBehaviorRegressionTests
{
    private static DnnDbContext NewInMemoryContext() =>
        new(new DbContextOptionsBuilder<DnnDbContext>()
            .UseInMemoryDatabase("repobehav_" + System.Guid.NewGuid().ToString("N"))
            .Options);

    private static User NewUser(int portalId = 0, string username = "jdoe") => new()
    {
        Username = username,
        Email = username + "@example.com",
        FirstName = "John",
        LastName = "Doe",
        DisplayName = "John Doe",
        PortalID = portalId
    };

    [Fact]
    public async Task AddAsync_persists_user_membership_and_portal_and_round_trips_credential()
    {
        using var ctx = NewInMemoryContext();
        var repo = new UserRepository(ctx);
        var user = NewUser(portalId: 7);
        user.Membership.Password = "bcrypt-hash-value";
        user.Membership.PasswordQuestion = "pet?";
        user.Membership.PasswordAnswer = "rex";

        await repo.AddAsync(user);
        user.UserID.Should().BeGreaterThan(0, "the [Users] IDENTITY key is assigned on insert");

        // Exactly one row was written to each of the three physical tables.
        (await ctx.Users.CountAsync()).Should().Be(1);
        (await ctx.UserMemberships.CountAsync()).Should().Be(1);
        (await ctx.UserPortals.CountAsync()).Should().Be(1);

        // GetById hydrates PortalID from the junction AND the credential from aspnet_Membership.
        var byId = (await repo.GetByIdAsync(user.UserID))!;
        byId.Should().NotBeNull();
        byId.PortalID.Should().Be(7, "PortalID is hydrated from the UserPortals junction");
        byId.Membership.Password.Should().Be("bcrypt-hash-value", "the credential round-trips through aspnet_Membership");

        // GetByUsername is scoped to the portal via the junction and also hydrates the credential.
        var byName = (await repo.GetByUsernameAsync(7, "jdoe"))!;
        byName.Should().NotBeNull();
        byName.Membership.Password.Should().Be("bcrypt-hash-value");

        // The same username in a DIFFERENT portal must not resolve (per-portal username scope).
        (await repo.GetByUsernameAsync(1, "jdoe")).Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_profile_only_change_does_not_wipe_credential()
    {
        // Use a shared InMemory database name across SEPARATE contexts so each logical operation runs in
        // its own context - faithfully mirroring the production per-HTTP-request scoped DbContext. This
        // both avoids the change-tracker "same key already tracked" conflict of add-then-update within a
        // single context AND strengthens the test: reloading in a fresh context proves the change was
        // PERSISTED, not merely cached in the first context's identity map.
        var dbName = "repobehav_" + System.Guid.NewGuid().ToString("N");
        DnnDbContext Ctx() => new(new DbContextOptionsBuilder<DnnDbContext>()
            .UseInMemoryDatabase(dbName).Options);

        int userId;
        using (var ctx = Ctx())
        {
            var repo = new UserRepository(ctx);
            var user = NewUser();
            user.Membership.Password = "original-hash";
            await repo.AddAsync(user);
            userId = user.UserID;
        }

        // Mirror UserService.UpdateAsync: load (hydrates real credential), mutate a profile field, save.
        using (var ctx = Ctx())
        {
            var repo = new UserRepository(ctx);
            var loaded = (await repo.GetByIdAsync(userId))!;
            loaded.Should().NotBeNull();
            loaded.Membership.Password.Should().Be("original-hash", "GetById hydrated the real stored credential");
            loaded.FirstName = "Jonathan";
            await repo.UpdateAsync(loaded);
        }

        using (var ctx = Ctx())
        {
            var repo = new UserRepository(ctx);
            var reloaded = (await repo.GetByIdAsync(userId))!;
            reloaded.Should().NotBeNull();
            reloaded.FirstName.Should().Be("Jonathan");
            reloaded.Membership.Password.Should().Be("original-hash", "a profile-only update must not wipe the credential");
            (await ctx.UserMemberships.CountAsync()).Should().Be(1, "the membership row was upserted in place, not duplicated");
        }
    }

    [Fact]
    public async Task UpdateAsync_password_change_is_persisted()
    {
        var dbName = "repobehav_" + System.Guid.NewGuid().ToString("N");
        DnnDbContext Ctx() => new(new DbContextOptionsBuilder<DnnDbContext>()
            .UseInMemoryDatabase(dbName).Options);

        int userId;
        using (var ctx = Ctx())
        {
            var repo = new UserRepository(ctx);
            var user = NewUser();
            user.Membership.Password = "old-hash";
            await repo.AddAsync(user);
            userId = user.UserID;
        }

        // Mirror UserService.ChangePasswordAsync in a fresh context: load, set a new hash, save.
        using (var ctx = Ctx())
        {
            var repo = new UserRepository(ctx);
            var loaded = (await repo.GetByIdAsync(userId))!;
            loaded.Membership.Password = "new-hash";
            await repo.UpdateAsync(loaded);
        }

        using (var ctx = Ctx())
        {
            var repo = new UserRepository(ctx);
            var reloaded = (await repo.GetByIdAsync(userId))!;
            reloaded.Membership.Password.Should().Be("new-hash");
        }
    }

    [Fact]
    public async Task DeleteAsync_removes_user_membership_and_portal_rows()
    {
        using var ctx = NewInMemoryContext();
        var repo = new UserRepository(ctx);
        var user = NewUser();
        user.Membership.Password = "h";
        await repo.AddAsync(user);
        var id = user.UserID;

        await repo.DeleteAsync(id);

        (await repo.GetByIdAsync(id)).Should().BeNull();
        (await ctx.Users.CountAsync()).Should().Be(0);
        (await ctx.UserMemberships.CountAsync()).Should().Be(0, "the aspnet_Membership row is cascaded");
        (await ctx.UserPortals.CountAsync()).Should().Be(0, "the UserPortals junction row is cascaded");
    }

    [Fact]
    public async Task GetByPortalAsync_returns_only_users_associated_with_that_portal()
    {
        using var ctx = NewInMemoryContext();
        var repo = new UserRepository(ctx);
        var inPortal = NewUser(portalId: 7, username: "alice");
        inPortal.Membership.Password = "a";
        var otherPortal = NewUser(portalId: 9, username: "bob");
        otherPortal.Membership.Password = "b";
        await repo.AddAsync(inPortal);
        await repo.AddAsync(otherPortal);

        var portal7 = (await repo.GetByPortalAsync(7)).ToList();
        portal7.Should().ContainSingle();
        portal7[0].Username.Should().Be("alice");
        portal7[0].PortalID.Should().Be(7);
    }

    [Fact]
    public async Task GetByDefinitionAsync_joins_through_DesktopModules_FriendlyName_and_excludes_deleted()
    {
        using var ctx = NewInMemoryContext();
        // DesktopModule -> ModuleDefinition -> Module(s); FriendlyName lives on DesktopModule.
        ctx.DesktopModules.Add(new DesktopModule { DesktopModuleID = 10, FriendlyName = "Announcements" });
        ctx.ModuleDefinitions.Add(new ModuleDefinition { ModuleDefID = 100, DesktopModuleID = 10 });
        ctx.Modules.Add(new Module { ModuleID = 2001, PortalID = 7, ModuleDefID = 100, ModuleTitle = "Ann Live", IsDeleted = false });
        ctx.Modules.Add(new Module { ModuleID = 2002, PortalID = 7, ModuleDefID = 100, ModuleTitle = "Ann Deleted", IsDeleted = true });
        ctx.Modules.Add(new Module { ModuleID = 2003, PortalID = 9, ModuleDefID = 100, ModuleTitle = "Other Portal", IsDeleted = false });
        await ctx.SaveChangesAsync();

        var repo = new ModuleRepository(ctx);

        var found = (await repo.GetByDefinitionAsync(7, "Announcements"))!;
        found.Should().NotBeNull();
        found.ModuleID.Should().Be(2001,
            "the join matches DesktopModules.FriendlyName, scopes to the portal, and excludes IsDeleted rows");

        (await repo.GetByDefinitionAsync(7, "Nonexistent")).Should().BeNull();
        (await repo.GetByDefinitionAsync(999, "Announcements")).Should().BeNull("no module for that portal");
    }
}
