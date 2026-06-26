using System;
using System.Linq;
using System.Threading.Tasks;
using DnnMigration.Domain.Entities;
using DnnMigration.Infrastructure.Data;
using DnnMigration.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DnnMigration.IntegrationTests.Isolation;

// MIGRATION (QA-4 #8 / "Areas of Concern" #4 â€” coverage gap, MINOR): an EXPLICIT two-portal cross-exclusion
// isolation test. The shipped integration suite scoped each test to a unique portal, so cross-portal LEAKAGE was
// never directly asserted (QA noted "no explicit two-portal cross-exclusion isolation test"). This test seeds TWO
// portals into ONE store and proves the repository query-scoping layer â€” the layer that enforces DNN multi-tenant
// isolation (AAP Â§0.7.1, "every entity and query remains scoped by PortalId") â€” excludes the other portal's rows
// for every scoped read. RoleRepository is exercised because its reads are portal-scoped (GetByPortalIdAsync,
// GetByIdAsync(portalId, roleId)) and because GetUserRolesAsync scopes the user->role->permission model via
// Role.PortalId, directly hardening the relationship path that Issues #4/#5 touched.
[Trait("Category", "Integration")]
public sealed class PortalIsolationTests
{
    private const int PortalA = 1;
    private const int PortalB = 2;

    // Each test gets its own InMemory store (unique name) so the assembly-level no-parallelization setting is not
    // even relied upon for isolation between tests.
    private static DnnDbContext NewInMemoryContext() =>
        new(new DbContextOptionsBuilder<DnnDbContext>()
            .UseInMemoryDatabase($"isolation-{Guid.NewGuid():N}")
            .Options);

    [Fact]
    public async Task GetByPortalIdAsync_returns_only_the_requested_portals_roles()
    {
        using var context = NewInMemoryContext();
        context.Roles.AddRange(
            new Role { PortalId = PortalA, RoleName = "A-Administrators" },
            new Role { PortalId = PortalA, RoleName = "A-Registered Users" },
            new Role { PortalId = PortalB, RoleName = "B-Administrators" });
        await context.SaveChangesAsync();
        var repository = new RoleRepository(context);

        var portalARoles = (await repository.GetByPortalIdAsync(PortalA)).ToList();

        portalARoles.Should().HaveCount(2);
        portalARoles.Should().OnlyContain(r => r.PortalId == PortalA);
        portalARoles.Should().NotContain(r => r.RoleName == "B-Administrators",
            "Portal A's role list must not leak Portal B's roles (multi-tenant isolation, AAP Â§0.7.1)");
    }

    [Fact]
    public async Task GetByIdAsync_excludes_a_role_that_belongs_to_another_portal()
    {
        using var context = NewInMemoryContext();
        var roleInB = new Role { PortalId = PortalB, RoleName = "B-Only" };
        context.Roles.Add(roleInB);
        await context.SaveChangesAsync();
        var repository = new RoleRepository(context);

        // Cross-portal read: Portal A must NOT be able to read Portal B's role by id.
        var leaked = await repository.GetByIdAsync(PortalA, roleInB.RoleId);
        leaked.Should().BeNull("a portal must not read another portal's role by id (multi-tenant isolation, AAP Â§0.7.1)");

        // Same-portal read of the very same row still succeeds (proves the null above is isolation, not a missing row).
        var samePortal = await repository.GetByIdAsync(PortalB, roleInB.RoleId);
        samePortal.Should().NotBeNull();
        samePortal!.RoleName.Should().Be("B-Only");
    }

    [Fact]
    public async Task GetUserRolesAsync_scopes_memberships_by_the_roles_portal()
    {
        using var context = NewInMemoryContext();
        const int userId = 500;
        var roleInB = new Role { PortalId = PortalB, RoleName = "B-Members" };
        context.Roles.Add(roleInB);
        await context.SaveChangesAsync();
        context.UserRoles.Add(new UserRole { UserId = userId, RoleId = roleInB.RoleId });
        await context.SaveChangesAsync();
        var repository = new RoleRepository(context);

        // The user's only membership is a Portal B role; querying Portal A must return nothing (scoped via Role.PortalId).
        var wrongPortal = (await repository.GetUserRolesAsync(PortalA, userId)).ToList();
        wrongPortal.Should().BeEmpty("user->role memberships are portal-scoped via Role.PortalId (AAP Â§0.7.1)");

        // Querying the correct portal returns the membership, eager-loading its Role.
        var rightPortal = (await repository.GetUserRolesAsync(PortalB, userId)).ToList();
        rightPortal.Should().ContainSingle().Which.RoleId.Should().Be(roleInB.RoleId);
    }
}
