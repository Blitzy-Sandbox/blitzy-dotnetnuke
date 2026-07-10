// -----------------------------------------------------------------------------
//  RoleSearchApiTests.cs
//
//  MIGRATION (QA finding - R10 Issue 13): net-new xUnit integration-test class
//  covering server-side ROLE search + pagination on /api/roles end-to-end against
//  the in-memory host.
//
//  Before this fix the roles endpoint had NO ?query= parameter at all: the SPA
//  could only filter the first bounded page client-side, so a newly created role
//  positioned beyond that page (the QA repro's "role 2009") was undiscoverable.
//  RolesController.GetAll now forwards ?query= to RoleService.SearchPagedAsync /
//  SearchByPortalPagedAsync (case-insensitive substring over RoleName/Description),
//  reaching parity with the Portal/Module/User search endpoints. These tests prove:
//
//    * a matching seed is returned and a non-matching seed is excluded (search is
//      honoured SERVER-SIDE, not accepted-and-ignored);
//    * a non-matching term returns an empty page (the filter is genuinely applied);
//    * an absent query returns the full list (search is strictly opt-in);
//    * a role beyond the first page is discoverable BOTH by paging to page 2 AND by
//      an exact ?query= search regardless of its position (the Issue-13 proof).
//
//  The class owns a uniquely-named, fully-isolated EF Core InMemory store via its
//  IClassFixture CustomWebApplicationFactory, seeding rows directly through
//  ResetAndSeedAsync (no dependence on POST validation). The default test identity is
//  a HOST super-user, so the controller's per-portal horizontal scoping is bypassed
//  and every seeded role is visible to the list/search feed.
// -----------------------------------------------------------------------------

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DnnMigration.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace DnnMigration.IntegrationTests;

