// -----------------------------------------------------------------------------
//  PortalSearchApiTests.cs
//
//  MIGRATION: Net-new xUnit integration-test class covering two review findings on
//  the Portal REST resource (/api/portals) end-to-end against the in-memory host:
//
//    * C1 (server-side search) - AAP §0.7.2 maps the legacy Portals.ascx.vb grid
//      text/letter search to "GET /api/portals?query=...". The migrated
//      PortalsController.GetAll now forwards ?query= to PortalService.SearchAsync
//      (case-insensitive substring over PortalName/Description/KeyWords) instead of
//      silently ignoring it. These tests prove the term is honoured SERVER-SIDE:
//      a matching seed is returned, a non-matching seed is excluded, and an
//      absent query returns the full list (search is strictly opt-in).
//
//    * M1 (Portal Aliases read model) - the legacy Portals.ascx.vb grid rendered a
//      "Portal Aliases" column (FormatPortalAliases). The migrated read model
//      surfaces the HTTP aliases on PortalDto.Aliases, populated by the service
//      from a grouped PortalAlias lookup. These tests prove the aliases appear on
//      both the list feed and the single-portal read.
//
//  The class uses its own CustomWebApplicationFactory instance (IClassFixture), so
//  it owns a uniquely-named, fully-isolated EF Core InMemory store. Rows are seeded
//  directly through the fixture's ResetAndSeedAsync helper (no dependence on POST
//  validation), and the default test identity is a HOST super-user, so the
//  controller's per-portal horizontal scoping is bypassed and every seeded portal is
//  visible to the list/search feed.
// -----------------------------------------------------------------------------

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DnnMigration.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace DnnMigration.IntegrationTests;

