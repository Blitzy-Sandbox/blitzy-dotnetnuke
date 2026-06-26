// MIGRATION: [AAP Gate 5 / QA-1 INFO-2] Tab (page) CRUD integration tests. Verifies the Gate-5 status-code
// contract (POST -> 201, GET -> 200, PUT -> 200, DELETE -> 204) end-to-end through the real Program.cs pipeline
// against an EF Core InMemory store, PLUS the two behavioral-parity rules ported from the legacy DotNetNuke
// TabController.vb (Library/Components/Tabs/TabController.vb) into TabService:
//   (1) the server-computed TabPath/Level on create (AddTab L333 -> GenerateTabPath): a ROOT tab is depth 0 with
//       TabPath "//" + StripNonWord(name); a CHILD is parent.Level + 1 with "//" + StripNonWord(parent) + "//" +
//       StripNonWord(child); and
//   (2) the delete-parent-with-children guard (DeleteTab L447-453, "parent tabs can not be deleted") — the legacy
//       silent no-op is surfaced here as an explicit 400 carrying the migrated message.
// Tabs is the ONLY list endpoint that is UNPAGED: GET /api/tabs?portalId= returns { data:[...], meta:{ count } }
// (ApiControllerBase.HandleList), so the list assertions read the count meta, NOT a paged meta.
//
// MIGRATION (test contract): the migrated TabsController scopes EVERY action by portalId — the list, the
// single-read, the update, and the delete all bind portalId as [FromQuery, BindRequired]; only POST carries it in
// the body. The seeded Test principal (TestAuthHandler) is a host SuperUser (isSuperUser=true, portalId=0) and
// therefore bypasses ApiControllerBase.EnforceTenant, so PortalId=0 is used throughout exactly as the sibling
// Module/User CRUD tests do — no Portal row needs to be seeded (TabService never loads a Portal entity and the
// InMemory provider does not enforce foreign keys). Success bodies are read through file-local envelope records
// using the shared EnvelopeReader.Web (JsonSerializerDefaults.Web) options.

using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;
using DnnMigration.Application.DTOs.Tab;

namespace DnnMigration.IntegrationTests.ApiTests;