/// <summary>
/// End-to-end integration tests for server-side role search and pagination (QA finding R10 Issue 13),
/// exercised against the in-memory <c>DnnMigration.Api</c> host.
/// </summary>
public class RoleSearchApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="RoleSearchApiTests"/> class. The factory authenticates
    /// every request via the default HOST super-user test scheme, so the list/search endpoint's per-portal
    /// scoping is bypassed and all seeded roles are visible.
    /// </summary>
    /// <param name="factory">The shared web-application factory that hosts the API in memory.</param>
    public RoleSearchApiTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // -------------------------------------------------------------------------
    //  Server-side ?query= filtering
    // -------------------------------------------------------------------------

    /// <summary>
    /// A free-text <c>?query=</c> is filtered SERVER-SIDE: only the role whose name contains the term is
    /// returned; the non-matching seed is excluded. This is the core assertion for finding R10 Issue 13 - the
    /// roles search term is honoured by the backend rather than being unavailable.
    /// </summary>
    [Fact]
    public async Task GetAll_WithQuery_FiltersServerSide()
    {
        await _factory.ResetAndSeedAsync(async db =>
        {
            db.Roles.Add(new Role { RoleID = 3001, PortalID = 0, RoleName = "Content Editors" });
            db.Roles.Add(new Role { RoleID = 3002, PortalID = 0, RoleName = "Subscribers" });
            await Task.CompletedTask;
        });

        var response = await _client.GetAsync("/api/roles?query=editor");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var envelope = await response.Content.ReadFromJsonAsync<Envelope<List<RoleRead>>>(JsonOptions);
        envelope.Should().NotBeNull();
        envelope!.Data.Should().NotBeNull();
        envelope.Data!.Should().ContainSingle(r => r.RoleID == 3001)
            .Which.RoleName.Should().Be("Content Editors");
        envelope.Data.Should().NotContain(r => r.RoleID == 3002,
            "the free-text query must exclude the non-matching role server-side");
    }

    /// <summary>
    /// A search term that matches no role returns an empty data array (HTTP 200 with count 0), proving the
    /// filter is genuinely applied rather than falling back to the full list.
    /// </summary>
    [Fact]
    public async Task GetAll_WithNonMatchingQuery_ReturnsEmpty()
    {
        await _factory.ResetAndSeedAsync(async db =>
        {
            db.Roles.Add(new Role { RoleID = 3101, PortalID = 0, RoleName = "Content Editors" });
            await Task.CompletedTask;
        });

        var response = await _client.GetAsync("/api/roles?query=zzz-no-such-role");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var envelope = await response.Content.ReadFromJsonAsync<Envelope<List<RoleRead>>>(JsonOptions);
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
            db.Roles.Add(new Role { RoleID = 3201, PortalID = 0, RoleName = "Content Editors" });
            db.Roles.Add(new Role { RoleID = 3202, PortalID = 0, RoleName = "Subscribers" });
            await Task.CompletedTask;
        });

        var response = await _client.GetAsync("/api/roles");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var envelope = await response.Content.ReadFromJsonAsync<Envelope<List<RoleRead>>>(JsonOptions);
        envelope!.Data.Should().NotBeNull();
        envelope.Data!.Select(r => r.RoleID).Should().Contain(new[] { 3201, 3202 });
    }

    // -------------------------------------------------------------------------
    //  Issue-13 discoverability proof: a role beyond the first page
    // -------------------------------------------------------------------------

    /// <summary>
    /// A newly created role positioned beyond the first bounded page is discoverable BOTH ways: by requesting
    /// the second page (server-side pagination) and by an exact <c>?query=</c> search regardless of its
    /// position. This directly refutes the QA repro (the new role could not be found via the first-page
    /// client-side filter). Also asserts the pagination <c>meta</c> reports the honest grand total.
    /// </summary>
    [Fact]
    public async Task GetAll_SurfacesRoleBeyondFirstPage_ViaPagingAndSearch()
    {
        // Seed 25 roles (IDs 4001..4025). The last one is the "brand new" role that lands beyond a 20-row
        // first page, mirroring the QA repro where the freshly created role fell outside the loaded window.
        await _factory.ResetAndSeedAsync(async db =>
        {
            for (var i = 1; i <= 24; i++)
            {
                db.Roles.Add(new Role { RoleID = 4000 + i, PortalID = 0, RoleName = $"Role {i:D2}" });
            }
            db.Roles.Add(new Role { RoleID = 4025, PortalID = 0, RoleName = "BrandNewRole2025" });
            await Task.CompletedTask;
        });

        // 1) Paging: page 2 (pageSize 20) must surface the 25th role, and meta.totalCount must be the honest 25.
        var pageTwo = await _client.GetAsync("/api/roles?page=2&pageSize=20");
        pageTwo.StatusCode.Should().Be(HttpStatusCode.OK);
        var pageEnvelope = await pageTwo.Content.ReadFromJsonAsync<Envelope<List<RoleRead>>>(JsonOptions);
        pageEnvelope!.Data.Should().NotBeNull();
        pageEnvelope.Data!.Should().Contain(r => r.RoleID == 4025,
            "the 25th role must be reachable on page 2 with a 20-row page size");
        pageEnvelope.Meta.GetProperty("totalCount").GetInt32().Should().Be(25);
        pageEnvelope.Meta.GetProperty("totalPages").GetInt32().Should().Be(2);

        // 2) Search: an exact query finds the new role regardless of its page position, and excludes the rest.
        var search = await _client.GetAsync("/api/roles?query=BrandNewRole2025");
        search.StatusCode.Should().Be(HttpStatusCode.OK);
        var searchEnvelope = await search.Content.ReadFromJsonAsync<Envelope<List<RoleRead>>>(JsonOptions);
        searchEnvelope!.Data.Should().NotBeNull();
        searchEnvelope.Data!.Should().ContainSingle(r => r.RoleID == 4025)
            .Which.RoleName.Should().Be("BrandNewRole2025");
    }

    // -------------------------------------------------------------------------
    //  Local read-models (test-only shapes)
    // -------------------------------------------------------------------------

    private sealed class Envelope<T>
    {
        public T? Data { get; set; }
        public JsonElement Meta { get; set; }
    }

    private sealed class RoleRead
    {
        public int RoleID { get; set; }
        public string? RoleName { get; set; }
    }
}