/// <summary>
/// End-to-end integration tests for server-side portal search (finding C1) and the portal-aliases read
/// model (finding M1), exercised against the in-memory <c>DnnMigration.Api</c> host.
/// </summary>
public class PortalSearchApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="PortalSearchApiTests"/> class. The factory
    /// authenticates every request via the default HOST super-user test scheme, so the list/search
    /// endpoint's per-portal scoping is bypassed and all seeded portals are visible.
    /// </summary>
    /// <param name="factory">The shared web-application factory that hosts the API in memory.</param>
    public PortalSearchApiTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // -------------------------------------------------------------------------
    //  C1 - server-side ?query= filtering
    // -------------------------------------------------------------------------

    /// <summary>
    /// A free-text <c>?query=</c> is filtered SERVER-SIDE: only the portal whose name contains the term is
    /// returned; the non-matching seed is excluded. This is the core assertion for finding C1 - the search
    /// term is no longer accepted-and-ignored.
    /// </summary>
    [Fact]
    public async Task GetAll_WithQuery_FiltersServerSide()
    {
        await _factory.ResetAndSeedAsync(async db =>
        {
            db.Portals.Add(new Portal { PortalID = 1001, PortalName = "Contoso Marketing" });
            db.Portals.Add(new Portal { PortalID = 1002, PortalName = "Northwind Traders" });
            await Task.CompletedTask;
        });

        var response = await _client.GetAsync("/api/portals?query=contoso");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var envelope = await response.Content.ReadFromJsonAsync<Envelope<List<PortalRead>>>(JsonOptions);
        envelope.Should().NotBeNull();
        envelope!.Data.Should().NotBeNull();
        envelope.Data!.Should().ContainSingle(p => p.PortalID == 1001)
            .Which.PortalName.Should().Be("Contoso Marketing");
        envelope.Data.Should().NotContain(p => p.PortalID == 1002,
            "the free-text query must exclude the non-matching portal server-side");
    }

    /// <summary>
    /// A search term that matches no portal returns an empty data array (HTTP 200 with count 0), proving the
    /// filter is genuinely applied rather than falling back to the full list.
    /// </summary>
    [Fact]
    public async Task GetAll_WithNonMatchingQuery_ReturnsEmpty()
    {
        await _factory.ResetAndSeedAsync(async db =>
        {
            db.Portals.Add(new Portal { PortalID = 1101, PortalName = "Contoso Marketing" });
            await Task.CompletedTask;
        });

        var response = await _client.GetAsync("/api/portals?query=zzz-no-such-portal");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var envelope = await response.Content.ReadFromJsonAsync<Envelope<List<PortalRead>>>(JsonOptions);
        envelope!.Data.Should().NotBeNull();
        envelope.Data!.Should().BeEmpty();
    }

    /// <summary>
    /// With no <c>?query=</c> the full list is returned (search is strictly opt-in), confirming the new
    /// parameter did not change the default list behaviour.
    /// </summary>
    [Fact]
    public async Task GetAll_WithoutQuery_ReturnsAll()
    {
        await _factory.ResetAndSeedAsync(async db =>
        {
            db.Portals.Add(new Portal { PortalID = 1201, PortalName = "Contoso Marketing" });
            db.Portals.Add(new Portal { PortalID = 1202, PortalName = "Northwind Traders" });
            await Task.CompletedTask;
        });

        var response = await _client.GetAsync("/api/portals");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var envelope = await response.Content.ReadFromJsonAsync<Envelope<List<PortalRead>>>(JsonOptions);
        envelope!.Data.Should().NotBeNull();
        envelope.Data!.Select(p => p.PortalID).Should().Contain(new[] { 1201, 1202 });
    }

    // -------------------------------------------------------------------------
    //  M1 - Portal Aliases read model
    // -------------------------------------------------------------------------

    /// <summary>
    /// The list feed enriches each portal with its HTTP aliases (finding M1): a portal with two seeded
    /// <see cref="PortalAlias"/> rows exposes both on <c>PortalDto.Aliases</c>, while a portal with no
    /// aliases exposes an empty array.
    /// </summary>
    [Fact]
    public async Task GetAll_PopulatesPortalAliases()
    {
        await _factory.ResetAndSeedAsync(async db =>
        {
            db.Portals.Add(new Portal { PortalID = 1301, PortalName = "Aliased Portal" });
            db.Portals.Add(new Portal { PortalID = 1302, PortalName = "Alias-Free Portal" });
            db.PortalAliases.Add(new PortalAlias { PortalAliasID = 5001, PortalID = 1301, HTTPAlias = "aliased.example" });
            db.PortalAliases.Add(new PortalAlias { PortalAliasID = 5002, PortalID = 1301, HTTPAlias = "www.aliased.example" });
            await Task.CompletedTask;
        });

        var response = await _client.GetAsync("/api/portals");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var envelope = await response.Content.ReadFromJsonAsync<Envelope<List<PortalRead>>>(JsonOptions);
        envelope!.Data.Should().NotBeNull();
        var data = envelope.Data!;

        var aliased = data.Single(p => p.PortalID == 1301);
        aliased.Aliases.Should().NotBeNull();
        aliased.Aliases!.Should().BeEquivalentTo("aliased.example", "www.aliased.example");

        var aliasFree = data.Single(p => p.PortalID == 1302);
        (aliasFree.Aliases ?? Array.Empty<string>()).Should().BeEmpty(
            "a portal with no PortalAlias rows exposes an empty alias list");
    }

    /// <summary>
    /// The single-portal read (<c>GET /api/portals/{id}</c>) also carries the HTTP aliases (finding M1),
    /// populated from the per-portal alias lookup.
    /// </summary>
    [Fact]
    public async Task GetById_PopulatesPortalAliases()
    {
        await _factory.ResetAndSeedAsync(async db =>
        {
            db.Portals.Add(new Portal { PortalID = 1401, PortalName = "Detail Portal" });
            db.PortalAliases.Add(new PortalAlias { PortalAliasID = 6001, PortalID = 1401, HTTPAlias = "detail.example" });
            await Task.CompletedTask;
        });

        var response = await _client.GetAsync("/api/portals/1401");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var envelope = await response.Content.ReadFromJsonAsync<Envelope<PortalRead>>(JsonOptions);
        envelope!.Data.Should().NotBeNull();
        envelope.Data!.PortalID.Should().Be(1401);
        envelope.Data.Aliases.Should().NotBeNull();
        envelope.Data.Aliases!.Should().ContainSingle().Which.Should().Be("detail.example");
    }

    // -------------------------------------------------------------------------
    //  Local read-models (test-only shapes)
    // -------------------------------------------------------------------------

    private sealed class Envelope<T>
    {
        public T? Data { get; set; }
        public JsonElement Meta { get; set; }
    }

    /// <summary>
    /// Minimal portal projection for these tests, extended (relative to PortalApiTests) with the
    /// <see cref="Aliases"/> array so the finding-M1 assertions can bind the wire's <c>"aliases"</c> key.
    /// </summary>
    private sealed class PortalRead
    {
        public int PortalID { get; set; }
        public string? PortalName { get; set; }
        public string[]? Aliases { get; set; }
    }
}
