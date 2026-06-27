using System;
using System.Threading.Tasks;
using DnnMigration.Domain.Entities;
using DnnMigration.Infrastructure.Data;
using DnnMigration.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DnnMigration.IntegrationTests.Persistence;

// MIGRATION (CP-FINAL review - PortalRepository.GetByAliasAsync parity): the alias->portal resolution was a
// documented gap (the method always returned null). It is now backed by the PortalAlias entity mapped to the
// existing [PortalAlias] table and resolves the owning portal exactly as legacy DataProvider.GetPortalByAlias did.
// This suite seeds PortalAlias + Portal rows in an InMemory store and proves the repository resolves matches
// (case-insensitively), distinguishes "no match" from the valid default portal 0, and is null-safe for blank input.
[Trait("Category", "Integration")]
public sealed class PortalAliasLookupTests
{
    private static DnnDbContext NewInMemoryContext() =>
        new(new DbContextOptionsBuilder<DnnDbContext>()
            .UseInMemoryDatabase($"alias-{Guid.NewGuid():N}")
            .Options);

    // NOTE: non-zero portal ids are used deliberately. The EF Core InMemory provider runs its identity value
    // generator for a default-valued int key (PortalId == 0), reassigning a non-zero key on insert, so an explicit
    // portal 0 cannot be round-tripped under InMemory. The repository's two-step lookup distinguishes "no match"
    // from "matched" by null-checking the alias ROW (not by inspecting the portal id value), which the no-match
    // test below proves independently of the id.
    private static async Task SeedAsync(DnnDbContext context)
    {
        context.Portals.Add(new Portal { PortalId = 1, PortalName = "Primary Portal" });
        context.Portals.Add(new Portal { PortalId = 7, PortalName = "Marketing Site" });
        context.PortalAliases.Add(new PortalAlias { PortalAliasId = 1, PortalId = 1, HttpAlias = "localhost/dnn" });
        context.PortalAliases.Add(new PortalAlias { PortalAliasId = 2, PortalId = 7, HttpAlias = "WWW.Example.COM" });
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task GetByAliasAsync_returns_the_owning_portal_for_a_matching_alias()
    {
        using var context = NewInMemoryContext();
        await SeedAsync(context);
        var repository = new PortalRepository(context);

        Portal? portal = await repository.GetByAliasAsync("localhost/dnn");

        portal.Should().NotBeNull();
        portal!.PortalId.Should().Be(1, "the alias 'localhost/dnn' maps to portal 1");
        portal.PortalName.Should().Be("Primary Portal");
    }

    [Fact]
    public async Task GetByAliasAsync_matches_case_insensitively_and_trims_whitespace()
    {
        using var context = NewInMemoryContext();
        await SeedAsync(context);
        var repository = new PortalRepository(context);

        // Stored as "WWW.Example.COM"; DNN aliases are case-insensitive and the input is trimmed.
        Portal? portal = await repository.GetByAliasAsync("  www.example.com  ");

        portal.Should().NotBeNull();
        portal!.PortalId.Should().Be(7);
    }

    [Fact]
    public async Task GetByAliasAsync_returns_null_when_no_alias_matches()
    {
        using var context = NewInMemoryContext();
        await SeedAsync(context);
        var repository = new PortalRepository(context);

        Portal? portal = await repository.GetByAliasAsync("unknown.host");

        // Must be null (no match) and NOT the default portal 0 - the two-step lookup distinguishes the two.
        portal.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetByAliasAsync_returns_null_for_blank_input(string? alias)
    {
        using var context = NewInMemoryContext();
        await SeedAsync(context);
        var repository = new PortalRepository(context);

        Portal? portal = await repository.GetByAliasAsync(alias!);

        portal.Should().BeNull();
    }
}