[Trait("Category", "Integration")]
public sealed class TabsControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    // MIGRATION: the seeded Test super-user carries portalId=0 and bypasses EnforceTenant, so every portal-scoped
    // Tab action targets portal 0 (mirrors ModuleCrudTests/UserCrudTests). No Portal entity is seeded.
    private const int PortalId = 0;

    private readonly HttpClient _client;

    public TabsControllerTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Post_Tab_Returns201Created_WithComputedRootPath()
    {
        // POST -> 201. PortalId + TabName satisfy CreateTabValidator (TabName NotEmpty + Max50). A root tab has no
        // ParentId, so TabService computes Level 0 and TabPath "//" + StripNonWord("Test Page") = "//TestPage".
        var response = await _client.PostAsJsonAsync(
            "/api/tabs",
            new CreateTabRequest { PortalId = PortalId, TabName = "Test Page" },
            EnvelopeReader.Web);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull("HandleCreated emits a CreatedAtAction Location header");

        var env = await response.Content.ReadFromJsonAsync<SingleEnvelope<TabResponse>>(EnvelopeReader.Web);
        env.Should().NotBeNull();
        env!.Data.Should().NotBeNull();

        var tab = env.Data!;
        tab.TabId.Should().BeGreaterThan(0, "the InMemory store assigns the generated key on insert");
        tab.TabName.Should().Be("Test Page");
        tab.Level.Should().Be(0, "a root tab is depth 0 (TabService.CreateAsync parent-derived Level rule)");
        tab.TabPath.Should().Be("//TestPage", "GenerateTabPath strips non-word chars and prefixes a root tab with //");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Get_Tab_ById_Returns200()
    {
        var created = await CreateTabAsync(_client, PortalId);

        // GET -> 200. portalId is a BindRequired query parameter on the single-read (multi-tenant scoping).
        var response = await _client.GetAsync($"/api/tabs/{created.TabId}?portalId={PortalId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var env = await response.Content.ReadFromJsonAsync<SingleEnvelope<TabResponse>>(EnvelopeReader.Web);
        env.Should().NotBeNull();
        env!.Data.Should().NotBeNull();
        env.Data!.TabId.Should().Be(created.TabId);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Put_Tab_Returns200()
    {
        var created = await CreateTabAsync(_client, PortalId);

        // PUT -> 200. UpdateTabValidator requires TabName (NotEmpty + Max50). portalId is a BindRequired query
        // parameter; the renamed tab's TabPath is recomputed by TabService (root => "//UpdatedPage").
        var response = await _client.PutAsJsonAsync(
            $"/api/tabs/{created.TabId}?portalId={PortalId}",
            new UpdateTabRequest { TabName = "Updated Page" },
            EnvelopeReader.Web);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var env = await response.Content.ReadFromJsonAsync<SingleEnvelope<TabResponse>>(EnvelopeReader.Web);
        env.Should().NotBeNull();
        env!.Data.Should().NotBeNull();

        var tab = env.Data!;
        tab.TabName.Should().Be("Updated Page");
        tab.TabPath.Should().Be("//UpdatedPage", "UpdateAsync recomputes TabPath after a rename");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Delete_Tab_Returns204()
    {
        // A freshly created tab is a leaf (no child tabs), so the delete-parent guard does not apply.
        var created = await CreateTabAsync(_client, PortalId, "Leaf Page");

        var response = await _client.DeleteAsync($"/api/tabs/{created.TabId}?portalId={PortalId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Get_Tabs_List_Returns200Unpaged_WithCountMeta()
    {
        var first = await CreateTabAsync(_client, PortalId, "List Page One");
        var second = await CreateTabAsync(_client, PortalId, "List Page Two");

        // GET -> 200. Tabs is the ONLY UNPAGED list: { data:[...], meta:{ count } } (HandleList), NOT a paged meta.
        var response = await _client.GetAsync($"/api/tabs?portalId={PortalId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var env = await response.Content.ReadFromJsonAsync<ListEnvelope<List<TabResponse>>>(EnvelopeReader.Web);
        env.Should().NotBeNull();
        env!.Data.Should().NotBeNull();

        var ids = env.Data!.Select(t => t.TabId).ToList();
        ids.Should().Contain(first.TabId);
        ids.Should().Contain(second.TabId);

        env.Meta.Should().NotBeNull("the unpaged list envelope carries a count meta");
        env.Meta!.Count.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Delete_ParentTabWithChildren_Returns400()
    {
        // Create a parent then a child wired to it (through the API so TabService computes paths/levels and the FK).
        var parent = await CreateTabAsync(_client, PortalId, "Parent Page");
        await CreateTabAsync(_client, PortalId, "Child Page", parent.TabId);

        // DELETE the parent -> 400. TabService.DeleteAsync surfaces the legacy "parent tabs can not be deleted"
        // guard (DeleteTab L447-453) as an explicit failure with the migrated message.
        var response = await _client.DeleteAsync($"/api/tabs/{parent.TabId}?portalId={PortalId}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain(
            "This page cannot be deleted because it has child pages.",
            "the RFC 7807 problem detail carries the exact migrated child-guard message");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Get_Tabs_List_MissingPortalId_Returns400()
    {
        // portalId is [FromQuery, BindRequired]; omitting it fails model binding -> 400 before the action runs.
        var response = await _client.GetAsync("/api/tabs");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Get_Tab_ById_NotFound_Returns404()
    {
        // No tab with this id exists (the InMemory store assigns small sequential keys), so the portal-scoped
        // lookup fails and HandleGet maps the Result failure to 404.
        var response = await _client.GetAsync($"/api/tabs/999999?portalId={PortalId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Post_ChildTab_ComputesNestedPathAndLevel()
    {
        var parent = await CreateTabAsync(_client, PortalId, "Parent Page");

        // A child created under a root parent is depth 1 and its TabPath nests under the parent's stripped name.
        var child = await CreateTabAsync(_client, PortalId, "Child Page", parent.TabId);

        child.Level.Should().Be(1, "a child of a root tab is parent.Level + 1");
        child.TabPath.Should().Be(
            "//ParentPage//ChildPage",
            "GenerateTabPath walks the parent chain prepending //StripNonWord(parent) then appends the child segment");
    }

    // MIGRATION: posts a CreateTabRequest and returns the created TabResponse projected from the success envelope's
    // data object. Asserts the Gate-5 create status (201) so every caller starts from a known-good created tab.
    private static async Task<TabResponse> CreateTabAsync(
        HttpClient client,
        int portalId,
        string name = "Test Page",
        int? parentId = null)
    {
        var response = await client.PostAsJsonAsync(
            "/api/tabs",
            new CreateTabRequest { PortalId = portalId, TabName = name, ParentId = parentId },
            EnvelopeReader.Web);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var env = await response.Content.ReadFromJsonAsync<SingleEnvelope<TabResponse>>(EnvelopeReader.Web);
        env.Should().NotBeNull();
        env!.Data.Should().NotBeNull();
        return env.Data!;
    }
}

// MIGRATION: file-local typed views over the API's standard success envelopes, deserialized with
// EnvelopeReader.Web. Declared `file`-scoped so they cannot collide with any envelope helper another test file in
// this namespace might declare. The single-resource envelope is { data, meta:{} } (ApiControllerBase.Envelope);
// the unpaged-list envelope is { data:[...], meta:{ count } } (ApiControllerBase.HandleList).
file sealed record SingleEnvelope<T>(T? Data);

file sealed record ListCountMeta(int Count);

file sealed record ListEnvelope<T>(T? Data, ListCountMeta? Meta);
