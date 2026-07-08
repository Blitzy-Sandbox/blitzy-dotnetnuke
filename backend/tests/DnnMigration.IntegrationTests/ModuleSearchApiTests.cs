// -----------------------------------------------------------------------------
//  ModuleSearchApiTests.cs
//
//  MIGRATION: Net-new xUnit integration-test class covering finding C2 (server-side
//  module search) end-to-end against the in-memory host.
//
//  AAP §0.7.2 maps the legacy Website/admin/Modules list search to
//  "GET /api/modules?query=...". The migrated ModulesController.GetAll now forwards
//  ?query= to ModuleService.SearchAsync (case-insensitive substring over
//  ModuleTitle/FriendlyName/ModuleName, honouring the effective portal scope)
//  instead of silently ignoring it. These tests prove the term is honoured
//  SERVER-SIDE: a matching seed is returned, a non-matching seed is excluded, and an
//  absent query returns the full list (search is strictly opt-in).
//
//  The class owns an isolated EF Core InMemory store (IClassFixture), rows are seeded
//  through ResetAndSeedAsync, and the default HOST super-user identity means the
//  unscoped (all-portals) search path is exercised.
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
/// End-to-end integration tests for server-side module search (finding C2), exercised against the in-memory
/// <c>DnnMigration.Api</c> host.
/// </summary>
public class ModuleSearchApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="ModuleSearchApiTests"/> class.
    /// </summary>
    /// <param name="factory">The shared web-application factory that hosts the API in memory.</param>
    public ModuleSearchApiTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    /// <summary>
    /// A free-text <c>?query=</c> is filtered SERVER-SIDE over the module title: only the matching module is
    /// returned; the non-matching seed is excluded. Core assertion for finding C2.
    /// </summary>
    [Fact]
    public async Task GetAll_WithQuery_FiltersServerSide()
    {
        await _factory.ResetAndSeedAsync(async db =>
        {
            db.Modules.Add(new Module { ModuleID = 2001, PortalID = 7, ModuleTitle = "Announcements", ModuleDefID = 1 });
            db.Modules.Add(new Module { ModuleID = 2002, PortalID = 7, ModuleTitle = "Weather Widget", ModuleDefID = 1 });
            await Task.CompletedTask;
        });

        var response = await _client.GetAsync("/api/modules?query=announce");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var envelope = await response.Content.ReadFromJsonAsync<Envelope<List<ModuleRead>>>(JsonOptions);
        envelope.Should().NotBeNull();
        envelope!.Data.Should().NotBeNull();
        envelope.Data!.Should().ContainSingle(m => m.ModuleID == 2001)
            .Which.ModuleTitle.Should().Be("Announcements");
        envelope.Data.Should().NotContain(m => m.ModuleID == 2002,
            "the free-text query must exclude the non-matching module server-side");
    }

    /// <summary>
    /// A search term matching no module returns an empty data array, proving the filter is genuinely applied.
    /// </summary>
    [Fact]
    public async Task GetAll_WithNonMatchingQuery_ReturnsEmpty()
    {
        await _factory.ResetAndSeedAsync(async db =>
        {
            db.Modules.Add(new Module { ModuleID = 2101, PortalID = 7, ModuleTitle = "Announcements", ModuleDefID = 1 });
            await Task.CompletedTask;
        });

        var response = await _client.GetAsync("/api/modules?query=zzz-no-such-module");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var envelope = await response.Content.ReadFromJsonAsync<Envelope<List<ModuleRead>>>(JsonOptions);
        envelope!.Data.Should().NotBeNull();
        envelope.Data!.Should().BeEmpty();
    }

    /// <summary>
    /// With no <c>?query=</c> the full list is returned (search is strictly opt-in).
    /// </summary>
    [Fact]
    public async Task GetAll_WithoutQuery_ReturnsAll()
    {
        await _factory.ResetAndSeedAsync(async db =>
        {
            db.Modules.Add(new Module { ModuleID = 2201, PortalID = 7, ModuleTitle = "Announcements", ModuleDefID = 1 });
            db.Modules.Add(new Module { ModuleID = 2202, PortalID = 7, ModuleTitle = "Weather Widget", ModuleDefID = 1 });
            await Task.CompletedTask;
        });

        var response = await _client.GetAsync("/api/modules");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var envelope = await response.Content.ReadFromJsonAsync<Envelope<List<ModuleRead>>>(JsonOptions);
        envelope!.Data.Should().NotBeNull();
        envelope.Data!.Select(m => m.ModuleID).Should().Contain(new[] { 2201, 2202 });
    }

    // -------------------------------------------------------------------------
    //  Local read-models (test-only shapes)
    // -------------------------------------------------------------------------

    private sealed class Envelope<T>
    {
        public T? Data { get; set; }
        public JsonElement Meta { get; set; }
    }

    private sealed class ModuleRead
    {
        public int ModuleID { get; set; }
        public int PortalID { get; set; }
        public string? ModuleTitle { get; set; }
    }
}
